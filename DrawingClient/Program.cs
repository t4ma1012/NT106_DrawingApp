using System;
using System.Threading;
using System.Windows.Forms;
using DrawingClient.Forms;
using SharedLib.Config;
using SharedLib.Logging;

namespace DrawingClient
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            EnvLoader.Load();
            Logger.Initialize("client_log.txt");
            Application.ThreadException += (s, e) =>
            {
                Logger.Exception("UI", e.Exception);
                MessageBox.Show("Ứng dụng gặp lỗi. Xem logs\\client_log.txt để biết chi tiết.", "Drawing App");
            };
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                if (e.ExceptionObject is Exception ex)
                    Logger.Exception("Unhandled", ex);
            };
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new LoginForm());
            Logger.Close();
        }
    }
}
