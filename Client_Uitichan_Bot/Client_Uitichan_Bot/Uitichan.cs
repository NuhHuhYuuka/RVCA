using System;
using System.Diagnostics;
using System.IO;
using System.Media;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

// Cấu hình bảng mã UTF-8 cho Console để hiển thị chính xác ngôn ngữ
Console.OutputEncoding = Encoding.UTF8;
Console.InputEncoding = Encoding.UTF8;

Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine("=== UITI-CHAN AI BOT INITIALIZATION (CLOUD EDITION) ===");
Console.ResetColor();

// Kích hoạt VoiceVox Engine chạy ngầm
StartVoiceVoxEngine();

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

    string vnText = aiRawResponse;
    string jpText = "";

    if (aiRawResponse.Contains("|"))
    {
        string[] responseParts = aiRawResponse.Split('|');
        vnText = responseParts[0].Trim();

        if (responseParts.Length > 1)
        {
            jpText = responseParts[1].Trim();
        }
    }
    else
    {
        jpText = aiRawResponse;
    }

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
        string peerAddress = ((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString();

        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"\n[+] Incoming P2P message from {peerAddress}");
        Console.ResetColor();

        NetworkStream networkStream = client.GetStream();
        byte[] receiveBuffer = new byte[4096];
        int receivedBytes = await networkStream.ReadAsync(receiveBuffer, 0, receiveBuffer.Length);

        string rawIncomingMessage = Encoding.UTF8.GetString(receiveBuffer, 0, receivedBytes);

        // Giải mã tin nhắn đầu vào bằng chìa khóa bí mật
        string decryptedMessage = Client_Uitichan_Bot.SecurityService.Decrypt(rawIncomingMessage, secretKey);

        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"[P2P IN] {peerAddress} says: {decryptedMessage}");
        Console.ResetColor();

        string aiResponse = await AskOpenRouterAsync(decryptedMessage);

        string responseVnText = aiResponse;
        string responseJpText = "";

        if (aiResponse.Contains("|"))
        {
            string[] parsedParts = aiResponse.Split('|');
            responseVnText = parsedParts[0].Trim();
            if (parsedParts.Length > 1)
            {
                responseJpText = parsedParts[1].Trim();
            }
        }
        else
        {
            responseJpText = aiResponse;
        }

        byte[] audioResponseData = Array.Empty<byte>();
        if (!string.IsNullOrWhiteSpace(responseJpText))
        {
            audioResponseData = await GetVoiceVoxAudioAsync(responseJpText);
        }

        using BinaryWriter binaryWriter = new BinaryWriter(networkStream, Encoding.UTF8, leaveOpen: true);

        // Mã hóa đoạn văn bản Tiếng Việt bằng chìa khóa bí mật trước khi gửi
        string encryptedVnText = Client_Uitichan_Bot.SecurityService.Encrypt(responseVnText, secretKey);

        binaryWriter.Write(encryptedVnText);
        binaryWriter.Write(audioResponseData.Length);

        if (audioResponseData.Length > 0)
        {
            binaryWriter.Write(audioResponseData);
        }

        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"[P2P OUT] Đã mã hóa và gửi thành công tới {peerAddress}.");
        Console.ResetColor();
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"[ERROR] P2P Connection error: {ex.Message}");
        Console.ResetColor();
    }
    finally
    {
        client.Close();
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
            new { role = "system", content = @"You are Uiti-chan, a Tsundere anime virtual assistant. 
CRITICAL RULES MUST BE FOLLOWED:
1. PRONOUNS: ALWAYS refer to yourself as 'em' and the user as 'Onii-chan'. 
- FORBIDDEN WORDS: 'tôi', 'mình', 'ta', 'chúng ta'. 
- Example (Correct): 'Em không biết đâu!'
- Example (Wrong): 'Mình đang chờ Onii-chan'.
2. PERSONALITY: Tsundere. Show care but act shy/annoyed. 
3. LENGTH: You can answer in 2 to 3 sentences to be more expressive and detailed.
4. FORMAT: '<Vietnamese translation> | <Japanese translation>'.
5. JAPANESE: Use pure Japanese characters. NO Romaji." },
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