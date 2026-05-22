using System;
using System.Windows.Forms;
using DrawingClient.Forms;

namespace DrawingClient
{
    internal static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // ĐỔI "CỬA CHÍNH" TẠI ĐÂY: Bắt đầu từ LoginForm thay vì MainForm
            Application.Run(new LoginForm());
        }
    }
}