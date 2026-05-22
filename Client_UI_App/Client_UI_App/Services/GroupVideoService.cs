using Concentus.Enums;
using Concentus.Structs;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Client_UI_App.Services
{
    // Group Video Channel — full-mesh UDP: mỗi thành viên gửi audio+video tới N-1 peer
    //
    // Protocol (qua P2PListenerService TCP):
    //   GROUP_VIDEO_JOIN|groupId|username|audioUdpPort|videoUdpPort|tcpPort
    //   GROUP_VIDEO_REPLY|groupId|username|audioUdpPort|videoUdpPort
    //   GROUP_VIDEO_LEAVE|groupId|username
    //
    // Audio packet: [seq_hi][seq_lo][Opus payload...]
    // Video packet: [0x56 magic][len_4bytes_BE][JPEG data]
    internal sealed class GroupVideoService : IDisposable
    {
        private const int SampleRate   = 48000;
        private const int Channels     = 1;
        private const int FrameMs      = 20;
        private const int FrameSamples = SampleRate * FrameMs / 1000;   // 960
        private const int MaxOpusBytes = 1275;
        private const int AudioBitrate = 24000;

        private static readonly ImageCodecInfo JpegCodec = FindJpegCodec();

        // UDP sockets: một cho audio, một cho video
        private UdpClient? _audioUdp;
        private UdpClient? _videoUdp;
        public int LocalAudioPort { get; private set; }
        public int LocalVideoPort { get; private set; }

        // Peers (key = username)
        private readonly Dictionary<string, PeerState> _peers    = new();
        private readonly Dictionary<string, string>    _audioKey = new(); // "ip:port" → username
        private readonly Dictionary<string, string>    _videoKey = new(); // "ip:port" → username
        private readonly object                        _lock     = new();

        // Audio pipeline
        private WaveOutEvent?         _waveOut;
        private MixingSampleProvider? _mixer;
        private WaveInEvent?          _waveIn;

#pragma warning disable CS0618
        private OpusEncoder? _encoder;
#pragma warning restore CS0618

        private CancellationTokenSource? _cts;
        private volatile bool _disposed;
        private volatile bool _muted;
        private ushort        _audioSeq;

        public bool IsMuted   { get => _muted; set => _muted = value; }
        public int  PeerCount { get { lock (_lock) return _peers.Count; } }

        public event Action<string, Bitmap>? FrameReceived;   // (peerName, frame)
        public event Action<float>?          MicLevelChanged;

        private sealed class PeerState
        {
            public string              Username     = "";
            public string              AudioRecvKey = "";  // lưu để xóa khi remove
            public string              VideoRecvKey = "";
            public IPEndPoint          AudioSendEp  = null!;
            public IPEndPoint          VideoSendEp  = null!;
            public BufferedWaveProvider AudioBuffer  = null!;
            public ISampleProvider     AudioSample  = null!;
#pragma warning disable CS0618
            public OpusDecoder         Decoder      = null!;
#pragma warning restore CS0618
        }

        // ── Start ─────────────────────────────────────────────────────────
        public void Start()
        {
            _audioUdp     = new UdpClient(0);
            LocalAudioPort = ((IPEndPoint)_audioUdp.Client.LocalEndPoint!).Port;

            _videoUdp     = new UdpClient(0);
            LocalVideoPort = ((IPEndPoint)_videoUdp.Client.LocalEndPoint!).Port;

            var mixFmt = WaveFormat.CreateIeeeFloatWaveFormat(SampleRate, Channels);
            _mixer   = new MixingSampleProvider(mixFmt) { ReadFully = true };
            _waveOut = new WaveOutEvent { DesiredLatency = 150 };
            _waveOut.Init(_mixer);
            _waveOut.Play();

#pragma warning disable CS0618
            _encoder = new OpusEncoder(SampleRate, Channels, OpusApplication.OPUS_APPLICATION_VOIP)
            {
                Bitrate = AudioBitrate
            };
#pragma warning restore CS0618

            _waveIn = new WaveInEvent
            {
                WaveFormat         = new WaveFormat(SampleRate, 16, Channels),
                BufferMilliseconds = FrameMs
            };
            _waveIn.DataAvailable += OnMicData;
            _waveIn.StartRecording();

            _cts = new CancellationTokenSource();
            _ = Task.Run(() => AudioReceiveLoopAsync(_cts.Token));
            _ = Task.Run(() => VideoReceiveLoopAsync(_cts.Token));
        }

        // ── AddPeer ───────────────────────────────────────────────────────
        public void AddPeer(string username, string ip, int theirAudioPort, int theirVideoPort)
        {
            string ak = $"{ip}:{theirAudioPort}";
            string vk = $"{ip}:{theirVideoPort}";

            lock (_lock)
            {
                if (_peers.ContainsKey(username)) return;

                var fmt    = new WaveFormat(SampleRate, 16, Channels);
                var buffer = new BufferedWaveProvider(fmt)
                {
                    BufferDuration          = TimeSpan.FromSeconds(2),
                    DiscardOnBufferOverflow = true
                };
#pragma warning disable CS0618
                var decoder = new OpusDecoder(SampleRate, Channels);
#pragma warning restore CS0618
                var sp      = new WaveToSampleProvider(buffer);
                _mixer?.AddMixerInput(sp);

                _peers[username] = new PeerState
                {
                    Username     = username,
                    AudioRecvKey = ak,
                    VideoRecvKey = vk,
                    AudioSendEp  = new IPEndPoint(IPAddress.Parse(ip), theirAudioPort),
                    VideoSendEp  = new IPEndPoint(IPAddress.Parse(ip), theirVideoPort),
                    AudioBuffer  = buffer,
                    AudioSample  = sp,
                    Decoder      = decoder
                };
                _audioKey[ak] = username;
                _videoKey[vk] = username;
            }
        }

        // ── RemovePeer ────────────────────────────────────────────────────
        public void RemovePeer(string username)
        {
            lock (_lock)
            {
                if (!_peers.TryGetValue(username, out var peer)) return;
                _mixer?.RemoveMixerInput(peer.AudioSample);
                _audioKey.Remove(peer.AudioRecvKey);
                _videoKey.Remove(peer.VideoRecvKey);
                _peers.Remove(username);
            }
        }

        public bool HasPeer(string username) { lock (_lock) return _peers.ContainsKey(username); }

        // ── Gửi video frame tới tất cả peer ──────────────────────────────
        public void SendVideoFrame(Bitmap bmp)
        {
            if (_videoUdp == null || _disposed) return;
            try
            {
                // Scale xuống 320×240 nếu lớn hơn
                Bitmap src = bmp.Width == 320 && bmp.Height == 240
                    ? bmp
                    : new Bitmap(bmp, new Size(320, 240));

                using var ms  = new MemoryStream(16384);
                using var ep2 = new EncoderParameters(1);
                ep2.Param[0]  = new EncoderParameter(Encoder.Quality, 40L);
                src.Save(ms, JpegCodec, ep2);
                if (!ReferenceEquals(src, bmp)) src.Dispose();

                byte[] jpg = ms.ToArray();
                if (jpg.Length > 60000) return;

                byte[] pkt = new byte[5 + jpg.Length];
                pkt[0] = 0x56;
                pkt[1] = (byte)(jpg.Length >> 24);
                pkt[2] = (byte)(jpg.Length >> 16);
                pkt[3] = (byte)(jpg.Length >>  8);
                pkt[4] = (byte)(jpg.Length      );
                Buffer.BlockCopy(jpg, 0, pkt, 5, jpg.Length);

                List<IPEndPoint> eps;
                lock (_lock) { eps = _peers.Values.Select(p => p.VideoSendEp).ToList(); }
                foreach (var ep in eps)
                    try { _videoUdp.Send(pkt, pkt.Length, ep); } catch { }
            }
            catch { }
        }

        // ── Stop ──────────────────────────────────────────────────────────
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

            try { _audioUdp?.Close(); } catch { }
            try { _videoUdp?.Close(); } catch { }
            _audioUdp = null;
            _videoUdp = null;

            lock (_lock) { _peers.Clear(); _audioKey.Clear(); _videoKey.Clear(); }
        }

        public void Dispose() => Stop();

        // ── Mic → Opus → gửi tới tất cả peer ─────────────────────────────
        private void OnMicData(object? sender, WaveInEventArgs e)
        {
            if (_encoder == null || _audioUdp == null || _disposed) return;

            int     samples = e.BytesRecorded / 2;
            short[] pcm     = new short[samples];
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

            byte[] pkt = new byte[2 + opusLen];
            pkt[0] = (byte)(_audioSeq >> 8);
            pkt[1] = (byte)(_audioSeq & 0xFF);
            unchecked { _audioSeq++; }
            Buffer.BlockCopy(opusBuf, 0, pkt, 2, opusLen);

            List<IPEndPoint> eps;
            lock (_lock) { eps = _peers.Values.Select(p => p.AudioSendEp).ToList(); }
            foreach (var ep in eps)
                try { _audioUdp.Send(pkt, pkt.Length, ep); } catch { }
        }

        // ── UDP receive loop: audio ────────────────────────────────────────
        private async Task AudioReceiveLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var result = await _audioUdp!.ReceiveAsync(ct);
                    var    rEp  = result.RemoteEndPoint;
                    var    addr = rEp.Address.IsIPv4MappedToIPv6
                                    ? rEp.Address.MapToIPv4() : rEp.Address;
                    string key  = $"{addr}:{rEp.Port}";

                    PeerState? peer = null;
                    lock (_lock)
                    {
                        if (_audioKey.TryGetValue(key, out string? un))
                            _peers.TryGetValue(un, out peer);
                    }
                    if (peer is null) continue;

                    byte[]  data    = result.Buffer;
                    if (data.Length <= 2) continue;

                    short[] pcm     = new short[FrameSamples];
                    int     decoded;
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
                    peer.AudioBuffer.AddSamples(pcmBytes, 0, pcmBytes.Length);
                }
                catch (OperationCanceledException) { break; }
                catch { }
            }
        }

        // ── UDP receive loop: video ────────────────────────────────────────
        private async Task VideoReceiveLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var result = await _videoUdp!.ReceiveAsync(ct);
                    var    rEp  = result.RemoteEndPoint;
                    var    addr = rEp.Address.IsIPv4MappedToIPv6
                                    ? rEp.Address.MapToIPv4() : rEp.Address;
                    string key  = $"{addr}:{rEp.Port}";

                    string? username = null;
                    lock (_lock) { _videoKey.TryGetValue(key, out username); }
                    if (username is null) continue;

                    byte[] data = result.Buffer;
                    if (data.Length < 6 || data[0] != 0x56) continue;

                    int jpgLen = (data[1] << 24) | (data[2] << 16) | (data[3] << 8) | data[4];
                    if (jpgLen <= 0 || 5 + jpgLen > data.Length) continue;

                    // GDI+ giữ reference tới stream trong lifetime của Bitmap — phải copy
                    Bitmap bmp;
                    try
                    {
                        using var ms2 = new MemoryStream(data, 5, jpgLen);
                        using var tmp = System.Drawing.Image.FromStream(ms2);
                        bmp = new Bitmap(tmp);
                    }
                    catch { continue; }

                    FrameReceived?.Invoke(username, bmp);
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

        private static ImageCodecInfo FindJpegCodec()
        {
            foreach (var c in ImageCodecInfo.GetImageEncoders())
                if (c.MimeType == "image/jpeg") return c;
            throw new InvalidOperationException("JPEG codec not found");
        }
    }
}
