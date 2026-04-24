using System.Net;
using System.Net.Sockets;
using System.Text;

Console.WriteLine("=== LOAD BALANCER IS RUNNING ON PORT 9000 ===");

// Khởi tạo TCPListener ở Port 9000
TcpListener listener = new TcpListener(IPAddress.Any, 9000);
listener.Start();

// Danh sách các Port của 2 Server Danh bạ (để chia đều)
int[] serverPorts = { 8888, 8889 };
int currentIndex = 0; // Biến đếm để chia tua (Round-Robin logic)

Console.WriteLine("[INFO] Waiting for incoming Client connections to assign ports...");

while (true)
{
    // Chờ Client gọi tới cổng 9000
    TcpClient client = listener.AcceptTcpClient();

    // Lấy IP của người vừa gọi để in ra màn hình
    string clientIP = ((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString();
    Console.WriteLine($"\n[+] New connection received from IP: {clientIP}");

    // Lấy Port tiếp theo theo luật Round-Robin
    int assignedPort = serverPorts[currentIndex];

    // Tăng biến đếm lên 1. Nếu đụng nóc (2) thì quay về 0.
    currentIndex = (currentIndex + 1) % serverPorts.Length;

    // Đóng gói số Port thành mảng Byte và gửi về cho Client
    NetworkStream stream = client.GetStream();
    byte[] data = Encoding.UTF8.GetBytes(assignedPort.ToString());
    stream.Write(data, 0, data.Length);

    Console.WriteLine($"[=>] Successfully redirected client to Server Port: {assignedPort}");

    // Ngắt kết nối để Client tự gọi qua Server mới
    client.Close();
}