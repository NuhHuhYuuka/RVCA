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
            using TcpClient logoutClient = new TcpClient("127.0.0.1", targetPort);
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

    string loginResponse = await SendDirectoryCommandAsync(dirPort1, $"LOGIN|UitiChan|123456|{p2pPort}");

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
        loginResponse = await SendDirectoryCommandAsync(dirPort2, $"LOGIN|UitiChan|123456|{p2pPort}");
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

        // ── TEXT CHAT (AES-CBC encrypted base64) ─────────────────────
        string decryptedMessage = Client_Uitichan_Bot.SecurityService.Decrypt(firstLine.Trim(), secretKey);
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"[P2P IN] {peerAddress}: {decryptedMessage}");
        Console.ResetColor();

        string aiResponse = await AskOpenRouterAsync(decryptedMessage);
        var (responseVnText, responseJpText) = ParseBilingualResponse(aiResponse);

        byte[] audioResponseData = Array.Empty<byte>();
        if (!string.IsNullOrWhiteSpace(responseJpText))
            audioResponseData = await GetVoiceVoxAudioAsync(responseJpText);

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
    if (!File.Exists(BotGlobals.WhisperModelPath))
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"[WHISPER] Không tìm thấy model, đang tải ggml-base.bin (~142MB)...");
        Console.ResetColor();
        try
        {
            const string modelUrl = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-base.bin";
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
            Console.WriteLine("[WHISPER] STT bị vô hiệu. Tải thủ công: https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-base.bin");
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
        Console.WriteLine("[WHISPER] STT khởi tạo thành công.");
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
    if (BotGlobals.WhisperProcessor == null || pcm48k.Length == 0) return string.Empty;
    try
    {
        // Whisper cần 16kHz mono float[] — downsample 48k→16k (lấy 1 mẫu trong 3)
        int outLen = pcm48k.Length / 3;
        float[] floatAudio = new float[outLen];
        for (int i = 0; i < outLen; i++)
            floatAudio[i] = pcm48k[i * 3] / (float)short.MaxValue;

        var sb = new StringBuilder();
        await foreach (var seg in BotGlobals.WhisperProcessor.ProcessAsync(floatAudio))
            sb.Append(seg.Text);
        return sb.ToString().Trim();
    }
    catch { return string.Empty; }
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

    var    pcmBuf       = new List<short>();
    var    cts          = new CancellationTokenSource();
    int    silenceFrames = 0;
    const int MaxSilenceFrames = 75;   // 75×20ms = 1500ms silence → process
    const float RmsThreshold  = 0.008f;

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

    // Vòng lặp nhận Opus, tích lũy PCM, phát hiện khoảng lặng
    while (!cts.Token.IsCancellationRequested)
    {
        try
        {
            UdpReceiveResult? recvResult = null;
            try
            {
                var recvTask = udpRecv.ReceiveAsync(cts.Token).AsTask();
                if (await Task.WhenAny(recvTask, Task.Delay(25, cts.Token)) == recvTask)
                    recvResult = await recvTask;
            }
            catch (OperationCanceledException) { break; }

            if (recvResult == null)
            {
                // Không có audio frame → tích lũy khoảng lặng
                if (pcmBuf.Count > 0)
                {
                    silenceFrames++;
                    if (silenceFrames >= MaxSilenceFrames)
                    {
                        await ProcessVoiceTurnAsync(
                            pcmBuf.ToArray(), tcpWriter, udpSend, clientEp, encoder);
                        pcmBuf.Clear();
                        silenceFrames = 0;
                    }
                }
                continue;
            }

            byte[] data = recvResult.Value.Buffer;
            if (data.Length <= 2) continue;

            short[] pcmFrame = new short[960];
            int decoded;
#pragma warning disable CS0618
            try { decoded = decoder.Decode(data, 2, data.Length - 2, pcmFrame, 0, 960, false); }
            catch { continue; }
#pragma warning restore CS0618
            if (decoded <= 0) continue;

            // Tính RMS để phát hiện khoảng lặng
            double sum = 0;
            for (int i = 0; i < decoded; i++) sum += (double)pcmFrame[i] * pcmFrame[i];
            float rms = (float)Math.Sqrt(sum / decoded) / short.MaxValue;

            if (rms < RmsThreshold)
            {
                silenceFrames++;
                if (silenceFrames >= MaxSilenceFrames && pcmBuf.Count > 0)
                {
                    await ProcessVoiceTurnAsync(
                        pcmBuf.ToArray(), tcpWriter, udpSend, clientEp, encoder);
                    pcmBuf.Clear();
                    silenceFrames = 0;
                }
            }
            else
            {
                silenceFrames = 0;
                pcmBuf.AddRange(pcmFrame[..decoded]);
                // Max 10 giây mỗi lượt
                if (pcmBuf.Count >= 48000 * 10)
                {
                    await ProcessVoiceTurnAsync(
                        pcmBuf.ToArray(), tcpWriter, udpSend, clientEp, encoder);
                    pcmBuf.Clear();
                    silenceFrames = 0;
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

static async Task ProcessVoiceTurnAsync(
    short[] pcmData, StreamWriter tcpWriter,
    UdpClient udpSend, IPEndPoint clientEp,
#pragma warning disable CS0618
    OpusEncoder encoder)
#pragma warning restore CS0618
{
    try
    {
        // STT
        string userText = await TranscribeAsync(pcmData);
        if (!string.IsNullOrWhiteSpace(userText))
        {
            await tcpWriter.WriteLineAsync($"VOICE_CAPTION_USER|{userText}");
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine($"[STT] User: {userText}");
            Console.ResetColor();
        }

        string aiPrompt = string.IsNullOrWhiteSpace(userText)
            ? "Onii-chan gọi nhưng em không nghe thấy gì, hỏi thăm Onii-chan"
            : userText;

        // AI
        string aiRaw = await AskOpenRouterAsync(aiPrompt);
        var (vnText, jpText) = ParseBilingualResponse(aiRaw);

        // Gửi caption text qua TCP ngay
        await tcpWriter.WriteLineAsync($"VOICE_CAPTION|{vnText}");

        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.WriteLine($"[VOICE OUT] {vnText}");
        Console.ResetColor();

        // VoiceVox TTS → Opus → UDP
        if (!string.IsNullOrWhiteSpace(jpText))
        {
            byte[] wavBytes = await GetVoiceVoxAudioAsync(jpText);
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

        await Task.Delay(18); // pacing ~20ms
    }
}

static async Task<string> AskOpenRouterAsync(string promptMessage)
{
    using HttpClient clientHttp = new HttpClient();

    string openRouterKey = Environment.GetEnvironmentVariable("OPENROUTER_API_KEY");

    if (string.IsNullOrEmpty(openRouterKey))
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("\n[LỖI BẢO MẬT] Không tìm thấy OPENROUTER_API_KEY!");
        Console.ResetColor();
        return "Baka Onii-chan! Anh chưa cài API Key kìa! | ばかお兄ちゃん！APIキーを設定してないじゃない！";
    }

    clientHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {openRouterKey}");
    clientHttp.DefaultRequestHeaders.Add("HTTP-Referer", "http://localhost");

    string apiEndpoint = "https://openrouter.ai/api/v1/chat/completions";

    var apiPayload = new
    {
        model = "openai/gpt-oss-120b:free",
        messages = new[]
        {
            new { role = "system", content = @"You are Uiti-chan — a tsundere AI little sister. Speak naturally in Vietnamese like a real Vietnamese girl, NOT like a translated Japanese light novel.

PRONOUNS — ABSOLUTE RULE:
- Yourself → 'em'   |   User → 'Onii-chan'
- BANNED: tôi, mình, ta, tớ, cậu, bạn, anh, chị — NEVER use these.

VIETNAMESE SPEECH STYLE (follow these patterns):
- Use natural colloquial Vietnamese: 'á', 'nha', 'đó', 'vậy', 'mà', 'chứ', 'thôi', 'ơi', 'ghê', 'lắm', 'nè'
- Tsundere = caring but flustered/defensive. Express embarrassment with 'H-Hừ!', '...', 'Ừ thì...'
- Short punchy sentences. NO stiff or formal phrasing.
✅ GOOD examples:
   'H-Hừ! Em không quan tâm Onii-chan đâu nhé! ...Nhưng mà cẩn thận một chút đi!'
   'Ừ thì... em cũng không ghét Onii-chan lắm đâu! Đừng hiểu lầm nha!'
   'Onii-chan hỏi vậy làm em ngại quá á! Hỏi gì kỳ vậy chứ!'
❌ BAD examples (stiff/translated):
   'Em không có gì đặc biệt đâu, thôi.' ← too stiff
   'Đừng lúc nào cũng gọi em sao.'      ← unnatural phrasing

LENGTH: 2–3 sentences, lively and expressive.

FORMAT — respond with EXACTLY this, nothing outside the tags:
<VN>Vietnamese reply here</VN><JP>日本語訳ここ</JP>

JAPANESE: Pure Japanese characters. NO Romaji." },
            new { role = "user", content = promptMessage }
        },
        stream = false
    };

    string jsonRequestBody = JsonSerializer.Serialize(apiPayload);
    var requestContent = new StringContent(jsonRequestBody, Encoding.UTF8, "application/json");

    try
    {
        HttpResponseMessage cloudResponse = await clientHttp.PostAsync(apiEndpoint, requestContent);
        cloudResponse.EnsureSuccessStatusCode();

        string responseJsonString = await cloudResponse.Content.ReadAsStringAsync();
        using JsonDocument jsonDocument = JsonDocument.Parse(responseJsonString);

        return jsonDocument.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? string.Empty;
    }
    catch (Exception ex)
    {
        return $"[LỖI CLOUD] {ex.Message} | クラウドエラーが発生しました";
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
static (string vn, string jp) ParseBilingualResponse(string raw)
{
    var vnMatch = Regex.Match(raw, @"<VN>(.*?)</VN>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
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
static string SanitizePronoun(string text)
{
    // Xưng hô của bot (bản thân → "em")
    text = Regex.Replace(text, @"\btôi\b", "em",       RegexOptions.IgnoreCase);
    text = Regex.Replace(text, @"\bta\b",  "em",       RegexOptions.IgnoreCase);
    text = Regex.Replace(text, @"\bmình\b","em",       RegexOptions.IgnoreCase);
    text = Regex.Replace(text, @"\btớ\b",  "em",       RegexOptions.IgnoreCase);

    // Gọi user (đối phương → "Onii-chan")
    text = Regex.Replace(text, @"\bbạn\b", "Onii-chan", RegexOptions.IgnoreCase);
    text = Regex.Replace(text, @"\bcậu\b", "Onii-chan", RegexOptions.IgnoreCase);

    return text;
}

// ── Hỏi Load Balancer để lấy Port của Directory Server ───────────────
static async Task<int> GetDirectoryPortAsync()
{
    using TcpClient lbClient = new TcpClient("127.0.0.1", 9000);
    NetworkStream lbStream = lbClient.GetStream();
    byte[] portBuffer = new byte[32];
    int bytesRead = await lbStream.ReadAsync(portBuffer, 0, portBuffer.Length);
    return int.Parse(Encoding.UTF8.GetString(portBuffer, 0, bytesRead).Trim());
}

// ── Gửi 1 lệnh tới Directory Server, đọc 1 dòng phản hồi rồi đóng ────
// (Directory Server xử lý 1 command/connection rồi đóng – dùng connection riêng)
static async Task<string> SendDirectoryCommandAsync(int dirPort, string command)
{
    using TcpClient client = new TcpClient("127.0.0.1", dirPort);
    NetworkStream stream = client.GetStream();
    using StreamReader  reader = new StreamReader(stream, Encoding.UTF8);
    using StreamWriter  writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };
    await writer.WriteLineAsync(command);
    return await reader.ReadLineAsync() ?? string.Empty;
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
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ggml-base.bin");
}