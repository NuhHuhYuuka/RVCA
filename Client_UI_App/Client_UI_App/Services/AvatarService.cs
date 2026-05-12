using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

namespace Client_UI_App.Services
{
    internal static class AvatarService
    {
        private static readonly string AvatarDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ChatApp_Avatars");

        // ── Đặt path này trỏ tới file ảnh avatar của UitiChan ────────
        // Ví dụ: @"D:\Assets\uitichan.png"
        public static string BotAvatarPath { get; set; } = string.Empty;

        // Lấy path ảnh đã lưu của user (trả "" nếu chưa set)
        public static string GetUserAvatarPath(string username)
        {
            foreach (string ext in new[] { ".png", ".jpg", ".jpeg" })
            {
                string p = Path.Combine(AvatarDir, $"{username}{ext}");
                if (File.Exists(p)) return p;
            }
            return string.Empty;
        }

        // Lưu avatar mới (convert sang PNG chuẩn) — trả path đã lưu
        public static string SaveUserAvatar(string username, string sourcePath)
        {
            Directory.CreateDirectory(AvatarDir);
            string dest = Path.Combine(AvatarDir, $"{username}.png");
            using var img = Image.FromFile(sourcePath);
            // Resize về 128x128 trước khi lưu
            using var resized = new Bitmap(128, 128);
            using (var g = Graphics.FromImage(resized))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.DrawImage(img, 0, 0, 128, 128);
            }
            resized.Save(dest, ImageFormat.Png);
            return dest;
        }

        // Load Bitmap từ file (trả null nếu không có hoặc lỗi)
        // Dùng bản sao in-memory để tránh lock file — cho phép SaveUserAvatar ghi đè sau này
        public static Bitmap? LoadBitmap(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
            try
            {
                using var locked = new Bitmap(path);
                return new Bitmap(locked); // clone vào RAM, giải phóng lock file ngay
            }
            catch { return null; }
        }

        // Tạo Bitmap chữ cái đầu kiểu Discord (màu nền theo hash tên)
        public static Bitmap CreateInitialsBitmap(string name, int size = 40)
        {
            var bmp = new Bitmap(size, size);
            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode     = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

            var palette = new[]
            {
                Color.FromArgb(88,  101, 242),  // blurple
                Color.FromArgb(35,  165, 90),   // green
                Color.FromArgb(200, 60,  130),  // pink
                Color.FromArgb(230, 150, 30),   // gold
                Color.FromArgb(210, 65,  65),   // red
                Color.FromArgb(0,   150, 200),  // cyan
                Color.FromArgb(120, 70,  200),  // purple
            };
            // & 0x7FFFFFFF tránh Math.Abs(int.MinValue) overflow → IndexOutOfRangeException
            var bg = palette[((name ?? "?").GetHashCode() & 0x7FFFFFFF) % palette.Length];
            g.Clear(bg);

            string letter = name?.Length > 0 ? name[0].ToString().ToUpper() : "?";
            float  fontSize = size * 0.40f;
            using var font = new Font("Segoe UI", fontSize, FontStyle.Bold, GraphicsUnit.Pixel);
            var sf = new StringFormat
            {
                Alignment     = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };
            g.DrawString(letter, font, Brushes.White, new RectangleF(0, 0, size, size), sf);
            return bmp;
        }

        // Áp clip hình tròn cho PictureBox (gọi sau khi control đã có kích thước thật)
        public static void ApplyCircularClip(PictureBox pb)
        {
            if (pb.Width <= 0 || pb.Height <= 0) return;
            using var gp = new GraphicsPath();
            gp.AddEllipse(0, 0, pb.Width - 1, pb.Height - 1);
            pb.Region = new Region(gp);
        }
    }
}
