# CODEBASE.md — Tài liệu kỹ thuật đồ án RVCA
> Realtime Video Call Application — Ứng dụng gọi thoại/video thời gian thực có mã hóa đầu-cuối và AI Bot

---

## 1. Tổng quan đồ án

RVCA là một hệ thống chat, gọi thoại và gọi video **ngang hàng (P2P)** kết hợp **kiến trúc tập trung** để quản lý danh bạ và xác thực. Điểm đặc biệt là mọi nội dung truyền tải (tin nhắn, âm thanh, video, file) đều được **mã hóa đầu-cuối (End-to-End Encryption)** — máy chủ không bao giờ đọc được nội dung. Ngoài ra, hệ thống tích hợp **AI Bot** có khả năng nói chuyện, nhận diện giọng nói và phát âm thanh phản hồi.

---

## 2. Kiến trúc tổng thể

```
                    ┌─────────────────────────────────────┐
                    │           INTERNET / LAN             │
                    └─────────────────────────────────────┘
                                      │
              ┌───────────────────────┼───────────────────────┐
              │                       │                       │
      [Client A]               [Client B]               [Bot UitiChan]
      WinForms UI              WinForms UI               Console App
              │                       │                       │
              └───────────── P2P TCP/UDP (E2EE) ─────────────┘
                    │                                   │
                    ▼                                   ▼
         [Load Balancer :9000]              [Load Balancer :9000]
                    │ round-robin
          ┌─────────┴─────────┐
          ▼                   ▼
[Directory Server :8888] [Directory Server :8889]
          │                   │
          └────── Auth.db ────┘  (SQLite dùng chung)
```

### Mô hình lai (Hybrid P2P)
- **Tập trung hóa phần nhỏ**: Load Balancer + Directory Server chỉ phục vụ **xác thực** và **tra cứu địa chỉ** (IP:Port). Không lưu, không đọc nội dung chat.
- **Phân tán phần lớn**: Sau khi biết địa chỉ của nhau, hai client kết nối **trực tiếp** qua TCP (chat/signaling) và UDP (audio/video). Máy chủ không tham gia vào luồng dữ liệu chính.

**Tại sao chọn mô hình lai?**
- Mô hình P2P thuần túy khó triển khai vì NAT traversal phức tạp.
- Mô hình client-server thuần túy tạo bottleneck và phụ thuộc vào băng thông server.
- Mô hình lai cho phép server nhẹ (chỉ TCP signaling) trong khi media đi thẳng P2P.

---

## 3. Các project và vai trò

### 3.1 `Server_LoadBalancer` — Cân bằng tải
- **Công nghệ**: .NET 10, `TcpListener`, top-level statements (C# 9+)
- **Cổng**: 9000
- **Vai trò**: Client kết nối vào đây trước tiên. Server trả về một số port (8888 hoặc 8889) theo thuật toán **Round-Robin** rồi ngắt kết nối.
- **Round-Robin**: `currentIndex = (currentIndex + 1) % serverPorts.Length` — đảm bảo phân đều tải giữa 2 Directory Server.
- **Tại sao cần?** Nếu chỉ có 1 Directory Server, đây là Single Point of Failure. 2 server chia sẻ cùng 1 DB đảm bảo consistency.

### 3.2 `Server_Directory` — Máy chủ danh bạ & xác thực
- **Công nghệ**: .NET 10, `TcpListener`, `Microsoft.Data.Sqlite`, BCrypt (PBKDF2-SHA256), `Thread` per connection
- **Cổng**: 8888 hoặc 8889 (2 instance chạy song song, cùng file `Auth.db`)
- **Database**: SQLite với 3 bảng:
  - `Users(Username PK, PasswordHash)` — lưu hash mật khẩu
  - `Groups(GroupId PK, GroupName, Creator)`
  - `GroupMembers(GroupId, Username)` — quan hệ nhiều-nhiều
- **Giao thức (text, pipe-delimited)**:
  - `SIGNUP|username|password` → `SIGNUP_SUCCESS` hoặc `SIGNUP_FAILED`
  - `LOGIN|username|password|ip:port` → `SUCCESS|user1,user2,...` hoặc `LOGIN_FAILED`
  - `LOGOUT|username`
  - `GETUSER|username` → `GETUSER_SUCCESS|ip:port`
  - `LIST_USERS` → `LIST_SUCCESS|user1,user2,...`
  - `CREATE_GROUP|...`, `JOIN_GROUP|...`, `GET_GROUP|...`
- **Tại sao Thread-per-connection (không phải async)?** Mỗi kết nối đến Directory Server ngắn gọn (1 command → 1 response → đóng). Độ phức tạp thấp, Thread đơn giản hơn. Nếu cần scale lớn mới dùng async.
- **Lưu ý bảo mật**: Mật khẩu được hash bằng PBKDF2-SHA256 trước khi lưu DB. Server **không lưu plaintext**.

### 3.3 `SecurityData_Module` — Thư viện bảo mật dùng chung
- **Công nghệ**: .NET 10, `System.Security.Cryptography` (built-in)
- **Được dùng bởi**: Client_UI_App (qua ProjectReference)
- **Các service chính**:
  - `SecurityService` — AES-256-GCM encrypt/decrypt
  - `KeyExchangeService` — ECDH key exchange
  - `FileTransferService` — chunked file transfer + SHA-256 verify
  - `DatabaseService` — lịch sử chat local (SQLite)
- **Các model**: `ChatMessage`, `EncryptionResult`, `FileChunk`, `NetworkPacket`

### 3.4 `Client_UI_App` — Ứng dụng client chính
- **Công nghệ**: .NET 10, WinForms, NAudio, Concentus (Opus), AForge.Video.DirectShow
- **Các form chính**:
  - `AuthForm` — Đăng ký / Đăng nhập
  - `MainChatForm` — Giao diện chính: danh sách user, chat P2P, group list
  - `GroupChatForm` — Chat nhóm (nhúng trong MainChatForm, không phải floating window)
  - `GroupVoiceForm` — Kênh voice nhóm (floating, independent window)
  - `GroupVideoForm` — Kênh video nhóm
  - `VoiceCallForm` — Gọi thoại 1-1
  - `VideoCallForm` — Gọi video 1-1

### 3.5 `Client_Uitichan_Bot` — AI Bot
- **Công nghệ**: .NET 10 (Console), NAudio, Concentus, Whisper.net, OpenRouter API
- **Vai trò**: Đăng ký vào hệ thống như một user bình thường (username: `UitiChan`), nhưng xử lý tất cả kết nối đến bằng AI.
- **Chi tiết**: Xem phần 8 — AI Bot.

---

## 4. Luồng kết nối từng bước (Login → Chat)

```
Client                Load Balancer          Directory Server
  │                        │                       │
  │──TCP connect :9000────►│                       │
  │◄──"8888" (hoặc 8889)──│                       │
  │                        │                       │
  │──TCP connect :8888────────────────────────────►│
  │──LOGIN|alice|hash|192.168.1.10:54321──────────►│
  │        │ (Server lưu alice → 192.168.1.10:54321 vào RAM)
  │◄──SUCCESS|bob,charlie,UitiChan─────────────────│
  │                        │                       │
  │ (Alice muốn chat với Bob)
  │──TCP connect :9000────►│ (xin vé mới)
  │◄──"8889"───────────────│
  │──GETUSER|bob──────────────────────────────────►│
  │◄──GETUSER_SUCCESS|192.168.1.20:61234───────────│
  │
  │──TCP connect 192.168.1.20:61234 (TRỰC TIẾP TỚI BOB)──►│
  │──E2E_INIT|alice|<publicKey>────────────────────────────►│
  │◄──E2E_INIT_ACK|bob|<publicKey>─────────────────────────│
  │ (Cả 2 derive session key từ ECDH)
  │──CHAT_E2E|alice|<cipher>|<nonce>|<tag>─────────────────►│
```

---

## 5. Bảo mật — End-to-End Encryption

### 5.1 Chat P2P (E2EE với Perfect Forward Secrecy)

**Thuật toán**: ECDH (Ephemeral) + AES-256-GCM

**Quy trình cho mỗi tin nhắn**:
1. Alice tạo **ephemeral** ECDH key pair (mới mỗi tin nhắn)
2. Alice gửi public key: `E2E_INIT|alice|<alicePubKey>`
3. Bob nhận, tạo ephemeral key pair của mình, gửi lại: `E2E_INIT_ACK|bob|<bobPubKey>`
4. Cả hai tính **shared secret** bằng ECDH: `ECDH(alicePriv, bobPub) == ECDH(bobPriv, alicePub)`
5. Shared secret này trở thành **session key** cho AES-256-GCM
6. Alice mã hóa: `AES-GCM(plaintext, sessionKey)` → `(ciphertext, nonce, tag)`
7. Gửi: `CHAT_E2E|alice|<cipher>|<nonce>|<tag>`

**Tại sao AES-GCM?**
- **Authenticated Encryption**: kết hợp mã hóa + MAC (Message Authentication Code) trong một bước.
- **Auth tag 128-bit**: phát hiện ngay nếu dữ liệu bị sửa đổi trên đường truyền.
- **Nonce ngẫu nhiên 96-bit**: đảm bảo cùng plaintext → ciphertext khác nhau mỗi lần.
- **Hiệu năng**: được tăng tốc bởi AES-NI instruction trên CPU hiện đại.

**Tại sao ECDH Ephemeral (bỏ key cũ sau mỗi phiên)?**
- **Perfect Forward Secrecy**: nếu key bị lộ hôm nay, các cuộc hội thoại cũ vẫn an toàn vì mỗi phiên dùng key khác nhau.
- **ECDiffieHellmanCng** (Windows CNG — Cryptography Next Generation): dùng API crypto native của OS, không cần thư viện bên ngoài.

### 5.2 Chat với Bot (AES-256-CBC)

**Tại sao khác với P2P?**
- Bot không hỗ trợ ECDH (thiết kế đơn giản hơn cho AI bot).
- Dùng **shared secret** cố định `"LTMCB_Secret_Key_2026"` + PBKDF2 để dẫn xuất AES key.
- **PBKDF2-SHA256** (100.000 iterations): biến mật khẩu yếu thành key mạnh, chống brute-force.
- Format wire: `[16B salt][16B IV][ciphertext]` → Base64

**So sánh AES-CBC vs AES-GCM**:
| | AES-CBC | AES-GCM |
|---|---|---|
| Xác thực | Không (chỉ mã hóa) | Có (AEAD) |
| Padding | PKCS7 cần | Không cần |
| Hiệu năng | Tốt | Tốt hơn |
| Dùng trong RVCA | Bot (đơn giản) | P2P client (bảo mật cao hơn) |

### 5.3 File Transfer
- Chia file thành **chunk 64KB**, gửi tuần tự qua TCP
- SHA-256 checksum toàn bộ file → verify sau khi nhận đủ
- Nếu hash sai → xóa file, không báo nhận thành công

---

## 6. Audio Pipeline — Gọi thoại

### 6.1 Codec Opus (qua thư viện Concentus)

**Tại sao Opus?**
- Là codec audio hiện đại nhất, dùng trong WebRTC, Discord, Zoom.
- Hỗ trợ bitrate 6kbps – 510kbps (RVCA dùng 24kbps cho voice).
- Độ trễ thấp (~20ms mỗi frame), tối ưu cho real-time conversation.
- Concentus là pure C# implementation — không cần native DLL.

**Pipeline gửi âm thanh**:
```
Microphone (WaveInEvent, 48kHz 16-bit mono, 20ms buffer)
    → OpusEncoder.Encode() [960 samples/frame]
    → [2-byte sequence header][opus bytes]
    → UDP socket → peer's IP:udpPort
```

**Pipeline nhận âm thanh** (group voice, full-mesh):
```
UDP socket (nhận từ nhiều peer)
    → OpusDecoder.Decode() [mỗi peer có Decoder riêng]
    → BufferedWaveProvider [buffer 2 giây, discard nếu tràn]
    → MixingSampleProvider [mix tất cả peer thành 1 luồng]
    → WaveOutEvent (speakers)
```

**Tại sao UDP thay vì TCP cho audio?**
- TCP đảm bảo thứ tự + không mất packet → nếu 1 packet delay, toàn bộ stream bị block (Head-of-Line Blocking).
- UDP: packet đến không theo thứ tự hoặc mất → bỏ qua, tiếp tục. Chất lượng âm thanh giảm nhẹ nhưng không bị freeze.
- Đối với voice real-time: **một chút nhiễu tốt hơn bị đơ vài giây**.

**Tại sao 48kHz?**
- Opus yêu cầu input 48kHz (hoặc 8/12/16/24kHz). 48kHz là chuẩn professional audio.
- 1 frame Opus = 960 samples × (1/48000) = 20ms.

### 6.2 Group Voice — Full-Mesh UDP
- Mỗi thành viên gửi UDP đến **tất cả** thành viên khác (full-mesh).
- Signaling qua TCP (`GROUP_VOICE_JOIN|groupId|username|udpPort|tcpPort`).
- Khi join: broadcast JOIN → nhận REPLY từ các member đang trong channel → `AddPeer()` → thêm vào mixer.

---

## 7. Video Pipeline — Gọi video

### 7.1 Capture (AForge.Video.DirectShow)
- **AForge** truy cập webcam qua **DirectShow API** của Windows (COM interface).
- Lý do dùng AForge thay vì WinForms camera: AForge là thư viện chuyên dụng cho computer vision, cung cấp `NewFrame` event mỗi khi có frame mới từ camera.

### 7.2 Nén và truyền video
- Frame từ camera: `Bitmap` (BGR24) → nén **JPEG** (chất lượng 40%) để giảm kích thước.
- Gửi qua UDP: `[4-byte length][jpeg bytes]`.
- Bên nhận: decode JPEG → hiển thị lên PictureBox.

**Tại sao JPEG thay vì H.264/H.265?**
- JPEG nén per-frame, không cần I-frame/P-frame như video codec → đơn giản hơn nhiều.
- Trade-off: bandwidth cao hơn H.264, nhưng không có temporal dependency — packet mất không ảnh hưởng frame tiếp theo.

---

## 8. AI Bot UitiChan

### 8.1 Kiến trúc tổng thể

```
User nói giọng
    → UDP Opus packets → Bot nhận
    → OpusDecoder → PCM 48kHz
    → [Voice Activity Detection bằng RMS threshold]
    → WAV bytes → Groq Whisper API (STT)
    → Vietnamese text
    → OpenRouter API (LLM — openai/gpt-oss-120b:free)
    → AI response (tiếng Việt + tiếng Nhật)
    → VoiceVox TTS (phát âm tiếng Nhật)
    → WAV → resample 48kHz → OpusEncoder
    → UDP packets → User
```

### 8.2 Speech-to-Text (STT)

**Ưu tiên 1 — Groq Whisper Cloud** (`GROQ_API_KEY` env var):
- Model: `whisper-large-v3-turbo`
- Gửi WAV bytes qua HTTPS → nhận transcript text
- Nhanh hơn local model, không cần download

**Ưu tiên 2 — Local Whisper** (Whisper.net + `ggml-small.bin`):
- Chạy hoàn toàn offline, không cần internet
- Model ~466MB, độ chính xác tốt cho tiếng Việt

**Voice Activity Detection (VAD)**:
- Không dùng thư viện ngoài, tự implement bằng **RMS (Root Mean Square)**
- `rms = sqrt(sum(sample²) / count) / SHORT_MAX`
- Nếu `rms >= 0.012` → người dùng đang nói → ghi vào buffer
- Sau 750ms silence (30 frames × 25ms) → gửi buffer đi xử lý

### 8.3 Large Language Model (LLM)

- **OpenRouter API**: proxy đến nhiều LLM khác nhau (GPT-4, Claude, Llama, v.v.)
- Model mặc định: `openai/gpt-oss-120b:free`
- **System prompt** định nghĩa nhân cách Uiti-chan (tsundere AI sister, xưng "em", gọi user là "Onii-chan")
- Response format: bilingual `<VN>tiếng Việt</VN><JP>日本語</JP>` để tách phần đọc TTS

**SSE Streaming**: khi gọi thoại, bot dùng Server-Sent Events để nhận từng token LLM → gửi subtitle qua TCP ngay khi có `</VN>` tag (giảm latency 1-3 giây).

### 8.4 Text-to-Speech (TTS)

- **VoiceVox Engine** (local server, port 50021)
- Input: tiếng Nhật (từ `<JP>` block của LLM response)
- Output: WAV audio 24kHz
- Bot resample WAV → 48kHz PCM → Opus encode → UDP gửi về client

**Tại sao tiếng Nhật cho TTS thay vì tiếng Việt?**
- VoiceVox là TTS engine tiếng Nhật chất lượng cao (anime voice).
- LLM được prompt để trả lời song ngữ: tiếng Việt hiển thị text, tiếng Nhật phát âm thanh.

### 8.5 IP Auto-Detection (vấn đề quan trọng khi deploy)

Bot cần đăng ký IP của mình vào Directory Server để client kết nối được. Có 3 scenario:
1. **Local dev** (tất cả cùng LAN): dùng LAN IP (UDP trick về 8.8.8.8)
2. **Azure VM**: dùng Azure IMDS (`169.254.169.254/metadata/...`) → trả về public IP
3. **VPS/cloud khác**: dùng `api.ipify.org` / `checkip.amazonaws.com`

Override bằng env var `BOT_IP` nếu cần kiểm soát thủ công.

---

## 9. Networking — Chi tiết giao thức

### 9.1 Port map

| Component | Protocol | Port | Ghi chú |
|---|---|---|---|
| Load Balancer | TCP | 9000 | Entry point |
| Directory Server 1 | TCP | 8888 | Xác thực, danh bạ |
| Directory Server 2 | TCP | 8889 | Dự phòng |
| Client P2P listener | TCP | Random (OS) | Nhận tin nhắn, signaling |
| Client Voice/Video | UDP | Random (OS) | Audio/video stream |
| Bot P2P listener | TCP | 5555 | Fixed port |

### 9.2 TCP Message Protocol

Tất cả TCP messages dùng format **text, UTF-8, newline-delimited** (`StreamWriter.WriteLine` / `StreamReader.ReadLine`). Dùng `|` làm separator.

**Tại sao text thay vì binary cho TCP?**
- Debug dễ hơn (telnet/wireshark đọc được)
- Đủ hiệu năng cho signaling (không phải media)
- Binary chỉ dùng khi cần (bot response: `BinaryWriter.Write(string)`)

### 9.3 UDP Packet Format (Voice)
```
[2 bytes: sequence number (ushort)][n bytes: Opus encoded frame]
```
- Sequence number: phát hiện packet đến không theo thứ tự (hiện tại chưa xử lý reorder)

### 9.4 UDP Packet Format (Video)
```
[4 bytes: JPEG length (int)][n bytes: JPEG data]
```

---

## 10. Thiết kế UI — WinForms

### 10.1 Tại sao WinForms thay vì WPF hay web?
- **Đơn giản**: WinForms dễ học, ít boilerplate hơn WPF (không cần XAML, binding).
- **GDI+**: tích hợp sẵn render ảnh, vẽ custom (OwnerDraw cho listbox).
- **DirectShow interop**: AForge.Video.DirectShow yêu cầu Windows COM, phù hợp với WinForms.
- Trade-off: UI không đẹp bằng WPF/Electron, nhưng đủ cho mục tiêu đồ án.

### 10.2 GroupChatForm nhúng (Embedded Form)
```csharp
form.TopLevel        = false;
form.FormBorderStyle = FormBorderStyle.None;
form.Dock            = DockStyle.Fill;
_pnlGroupHost.Controls.Add(form);
form.Show();
```
- GroupChatForm **không** là cửa sổ độc lập mà được nhúng vào một panel trong MainChatForm.
- Điều này tạo trải nghiệm như Discord (group chat hiện trong cùng cửa sổ, không mở tab mới).
- GroupVoiceForm và GroupVideoForm vẫn là **floating window** độc lập (cần `FindForm()` làm owner để hiện đúng trên cùng).

### 10.3 Threading trong WinForms
- WinForms chỉ cho phép update UI từ **UI thread** (main STA thread).
- Mọi callback từ network (background thread) phải dùng `Control.Invoke()` hoặc `BeginInvoke()` để marshal về UI thread.
- Pattern dùng trong RVCA:
```csharp
if (InvokeRequired) { Invoke(() => DoUIWork()); return; }
DoUIWork();
```

### 10.4 Image Rendering trong RTB
- **RichTextBox** hỗ trợ RTF format với `\pict\pngblip` để nhúng ảnh trực tiếp vào document.
- Cách dùng: `rtbChat.SelectedRtf = "{\\rtf1 {\\pict\\pngblip\\picwgoalW\\pichgoalH HEX}\\par}"`.
- Kích thước tính theo **twips** (1/1440 inch; ở 96 DPI: 1 pixel = 15 twips).
- Ảnh nhận được lưu path vào session dưới dạng marker `[IMAGE:/path/to/file]` để render lại khi switch peer.

---

## 11. Database — Lịch sử chat local

- **SQLite** qua `Microsoft.Data.Sqlite` (wrapper chính thức của Microsoft)
- Database local (`chat_history.db`) lưu tại `%APPDATA%/ChatApp/`
- Bảng `Messages(Id, Sender, Receiver, Content, Timestamp, IsFile)`
- **Tại sao local thay vì server-side?**
  - Nhất quán với nguyên tắc E2EE: server không biết nội dung.
  - Privacy: lịch sử chỉ tồn tại trên máy người dùng.
  - Trade-off: mất thiết bị → mất lịch sử.

---

## 12. Cấu hình — appsettings.json

Client đọc cấu hình từ `appsettings.json` một lần duy nhất lúc khởi động (static constructor của `AppConfig`):

```json
{
  "Server": {
    "LoadBalancerIp": "127.0.0.1",
    "LoadBalancerPort": 9000,
    "DirectoryPorts": [8888, 8889]
  }
}
```

- Thay IP ở đây khi deploy lên LAN hoặc cloud — không cần rebuild.
- `DirectoryPorts` dùng trực tiếp khi refresh danh sách user (bỏ qua Load Balancer, query song song cả 2 server).

---

## 13. Thứ tự khởi động (quan trọng)

```
1. Load Balancer (port 9000)     ← phải chạy trước
2. Directory Server port 8888    ← 2 instance, mỗi cái nhập port khác nhau
3. Directory Server port 8889    ←
4. Bot UitiChan (optional)       ← đăng ký vào directory như user thường
5. Client A, Client B, ...       ← kết nối vào Load Balancer
```

Nếu Load Balancer chưa chạy → Client báo lỗi kết nối ngay lập tức.
Nếu Directory Server chưa chạy → Client kết nối được Load Balancer nhưng không đăng nhập được.

---

## 14. Câu hỏi vấn đáp thường gặp

**Q: Tại sao dùng 2 Directory Server thay vì 1?**
> Tăng khả năng chịu lỗi (fault tolerance). Nếu 1 server crash, server còn lại vẫn phục vụ. Cả 2 chia sẻ cùng file `Auth.db` — tuy nhiên SQLite không tối ưu cho concurrent writes từ 2 process, đây là điểm cần cải thiện trong production.

**Q: Tại sao không dùng WebSocket/SignalR?**
> RVCA dùng raw TCP/UDP socket để kiểm soát hoàn toàn protocol. WebSocket/SignalR thêm overhead HTTP và không phù hợp cho UDP audio stream. Mục tiêu đồ án cũng là học cách xây dựng networking từ đầu.

**Q: Perfect Forward Secrecy là gì?**
> Mỗi phiên chat dùng một cặp ECDH key ephemeral (tạo mới, bỏ sau khi xong). Nếu attacker ghi lại traffic và sau này lấy được private key dài hạn, họ vẫn không giải mã được các phiên cũ vì key đó đã bị xóa.

**Q: Opus encode/decode hoạt động thế nào?**
> Input: 960 samples PCM 48kHz 16-bit mono (= 20ms audio). OpusEncoder nén xuống ~60 bytes ở 24kbps. OpusDecoder phục hồi 960 samples từ 60 bytes. Chất lượng nghe gần như không phân biệt được với PCM gốc.

**Q: Tại sao AES-GCM mà không dùng RSA?**
> RSA là asymmetric crypto — chậm và chỉ dùng để trao đổi key, không mã hóa data lớn. ECDH trao đổi key (asymmetric), AES-GCM mã hóa data (symmetric). Đây là kiến trúc chuẩn của mọi secure channel hiện đại (TLS, Signal Protocol).

**Q: NAudio là gì?**
> .NET Audio library. `WaveInEvent`: capture microphone qua Windows MME API, raise event mỗi 20ms. `WaveOutEvent`: phát audio qua speaker. `MixingSampleProvider`: mix nhiều luồng audio thành 1 (dùng cho group voice).

**Q: Bot tự phát hiện IP như thế nào khi deploy lên Azure?**
> Azure IMDS (Instance Metadata Service) là HTTP endpoint nội bộ `169.254.169.254` — chỉ truy cập được từ bên trong VM. Bot request endpoint này với header `Metadata: true` → nhận về public IP của VM. Nếu không phải Azure, fallback về external IP service (`api.ipify.org`).

**Q: Whisper là gì? Tại sao cần 2 mode (cloud và local)?**
> OpenAI Whisper là model STT (Speech-to-Text) đa ngôn ngữ, rất tốt với tiếng Việt. Cloud mode (Groq) nhanh hơn và không tốn RAM. Local mode (Whisper.net + ggml model file) hoạt động offline. Bot ưu tiên cloud, fallback về local nếu không có API key.

**Q: Tại sao GroupChatForm được nhúng vào panel thay vì mở cửa sổ mới?**
> UX design — giống Discord. Mở cửa sổ mới (MDI hoặc floating) gây rối khi có nhiều nhóm. Nhúng vào panel tạo trải nghiệm single-window, dễ quản lý hơn cho người dùng.

**Q: File transfer hoạt động như thế nào?**
> File được chia thành chunk 64KB. Mỗi chunk encode Base64 và gửi qua TCP theo format `FILE_CHUNK|index|base64Data`. Sau khi nhận đủ, tính SHA-256 toàn file và so sánh với hash đã gửi kèm trong `FILE_INIT`. Nếu khớp → lưu file. Không khớp → xóa (dữ liệu bị lỗi/tamper).

---

## 15. Sơ đồ luồng dữ liệu: Voice Call 1-1

```
Alice (caller)                              Bob (callee)
     │                                           │
     │ PrepareUdp() → port 54000                 │
     │                                           │
     │──VOICE_OFFER|alice|54000|tcpPort─────────►│ (qua TCP P2P)
     │                                           │ PrepareUdp() → port 55000
     │◄──VOICE_ANSWER|bob|55000──────────────────│
     │                                           │
     │ SetRemoteEndpoint(bob_ip, 55000)           │ SetRemoteEndpoint(alice_ip, 54000)
     │ StartAudio()                               │ StartAudio()
     │                                           │
     │────── UDP Opus frames ─────────────────►  │
     │  ◄──── UDP Opus frames ────────────────── │
     │                                           │
     │ (khi kết thúc)                            │
     │──VOICE_HANGUP|alice──────────────────────►│ (qua TCP)
```

---

## 16. Thư viện & lý do chọn — Tóm tắt

| Thư viện | Dùng cho | Lý do chọn |
|---|---|---|
| **NAudio 2.3.0** | Capture mic, phát speaker | Thư viện audio .NET phổ biến nhất, hỗ trợ WaveIn/WaveOut/Mixing |
| **Concentus 2.2.2** | Opus encode/decode | Pure C# — không cần native DLL, cross-platform |
| **AForge.Video.DirectShow 2.2.5** | Capture webcam | Truy cập camera qua DirectShow COM trên Windows |
| **Microsoft.Data.Sqlite 10.0.7** | SQLite database | Official Microsoft wrapper, nhẹ, zero-config |
| **Whisper.net 1.7.4** | STT local | Wrapper của OpenAI Whisper, tốt với tiếng Việt |
| **System.Windows.Extensions 10.0.5** | Windows audio playback (Bot) | `SoundPlayer` và Windows-specific APIs |
| **ECDiffieHellmanCng** (built-in) | ECDH key exchange | API crypto native Windows CNG, không cần thư viện ngoài |
| **AesGcm** (built-in) | Mã hóa E2EE | Built-in .NET, hỗ trợ AES-NI hardware acceleration |
| **Rfc2898DeriveBytes** (built-in) | PBKDF2 key derivation | Chuẩn NIST, 100k iterations chống brute-force |
