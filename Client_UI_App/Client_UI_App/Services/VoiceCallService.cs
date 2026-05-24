using Concentus.Enums;
using Concentus.Structs;
using NAudio.Wave;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Client_UI_App.Services
{
    // Voice Call Service — NAudio + Concentus Opus + UDP transport
    //
    // Không dùng WebRTC/SIPSorcery (API quá unstable giữa các version).
    // Đủ cho demo LAN; Phase 4 video call sẽ dùng giải pháp riêng.
    //
    // Pipeline gửi:
    //   WaveInEvent (48kHz 16-bit mono, 20ms) → Concentus Encode → UDP packet
    //
    // Pipeline nhận:
    //   UDP packet → Concentus Decode → BufferedWaveProvider → WaveOutEvent
    //
    // Signaling (qua TCP, bên ngoài service):
    //   VOICE_OFFER|callerName|myUdpPort
    //   VOICE_ANSWER|callerName|myUdpPort
    //   VOICE_REJECT|callerName
    //   VOICE_HANGUP|callerName
    internal sealed class VoiceCallService : IDisposable
    {
        private const int SampleRate   = 48000;
        private const int Channels     = 1;
        private const int FrameMs      = 20;
        private const int FrameSamples = SampleRate * FrameMs / 1000;   // 960
        private const int MaxOpusBytes = 1275;
        private const int AudioBitrate = 24000;

        // UDP transport — DÙNG 1 SOCKET cho cả send + recv
        // Lý do: nếu dùng 2 socket khác nhau, source port của gói gửi đi != port nhận.
        // Khi peer (bot) NAT-remap về source port, response sẽ tới ephemeral _udpSend
        // mà _udpRecv không listen → mất audio. Single-socket tránh hoàn toàn case này.
        private UdpClient?            _udp;
        private IPEndPoint?           _remoteEp;
        private CancellationTokenSource? _recvCts;

        // NAudio
        private WaveInEvent?          _waveIn;
        private WaveOutEvent?         _waveOut;
        private BufferedWaveProvider? _playBuffer;

        // Concentus (dùng trực tiếp concrete type — obsolete warning là OK)
#pragma warning disable CS0618
        private OpusEncoder? _encoder;
        private OpusDecoder? _decoder;
#pragma warning restore CS0618

        private bool            _audioStarted = false;
        private volatile bool   _muted        = false;
        private bool   _disposed     = false;
        private ushort _sendSeq      = 0;

        // Half-duplex echo suppression — khi đang nhận audio từ peer (bot đang nói),
        // tự động suppress mic để loa không bị mic capture lại → tránh feedback loop.
        // Cập nhật mỗi lần nhận packet; OnMicData check timestamp này.
        private long          _lastIncomingTickMs;
        private const int     EchoSuppressMs = 600;  // mic re-enabled ~600ms sau khi bot ngừng nói
        // Bật/tắt tính năng half-duplex (1:1 P2P giữa user có thể không cần — chỉ bot)
        public bool EchoSuppression { get; set; } = false;

        public bool IsMuted
        {
            get => _muted;
            set
            {
                _muted = value;
                // Thực sự stop/start mic capture — đảm bảo không còn audio nào lọt qua
                // (chỉ flag dễ bị race với buffer NAudio đã queue trước khi flag set)
                try
                {
                    if (value) _waveIn?.StopRecording();
                    else       _waveIn?.StartRecording();
                }
                catch { }
            }
        }
        public int  LocalUdpPort     { get; private set; }

        // Fired khi audio bắt đầu chạy (cả 2 bên đã sẵn sàng)
        public event Action?        CallConnected;
        // Fired khi receive loop kết thúc (peer disconnect hoặc Stop gọi)
        public event Action?        CallEnded;
        public event Action<float>? MicLevelChanged;
        public event Action<float>? SpkLevelChanged;

        // ─────────────────────────────────────────────────────────────
        //  Bước 1 — Caller gọi PrepareUdp() → lấy localUdpPort để gửi qua signaling
        // ─────────────────────────────────────────────────────────────
        public int PrepareUdp()
        {
            _udp         = new UdpClient(0);         // OS chọn port ngẫu nhiên — dùng cho cả send+recv
            LocalUdpPort = ((IPEndPoint)_udp.Client.LocalEndPoint!).Port;
            return LocalUdpPort;
        }

        // ─────────────────────────────────────────────────────────────
        //  Bước 2 — Cả 2 bên gọi SetRemoteEndpoint sau khi trao đổi port qua signaling
        // ─────────────────────────────────────────────────────────────
        public void SetRemoteEndpoint(string ip, int port)
        {
            _remoteEp = new IPEndPoint(IPAddress.Parse(ip), port);
        }

        // ─────────────────────────────────────────────────────────────
        //  Bước 3 — StartAudio() — bắt đầu thu âm + phát + receive loop
        // ─────────────────────────────────────────────────────────────
        public void StartAudio()
        {
            if (_audioStarted || _disposed) return;
            _audioStarted = true;

#pragma warning disable CS0618
            _encoder = new OpusEncoder(SampleRate, Channels, OpusApplication.OPUS_APPLICATION_VOIP);
            _encoder.Bitrate = AudioBitrate;
            _decoder = new OpusDecoder(SampleRate, Channels);
#pragma warning restore CS0618

            var fmt     = new WaveFormat(SampleRate, 16, Channels);
            _playBuffer = new BufferedWaveProvider(fmt)
            {
                BufferDuration          = TimeSpan.FromSeconds(2),
                DiscardOnBufferOverflow = true
            };
            _waveOut = new WaveOutEvent { DesiredLatency = 150 };
            _waveOut.Init(_playBuffer);
            _waveOut.Play();

            _waveIn = new WaveInEvent
            {
                WaveFormat         = new WaveFormat(SampleRate, 16, Channels),
                BufferMilliseconds = FrameMs
            };
            _waveIn.DataAvailable += OnMicData;
            _waveIn.StartRecording();

            _recvCts = new CancellationTokenSource();
            _ = Task.Run(() => ReceiveLoopAsync(_recvCts.Token));

            CallConnected?.Invoke();
        }

        public void Stop()
        {
            if (_disposed) return;
            _disposed = true;

            _recvCts?.Cancel();

            _waveIn?.StopRecording();
            _waveIn?.Dispose();
            _waveIn = null;

            _waveOut?.Stop();
            _waveOut?.Dispose();
            _waveOut    = null;
            _playBuffer = null;

            try { _udp?.Close(); } catch { }
            _udp = null;

            _audioStarted = false;
        }

        public void Dispose() => Stop();

        // ─────────────────────────────────────────────────────────────
        //  Audio pipeline
        // ─────────────────────────────────────────────────────────────

        // Mic → Opus encode → UDP send
        private void OnMicData(object? sender, WaveInEventArgs e)
        {
            if (_encoder is null || _udp is null || _remoteEp is null || _disposed) return;

            int    samples = e.BytesRecorded / 2;
            short[] pcm    = new short[samples];
            Buffer.BlockCopy(e.Buffer, 0, pcm, 0, e.BytesRecorded);

            if (_muted)
            {
                MicLevelChanged?.Invoke(0f);
                return;
            }

            // Echo suppression — nếu bot vừa gửi audio gần đây thì mic của user có thể
            // đang capture loa (echo). Discard mic frame để không loop ngược lại bot.
            if (EchoSuppression)
            {
                long nowMs = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond;
                if (nowMs - _lastIncomingTickMs < EchoSuppressMs)
                {
                    MicLevelChanged?.Invoke(0f);
                    return;
                }
            }

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

            // Packet format: [seq_hi][seq_lo][opus_bytes...]
            byte[] packet = new byte[2 + opusLen];
            packet[0] = (byte)(_sendSeq >> 8);
            packet[1] = (byte)(_sendSeq & 0xFF);
            unchecked { _sendSeq++; }
            Buffer.BlockCopy(opusBuf, 0, packet, 2, opusLen);

            try { _udp.Send(packet, packet.Length, _remoteEp); }
            catch { }
        }

        // UDP receive → Opus decode → playback
        private int _recvPacketCount = 0;
        private async Task ReceiveLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var result = await _udp!.ReceiveAsync(ct);
                    byte[] data = result.Buffer;

                    // Mark "peer đang nói" — half-duplex sẽ suppress mic trong EchoSuppressMs
                    _lastIncomingTickMs = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond;

                    _recvPacketCount++;
                    if (_recvPacketCount == 1 || _recvPacketCount % 100 == 0)
                        System.Diagnostics.Debug.WriteLine(
                            $"[VoiceCall] Received UDP packet #{_recvPacketCount} from {result.RemoteEndPoint}, size={data.Length}");

                    // Packet quá ngắn (chỉ có header hoặc trống)
                    if (data.Length <= 2) continue;

                    int    opusOffset = 2;
                    int    opusLen    = data.Length - opusOffset;
                    short[] pcm      = new short[FrameSamples];
                    int    decoded;
                    try
                    {
#pragma warning disable CS0618
                        decoded = _decoder!.Decode(data, opusOffset, opusLen, pcm, 0, FrameSamples, false);
#pragma warning restore CS0618
                    }
                    catch { continue; }

                    if (decoded <= 0) continue;

                    SpkLevelChanged?.Invoke(ComputeRms(pcm, decoded));

                    byte[] pcmBytes = new byte[decoded * 2];
                    Buffer.BlockCopy(pcm, 0, pcmBytes, 0, pcmBytes.Length);
                    _playBuffer?.AddSamples(pcmBytes, 0, pcmBytes.Length);
                }
                catch (OperationCanceledException) { break; }
                catch { /* UDP socket bị đóng hoặc lỗi mạng tạm thời */ }
            }

            if (!_disposed)
                CallEnded?.Invoke();
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
