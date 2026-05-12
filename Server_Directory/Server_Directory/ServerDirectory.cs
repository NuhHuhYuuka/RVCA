using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Microsoft.Data.Sqlite;

// Khởi tạo Database Xác thực (Auth DB) ngay khi Server chạy
InitializeDatabase();

// Yêu cầu thiết lập Port để khởi tạo Server Instance (Khuyến nghị: 8888 hoặc 8889)
Console.Write("Enter Port to run this Server Instance (e.g., 8888 or 8889): ");
int port = int.Parse(Console.ReadLine() ?? "8888");

Console.WriteLine($"=== DIRECTORY SERVER IS RUNNING ON PORT {port} ===");

// Cấu trúc dữ liệu lưu trữ danh bạ: [Username] -> [IP:Port]
ConcurrentDictionary<string, string>    activeDirectory = new();
// Cấu trúc dữ liệu lưu trữ nhóm: [GroupId] -> [GroupInfo]
ConcurrentDictionary<string, GroupInfo> groupDirectory  = new();

// Nạp nhóm đã tồn tại từ DB khi server khởi động
LoadGroupsFromDatabase(groupDirectory);

TcpListener listener = new TcpListener(IPAddress.Any, port);
listener.Start();

Console.WriteLine("[INFO] Waiting for incoming Client connections...");

while (true)
{
    TcpClient client = listener.AcceptTcpClient();
    string clientIP = ((IPEndPoint)client.Client.RemoteEndPoint!).Address.ToString();
    Console.WriteLine($"\n[+] Client connected from IP: {clientIP}");

    Thread clientThread = new Thread(() => HandleClient(client, activeDirectory, groupDirectory));
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
            );
            CREATE TABLE IF NOT EXISTS Groups (
                GroupId   TEXT PRIMARY KEY,
                GroupName TEXT NOT NULL,
                Creator   TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS GroupMembers (
                GroupId  TEXT NOT NULL,
                Username TEXT NOT NULL,
                PRIMARY KEY (GroupId, Username)
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
static void HandleClient(
    TcpClient client,
    ConcurrentDictionary<string, string>    directory,
    ConcurrentDictionary<string, GroupInfo> groups)
{
    try
    {
        NetworkStream stream = client.GetStream();
        using StreamReader  reader = new(stream, Encoding.UTF8);
        using StreamWriter  writer = new(stream, Encoding.UTF8) { AutoFlush = true };

        string incomingMessage = reader.ReadLine() ?? string.Empty;
        if (string.IsNullOrEmpty(incomingMessage)) return;

        string[] protocolParts = incomingMessage.Split('|');
        string command         = protocolParts[0];
        string connectionString = "Data Source=Auth.db";

        // ── CHỨC NĂNG 1: ĐĂNG KÝ TÀI KHOẢN (SIGNUP) ─────────────────
        if (command == "SIGNUP" && protocolParts.Length == 3)
        {
            string username = protocolParts[1];
            string password = protocolParts[2];

            try
            {
                using SqliteConnection dbConnection = new(connectionString);
                dbConnection.Open();

                string checkQuery = "SELECT COUNT(1) FROM Users WHERE Username = @username";
                using SqliteCommand checkCmd = new(checkQuery, dbConnection);
                checkCmd.Parameters.AddWithValue("@username", username);
                long userExists = (long)checkCmd.ExecuteScalar()!;

                if (userExists > 0)
                {
                    writer.WriteLine("SIGNUP_FAILED|Tài khoản đã tồn tại!");
                }
                else
                {
                    string insertQuery = "INSERT INTO Users (Username, PasswordHash) VALUES (@username, @password)";
                    using SqliteCommand insertCmd = new(insertQuery, dbConnection);
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
        // ── CHỨC NĂNG 2: ĐĂNG NHẬP VÀ GHI DANH BẠ (LOGIN) ──────────
        else if (command == "LOGIN" && protocolParts.Length == 4)
        {
            string username           = protocolParts[1];
            string password           = protocolParts[2];
            string clientListeningPort = protocolParts[3];
            // Bot gửi "IP:Port" trực tiếp; client thường gửi chỉ port → dùng RemoteEndPoint
            string fullAddressOverride = clientListeningPort.Contains(':') ? clientListeningPort : null!;
            string clientIP           = ((IPEndPoint)client.Client.RemoteEndPoint!).Address.ToString();

            try
            {
                using SqliteConnection dbConnection = new(connectionString);
                dbConnection.Open();

                string authQuery = "SELECT COUNT(1) FROM Users WHERE Username = @username AND PasswordHash = @password";
                using SqliteCommand authCmd = new(authQuery, dbConnection);
                authCmd.Parameters.AddWithValue("@username", username);
                authCmd.Parameters.AddWithValue("@password", password);
                long isAuthenticated = (long)authCmd.ExecuteScalar()!;

                if (isAuthenticated > 0)
                {
                    string fullAddress = fullAddressOverride ?? $"{clientIP}:{clientListeningPort}";
                    directory.AddOrUpdate(username, fullAddress, (_, _) => fullAddress);

                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"[LOGIN] User '{username}' authenticated and online at {fullAddress}");
                    Console.ResetColor();

                    string userListStr = string.Join(",", directory.Keys);
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
        // ── CHỨC NĂNG 3: LẤY THÔNG TIN MỘT USER (IP:Port) ───────────
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
        // ── CHỨC NĂNG 4: LẤY DANH SÁCH USER ONLINE ──────────────────
        else if (command == "LIST_USERS" && protocolParts.Length == 1)
        {
            string userListStr = string.Join(",", directory.Keys);
            writer.WriteLine($"LIST_SUCCESS|{userListStr}");
        }
        // ── CHỨC NĂNG 5: ĐĂNG XUẤT ───────────────────────────────────
        else if (command == "LOGOUT" && protocolParts.Length == 2)
        {
            string username = protocolParts[1];
            if (directory.TryRemove(username, out _))
            {
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine($"[LOGOUT] User '{username}' has left the network.");
                Console.ResetColor();
                writer.WriteLine("LOGOUT_SUCCESS");
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  CHỨC NĂNG NHÓM (GROUP)
        // ══════════════════════════════════════════════════════════════

        // ── TẠO NHÓM: CREATE_GROUP|groupId|groupName|creator ─────────
        // GroupId do client tạo (6-ký-tự) để đồng bộ giữa 2 server instance
        else if (command == "CREATE_GROUP" && protocolParts.Length == 4)
        {
            string groupId   = protocolParts[1];
            string groupName = protocolParts[2];
            string creator   = protocolParts[3];

            // Idempotent: nếu nhóm đã tồn tại (do server kia đã tạo), không làm gì
            groups.TryAdd(groupId, new GroupInfo(groupId, groupName, creator));
            PersistGroup(groupId, groupName, creator);

            writer.WriteLine($"GROUP_CREATED|{groupId}");
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"[GROUP] Created: '{groupName}' [{groupId}] by {creator}");
            Console.ResetColor();
        }
        // ── THAM GIA NHÓM: JOIN_GROUP|groupId|username ───────────────
        else if (command == "JOIN_GROUP" && protocolParts.Length == 3)
        {
            string groupId  = protocolParts[1];
            string username = protocolParts[2];

            if (groups.TryGetValue(groupId, out GroupInfo? grp))
            {
                lock (grp.Members)
                    grp.Members.Add(username);
                PersistMemberAdd(groupId, username);

                string memberList;
                lock (grp.Members)
                    memberList = string.Join(",", grp.Members);

                writer.WriteLine($"GROUP_JOIN_SUCCESS|{grp.Name}|{grp.Creator}|{memberList}");
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"[GROUP] {username} joined '{grp.Name}' [{groupId}]");
                Console.ResetColor();
            }
            else
            {
                writer.WriteLine("GROUP_NOT_FOUND");
            }
        }
        // ── RỜI NHÓM: LEAVE_GROUP|groupId|username ───────────────────
        else if (command == "LEAVE_GROUP" && protocolParts.Length == 3)
        {
            string groupId  = protocolParts[1];
            string username = protocolParts[2];

            if (groups.TryGetValue(groupId, out GroupInfo? grp))
            {
                lock (grp.Members)
                    grp.Members.Remove(username);
                PersistMemberRemove(groupId, username);

                writer.WriteLine("GROUP_LEAVE_SUCCESS");
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine($"[GROUP] {username} left '{grp.Name}' [{groupId}]");
                Console.ResetColor();
            }
            else
            {
                writer.WriteLine("GROUP_NOT_FOUND");
            }
        }
        // ── LẤY THÀNH VIÊN: GET_GROUP_MEMBERS|groupId ────────────────
        else if (command == "GET_GROUP_MEMBERS" && protocolParts.Length == 2)
        {
            string groupId = protocolParts[1];

            if (groups.TryGetValue(groupId, out GroupInfo? grp))
            {
                string memberList;
                lock (grp.Members)
                    memberList = string.Join(",", grp.Members);

                writer.WriteLine($"GROUP_MEMBERS|{grp.Name}|{memberList}");
            }
            else
            {
                writer.WriteLine("GROUP_NOT_FOUND");
            }
        }
        // ── ĐỔI TÊN NHÓM: RENAME_GROUP|groupId|newName|requester ────
        else if (command == "RENAME_GROUP" && protocolParts.Length == 4)
        {
            string groupId   = protocolParts[1];
            string newName   = protocolParts[2];
            string requester = protocolParts[3];

            if (groups.TryGetValue(groupId, out GroupInfo? grp))
            {
                if (grp.Creator != requester)
                {
                    writer.WriteLine("RENAME_NOT_AUTHORIZED");
                }
                else
                {
                    grp.Name = newName;
                    UpdateGroupNameInDb(groupId, newName);
                    writer.WriteLine($"RENAME_SUCCESS|{newName}");
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine($"[GROUP] '{groupId}' renamed to '{newName}' by {requester}");
                    Console.ResetColor();
                }
            }
            else
            {
                writer.WriteLine("GROUP_NOT_FOUND");
            }
        }
        // ── NHÓM CỦA TÔI: LIST_MY_GROUPS|username ────────────────────
        else if (command == "LIST_MY_GROUPS" && protocolParts.Length == 2)
        {
            string username = protocolParts[1];

            var myGroups = groups.Values
                .Where(g => { lock (g.Members) return g.Members.Contains(username); })
                .Select(g => $"{g.Id}:{g.Name}:{g.Creator}");

            writer.WriteLine($"MY_GROUPS|{string.Join(",", myGroups)}");
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
        // Bình thường khi client đóng kết nối đột ngột
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

// --- Load nhóm từ DB khi khởi động server ---
static void LoadGroupsFromDatabase(ConcurrentDictionary<string, GroupInfo> groups)
{
    try
    {
        using SqliteConnection conn = new("Data Source=Auth.db");
        conn.Open();

        using (var cmd = new SqliteCommand("SELECT GroupId, GroupName, Creator FROM Groups", conn))
        using (var r = cmd.ExecuteReader())
            while (r.Read())
                groups.TryAdd(r.GetString(0), new GroupInfo(r.GetString(0), r.GetString(1), r.GetString(2)));

        using (var cmd2 = new SqliteCommand("SELECT GroupId, Username FROM GroupMembers", conn))
        using (var r2 = cmd2.ExecuteReader())
            while (r2.Read())
                if (groups.TryGetValue(r2.GetString(0), out var grp))
                    lock (grp.Members) grp.Members.Add(r2.GetString(1));

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"[DB] Loaded {groups.Count} group(s) from database.");
        Console.ResetColor();
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"[DB ERROR] LoadGroups: {ex.Message}");
        Console.ResetColor();
    }
}

static void PersistGroup(string groupId, string groupName, string creator)
{
    try
    {
        using SqliteConnection conn = new("Data Source=Auth.db");
        conn.Open();
        using var cmd = new SqliteCommand(
            "INSERT OR IGNORE INTO Groups (GroupId, GroupName, Creator) VALUES (@id, @name, @creator)", conn);
        cmd.Parameters.AddWithValue("@id", groupId);
        cmd.Parameters.AddWithValue("@name", groupName);
        cmd.Parameters.AddWithValue("@creator", creator);
        cmd.ExecuteNonQuery();
        // Creator cũng là thành viên đầu tiên
        PersistMemberAdd(groupId, creator);
    }
    catch { /* không block flow chính */ }
}

static void PersistMemberAdd(string groupId, string username)
{
    try
    {
        using SqliteConnection conn = new("Data Source=Auth.db");
        conn.Open();
        using var cmd = new SqliteCommand(
            "INSERT OR IGNORE INTO GroupMembers (GroupId, Username) VALUES (@id, @user)", conn);
        cmd.Parameters.AddWithValue("@id", groupId);
        cmd.Parameters.AddWithValue("@user", username);
        cmd.ExecuteNonQuery();
    }
    catch { }
}

static void UpdateGroupNameInDb(string groupId, string newName)
{
    try
    {
        using SqliteConnection conn = new("Data Source=Auth.db");
        conn.Open();
        using var cmd = new SqliteCommand(
            "UPDATE Groups SET GroupName = @name WHERE GroupId = @id", conn);
        cmd.Parameters.AddWithValue("@name", newName);
        cmd.Parameters.AddWithValue("@id", groupId);
        cmd.ExecuteNonQuery();
    }
    catch { }
}

static void PersistMemberRemove(string groupId, string username)
{
    try
    {
        using SqliteConnection conn = new("Data Source=Auth.db");
        conn.Open();
        using var cmd = new SqliteCommand(
            "DELETE FROM GroupMembers WHERE GroupId = @id AND Username = @user", conn);
        cmd.Parameters.AddWithValue("@id", groupId);
        cmd.Parameters.AddWithValue("@user", username);
        cmd.ExecuteNonQuery();
    }
    catch { }
}

// --- Model dữ liệu cho Nhóm ---
class GroupInfo
{
    public string          Id      { get; }
    public string          Name    { get; set; }
    public string          Creator { get; }
    public HashSet<string> Members { get; } = new();

    public GroupInfo(string id, string name, string creator)
    {
        Id = id; Name = name; Creator = creator;
        Members.Add(creator);
    }
}
