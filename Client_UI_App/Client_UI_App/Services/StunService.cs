using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Client_UI_App.Services
{
    // RFC 5389 STUN Binding Request — khám phá địa chỉ IP:Port bên ngoài NAT
    // Gửi UDP tới STUN server công khai, đọc MAPPED-ADDRESS từ response
    internal static class StunService
    {
        private const string StunHost    = "stun.l.google.com";
        private const int    StunPort    = 19302;
        private const int    TimeoutMs   = 3_000;

        // Magic Cookie theo RFC 5389
        private static readonly byte[] MagicCookie = { 0x21, 0x12, 0xA4, 0x42 };

        // Trả về (externalIp, externalPort) nếu thành công; ném exception nếu thất bại
        public static async Task<(string ip, int port)> GetExternalEndpointAsync()
        {
            // Tạo Transaction ID ngẫu nhiên 12 bytes
            byte[] txId = new byte[12];
            Random.Shared.NextBytes(txId);

            // Xây STUN Binding Request (20 bytes header)
            byte[] request = BuildBindingRequest(txId);

            IPAddress[] addrs = await Dns.GetHostAddressesAsync(StunHost);
            if (addrs.Length == 0)
                throw new Exception("Không thể resolve STUN host");

            // Ưu tiên IPv4
            IPAddress stunAddr = Array.Find(addrs, a => a.AddressFamily == AddressFamily.InterNetwork)
                                 ?? addrs[0];

            using UdpClient udp = new(stunAddr.AddressFamily);
            udp.Client.ReceiveTimeout = TimeoutMs;

            IPEndPoint stunEp = new(stunAddr, StunPort);

            // Gửi tối đa 3 lần (RFC khuyến nghị re-transmit)
            for (int i = 0; i < 3; i++)
            {
                await udp.SendAsync(request, request.Length, stunEp);

                try
                {
                    UdpReceiveResult result = await ReceiveWithTimeoutAsync(udp);
                    var (ip, port) = ParseResponse(result.Buffer, txId);
                    return (ip, port);
                }
                catch (SocketException) { /* timeout — thử lại */ }
                catch (TimeoutException) { /* timeout — thử lại */ }
            }

            throw new Exception("STUN không phản hồi sau 3 lần thử");
        }

        // ── Build STUN Binding Request (RFC 5389 §6) ─────────────────
        private static byte[] BuildBindingRequest(byte[] txId)
        {
            byte[] msg = new byte[20];

            // Message Type = Binding Request (0x0001)
            msg[0] = 0x00; msg[1] = 0x01;
            // Message Length = 0 (không có attribute)
            msg[2] = 0x00; msg[3] = 0x00;
            // Magic Cookie
            Array.Copy(MagicCookie, 0, msg, 4, 4);
            // Transaction ID
            Array.Copy(txId, 0, msg, 8, 12);

            return msg;
        }

        // ── Đọc UDP với timeout ───────────────────────────────────────
        private static async Task<UdpReceiveResult> ReceiveWithTimeoutAsync(UdpClient udp)
        {
            using var cts = new System.Threading.CancellationTokenSource(TimeoutMs);
            try
            {
                return await udp.ReceiveAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                throw new TimeoutException();
            }
        }

        // ── Parse STUN response — tìm XOR-MAPPED-ADDRESS hoặc MAPPED-ADDRESS ──
        private static (string ip, int port) ParseResponse(byte[] data, byte[] txId)
        {
            if (data.Length < 20)
                throw new Exception("Response quá ngắn");

            // Kiểm tra Magic Cookie
            if (data[4] != MagicCookie[0] || data[5] != MagicCookie[1] ||
                data[6] != MagicCookie[2] || data[7] != MagicCookie[3])
                throw new Exception("Magic Cookie không khớp");

            int msgLen = (data[2] << 8) | data[3];
            int offset = 20;
            int end    = Math.Min(20 + msgLen, data.Length);

            while (offset + 4 <= end)
            {
                int attrType = (data[offset] << 8) | data[offset + 1];
                int attrLen  = (data[offset + 2] << 8) | data[offset + 3];
                offset += 4;

                if (offset + attrLen > end) break;

                // XOR-MAPPED-ADDRESS (0x0020) — RFC 5389
                if (attrType == 0x0020)
                    return ParseXorMappedAddress(data, offset, txId);

                // MAPPED-ADDRESS (0x0001) — RFC 3489 cũ, fallback
                if (attrType == 0x0001)
                    return ParseMappedAddress(data, offset);

                // Căn chỉnh 4 bytes
                offset += (attrLen + 3) & ~3;
            }

            throw new Exception("Không tìm thấy MAPPED-ADDRESS trong response");
        }

        // XOR-MAPPED-ADDRESS: family 0x01=IPv4, port^magic[0:2], addr^magic[0:4]
        private static (string ip, int port) ParseXorMappedAddress(byte[] data, int offset, byte[] txId)
        {
            // offset+0: reserved, offset+1: family
            byte family = data[offset + 1];
            int xPort   = ((data[offset + 2] << 8) | data[offset + 3]) ^ ((MagicCookie[0] << 8) | MagicCookie[1]);

            if (family == 0x01) // IPv4
            {
                byte[] xAddr = new byte[4];
                for (int i = 0; i < 4; i++)
                    xAddr[i] = (byte)(data[offset + 4 + i] ^ MagicCookie[i]);
                return (new IPAddress(xAddr).ToString(), xPort);
            }
            else if (family == 0x02) // IPv6
            {
                byte[] xAddr = new byte[16];
                byte[] xorKey = new byte[16];
                Array.Copy(MagicCookie, 0, xorKey, 0, 4);
                Array.Copy(txId,        0, xorKey, 4, 12);
                for (int i = 0; i < 16; i++)
                    xAddr[i] = (byte)(data[offset + 4 + i] ^ xorKey[i]);
                return (new IPAddress(xAddr).ToString(), xPort);
            }

            throw new Exception("Không hỗ trợ address family: " + family);
        }

        // MAPPED-ADDRESS plain (RFC 3489)
        private static (string ip, int port) ParseMappedAddress(byte[] data, int offset)
        {
            byte family = data[offset + 1];
            int  port   = (data[offset + 2] << 8) | data[offset + 3];

            if (family == 0x01)
            {
                byte[] addr = new byte[4];
                Array.Copy(data, offset + 4, addr, 0, 4);
                return (new IPAddress(addr).ToString(), port);
            }

            throw new Exception("Không hỗ trợ address family: " + family);
        }
    }
}
