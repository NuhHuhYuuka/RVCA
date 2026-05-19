# RVCA — Realtime Video Call Application

Ứng dụng chat / voice / video P2P phân tán viết bằng C#/.NET 10, tích hợp AI Bot Uiti-chan.

---

## Yêu cầu hệ thống

| Thành phần | Yêu cầu |
|---|---|
| .NET SDK | 10.0 trở lên |
| OS | Windows 10/11 (WinForms) |
| VoiceVox Engine | Cài tại `D:\VOICEVOX\vv-engine\run.exe` (chỉ cần cho Bot voice call) |
| API keys | `OPENROUTER_API_KEY`, `GROQ_API_KEY` (chỉ cần cho Bot) |

---

## NuGet Packages

`dotnet restore` (hoặc `dotnet build`) sẽ tải tự động — không cần cài tay. Bảng dưới liệt kê để tham khảo:

| Package | Version | Dùng trong | Mục đích |
|---|---|---|---|
| `NAudio` | 2.3.0 | Client_UI_App, Bot | Capture / playback âm thanh (MME) |
| `Concentus` | 2.2.2 | Client_UI_App, Bot | Codec Opus thuần C# (không cần native DLL) |
| `AForge.Video.DirectShow` | 2.2.5 | Client_UI_App | Capture webcam qua DirectShow (video call) |
| `Microsoft.Data.Sqlite` | 10.0.7 | Server_Directory, SecurityData_Module | Lưu tài khoản người dùng (`Auth.db`) |
| `Whisper.net` | 1.7.4 | Bot | Speech-to-text tiếng Việt (local) |
| `Whisper.net.Runtime` | 1.7.4 | Bot | Native runtime cho Whisper.net |
| `System.Windows.Extensions` | 10.0.5 | Bot | Hỗ trợ API Windows cho console app |

> **Lưu ý Whisper:** Bot cần file model `ggml-base.bin` (khoảng 142 MB).
> Tải tại: <https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-base.bin>
> Đặt vào thư mục `Client_Uitichan_Bot/Client_Uitichan_Bot/` trước khi chạy Bot.

---

## Cấu trúc project

```
RVCA/
├── Server_LoadBalancer/      Port 9000  — Round-robin dispatcher
├── Server_Directory/         Port 8888 / 8889  — Auth DB + User registry
├── Client_Uitichan_Bot/      Port 5555  — AI Bot P2P
├── SecurityData_Module/      Class library dùng chung
└── Client_UI_App/            WinForms UI client
    └── appsettings.json      ← CHỈ CẦN ĐỔI FILE NÀY để chuyển môi trường
```

---

## Cách build

```powershell
# Build tất cả từ thư mục gốc
dotnet build

# Hoặc build riêng từng project
dotnet build Server_LoadBalancer/Server_LoadBalancer
dotnet build Server_Directory/Server_Directory
dotnet build Client_Uitichan_Bot/Client_Uitichan_Bot
dotnet build Client_UI_App/Client_UI_App/Client_UI_App.csproj
```

---

## Biến môi trường cho Bot

Bot cần hai API key để hoạt động. Set trước khi chạy:

```powershell
# PowerShell (chỉ có hiệu lực trong session hiện tại)
$env:OPENROUTER_API_KEY = "sk-or-..."
$env:GROQ_API_KEY       = "gsk_..."

# Hoặc set vĩnh viễn (System Properties → Environment Variables)
```

---

## Cách 1: Demo trên LAN

Toàn bộ máy nằm cùng một mạng nội bộ (WiFi phòng, hotspot, Tailscale…).

### Bước 1 — Chỉnh `appsettings.json` trên **mọi máy client**

Tìm file: `Client_UI_App/Client_UI_App/appsettings.json`

```json
{
  "Server": {
    "LoadBalancerIp": "192.168.1.100",
    "LoadBalancerPort": 9000,
    "DirectoryPorts": [ 8888, 8889 ]
  }
}
```

Thay `192.168.1.100` bằng **IP LAN thật của máy chạy server** (xem bằng `ipconfig`, trường `IPv4 Address`).

> File `appsettings.json` được copy tự động vào `bin/` lúc build — chỉ cần sửa 1 chỗ rồi build lại là xong.

### Bước 2 — Mở Firewall trên máy server (chạy 1 lần, quyền Admin)

```powershell
New-NetFirewallRule -DisplayName "RVCA TCP" -Direction Inbound -Protocol TCP `
    -LocalPort 9000,8888,8889,5555 -Action Allow

New-NetFirewallRule -DisplayName "RVCA UDP" -Direction Inbound -Protocol UDP `
    -LocalPort 49152-65535 -Action Allow
```

### Bước 3 — Khởi động server theo thứ tự

Mở **3 terminal riêng** trên máy server:

```powershell
# Terminal 1 — Load Balancer
cd Server_LoadBalancer/Server_LoadBalancer
dotnet run

# Terminal 2 — Directory Server 1
cd Server_Directory/Server_Directory
dotnet run
# Khi hỏi port → nhập: 8888

# Terminal 3 — Directory Server 2
cd Server_Directory/Server_Directory
dotnet run
# Khi hỏi port → nhập: 8889
```

Nếu dùng Bot (voice/video call với AI):

```powershell
# Terminal 4 — UitiChan Bot (cần VoiceVox Engine đang chạy)
$env:OPENROUTER_API_KEY = "sk-or-..."
$env:GROQ_API_KEY       = "gsk_..."
cd Client_Uitichan_Bot/Client_Uitichan_Bot
dotnet run
```

### Bước 4 — Chạy client trên các máy

```powershell
cd Client_UI_App
dotnet run --project Client_UI_App/Client_UI_App.csproj
```

Hoặc double-click `Client_UI_App.exe` trong `bin/Debug/net10.0-windows/`.

### Checklist LAN

- [ ] `appsettings.json` đã đổi đúng IP LAN của máy server
- [ ] Đã rebuild sau khi đổi `appsettings.json`
- [ ] Load Balancer (9000) đang chạy
- [ ] Cả 2 Directory Server (8888 + 8889) đang chạy
- [ ] Firewall cho phép TCP 9000, 8888, 8889 inbound
- [ ] Firewall cho phép UDP 49152–65535 inbound (voice/video call)

---

## Cách 2: Demo trên Azure VM

Server đặt trên Azure, client kết nối qua internet.

### Bước 1 — Tạo Azure VM

- Loại: **Standard B2s** (2 vCPU, 4 GB RAM)
- OS: Windows Server 2022 hoặc Ubuntu 22.04
- Ghi nhớ **Public IP** của VM (ví dụ `20.10.20.30`)

Mở Inbound port trong **Network Security Group** của VM:

| Port | Protocol | Dùng cho |
|---|---|---|
| 9000 | TCP | Load Balancer |
| 8888 | TCP | Directory Server 1 |
| 8889 | TCP | Directory Server 2 |
| 5555 | TCP | UitiChan Bot |

### Bước 2 — Publish và deploy server lên VM

Chạy trên máy dev:

```powershell
# Publish self-contained (không cần cài .NET trên VM)
dotnet publish Server_LoadBalancer/Server_LoadBalancer -r win-x64 `
    --self-contained true -o ./deploy/lb

dotnet publish Server_Directory/Server_Directory -r win-x64 `
    --self-contained true -o ./deploy/dir

dotnet publish Client_Uitichan_Bot/Client_Uitichan_Bot -r win-x64 `
    --self-contained true -o ./deploy/bot
```

Copy thư mục `./deploy/` lên VM (qua SCP, RDP, hoặc Azure File Share).

### Bước 3 — Chạy server trên VM

```powershell
# Trên Azure VM — PowerShell
.\lb\Server_LoadBalancer.exe

.\dir\Server_Directory.exe   # nhập 8888
.\dir\Server_Directory.exe   # nhập 8889

# Bot (nếu cần)
$env:OPENROUTER_API_KEY = "sk-or-..."
$env:GROQ_API_KEY       = "gsk_..."
.\bot\Client_Uitichan_Bot.exe
```

> Cả hai instance Directory Server phải **chạy cùng thư mục** để dùng chung file `Auth.db`.
> Khi publish, copy cả hai vào cùng một folder `dir/`.

### Bước 4 — Chỉnh `appsettings.json` cho client

```json
{
  "Server": {
    "LoadBalancerIp": "20.10.20.30",
    "LoadBalancerPort": 9000,
    "DirectoryPorts": [ 8888, 8889 ]
  }
}
```

Thay `20.10.20.30` bằng **Public IP của Azure VM**.

### Bước 5 — Publish và phân phối client

```powershell
dotnet publish Client_UI_App/Client_UI_App/Client_UI_App.csproj `
    -r win-x64 --self-contained true -p:PublishSingleFile=true -o ./publish
```

Gửi file `publish/Client_UI_App.exe` (khoảng 80 MB) cho người dùng — không cần cài .NET.

### Checklist Azure

- [ ] NSG của VM mở TCP 9000, 8888, 8889
- [ ] `appsettings.json` client trỏ đúng Public IP Azure
- [ ] Đã rebuild / publish lại client sau khi đổi IP
- [ ] Hai Directory Server chạy cùng thư mục (share `Auth.db`)
- [ ] Bot set đủ hai env var API key trước khi chạy

---

## Thứ tự khởi động (bắt buộc)

```
1. Load Balancer   (port 9000)
2. Directory Server 1  (port 8888)
3. Directory Server 2  (port 8889)
4. Bot (tuỳ chọn)
5. Client(s)
```

Client khởi động trước Load Balancer sẽ báo lỗi kết nối khi đăng nhập.

---

## Voice / Video Call

- Audio và video là **P2P trực tiếp giữa hai client** — không đi qua server.
- Cả hai client phải mở được port UDP inbound để nhận stream từ peer.
- Cùng LAN / WiFi: hoạt động ngay.
- Khác mạng (internet): cần Tailscale hoặc port forwarding UDP phía router.

---

## Ghi chú nhanh

| Tình huống | Giải pháp |
|---|---|
| Client không đăng nhập được | Kiểm tra Load Balancer đang chạy, IP trong `appsettings.json` đúng |
| Không thấy user khác trong danh sách | Cả hai Directory Server phải đang chạy |
| Voice/Video call không kết nối | Mở UDP 49152–65535 inbound trên cả hai máy |
| Bot không trả lời | Kiểm tra `OPENROUTER_API_KEY` đã set, VoiceVox Engine đang chạy |
| Bot voice call không nhận tiếng | Kiểm tra `GROQ_API_KEY` đã set |
