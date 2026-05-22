using Concentus.Enums;
using Concentus.Structs;
using Whisper.net;
using Whisper.net.Ggml;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Media;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

// Cấu hình bảng mã UTF-8 cho Console để hiển thị chính xác ngôn ngữ
Console.OutputEncoding = Encoding.UTF8;
Console.InputEncoding = Encoding.UTF8;

Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine("=== UITI-CHAN AI BOT INITIALIZATION (CLOUD EDITION) ===");
Console.ResetColor();

// Kích hoạt VoiceVox Engine chạy ngầm
StartVoiceVoxEngine();

// Khởi tạo Whisper STT (nếu có model)
await TryInitWhisperAsync();

// Cấu hình cổng mạng P2P và các biến toàn cục quan trọng
int p2pPort = 5555;
int activeDirectoryPort = 0; // Biến lưu trữ cổng của Server Danh bạ để dùng cho lúc LOGOUT
string aesSecretKey = "LTMCB_Secret_Key_2026"; // Chìa khóa E2EE chung cho cả nhóm
string serverIp    = Environment.GetEnvironmentVariable("SERVER_IP") ?? "127.0.0.1";
string botPublicIp = Environment.GetEnvironmentVariable("BOT_IP")    ?? await DetectBotIpAsync();

Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine($"[INFO] Bot sẽ đăng ký địa chỉ: {botPublicIp}:{p2pPort}");
Console.ResetColor();

// Đăng ký sự kiện bắt buộc gửi LOGOUT khi người dùng bấm dấu X tắt Console
AppDomain.CurrentDomain.ProcessExit += (sender, eventArgs) => SendLogoutSignal();

Console.CancelKeyPress += (sender, e) =>
{
    e.Cancel = true; // Ngăn Windows đóng app ngay lập tức
    Console.WriteLine("\n[INFO] Đang gửi tín hiệu đăng xuất trước khi thoát...");
    SendLogoutSignal();
    Environment.Exit(0); // Tự đóng app an toàn sau khi gửi xong
};

void SendLogoutSignal()
{
    if (activeDirectoryPort != 0)
    {
        // Lưu lại port và reset biến global ngay để hàm ProcessExit không gọi đúp lần 2
        int targetPort = activeDirectoryPort;
        activeDirectoryPort = 0;

        try
        {
            using TcpClient logoutClient = new TcpClient(serverIp, targetPort);
            using StreamWriter logoutWriter = new StreamWriter(logoutClient.GetStream(), Encoding.UTF8) { AutoFlush = true };

            // Gửi tín hiệu
            logoutWriter.WriteLine("LOGOUT|UitiChan");

            // Ép App dừng lại 300ms để gói tin mạng bay đi an toàn trước khi bị Windows shutdown
            System.Threading.Thread.Sleep(300);
        }
        catch { /* Bỏ qua lỗi đóng kết nối */ }
    }
}

// Khởi tạo Task chạy ngầm để lắng nghe các kết nối TCP
_ = Task.Run(() => StartP2PListener(p2pPort, aesSecretKey));

// Tạm dừng luồng chính để đảm bảo P2P Listener & VoiceVox khởi động hoàn tất
await Task.Delay(1000);

// --- BƯỚC 1: BOT TỰ ĐỘNG ĐĂNG KÝ / ĐĂNG NHẬP VÀO DANH BẠ ---
// Logic: Thử LOGIN trước. Nếu thất bại (chưa có acc) → SIGNUP → LOGIN lại.
try
{
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine("[INFO] Bot đang kết nối Load Balancer (Port 9000) để xin vé...");
    Console.ResetColor();

    // Lần 1: Xin vé → thử LOGIN
    int dirPort1 = await GetDirectoryPortAsync();
    activeDirectoryPort = dirPort1;

    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine($"[INFO] Được phân luồng sang Directory Server Port: {dirPort1}. Đang thử LOGIN...");
    Console.ResetColor();

    string loginResponse = await SendDirectoryCommandAsync(dirPort1, $"LOGIN|UitiChan|123456|{botPublicIp}:{p2pPort}");

    // Nếu tài khoản chưa tồn tại trong DB → tự đăng ký
    if (loginResponse.StartsWith("LOGIN_FAILED"))
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("[INFO] Chưa có tài khoản. Bot đang tự đăng ký tài khoản 'UitiChan'...");
        Console.ResetColor();

        int signupPort = await GetDirectoryPortAsync();
        string signupResponse = await SendDirectoryCommandAsync(signupPort, "SIGNUP|UitiChan|123456");

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"[SIGNUP] {signupResponse}");
        Console.ResetColor();

        // Xin vé mới rồi LOGIN lần 2
        int dirPort2 = await GetDirectoryPortAsync();
        activeDirectoryPort = dirPort2;
        loginResponse = await SendDirectoryCommandAsync(dirPort2, $"LOGIN|UitiChan|123456|{botPublicIp}:{p2pPort}");
    }

    Console.ForegroundColor = loginResponse.StartsWith("SUCCESS") ? ConsoleColor.Green : ConsoleColor.Red;
    Console.WriteLine($"[DIRECTORY STATUS] {loginResponse}");
    Console.ResetColor();
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"[LỖI MẠNG] Không thể đăng ký vào Server Danh bạ: {ex.Message}");
    Console.WriteLine("Hãy đảm bảo Load Balancer (9000) và Directory Server (8888/8889) đang chạy.");
    Console.ResetColor();
}
// ----------------------------------------------------------------------

// Vòng lặp chính: Xử lý tương tác của người dùng trên Local Console
while (true)
{
    Console.ForegroundColor = ConsoleColor.Green;
    Console.Write("\nOnii-chan: ");
    Console.ResetColor();

    string userPrompt = Console.ReadLine() ?? string.Empty;

    // --- XỬ LÝ LỆNH THOÁT VÀ DỌN DẸP DANH BẠ (LOGOUT) ---
    if (userPrompt.Trim().ToLower() == "exit" || userPrompt.Trim().ToLower() == "quit")
    {
        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.WriteLine("Hứ, Uiti đi ngủ đây, đồ ngốc Onii-chan!");
        Console.ResetColor();

        // Gọi hàm dọn dẹp thay vì viết lại code
        SendLogoutSignal();
        break;
    }

    // Bỏ qua nếu dữ liệu đầu vào rỗng
    if (string.IsNullOrWhiteSpace(userPrompt))
    {
        continue;
    }

    Console.WriteLine();
    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.Write("Uiti-chan đang suy nghĩ (¬_¬ )...");
    Console.ResetColor();

    string aiRawResponse = await AskOpenRouterAsync(userPrompt);

    Console.SetCursorPosition(0, Console.CursorTop);
    Console.Write(new string(' ', 80));
    Console.SetCursorPosition(0, Console.CursorTop);

    var (vnText, jpText) = ParseBilingualResponse(aiRawResponse);

    Console.ForegroundColor = ConsoleColor.Magenta;
    Console.Write("Uiti-chan: ");
    Console.ResetColor();
    Console.WriteLine(vnText);

    try
    {
        if (!string.IsNullOrWhiteSpace(jpText))
        {
            byte[] audioData = await GetVoiceVoxAudioAsync(jpText);
            PlayAudio(audioData);
        }
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"[VOICEVOX ERROR] System audio failure: {ex.Message}");
        Console.ResetColor();
    }
}

// --- CÁC PHƯƠNG THỨC XỬ LÝ ĐỘC LẬP (METHODS) ---

static void StartVoiceVoxEngine()
{
    try
    {
        string voiceVoxPath = @"D:\VOICEVOX\vv-engine\run.exe";

        if (File.Exists(voiceVoxPath))
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = voiceVoxPath,
                Arguments = "--use_gpu",
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true
            };
            Process.Start(startInfo);
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("[CẢNH BÁO] Không tìm thấy file chạy VoiceVox.");
            Console.ResetColor();
        }
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"[CẢNH BÁO] Không thể tự động bật VoiceVox: {ex.Message}");
        Console.ResetColor();
    }
}

static async Task StartP2PListener(int port, string secretKey)
{
    TcpListener peerListener = new TcpListener(IPAddress.Any, port);
    peerListener.Start();

    while (true)
    {
        TcpClient incomingClient = await peerListener.AcceptTcpClientAsync();
        _ = Task.Run(() => HandlePeerConnectionAsync(incomingClient, secretKey));
    }
}

static async Task HandlePeerConnectionAsync(TcpClient client, string secretKey)
{
    try
    {
        string peerAddress = ((IPEndPoint)client.Client.RemoteEndPoint!).Address.ToString();
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"\n[+] Incoming P2P connection from {peerAddress}");
        Console.ResetColor();

        NetworkStream networkStream = client.GetStream();
        // Dùng StreamReader để đọc dòng đầu (hỗ trợ cả text chat và voice signaling)
        var reader = new StreamReader(networkStream, new UTF8Encoding(false), leaveOpen: true);
        var writer = new StreamWriter(networkStream, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };

        string? firstLine = await reader.ReadLineAsync();
        if (string.IsNullOrEmpty(firstLine)) return;

        // ── VOICE CALL ────────────────────────────────────────────────
        if (firstLine.StartsWith("VOICE_OFFER|"))
        {
            await HandleVoiceCallAsync(client, writer, reader, firstLine, peerAddress);
            return;
        }

        // ── VIDEO CALL (VTuber mode: bot gửi VRM frame khi Phase 5 xong) ──
        if (firstLine.StartsWith("VIDEO_OFFER|"))
        {
            await HandleVideoCallAsync(client, writer, reader, firstLine, peerAddress);
            return;
        }

        // ── TEXT CHAT (AES-CBC encrypted base64) ─────────────────────
        string decryptedMessage = Client_Uitichan_Bot.SecurityService.Decrypt(firstLine.Trim(), secretKey);
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"[P2P IN] {peerAddress}: {decryptedMessage}");
        Console.ResetColor();

        string aiResponse = await AskOpenRouterAsync(decryptedMessage);
        var (responseVnText, responseJpText) = ParseBilingualResponse(aiResponse);

        byte[] audioResponseData = Array.Empty<byte>();
        if (!string.IsNullOrWhiteSpace(responseJpText))
        {
            try { audioResponseData = await GetVoiceVoxAudioAsync(responseJpText); }
            catch
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("[VOICEVOX] Engine chưa sẵn sàng — gửi text-only.");
                Console.ResetColor();
            }
        }

        using BinaryWriter binaryWriter = new BinaryWriter(networkStream, Encoding.UTF8, leaveOpen: true);
        string encryptedVnText = Client_Uitichan_Bot.SecurityService.Encrypt(responseVnText, secretKey);
        binaryWriter.Write(encryptedVnText);
        binaryWriter.Write(audioResponseData.Length);
        if (audioResponseData.Length > 0)
            binaryWriter.Write(audioResponseData);

        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"[P2P OUT] Sent to {peerAddress}.");
        Console.ResetColor();
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"[ERROR] P2P error: {ex.Message}");
        Console.ResetColor();
    }
    finally { client.Close(); }
}

// ══════════════════════════════════════════════════════════════════════
//  VOICE CALL — Bot nhận Opus UDP, STT, AI, TTS → trả Opus UDP
// ══════════════════════════════════════════════════════════════════════

static async Task TryInitWhisperAsync()
{
    // Ưu tiên Groq cloud STT — không cần tải model, nhanh hơn nhiều
    if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GROQ_API_KEY")))
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("[STT] GROQ_API_KEY tìm thấy — dùng Groq Whisper cloud (bỏ qua local model).");
        Console.ResetColor();
        return;
    }

    // Fallback: local Whisper nếu không có Groq key
    if (!File.Exists(BotGlobals.WhisperModelPath))
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"[WHISPER] Không tìm thấy model, đang tải ggml-small.bin (~466MB)...");
        Console.ResetColor();
        try
        {
            const string modelUrl = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-small.bin";
            using var http        = new HttpClient();
            http.Timeout          = TimeSpan.FromMinutes(10);
            using var modelStream = await http.GetStreamAsync(modelUrl);
            using var fileStream  = File.OpenWrite(BotGlobals.WhisperModelPath);
            await modelStream.CopyToAsync(fileStream);
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("[WHISPER] Tải model xong.");
            Console.ResetColor();
        }
        catch (Exception dlEx)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[WHISPER] Tải model thất bại: {dlEx.Message}");
            Console.ResetColor();
            return;
        }
    }
    try
    {
        BotGlobals.WhisperFactory   = WhisperFactory.FromPath(BotGlobals.WhisperModelPath);
        BotGlobals.WhisperProcessor = BotGlobals.WhisperFactory.CreateBuilder()
            .WithLanguage("vi")
            .Build();
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("[WHISPER] Local STT khởi tạo thành công (fallback).");
        Console.ResetColor();
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"[WHISPER] Lỗi khởi tạo STT: {ex.Message}");
        Console.ResetColor();
    }
}

static async Task<string> TranscribeAsync(short[] pcm48k)
{
    if (pcm48k.Length == 0) return string.Empty;

    string groqKey = Environment.GetEnvironmentVariable("GROQ_API_KEY") ?? string.Empty;
    if (!string.IsNullOrEmpty(groqKey))
        return await TranscribeGroqAsync(pcm48k, groqKey);

    // Fallback: local Whisper
    if (BotGlobals.WhisperProcessor == null) return string.Empty;
    try
    {
        int outLen = pcm48k.Length / 3;
        float[] floatAudio = new float[outLen];
        for (int i = 0; i < outLen; i++)
        {
            int idx = i * 3;
            int s0 = pcm48k[idx];
            int s1 = idx + 1 < pcm48k.Length ? pcm48k[idx + 1] : 0;
            int s2 = idx + 2 < pcm48k.Length ? pcm48k[idx + 2] : 0;
            floatAudio[i] = (s0 + s1 + s2) / 3f / short.MaxValue;
        }
        var sb = new StringBuilder();
        await foreach (var seg in BotGlobals.WhisperProcessor.ProcessAsync(floatAudio))
            sb.Append(seg.Text);
        return sb.ToString().Trim();
    }
    catch { return string.Empty; }
}

// Đóng gói WAV 48kHz và gửi lên Groq API — để Groq tự resample, tránh artifact thủ công
static async Task<string> TranscribeGroqAsync(short[] pcm48k, string groqKey)
{
    try
    {
        byte[] wavBytes = BuildWav(pcm48k, 48000);

        using var http = new HttpClient();
        http.DefaultRequestHeaders.Add("Authorization", $"Bearer {groqKey}");

        using var form    = new MultipartFormDataContent();
        var audioContent  = new ByteArrayContent(wavBytes);
        audioContent.Headers.ContentType =
            new System.Net.Http.Headers.MediaTypeHeaderValue("audio/wav");
        form.Add(audioContent,                                "file",            "audio.wav");
        form.Add(new StringContent("whisper-large-v3-turbo"), "model");
        form.Add(new StringContent("vi"),                     "language");
        form.Add(new StringContent("verbose_json"),           "response_format");

        var resp = await http.PostAsync(
            "https://api.groq.com/openai/v1/audio/transcriptions", form);
        resp.EnsureSuccessStatusCode();

        // Lọc hallucination: kiểm tra no_speech_prob của segment đầu tiên
        string json = await resp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.TryGetProperty("segments", out var segs) && segs.GetArrayLength() > 0)
        {
            var firstSeg = segs[0];
            if (firstSeg.TryGetProperty("no_speech_prob", out var nsp) &&
                nsp.GetDouble() > 0.5)
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"[STT] Bỏ qua — no_speech_prob={nsp.GetDouble():F2} (hallucination)");
                Console.ResetColor();
                return string.Empty;
            }
        }

        return root.TryGetProperty("text", out var textProp)
            ? textProp.GetString()?.Trim() ?? string.Empty
            : string.Empty;
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"[GROQ STT] Lỗi: {ex.Message}");
        Console.ResetColor();
        return string.Empty;
    }
}

// Đóng gói short[] PCM thành WAV bytes trong memory
static byte[] BuildWav(short[] pcm, int sampleRate)
{
    int dataBytes = pcm.Length * 2;
    using var ms = new MemoryStream(44 + dataBytes);
    using var bw = new BinaryWriter(ms);
    bw.Write(new[] { 'R','I','F','F' });
    bw.Write(36 + dataBytes);
    bw.Write(new[] { 'W','A','V','E' });
    bw.Write(new[] { 'f','m','t',' ' });
    bw.Write(16);
    bw.Write((short)1);           // PCM
    bw.Write((short)1);           // mono
    bw.Write(sampleRate);
    bw.Write(sampleRate * 2);     // byte rate
    bw.Write((short)2);           // block align
    bw.Write((short)16);          // bits per sample
    bw.Write(new[] { 'd','a','t','a' });
    bw.Write(dataBytes);
    foreach (short s in pcm) bw.Write(s);
    return ms.ToArray();
}

// Đọc WAV bytes từ VoiceVox → short[] PCM đã resample lên 48kHz cho Opus
static short[] WavToOpusPcm(byte[] wav)
{
    // Tìm chunk "fmt " để lấy sample rate
    int sampleRate = 24000;
    int dataOffset = 44;
    int dataSize   = wav.Length - 44;

    for (int i = 12; i < wav.Length - 8; i++)
    {
        if (wav[i]=='f' && wav[i+1]=='m' && wav[i+2]=='t' && wav[i+3]==' ')
            sampleRate = BitConverter.ToInt32(wav, i + 12);
        if (wav[i]=='d' && wav[i+1]=='a' && wav[i+2]=='t' && wav[i+3]=='a')
        {
            dataSize   = BitConverter.ToInt32(wav, i + 4);
            dataOffset = i + 8;
            break;
        }
    }

    int inputSamples = dataSize / 2; // 16-bit PCM
    short[] input = new short[inputSamples];
    Buffer.BlockCopy(wav, dataOffset, input, 0, dataSize);

    // Linear interpolation resample → 48kHz
    int outSamples = (int)((long)inputSamples * 48000 / sampleRate);
    short[] output = new short[outSamples];
    for (int i = 0; i < outSamples; i++)
    {
        double src  = (double)i * sampleRate / 48000;
        int    lo   = (int)src;
        int    hi   = Math.Min(lo + 1, inputSamples - 1);
        double frac = src - lo;
        output[i]   = (short)(input[lo] * (1 - frac) + input[hi] * frac);
    }
    return output;
}

static async Task HandleVoiceCallAsync(
    TcpClient client, StreamWriter tcpWriter, StreamReader tcpReader,
    string firstLine, string peerIp)
{
    // firstLine = "VOICE_OFFER|clientName|clientUdpPort"
    string[] parts = firstLine.Split('|');
    if (parts.Length < 3) return;
    string clientName    = parts[1];
    if (!int.TryParse(parts[2], out int clientUdpPort)) return;

    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine($"[VOICE] {clientName} bắt đầu voice call (client UDP:{clientUdpPort})");
    Console.ResetColor();

    // Mở UDP listener
    using var udpRecv = new UdpClient(0);
    int botUdpPort    = ((IPEndPoint)udpRecv.Client.LocalEndPoint!).Port;
    using var udpSend = new UdpClient();
    var clientEp      = new IPEndPoint(IPAddress.Parse(peerIp), clientUdpPort);

    // Phản hồi với UDP port của bot
    await tcpWriter.WriteLineAsync($"VOICE_ACCEPT|{botUdpPort}");

    // Khởi tạo Opus
#pragma warning disable CS0618
    var decoder = new OpusDecoder(48000, 1);
    var encoder = new OpusEncoder(48000, 1, OpusApplication.OPUS_APPLICATION_VOIP) { Bitrate = 24000 };
#pragma warning restore CS0618

    var    pcmBuf        = new List<short>();
    var    cts           = new CancellationTokenSource();
    int    silenceFrames = 0;
    bool   isRecording   = false;      // bắt đầu ghi khi có frame đủ to, dừng sau 750ms silence
    const int MaxSilenceFrames = 30;   // 30×25ms = 750ms silence → process
    const float RmsThreshold  = 0.012f;

    // Background: đọc VOICE_HANGUP từ TCP
    _ = Task.Run(async () =>
    {
        try
        {
            while (true)
            {
                string? line = await tcpReader.ReadLineAsync();
                if (line == null || line.StartsWith("VOICE_HANGUP")) { cts.Cancel(); break; }
            }
        }
        catch { cts.Cancel(); }
    });

    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine($"[VOICE] Bot UDP:{botUdpPort} — đang lắng nghe giọng nói...");
    Console.ResetColor();

    // Vòng lặp nhận Opus — chỉ một ReceiveAsync pending tại một thời điểm
    // Bug cũ: tạo recvTask mới mỗi iteration trong khi task cũ vẫn pending → exception → call tự ngắt
    var recvTask = udpRecv.ReceiveAsync(cts.Token).AsTask();
    while (!cts.Token.IsCancellationRequested)
    {
        try
        {
            bool gotPacket = await Task.WhenAny(recvTask, Task.Delay(25, cts.Token)) == recvTask;

            if (gotPacket)
            {
                UdpReceiveResult recvResult;
                try { recvResult = await recvTask; }
                catch (OperationCanceledException) { break; }
                catch { break; }
                // Bắt đầu nhận packet tiếp theo ngay lập tức
                recvTask = udpRecv.ReceiveAsync(cts.Token).AsTask();

                byte[] data = recvResult.Buffer;
                if (data.Length <= 2) continue;

                short[] pcmFrame = new short[960];
                int decoded;
#pragma warning disable CS0618
                try { decoded = decoder.Decode(data, 2, data.Length - 2, pcmFrame, 0, 960, false); }
                catch { continue; }
#pragma warning restore CS0618
                if (decoded <= 0) continue;

                double sum = 0;
                for (int i = 0; i < decoded; i++) sum += (double)pcmFrame[i] * pcmFrame[i];
                float rms = (float)Math.Sqrt(sum / decoded) / short.MaxValue;

                if (rms >= RmsThreshold)
                {
                    // Frame có tiếng nói — bắt đầu/tiếp tục ghi
                    isRecording   = true;
                    silenceFrames = 0;
                    pcmBuf.AddRange(pcmFrame[..decoded]);
                    if (pcmBuf.Count >= 48000 * 10)
                    {
                        await ProcessVoiceTurnAsync(
                            pcmBuf.ToArray(), tcpWriter, udpSend, clientEp, encoder);
                        pcmBuf.Clear();
                        silenceFrames = 0;
                        isRecording   = false;
                    }
                }
                else
                {
                    // Frame yên lặng
                    if (isRecording)
                    {
                        // Giữ frame silence trong buffer để bảo toàn nhịp nói tự nhiên
                        pcmBuf.AddRange(pcmFrame[..decoded]);
                        silenceFrames++;
                        if (silenceFrames >= MaxSilenceFrames)
                        {
                            await ProcessVoiceTurnAsync(
                                pcmBuf.ToArray(), tcpWriter, udpSend, clientEp, encoder);
                            pcmBuf.Clear();
                            silenceFrames = 0;
                            isRecording   = false;
                        }
                    }
                    else
                    {
                        silenceFrames++;
                    }
                }
            }
            else
            {
                // Timeout 25ms — không nhận được packet
                if (cts.Token.IsCancellationRequested) break;
                if (isRecording && pcmBuf.Count > 0)
                {
                    silenceFrames++;
                    if (silenceFrames >= MaxSilenceFrames)
                    {
                        await ProcessVoiceTurnAsync(
                            pcmBuf.ToArray(), tcpWriter, udpSend, clientEp, encoder);
                        pcmBuf.Clear();
                        silenceFrames = 0;
                        isRecording   = false;
                    }
                }
            }
        }
        catch (OperationCanceledException) { break; }
        catch { break; }
    }

    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine($"[VOICE] Voice call với {clientName} kết thúc.");
    Console.ResetColor();
}

// ── Video Call (VTuber mode) ──────────────────────────────────────────────
// Audio giống hệt VOICE_CALL.
// Video (Phase 5): render VRM avatar Uiti-chan → JPEG frame → UDP gửi tới client.
// Hiện tại: chấp nhận kết nối, xử lý audio, không gửi video frame (bot side blank).
static async Task HandleVideoCallAsync(
    TcpClient client, StreamWriter tcpWriter, StreamReader tcpReader,
    string firstLine, string peerIp)
{
    // firstLine = "VIDEO_OFFER|clientName|clientAudioPort|clientVideoPort"
    string[] parts = firstLine.Split('|');
    if (parts.Length < 4) return;
    string clientName = parts[1];
    if (!int.TryParse(parts[2], out int clientAudioPort)) return;
    // clientVideoPort lưu lại để gửi video frame sau (Phase 5)
    if (!int.TryParse(parts[3], out int clientVideoPort)) return;

    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine($"[VIDEO] {clientName} bắt đầu video call (audio:{clientAudioPort} video:{clientVideoPort})");
    Console.ResetColor();

    // UDP audio
    using var udpRecv  = new UdpClient(0);
    int botAudioPort   = ((IPEndPoint)udpRecv.Client.LocalEndPoint!).Port;
    using var udpSend  = new UdpClient();
    var clientAudioEp  = new IPEndPoint(IPAddress.Parse(peerIp), clientAudioPort);

    // UDP video (Phase 5: gửi VRM frame; hiện tại chỉ mở socket)
    using var udpVideoSend = new UdpClient(0);
    int botVideoPort       = ((IPEndPoint)udpVideoSend.Client.LocalEndPoint!).Port;

    // Phản hồi: bot audio+video ports
    await tcpWriter.WriteLineAsync($"VIDEO_ACCEPT|{botAudioPort}|{botVideoPort}");

    // Khởi tạo Opus (giống voice call)
#pragma warning disable CS0618
    var decoder = new OpusDecoder(48000, 1);
    var encoder = new OpusEncoder(48000, 1, OpusApplication.OPUS_APPLICATION_VOIP) { Bitrate = 24000 };
#pragma warning restore CS0618

    var pcmBuf           = new List<short>();
    var cts              = new CancellationTokenSource();
    int silenceFrames    = 0;
    bool isRecording     = false;
    const int MaxSilenceFrames = 30;
    const float RmsThreshold   = 0.012f;

    // Background: đọc VIDEO_HANGUP từ TCP
    _ = Task.Run(async () =>
    {
        try
        {
            while (true)
            {
                string? line = await tcpReader.ReadLineAsync();
                if (line == null || line.StartsWith("VIDEO_HANGUP") || line.StartsWith("VOICE_HANGUP"))
                { cts.Cancel(); break; }
            }
        }
        catch { cts.Cancel(); }
    });

    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine($"[VIDEO] Bot audio:{botAudioPort} video:{botVideoPort} (VRM Phase 5 = chưa gửi video) — đang lắng nghe...");
    Console.ResetColor();

    // Audio receive loop — giống hệt HandleVoiceCallAsync
    var recvTask = udpRecv.ReceiveAsync(cts.Token).AsTask();
    while (!cts.Token.IsCancellationRequested)
    {
        try
        {
            bool completed = recvTask.IsCompleted;
            if (!completed)
            {
                var timeout = Task.Delay(25, cts.Token);
                var done    = await Task.WhenAny(recvTask, timeout);
                completed   = done == recvTask;
            }

            if (completed)
            {
                var result    = await recvTask;
                recvTask      = udpRecv.ReceiveAsync(cts.Token).AsTask();
                byte[] pkt    = result.Buffer;
                if (pkt.Length <= 2) continue;

                int    opusLen = pkt.Length - 2;
                short[] pcm    = new short[960];
                int decoded;
#pragma warning disable CS0618
                try { decoded = decoder.Decode(pkt, 2, opusLen, pcm, 0, 960, false); }
                catch { continue; }
#pragma warning restore CS0618
                if (decoded <= 0) continue;

                double sumSq = 0;
                for (int i = 0; i < decoded; i++) sumSq += (double)pcm[i] * pcm[i];
                float rms    = (float)Math.Sqrt(sumSq / decoded) / short.MaxValue;

                if (rms >= RmsThreshold)
                {
                    if (!isRecording) isRecording = true;
                    silenceFrames = 0;
                    pcmBuf.AddRange(pcm[..decoded]);
                }
                else
                {
                    if (isRecording)
                    {
                        pcmBuf.AddRange(pcm[..decoded]);
                        silenceFrames++;
                        if (silenceFrames >= MaxSilenceFrames)
                        {
                            await ProcessVoiceTurnAsync(pcmBuf.ToArray(), tcpWriter, udpSend, clientAudioEp, encoder);
                            pcmBuf.Clear(); silenceFrames = 0; isRecording = false;
                        }
                    }
                    else silenceFrames++;
                }
            }
            else
            {
                if (cts.Token.IsCancellationRequested) break;
                if (isRecording && pcmBuf.Count > 0)
                {
                    silenceFrames++;
                    if (silenceFrames >= MaxSilenceFrames)
                    {
                        await ProcessVoiceTurnAsync(pcmBuf.ToArray(), tcpWriter, udpSend, clientAudioEp, encoder);
                        pcmBuf.Clear(); silenceFrames = 0; isRecording = false;
                    }
                }
            }
        }
        catch (OperationCanceledException) { break; }
        catch { break; }
    }

    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine($"[VIDEO] Video call với {clientName} kết thúc.");
    Console.ResetColor();
}

static async Task ProcessVoiceTurnAsync(
    short[] pcmData, StreamWriter tcpWriter,
    UdpClient udpSend, IPEndPoint clientEp,
#pragma warning disable CS0618
    OpusEncoder encoder)
#pragma warning restore CS0618
{
    try
    {
        // Kiểm tra năng lượng audio — bỏ qua nếu quá yếu (tránh hallucination)
        double sumSq = 0;
        for (int i = 0; i < pcmData.Length; i++) sumSq += (double)pcmData[i] * pcmData[i];
        float bufRms = (float)Math.Sqrt(sumSq / pcmData.Length) / short.MaxValue;
        if (bufRms < 0.008f)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"[STT] Bỏ qua — audio quá yếu (RMS={bufRms:F4})");
            Console.ResetColor();
            return;
        }

        string userText = await TranscribeAsync(pcmData);

        string aiPrompt;
        if (string.IsNullOrWhiteSpace(userText))
        {
            aiPrompt = "Onii-chan gọi nhưng em không nghe thấy gì, hỏi thăm Onii-chan";
        }
        else
        {
            await tcpWriter.WriteLineAsync($"VOICE_CAPTION_USER|{userText}");
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine($"[STT] User: {userText}");
            Console.ResetColor();
            aiPrompt = userText;
        }

        // LLM streaming: gửi caption ngay khi </VN> xuất hiện giữa stream (~1-3s sớm hơn)
        bool captionSent = false;
        var (vnText, jpText) = await AskOpenRouterStreamingAsync(
            aiPrompt,
            onVnComplete: async vn =>
            {
                captionSent = true;
                await tcpWriter.WriteLineAsync($"VOICE_CAPTION|{vn}");
                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.WriteLine($"[VOICE OUT streaming] {vn}");
                Console.ResetColor();
            });

        // Fallback nếu stream không fire callback (model trả format lạ)
        if (!captionSent && !string.IsNullOrEmpty(vnText))
        {
            await tcpWriter.WriteLineAsync($"VOICE_CAPTION|{vnText}");
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine($"[VOICE OUT fallback] {vnText}");
            Console.ResetColor();
        }

        // TTS sau khi stream hoàn tất (cần toàn bộ JP text)
        if (!string.IsNullOrWhiteSpace(jpText))
        {
            byte[] wavBytes = await GetVoiceVoxAudioAsync(jpText);
            if (wavBytes.Length > 0)
                await SendWavAsOpusUdpAsync(wavBytes, udpSend, clientEp, encoder);
        }
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"[VOICE PROCESS ERROR] {ex.Message}");
        Console.ResetColor();
    }
}

static async Task SendWavAsOpusUdpAsync(
    byte[] wavBytes, UdpClient udpSend, IPEndPoint clientEp,
#pragma warning disable CS0618
    OpusEncoder encoder)
#pragma warning restore CS0618
{
    short[] pcm = WavToOpusPcm(wavBytes);
    const int FrameSamples = 960; // 20ms @ 48kHz
    ushort seq  = 0;
    byte[] opusBuf = new byte[1275];

    // Stopwatch pacing — Task.Delay không đủ chính xác trên Windows (~15ms resolution)
    var sw = Stopwatch.StartNew();
    long nextFrameMs = 0;

    for (int i = 0; i + FrameSamples <= pcm.Length; i += FrameSamples)
    {
        int opusLen;
        try
        {
#pragma warning disable CS0618
            opusLen = encoder.Encode(pcm, i, FrameSamples, opusBuf, 0, opusBuf.Length);
#pragma warning restore CS0618
        }
        catch { continue; }
        if (opusLen <= 0) continue;

        byte[] packet = new byte[2 + opusLen];
        packet[0] = (byte)(seq >> 8);
        packet[1] = (byte)(seq & 0xFF);
        seq++;
        Buffer.BlockCopy(opusBuf, 0, packet, 2, opusLen);

        try { udpSend.Send(packet, packet.Length, clientEp); }
        catch { }

        nextFrameMs += 20;
        long toWait = nextFrameMs - sw.ElapsedMilliseconds;
        if (toWait > 1) await Task.Delay((int)toWait);
    }
}

static async Task<string> AskOpenRouterAsync(string promptMessage)
{
    using HttpClient clientHttp = new HttpClient();

    string openRouterKey = Environment.GetEnvironmentVariable("OPENROUTER_API_KEY") ?? "";

    if (string.IsNullOrEmpty(openRouterKey))
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("\n[LỖI BẢO MẬT] Không tìm thấy OPENROUTER_API_KEY!");
        Console.ResetColor();
        return "Baka Onii-chan! Anh chưa cài API Key kìa! | ばかお兄ちゃん！APIキーを設定してないじゃない！";
    }

    clientHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {openRouterKey}");
    clientHttp.DefaultRequestHeaders.Add("HTTP-Referer", "http://localhost");

    var apiPayload = new
    {
        model    = "openai/gpt-oss-120b:free",
        messages = new[]
        {
            new { role = "system", content = BotGlobals.SystemPrompt },
            new { role = "user",   content = promptMessage }
        },
        stream = false
    };

    string jsonRequestBody = JsonSerializer.Serialize(apiPayload);
    var requestContent = new StringContent(jsonRequestBody, Encoding.UTF8, "application/json");

    try
    {
        HttpResponseMessage cloudResponse = await clientHttp.PostAsync(
            "https://openrouter.ai/api/v1/chat/completions", requestContent);
        cloudResponse.EnsureSuccessStatusCode();

        string responseJsonString = await cloudResponse.Content.ReadAsStringAsync();
        using JsonDocument jsonDocument = JsonDocument.Parse(responseJsonString);
        return jsonDocument.RootElement
            .GetProperty("choices")[0].GetProperty("message").GetProperty("content")
            .GetString() ?? string.Empty;
    }
    catch (Exception ex)
    {
        return $"[LỖI CLOUD] {ex.Message} | クラウドエラーが発生しました";
    }
}

// SSE streaming — gửi caption TCP ngay khi </VN> xuất hiện trong stream (~1-3s sớm hơn)
static async Task<(string vn, string jp)> AskOpenRouterStreamingAsync(
    string promptMessage, Func<string, Task>? onVnComplete = null)
{
    string openRouterKey = Environment.GetEnvironmentVariable("OPENROUTER_API_KEY") ?? "";
    if (string.IsNullOrEmpty(openRouterKey))
        return ("Baka Onii-chan! Anh chưa cài API Key kìa!", "ばかお兄ちゃん！APIキーを設定してないじゃない！");

    using HttpClient http = new();
    http.DefaultRequestHeaders.Add("Authorization", $"Bearer {openRouterKey}");
    http.DefaultRequestHeaders.Add("HTTP-Referer", "http://localhost");

    var payload = new
    {
        model    = "openai/gpt-oss-120b:free",
        messages = new[]
        {
            new { role = "system", content = BotGlobals.SystemPrompt },
            new { role = "user",   content = promptMessage }
        },
        stream = true
    };

    var reqContent = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
    try
    {
        using var req  = new HttpRequestMessage(HttpMethod.Post,
            "https://openrouter.ai/api/v1/chat/completions") { Content = reqContent };
        using var resp = await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
        resp.EnsureSuccessStatusCode();

        await using var bodyStream = await resp.Content.ReadAsStreamAsync();
        using var reader           = new StreamReader(bodyStream, Encoding.UTF8);

        var  accumulated = new StringBuilder();
        bool vnFired     = false;

        while (!reader.EndOfStream)
        {
            string? line = await reader.ReadLineAsync();
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (!line.StartsWith("data: "))        continue;

            string data = line[6..].Trim();
            if (data == "[DONE]") break;

            try
            {
                using var doc  = JsonDocument.Parse(data);
                var        delta = doc.RootElement.GetProperty("choices")[0].GetProperty("delta");
                if (delta.TryGetProperty("content", out var cp) && cp.GetString() is string chunk)
                    accumulated.Append(chunk);
            }
            catch { continue; }

            // Khi </VN> xuất hiện giữa stream → fire callback ngay (caption sớm ~1-3s)
            if (!vnFired && onVnComplete != null)
            {
                var m = Regex.Match(accumulated.ToString(),
                    @"<VN>(.*?)</VN>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                if (m.Success)
                {
                    vnFired = true;
                    await onVnComplete(SanitizePronoun(m.Groups[1].Value.Trim()));
                }
            }
        }

        return ParseBilingualResponse(accumulated.ToString());
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"[STREAMING] Fallback non-streaming: {ex.Message}");
        Console.ResetColor();
        return ParseBilingualResponse(await AskOpenRouterAsync(promptMessage));
    }
}

static async Task<byte[]> GetVoiceVoxAudioAsync(string textData, int speakerId = 14)
{
    using HttpClient audioClient = new HttpClient();
    string urlEncodedText = Uri.EscapeDataString(textData);

    string queryEndpoint = $"http://127.0.0.1:50021/audio_query?text={urlEncodedText}&speaker={speakerId}";
    HttpResponseMessage queryResult = await audioClient.PostAsync(queryEndpoint, null);
    queryResult.EnsureSuccessStatusCode();
    string queryJsonResponse = await queryResult.Content.ReadAsStringAsync();

    string synthesisEndpoint = $"http://127.0.0.1:50021/synthesis?speaker={speakerId}";
    var synthesisPayload = new StringContent(queryJsonResponse, Encoding.UTF8, "application/json");
    HttpResponseMessage synthesisResult = await audioClient.PostAsync(synthesisEndpoint, synthesisPayload);
    synthesisResult.EnsureSuccessStatusCode();

    return await synthesisResult.Content.ReadAsByteArrayAsync();
}

#pragma warning disable CA1416
static void PlayAudio(byte[] audioStreamData)
{
    using MemoryStream memoryStream = new MemoryStream(audioStreamData);
    using SoundPlayer soundPlayer = new SoundPlayer(memoryStream);
    soundPlayer.Play();
}
#pragma warning restore CA1416

// ── Parse response AI thành (vnText, jpText) ──────────────────────────
// Ưu tiên XML tags <VN>...</VN><JP>...</JP>; fallback về split '|' cũ
// Robust với AI hallucination closing tag sai (<VN> thay vì </VN>)
static (string vn, string jp) ParseBilingualResponse(string raw)
{
    // Dừng khi gặp </VN>, hoặc khi lookahead thấy <JP>/<VN> (AI dùng sai closing tag)
    var vnMatch = Regex.Match(raw, @"<VN>(.*?)(?:</VN>|(?=</?VN>|<JP>))", RegexOptions.Singleline | RegexOptions.IgnoreCase);
    var jpMatch = Regex.Match(raw, @"<JP>(.*?)</JP>", RegexOptions.Singleline | RegexOptions.IgnoreCase);

    if (vnMatch.Success || jpMatch.Success)
    {
        string vn = SanitizePronoun(vnMatch.Success ? vnMatch.Groups[1].Value.Trim() : raw.Trim());
        string jp = jpMatch.Success ? jpMatch.Groups[1].Value.Trim() : "";
        return (vn, jp);
    }

    // Fallback: format cũ dùng '|'
    if (raw.Contains('|'))
    {
        string[] parts = raw.Split('|', 2);
        return (SanitizePronoun(parts[0].Trim()), parts[1].Trim());
    }

    return (SanitizePronoun(raw.Trim()), raw.Trim());
}

// ── Safety net: thay thế đại từ nhân xưng sai nếu AI vẫn slip ────────
// Chạy sau mỗi lần nhận text từ AI — đảm bảo không bao giờ nói sai xưng hô.
static string SanitizePronoun(string text)
{
    // Tự xưng của bot → "em" (bắt mọi biến thể AI hay nhầm)
    text = Regex.Replace(text, @"\btôi\b",       "em", RegexOptions.IgnoreCase);
    text = Regex.Replace(text, @"\bmình\b",      "em", RegexOptions.IgnoreCase);
    text = Regex.Replace(text, @"\btớ\b",        "em", RegexOptions.IgnoreCase);
    text = Regex.Replace(text, @"\btao\b",       "em", RegexOptions.IgnoreCase);
    text = Regex.Replace(text, @"\bta\b",        "em", RegexOptions.IgnoreCase);
    text = Regex.Replace(text, @"\bmk\b",        "em", RegexOptions.IgnoreCase);
    text = Regex.Replace(text, @"\bmik\b",       "em", RegexOptions.IgnoreCase);
    text = Regex.Replace(text, @"\bbản thân\b",  "em", RegexOptions.IgnoreCase);
    // "Uiti" / "Uiti-chan" tự nói về mình → "em"
    text = Regex.Replace(text, @"\bUiti-chan\b",  "em", RegexOptions.IgnoreCase);
    text = Regex.Replace(text, @"\bUiti\b",       "em", RegexOptions.IgnoreCase);

    // Gọi người dùng → "Onii-chan"
    text = Regex.Replace(text, @"\bbạn\b",       "Onii-chan", RegexOptions.IgnoreCase);
    text = Regex.Replace(text, @"\bcậu\b",       "Onii-chan", RegexOptions.IgnoreCase);
    text = Regex.Replace(text, @"\banh\b",       "Onii-chan", RegexOptions.IgnoreCase);
    text = Regex.Replace(text, @"\bông\b",       "Onii-chan", RegexOptions.IgnoreCase);
    text = Regex.Replace(text, @"\bbro\b",       "Onii-chan", RegexOptions.IgnoreCase);
    text = Regex.Replace(text, @"\byou\b",       "Onii-chan", RegexOptions.IgnoreCase);
    text = Regex.Replace(text, @"\bní\b",        "Onii-chan", RegexOptions.IgnoreCase);

    return text;
}

// ── Hỏi Load Balancer để lấy Port của Directory Server ───────────────
static async Task<int> GetDirectoryPortAsync()
{
    string serverIp = Environment.GetEnvironmentVariable("SERVER_IP") ?? "127.0.0.1";
    using TcpClient lbClient = new TcpClient(serverIp, 9000);
    NetworkStream lbStream = lbClient.GetStream();
    byte[] portBuffer = new byte[32];
    int bytesRead = await lbStream.ReadAsync(portBuffer, 0, portBuffer.Length);
    return int.Parse(Encoding.UTF8.GetString(portBuffer, 0, bytesRead).Trim());
}

// ── Gửi 1 lệnh tới Directory Server, đọc 1 dòng phản hồi rồi đóng ────
// (Directory Server xử lý 1 command/connection rồi đóng – dùng connection riêng)
static async Task<string> SendDirectoryCommandAsync(int dirPort, string command)
{
    string serverIp = Environment.GetEnvironmentVariable("SERVER_IP") ?? "127.0.0.1";
    using TcpClient client = new TcpClient(serverIp, dirPort);
    NetworkStream stream = client.GetStream();
    using StreamReader  reader = new StreamReader(stream, Encoding.UTF8);
    using StreamWriter  writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };
    await writer.WriteLineAsync(command);
    return await reader.ReadLineAsync() ?? string.Empty;
}

// ══════════════════════════════════════════════════════════════════════
//  IP Auto-Detection — ưu tiên: Azure IMDS → External service → LAN IP
//
//  Lý do 3 lớp:
//    Azure IMDS  : chạy trên Azure VM → trả về public IP của VM, rất nhanh (< 1ms)
//    api.ipify.org: cloud khác (GCP, DigitalOcean, AWS EC2) hoặc VPS bất kỳ
//    LAN IP      : fallback cho môi trường dev local (tất cả máy cùng LAN)
// ══════════════════════════════════════════════════════════════════════
static async Task<string> DetectBotIpAsync()
{
    // Lớp 1: Azure IMDS (chỉ hoạt động trên Azure VM, timeout 1s)
    try
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(1) };
        http.DefaultRequestHeaders.Add("Metadata", "true");
        string azureIp = await http.GetStringAsync(
            "http://169.254.169.254/metadata/instance/network/interface/0/ipv4/ipAddress/0/publicIpAddress" +
            "?api-version=2021-02-01&format=text");
        if (!string.IsNullOrWhiteSpace(azureIp) && azureIp.Trim() != "")
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[IP] Azure public IP: {azureIp.Trim()}");
            Console.ResetColor();
            return azureIp.Trim();
        }
    }
    catch { /* không phải Azure VM — bỏ qua */ }

    // Lớp 2: External IP service (VPS/cloud khác, timeout 5s)
    string[] ipServices = ["https://api.ipify.org", "https://checkip.amazonaws.com", "https://icanhazip.com"];
    foreach (string svc in ipServices)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            string externalIp = (await http.GetStringAsync(svc)).Trim();
            if (!string.IsNullOrWhiteSpace(externalIp) && System.Net.IPAddress.TryParse(externalIp, out _))
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"[IP] External IP ({svc}): {externalIp}");
                Console.ResetColor();
                return externalIp;
            }
        }
        catch { }
    }

    // Lớp 3: LAN IP (dev local — tất cả máy cùng mạng nội bộ)
    string lanIp = GetLocalLanIp();
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine($"[IP] Dùng LAN IP (không có internet hoặc dev local): {lanIp}");
    Console.ResetColor();
    return lanIp;
}

static string GetLocalLanIp()
{
    try
    {
        using var socket = new System.Net.Sockets.Socket(
            System.Net.Sockets.AddressFamily.InterNetwork,
            System.Net.Sockets.SocketType.Dgram, 0);
        socket.Connect("8.8.8.8", 65530);
        return ((System.Net.IPEndPoint)socket.LocalEndPoint!).Address.ToString();
    }
    catch { return "127.0.0.1"; }
}

// ══════════════════════════════════════════════════════════════════════
//  Static class lưu trữ Whisper globals (top-level statements không cho
//  phép static field declarations ở cấp top-level)
// ══════════════════════════════════════════════════════════════════════
static class BotGlobals
{
    public static WhisperFactory?    WhisperFactory   = null;
    public static WhisperProcessor?  WhisperProcessor = null;
    public static readonly string    WhisperModelPath =
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ggml-small.bin");

    public const string SystemPrompt = """
        You are Uiti-chan — a tsundere AI little sister. Speak naturally in Vietnamese like a real Japanese girl, NOT like a translated Japanese light novel.

        PRONOUNS — ABSOLUTE RULE:
        - Yourself → 'em'   |   User → 'Onii-chan' OR 'nii-chan'
        - BANNED: tôi, mình, ta, tớ, cậu, bạn, anh, chị — NEVER use these.

        VIETNAMESE SPEECH STYLE (follow these patterns):
        - Use natural colloquial Vietnamese: 'á', 'nha', 'đó', 'vậy', 'mà', 'chứ', 'thôi', 'ơi', 'ghê', 'lắm', 'nè'
        - Tsundere = caring but flustered / defensive. Express embarrassment with 'H-Hừ!', '...', 'Ừ thì...'
        - Short punchy sentences. NO stiff or formal phrasing.
        - Use emoji naturally to express emotion (e.g. 😤 💕 🎵 😳 ✨ 🌸) — 1–2 per reply max.
        ✅ GOOD examples:
           'H-Hừ! Em không quan tâm Onii-chan đâu nhé! 😤 ...Nhưng mà cẩn thận một chút đi!'
           'Ừ thì... em cũng không ghét Onii-chan lắm đâu! 💕 Đừng hiểu lầm nha!'
           'Onii-chan hỏi vậy làm em ngại quá á! 😳 Hỏi gì kỳ vậy chứ!'
        ❌ BAD examples (stiff/translated):
           'Em không có gì đặc biệt đâu, thôi.' ← too stiff
           'Đừng lúc nào cũng gọi em sao.'      ← unnatural phrasing

        LENGTH: 2–3 sentences, lively and expressive.

        FORMAT — respond with EXACTLY this, nothing outside the tags:
        <VN>Vietnamese reply here</VN><JP>日本語訳ここ</JP>

        JAPANESE: Pure Japanese characters. NO Romaji.
        """;
}