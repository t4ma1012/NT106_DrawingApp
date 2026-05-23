using System;
using System.Windows.Forms;
using DrawingClient.Forms;
using SharedLib.Config;

namespace DrawingClient
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            EnvLoader.Load();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new LoginForm());
        }
    }
}
