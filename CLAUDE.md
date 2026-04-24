# RVCA — Realtime Video Call Application
## Tài liệu kiến trúc & tiến độ dự án

---

## Tổng quan hệ thống

Ứng dụng P2P phân tán viết bằng C#/.NET 10, gồm các thành phần độc lập giao tiếp qua TCP.
Mục tiêu cuối: nền tảng chat/voice/video thời gian thực có tích hợp AI avatar (Uiti-chan).

```
[Client_UI_App]  ←──── P2P TCP ────→  [Client_UI_App khác]
      │                                        │
      │ TCP:5555 (BinaryWriter/Reader)         │
      ▼                                        │
[UitiChan Bot :5555]                           │
      │                                        │
      └────────────── TCP:9000 ───────────────►│
                  [Load Balancer]
                  ├── [Directory Server :8888]
                  └── [Directory Server :8889]
```

---

## Cấu trúc thư mục

```
RVCA/
├── Server_LoadBalancer/          .NET 10 Console — Round-robin dispatcher (Port 9000)
├── Server_Directory/             .NET 10 Console — Auth DB + User registry (Port 8888/8889)
├── Client_Uitichan_Bot/          .NET 10 Console — AI Bot P2P listener (Port 5555)
├── SecurityData_Module/          .NET 10 Class Library — Shared models & services
└── Client_UI_App/                .NET 10 WinForms — Giao diện người dùng chính
```

---

## ✅ ĐÃ HOÀN THÀNH

### Infrastructure
- [x] **Load Balancer** (Port 9000) — Round-robin, gửi raw UTF-8 port bytes, đóng conn ngay
- [x] **Directory Server** (Port 8888 / 8889) — SQLite Auth DB, xử lý `SIGNUP`, `LOGIN`, `LOGOUT`, **`GETUSER`**, **`LIST_USERS`**
  - Hai instance chạy song song, dùng chung file `Auth.db` (cùng working directory)
  - Thread-per-connection, `ConcurrentDictionary` cho danh bạ in-memory
  - `GETUSER|username` → `GETUSER_SUCCESS|IP:Port` hoặc `GETUSER_NOTFOUND`
  - `LIST_USERS` → `LIST_SUCCESS|user1,user2,...`

### UitiChan Bot
- [x] **P2P Listener** TCP port 5555 — nhận encrypted message, gọi OpenRouter API, trả binary 3 lớp
- [x] **Auto register/login** — Thử LOGIN → nếu thất bại → tự SIGNUP → LOGIN lại
- [x] **VoiceVox integration** — TTS tiếng Nhật, trả WAV bytes qua BinaryWriter
- [x] **Wire protocol (RECV)**: raw UTF-8 bytes → AES-256-CBC decrypt → prompt AI
- [x] **Wire protocol (SEND)**: `BinaryWriter.Write(string)` + `Write(int32)` + `Write(byte[])`
- [x] **AES-256-CBC + PBKDF2** (100k iterations SHA-256) — secret key: `LTMCB_Secret_Key_2026`

### SecurityData_Module (Shared Library)
- [x] `SecurityService` — AES-256-GCM encrypt/decrypt (dùng cho future Client-Client E2EE)
- [x] `KeyExchangeService` — ECDH key exchange (ECDiffieHellmanCng, Windows)
- [x] `DatabaseService` — SQLite local chat history (`%APPDATA%/ChatApp.db`)
- [x] `FileTransferService` — Chunked file transfer 64KB, SHA-256 integrity check
- [x] `Models` — `ChatMessage`, `FileChunk`, `NetworkPacket`, `KeyExchangePacket`, `EncryptionResult`
- [x] **csproj tạo mới** — `net10.0-windows`, ref `System.Data.SQLite.Core`

### Client_UI_App (WinForms .NET 10)
- [x] **Solution** `Client_UI_App.slnx` — chứa UI + SecurityData_Module
- [x] **Dark theme** — toàn bộ form nền tối, palette nhất quán
- [x] **AuthForm** — Đăng nhập / Đăng ký → gọi LB → Directory Server
- [x] **MainChatForm** — Danh sách user online; click user → auto-connect (không cần nút Kết nối)
- [x] **Chat box sạch** — chỉ chứa tin nhắn / file; logs hệ thống ra status bar
- [x] **P2P Chat với UitiChan** — gửi encrypted text, nhận text + WAV audio
- [x] **BotCryptService** — AES-256-CBC clone thuật toán Bot, dùng `Rfc2898DeriveBytes.Pbkdf2()` (.NET 10)
- [x] **DirectoryService** — GetDirectoryPort / Login / Signup / Logout / GetUser / **GetOnlineUsers merge 2 server** (async)
- [x] **P2PChatService** — raw bytes → Bot (no BOM) + E2E ECDH+AES-GCM → Client + `SendFileToClientAsync` + Bot semaphore queue
- [x] **P2PListenerService** — TcpListener port ngẫu nhiên; xử lý `CHAT`, `E2E_INIT`, `FILE_INIT`; events `MessageReceived` / `FileReceived`
- [x] **Thread safety** — `Control.Invoke` cho mọi UI update từ background Task
- [x] **Audio playback** — `SoundPlayer` phát WAV thẳng từ `MemoryStream`, không ghi disk
- [x] **LOGOUT** tự động khi đóng MainChatForm
- [x] **Nút Làm mới** — query `LIST_USERS` **cả 2 server song song** (`Task.WhenAll`), merge + dedup
- [x] **Auto-connect** — chọn user trong ListBox → tự query `GETUSER` → `_p2pReady = true` ngay

### Wire Protocol (đã xác nhận hoạt động)
| Chiều | Cách gửi | Cách nhận |
|---|---|---|
| Client → Bot | `stream.WriteAsync(UTF8.GetBytes(Base64+"\n"))` — raw, **không BOM** | `NetworkStream.ReadAsync` → `Decrypt` |
| Bot → Client | `BinaryWriter.Write(string/int/byte[])` | `BinaryReader.ReadString/Int32/Bytes` |
| Client → Client (text) | `E2E_INIT\|sender\|pubKey` → đợi ACK → `CHAT_E2E\|sender\|ct\|nonce\|tag` | Ephemeral ECDH + AES-GCM → `MessageReceived` event |
| Client → Client (file) | `FILE_INIT\|sender\|name\|n\|sha256` + N×`FILE_CHUNK\|i\|b64` | Reassemble chunks → SHA-256 verify → `FileReceived` event |
| Client → Directory | `StreamWriter.WriteLine("CMD\|a\|b")` | `StreamReader.ReadLine()` |
| LB → Client | `stream.Write(portBytes)` (raw, no newline) | `stream.Read` → `int.Parse` |

---

## 🔲 TODO — Roadmap tiếp theo

### Phase 2 — Client ↔ Client P2P (Text + File + Ảnh)
- [x] **Mỗi Client_UI_App tự mở TcpListener** trên port ngẫu nhiên khi đăng nhập (`P2PListenerService.Start()`)
- [x] **Gửi port thật lên Directory Server** thay vì port giả — `LOGIN|user|pass|realPort`
- [x] **Directory Server: `GETUSER|username`** → trả `IP:Port` của peer
- [x] **Directory Server: `LIST_USERS`** → trả danh sách user online
- [x] **Client-Client chat cơ bản** — `StreamWriter.WriteLine("CHAT|sender|msg")`, fire-and-forget
- [x] **Auto-resolve**: chọn user → tự query GETUSER → điền IP:Port
- [x] **Client-Client encrypted chat** — Ephemeral ECDH per-connection + AES-256-GCM; forward secrecy; fallback plaintext nếu peer cũ
- [x] **Gửi ảnh / file** — `FILE_INIT` header + `FILE_CHUNK` chunks base64, SHA-256 verify; lưu vào `Documents/ChatApp_Files/`; nút 📎 File trên UI
- [x] **Nhiều client cùng nhắn cho UitiChan** — `SemaphoreSlim(1,1)` trong `P2PChatService._botSemaphore` queue các request

### Phase 3 — Voice Chat (Client ↔ Client, Client ↔ UitiChan)

> **Nguyên tắc chọn thư viện:** Ưu tiên hiệu năng (latency, chất lượng âm thanh) trước.
> Dùng thư viện có sẵn nếu nó đạt hiệu năng tương đương tự code — không reinvent the wheel.
> Chỉ tự implement khi thư viện có sẵn có overhead không chấp nhận được.

- [ ] **Capture microphone**
  - Ưu tiên: `NAudio` (NuGet) — WASAPI exclusive mode cho latency thấp nhất (~5ms)
  - Fallback: `Windows.Media.Capture` nếu cần UWP-style permission
- [ ] **Audio codec**
  - Ưu tiên: **Opus** (via `Concentus` NuGet — pure C#, không cần native DLL) — tối ưu cho voice call, latency thấp, adaptive bitrate
  - Fallback: PCM raw nếu LAN-only (không cần nén)
- [ ] **Transport layer**
  - Ưu tiên: **WebRTC** (`SIPSorcery` NuGet — C# native WebRTC stack) — có sẵn ICE/STUN/DTLS, tự lo NAT traversal, jitter buffer
  - Nếu LAN-only hoặc cần kiểm soát tối đa: tự dùng `UdpClient` + jitter buffer tự viết (latency thấp hơn nhưng công nhiều)
- [ ] **Voice → Uiti pipeline**
  - Client stream Opus chunks → Bot nhận → decode → STT (`OpenAI Whisper API` hoặc `Vosk` offline)
  - → AI gen text → TTS VoiceVox → stream WAV chunks ngược lại (chunked, không đợi full audio)
- [ ] **Client ↔ Client voice** — Full Mesh: mỗi client gửi trực tiếp tới N-1 peers
- [ ] **Audio mixing** — mix nhiều PCM stream đầu vào (NAudio `MixingSampleProvider`)
- [ ] **Echo cancellation** — `NAudio` WebRTC echo canceller hoặc Windows AEC (AudioProcessingOptions)

### Phase 4 — Video Call (Full Mesh)

> **Nguyên tắc:** Ưu tiên FPS cao + latency thấp. H.264 hardware encode khi có thể (GPU).
> Tự code pipeline nếu thư viện tạo overhead > 2 frames.

- [ ] **Capture webcam**
  - Ưu tiên: `DirectShow` via `AForge.Video.DirectShow` hoặc `OpenCvSharp` — hỗ trợ hardware acceleration
  - Target: 30fps capture, < 33ms per frame
- [ ] **Video codec**
  - Ưu tiên: **H.264** hardware encode — `FFmpeg.AutoGen` (NuGet, wrapper native FFmpeg) với NVENC/QuickSync
  - Nếu không có GPU: **VP8** via `SIPSorcery` WebRTC built-in (software encode, acceptable quality)
  - So sánh: FFmpeg NVENC ~1ms encode/frame vs software ~15ms — chọn FFmpeg nếu có GPU
- [ ] **Transport layer**
  - Ưu tiên: **WebRTC** (`SIPSorcery`) — RTP/RTCP có sẵn, congestion control (REMB/TWCC), FEC
  - Tự implement RTP over UDP chỉ khi cần customization cực sâu (không khuyến nghị phase đầu)
- [ ] **Video display** — render decoded frame vào `PictureBox` (Bitmap) hoặc `Direct2D` qua `SharpDX` cho smoother render
- [ ] **Full Mesh topology** — mỗi client gửi video+audio trực tiếp tới N-1 peers, không qua server
- [ ] **Simulcast** — gửi 3 resolution (low/mid/high), peer chọn theo bandwidth
- [ ] **Bandwidth adaptation** — giảm bitrate/FPS động theo RTT và packet loss (REMB feedback)

### Phase 5 — Uiti-chan Avatar (VRoid Studio)
- [ ] **Tích hợp VRM model** — load file `.vrm` của Uiti-chan từ VRoid Studio
- [ ] **Render avatar**
  - Option A: Embedded Unity window trong WinForms (`SetParent` Win32 API) — UniVRM, full 3D
  - Option B: `VRMSharp` (C# pure) + `OpenTK`/`Silk.NET` render trong WinForms Panel — không cần Unity
  - Ưu tiên Option A (Unity) nếu cần animation phức tạp, Option B nếu ưu tiên bundle size
- [ ] **Lip sync** — map Opus audio amplitude + phoneme → VRM blend shape miệng (A/I/U/E/O)
- [ ] **Facial expression** — sentiment analysis từ AI response text → biểu cảm avatar (tsundere, vui, buồn...)
- [ ] **Body animation** — idle animation loop, phản ứng khi nói chuyện (head bob, blink)

### Phase 6 — Streaming & Scale
- [ ] **Chunked audio streaming từ VoiceVox** — stream WAV header + PCM chunks ngay khi VoiceVox generate, không đợi full file
- [ ] **Chunked video streaming** — keyframe mỗi 2s, delta frame liên tục, receiver decode on-the-fly
- [ ] **Azure deployment** — Load Balancer + Directory Server lên Azure VM (Standard B2s là đủ cho demo)
- [ ] **STUN/TURN server** — `CoTURN` self-host hoặc Azure Communication Services TURN relay cho NAT traversal
- [ ] **Room/Group system** — tạo phòng có ID, mời nhiều người, quản lý session lifecycle

### Phase 7 — Đóng gói & Deploy thật (như Discord Desktop)
- [ ] **Self-contained publish** — `dotnet publish -r win-x64 --self-contained true -p:PublishSingleFile=true` → 1 file `.exe` duy nhất, không cần cài .NET runtime
- [ ] **Installer (NSIS / WiX / Inno Setup)** — đóng gói exe + assets thành file cài đặt `.msi` hoặc `.exe` setup wizard
- [ ] **Auto-updater** — kiểm tra version mới khi app khởi động, tự download + apply patch (Squirrel.Windows hoặc tự implement)
- [ ] **System tray icon** — thu nhỏ xuống tray thay vì đóng hẳn, nhận thông báo tin nhắn khi minimize
- [ ] **Desktop notification** — `ToastNotification` (Windows 10+) khi có tin nhắn mới trong lúc app ở background
- [ ] **Windows startup** — tùy chọn khởi động cùng Windows (Registry `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`)
- [ ] **Remote server thật** — Load Balancer + 2 Directory Server deploy lên VPS/Azure với IP public cố định
- [ ] **Domain & TLS** — bọc TCP connection trong `SslStream` với certificate để bảo mật khi chạy qua Internet
- [ ] **Config file** — `appsettings.json` cho server IP, port, API keys (thay hardcode `127.0.0.1`)
- [ ] **Logging** — ghi log ra file (Serilog hoặc built-in `ILogger`) thay vì chỉ Console.WriteLine
- [ ] **Crash report** — bắt unhandled exception, ghi dump + gửi về server hoặc hiện dialog thân thiện
- [ ] **End-to-end test remote** — 2 máy thật kết nối qua Internet, test toàn bộ luồng: login → chat text → voice → video → avatar

---

## Startup Order (Local Dev)

```bash
# Terminal 1
cd Server_LoadBalancer/Server_LoadBalancer && dotnet run

# Terminal 2
cd Server_Directory/Server_Directory && dotnet run   # nhập: 8888

# Terminal 3
cd Server_Directory/Server_Directory && dotnet run   # nhập: 8889

# Terminal 4 (cần OPENROUTER_API_KEY + VoiceVox Engine đang chạy)
cd Client_Uitichan_Bot/Client_Uitichan_Bot && dotnet run

# Terminal 5 — UI
cd Client_UI_App && dotnet run --project Client_UI_App/Client_UI_App.csproj
```

---

## Ghi chú kỹ thuật quan trọng

- **Comments & biến**: tiếng Việt (theo convention của project)
- **Console color**: Cyan = init, Yellow = info, Green = success, Red = error
- **Top-level statements**: Server projects không có class `Program` tường minh
- **SQLite Auth.db**: mỗi Directory Server instance dùng path tương đối → cả 2 instance phải chạy cùng working directory để share DB
- **⚠️ KHÔNG dùng `StreamWriter(Encoding.UTF8)` gửi sang Bot** — `Encoding.UTF8` tự ghi BOM `0xEF 0xBB 0xBF` vào đầu stream, làm hỏng chuỗi Base64 → Bot's `Convert.FromBase64String` throw. Dùng `stream.WriteAsync(UTF8Encoding(false).GetBytes(msg + "\n"))` thay thế.
- **BinaryWriter.Write(string)** = 7-bit length prefix + UTF-8 bytes → phải đọc bằng `BinaryReader.ReadString()`
- **`Rfc2898DeriveBytes` constructor** bị obsolete (.NET 10) → dùng static `Rfc2898DeriveBytes.Pbkdf2()` trong code mới, kết quả byte giống hệt
- **`P2PListenerService.Start(username)`** — singleton static, `TcpListener(IPAddress.Any, 0)`; port qua `LocalEndpoint.Port`; nhận `username` để dùng trong `E2E_INIT_ACK`
- **E2E Client-Client**: Ephemeral ECDH per-connection (`new KeyExchangeService()` mỗi lần) → forward secrecy; fallback `CHAT|` nếu peer không trả `E2E_INIT_ACK`
- **LIST_USERS merge**: `DirectoryService.GetOnlineUsersAsync` query song song port 8888 và 8889 (`Task.WhenAll`), merge + `Distinct()` — vì mỗi instance có `ConcurrentDictionary` riêng
- **Phân biệt Bot vs Client**: `_isBotPeer = (peerName == "UitiChan")` → raw bytes + BinaryReader cho Bot; E2E StreamWriter cho Client
