using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Microsoft.Data.Sqlite; // Thêm thư viện SQLite

// Khởi tạo Database Xác thực (Auth DB) ngay khi Server chạy
InitializeDatabase();

// Yêu cầu thiết lập Port để khởi tạo Server Instance (Khuyến nghị: 8888 hoặc 8889)
Console.Write("Enter Port to run this Server Instance (e.g., 8888 or 8889): ");
int port = int.Parse(Console.ReadLine() ?? "8888");

Console.WriteLine($"=== DIRECTORY SERVER IS RUNNING ON PORT {port} ===");

// Cấu trúc dữ liệu lưu trữ danh bạ: [Username] -> [IP:Port]
ConcurrentDictionary<string, string> activeDirectory = new ConcurrentDictionary<string, string>();

TcpListener listener = new TcpListener(IPAddress.Any, port);
listener.Start();

Console.WriteLine("[INFO] Waiting for incoming Client connections...");

while (true)
{
    TcpClient client = listener.AcceptTcpClient();
    string clientIP = ((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString();

    Console.WriteLine($"\n[+] Client connected from IP: {clientIP}");

    Thread clientThread = new Thread(() => HandleClient(client, activeDirectory));
    clientThread.Start();
}

// --- Phương thức khởi tạo Database ---
static void InitializeDatabase()
{
    string connectionString = "Data Source=Auth.db";
    try
    {
        using SqliteConnection connection = new SqliteConnection(connectionString);
        connection.Open();

        string createTableQuery = @"
            CREATE TABLE IF NOT EXISTS Users (
                Username TEXT PRIMARY KEY,
                PasswordHash TEXT NOT NULL
            );";

        using SqliteCommand command = new SqliteCommand(createTableQuery, connection);
        command.ExecuteNonQuery();

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("[DB STATUS] Auth.db initialized successfully.");
        Console.ResetColor();
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"[DB ERROR] Cannot initialize database: {ex.Message}");
        Console.ResetColor();
    }
}

// --- Phương thức xử lý luồng độc lập cho từng Client ---
static void HandleClient(TcpClient client, ConcurrentDictionary<string, string> directory)
{
    try
    {
        NetworkStream stream = client.GetStream();
        using StreamReader reader = new StreamReader(stream, Encoding.UTF8);
        using StreamWriter writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

        string incomingMessage = reader.ReadLine();
        if (string.IsNullOrEmpty(incomingMessage)) return;

        string[] protocolParts = incomingMessage.Split('|');
        string command = protocolParts[0];
        string connectionString = "Data Source=Auth.db";

        // --- CHỨC NĂNG 1: ĐĂNG KÝ TÀI KHOẢN (SIGNUP) ---
        if (command == "SIGNUP" && protocolParts.Length == 3)
        {
            string username = protocolParts[1];
            string password = protocolParts[2];

            try
            {
                using SqliteConnection dbConnection = new SqliteConnection(connectionString);
                dbConnection.Open();

                string checkQuery = "SELECT COUNT(1) FROM Users WHERE Username = @username";
                using SqliteCommand checkCmd = new SqliteCommand(checkQuery, dbConnection);
                checkCmd.Parameters.AddWithValue("@username", username);

                long userExists = (long)checkCmd.ExecuteScalar();

                if (userExists > 0)
                {
                    writer.WriteLine("SIGNUP_FAILED|Tài khoản đã tồn tại!");
                }
                else
                {
                    string insertQuery = "INSERT INTO Users (Username, PasswordHash) VALUES (@username, @password)";
                    using SqliteCommand insertCmd = new SqliteCommand(insertQuery, dbConnection);
                    insertCmd.Parameters.AddWithValue("@username", username);
                    insertCmd.Parameters.AddWithValue("@password", password);
                    insertCmd.ExecuteNonQuery();

                    writer.WriteLine("SIGNUP_SUCCESS|Đăng ký thành công!");
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine($"[DB] New user registered: {username}");
                    Console.ResetColor();
                }
            }
            catch (Exception ex)
            {
                writer.WriteLine($"SIGNUP_FAILED|Lỗi hệ thống: {ex.Message}");
            }
        }
        // --- CHỨC NĂNG 2: ĐĂNG NHẬP VÀ GHI DANH BẠ (LOGIN) ---
        else if (command == "LOGIN" && protocolParts.Length == 4)
        {
            string username = protocolParts[1];
            string password = protocolParts[2];
            string clientListeningPort = protocolParts[3];
            string clientIP = ((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString();

            try
            {
                using SqliteConnection dbConnection = new SqliteConnection(connectionString);
                dbConnection.Open();

                string authQuery = "SELECT COUNT(1) FROM Users WHERE Username = @username AND PasswordHash = @password";
                using SqliteCommand authCmd = new SqliteCommand(authQuery, dbConnection);
                authCmd.Parameters.AddWithValue("@username", username);
                authCmd.Parameters.AddWithValue("@password", password);

                long isAuthenticated = (long)authCmd.ExecuteScalar();

                if (isAuthenticated > 0)
                {
                    string fullAddress = $"{clientIP}:{clientListeningPort}";
                    directory.AddOrUpdate(username, fullAddress, (key, oldValue) => fullAddress);

                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"[LOGIN] User '{username}' authenticated and online at {fullAddress}");
                    Console.ResetColor();

                    var onlineUsers = directory.Keys;
                    string userListStr = string.Join(",", onlineUsers);
                    writer.WriteLine($"SUCCESS|{userListStr}");
                }
                else
                {
                    writer.WriteLine("LOGIN_FAILED|Sai tài khoản hoặc mật khẩu!");
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"[AUTH FAILED] Failed login attempt for user: {username}");
                    Console.ResetColor();
                }
            }
            catch (Exception ex)
            {
                writer.WriteLine($"LOGIN_FAILED|Lỗi hệ thống DB: {ex.Message}");
            }
        }
        // --- CHỨC NĂNG 3: LẤY THÔNG TIN MỘT USER (IP:Port) ---
        else if (command == "GETUSER" && protocolParts.Length == 2)
        {
            string targetUser = protocolParts[1];
            if (directory.TryGetValue(targetUser, out string? address))
            {
                writer.WriteLine($"GETUSER_SUCCESS|{address}");
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"[GETUSER] {targetUser} → {address}");
                Console.ResetColor();
            }
            else
            {
                writer.WriteLine("GETUSER_NOTFOUND");
            }
        }
        // --- CHỨC NĂNG 4: LẤY DANH SÁCH USER ONLINE ---
        else if (command == "LIST_USERS" && protocolParts.Length == 1)
        {
            string userListStr = string.Join(",", directory.Keys);
            writer.WriteLine($"LIST_SUCCESS|{userListStr}");
        }
        // --- CHỨC NĂNG 5: ĐĂNG XUẤT ---
        else if (command == "LOGOUT" && protocolParts.Length == 2)
        {
            string username = protocolParts[1];
            if (directory.TryRemove(username, out _))
            {
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine($"[LOGOUT] User '{username}' has left the network. Directory cleaned.");
                Console.ResetColor();
                writer.WriteLine("LOGOUT_SUCCESS");
            }
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[WARNING] Invalid protocol format received: {incomingMessage}");
            Console.ResetColor();
        }
    }
    catch (IOException ioEx) when (ioEx.InnerException is SocketException socketEx &&
                                  (socketEx.SocketErrorCode == SocketError.ConnectionAborted ||
                                   socketEx.SocketErrorCode == SocketError.ConnectionReset))
    {
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"[ERROR] Unexpected server error: {ex.Message}");
        Console.ResetColor();
    }
    finally
    {
        client.Close();
    }
}