using Client_UI_App.Forms;

namespace Client_UI_App
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            // Cấu hình chuẩn WinForms .NET 6+
            ApplicationConfiguration.Initialize();
            Application.Run(new AuthForm());
        }
    }
}
