using Concentus.Enums;
using Concentus.Structs;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Client_UI_App.Services
{
    // Voice channel nhóm — full-mesh UDP, NAudio MixingSampleProvider
    //
    // Pipeline gửi:  WaveInEvent (48kHz 16-bit mono 20ms) → Opus encode → UDP → tất cả peer
    // Pipeline nhận: UDP từ nhiều peer → Opus decode (mỗi peer decoder riêng) → mix → WaveOutEvent
    //
    // Protocol (qua P2PListenerService TCP):
    //   GROUP_VOICE_JOIN|groupId|username|udpPort|tcpPort   → thông báo tham gia + đề nghị reply
    //   GROUP_VOICE_REPLY|groupId|username|udpPort           → phản hồi (không loop)
    //   GROUP_VOICE_LEAVE|groupId|username                   → rời channel
    internal sealed class GroupVoiceService : IDisposable
    {
        private const int SampleRate   = 48000;
        private const int Channels     = 1;
        private const int FrameMs      = 20;
        private const int FrameSamples = SampleRate * FrameMs / 1000;   // 960
        private const int MaxOpusBytes = 1275;
        private const int AudioBitrate = 24000;

        // UDP socket nhận từ nhiều peer cùng lúc
        private UdpClient? _udp;
        public  int         LocalUdpPort    { get; private set; }
        public  int         ExternalUdpPort { get; private set; }

        // Danh sách peer: key = "ip:udpPort" (endpoint gửi UDP của peer)
        private readonly Dictionary<string, PeerState> _peers    = new();
        private readonly object                        _peersLock = new();

        // NAudio output — 1 WaveOut + MixingSampleProvider cho tất cả peer
        private WaveOutEvent?          _waveOut;
        private MixingSampleProvider?  _mixer;

        // NAudio input — 1 WaveIn cho microphone
        private WaveInEvent?           _waveIn;

#pragma warning disable CS0618
        private OpusEncoder? _encoder;
#pragma warning restore CS0618

        private CancellationTokenSource? _cts;
        private bool           _disposed;
        private volatile bool  _muted;
        private ushort         _sendSeq;

        public bool IsMuted        { get => _muted; set => _muted = value; }
        public int  PeerCount      { get { lock (_peersLock) return _peers.Count; } }

        public event Action<float>?          MicLevelChanged;
        public event Action<string, float>?  PeerLevelChanged;  // (peerName, rmsLevel)

        private sealed class PeerState
        {
            public string               Username     = "";
            public IPEndPoint           SendEp       = null!;
            public BufferedWaveProvider Buffer       = null!;
            public ISampleProvider      SampleInput  = null!;  // đã add vào mixer
#pragma warning disable CS0618
            public OpusDecoder          Decoder      = null!;
#pragma warning restore CS0618
        }

        // ─────────────────────────────────────────────────────────────
        //  Start — khởi tạo toàn bộ audio pipeline
        //  Quan trọng: _mixer phải được khởi tạo trước khi bất kỳ AddPeer nào được gọi.
        //  Trước đây STUN tạo ra race condition: _mixer == null trong khi JOIN từ peer
        //  có thể đến trong suốt 4 giây chờ STUN, dẫn đến audio buffer của peer không
        //  bao giờ được đưa vào mixer và không nghe được âm thanh.
        // ─────────────────────────────────────────────────────────────
        public Task<int> StartAsync()
        {
            _udp = new UdpClient(0);
            LocalUdpPort    = ((IPEndPoint)_udp.Client.LocalEndPoint!).Port;
            ExternalUdpPort = LocalUdpPort;

            // Output: mixer → WaveOut — phải khởi tạo TRƯỚC khi trả về để AddPeer an toàn
            var mixFmt = WaveFormat.CreateIeeeFloatWaveFormat(SampleRate, Channels);
            _mixer   = new MixingSampleProvider(mixFmt) { ReadFully = true };
            _waveOut = new WaveOutEvent { DesiredLatency = 150 };
            _waveOut.Init(_mixer);
            _waveOut.Play();

            // Encoder cho mic
#pragma warning disable CS0618
            _encoder = new OpusEncoder(SampleRate, Channels, OpusApplication.OPUS_APPLICATION_VOIP)
            {
                Bitrate = AudioBitrate
            };
#pragma warning restore CS0618

            // Input: WaveIn mic
            _waveIn = new WaveInEvent
            {
                WaveFormat         = new WaveFormat(SampleRate, 16, Channels),
                BufferMilliseconds = FrameMs
            };
            _waveIn.DataAvailable += OnMicData;
            _waveIn.StartRecording();

            _cts = new CancellationTokenSource();
            _ = Task.Run(() => ReceiveLoopAsync(_cts.Token));

            return Task.FromResult(ExternalUdpPort);
        }

        // ─────────────────────────────────────────────────────────────
        //  AddPeer — thêm thành viên vào voice channel
        // ─────────────────────────────────────────────────────────────
        public void AddPeer(string username, string ip, int udpPort)
        {
            string key = $"{ip}:{udpPort}";
            lock (_peersLock)
            {
                if (_peers.ContainsKey(key)) return;

                var fmt    = new WaveFormat(SampleRate, 16, Channels);
                var buffer = new BufferedWaveProvider(fmt)
                {
                    BufferDuration          = TimeSpan.FromSeconds(2),
                    DiscardOnBufferOverflow = true
                };
#pragma warning disable CS0618
                var decoder = new OpusDecoder(SampleRate, Channels);
#pragma warning restore CS0618

                // WaveToSampleProvider chuyển 16-bit PCM → IEEE float ISampleProvider
                var sp = new WaveToSampleProvider(buffer);
                _mixer?.AddMixerInput(sp);

                _peers[key] = new PeerState
                {
                    Username    = username,
                    SendEp      = new IPEndPoint(IPAddress.Parse(ip), udpPort),
                    Buffer      = buffer,
                    SampleInput = sp,
                    Decoder     = decoder
                };
            }
        }

        // ─────────────────────────────────────────────────────────────
        //  RemovePeer — xoá thành viên khỏi voice channel
        // ─────────────────────────────────────────────────────────────
        public void RemovePeer(string username)
        {
            lock (_peersLock)
            {
                var key = _peers.FirstOrDefault(p => p.Value.Username == username).Key;
                if (key is null) return;
                var peer = _peers[key];
                _mixer?.RemoveMixerInput(peer.SampleInput);
                _peers.Remove(key);
            }
        }

        public bool HasPeer(string username)
        {
            lock (_peersLock) return _peers.Values.Any(p => p.Username == username);
        }

        // ─────────────────────────────────────────────────────────────
        //  Stop
        // ─────────────────────────────────────────────────────────────
        public void Stop()
        {
            if (_disposed) return;
            _disposed = true;

            _cts?.Cancel();

            _waveIn?.StopRecording();
            _waveIn?.Dispose();
            _waveIn = null;

            _waveOut?.Stop();
            _waveOut?.Dispose();
            _waveOut = null;
            _mixer   = null;

            try { _udp?.Close(); } catch { }
            _udp = null;

            lock (_peersLock) { _peers.Clear(); }
        }

        public void Dispose() => Stop();

        // ─────────────────────────────────────────────────────────────
        //  Mic → Opus encode → gửi UDP tới tất cả peer
        // ─────────────────────────────────────────────────────────────
        private void OnMicData(object? sender, WaveInEventArgs e)
        {
            if (_encoder is null || _udp is null || _disposed) return;

            int    samples = e.BytesRecorded / 2;
            short[] pcm   = new short[samples];
            Buffer.BlockCopy(e.Buffer, 0, pcm, 0, e.BytesRecorded);

            if (_muted) { MicLevelChanged?.Invoke(0f); return; }
            MicLevelChanged?.Invoke(ComputeRms(pcm, samples));

            byte[] opusBuf = new byte[MaxOpusBytes];
            int    opusLen;
            try
            {
#pragma warning disable CS0618
                opusLen = _encoder.Encode(pcm, 0, FrameSamples, opusBuf, 0, opusBuf.Length);
#pragma warning restore CS0618
            }
            catch { return; }
            if (opusLen <= 0) return;

            byte[] packet = new byte[2 + opusLen];
            packet[0] = (byte)(_sendSeq >> 8);
            packet[1] = (byte)(_sendSeq & 0xFF);
            unchecked { _sendSeq++; }
            Buffer.BlockCopy(opusBuf, 0, packet, 2, opusLen);

            List<IPEndPoint> endpoints;
            lock (_peersLock) { endpoints = _peers.Values.Select(p => p.SendEp).ToList(); }

            foreach (var ep in endpoints)
            {
                try { _udp.Send(packet, packet.Length, ep); } catch { }
            }
        }

        // ─────────────────────────────────────────────────────────────
        //  UDP receive → Opus decode → push vào buffer của peer
        // ─────────────────────────────────────────────────────────────
        private async Task ReceiveLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var result = await _udp!.ReceiveAsync(ct);
                    var    rEp  = result.RemoteEndPoint;
                    var    addr = rEp.Address.IsIPv4MappedToIPv6
                                    ? rEp.Address.MapToIPv4() : rEp.Address;
                    string key  = $"{addr}:{rEp.Port}";

                    PeerState? peer;
                    lock (_peersLock)
                    {
                        if (!_peers.TryGetValue(key, out peer))
                        {
                            // NAT remap: tìm theo IP trước, fallback theo port (Tailscale vs LAN IP change)
                            string strIp = addr.ToString();
                            var match = _peers.FirstOrDefault(kv =>
                                kv.Value.SendEp.Address.ToString() == strIp);
                            if (match.Value == null)
                                match = _peers.FirstOrDefault(kv =>
                                    kv.Value.SendEp.Port == rEp.Port);
                            if (match.Value != null)
                            {
                                peer = match.Value;
                                _peers.Remove(match.Key);
                                peer.SendEp = new IPEndPoint(addr, rEp.Port);
                                _peers[key] = peer;
                            }
                        }
                    }
                    if (peer is null) continue;

                    byte[] data = result.Buffer;
                    if (data.Length <= 2) continue;

                    short[] pcm = new short[FrameSamples];
                    int decoded;
                    try
                    {
#pragma warning disable CS0618
                        decoded = peer.Decoder.Decode(data, 2, data.Length - 2, pcm, 0, FrameSamples, false);
#pragma warning restore CS0618
                    }
                    catch { continue; }
                    if (decoded <= 0) continue;

                    byte[] pcmBytes = new byte[decoded * 2];
                    Buffer.BlockCopy(pcm, 0, pcmBytes, 0, pcmBytes.Length);
                    peer.Buffer.AddSamples(pcmBytes, 0, pcmBytes.Length);
                    PeerLevelChanged?.Invoke(peer.Username, ComputeRms(pcm, decoded));
                }
                catch (OperationCanceledException) { break; }
                catch { }
            }
        }

        private static float ComputeRms(short[] pcm, int count)
        {
            if (count == 0) return 0f;
            double sum = 0;
            for (int i = 0; i < count; i++) sum += (double)pcm[i] * pcm[i];
            return (float)Math.Sqrt(sum / count) / short.MaxValue;
        }

    }
}
