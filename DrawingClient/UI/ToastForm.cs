using System;
using System.Drawing;
using System.Windows.Forms;

namespace DrawingClient.UI
{
    public class ToastForm : Form
    {
        private Timer closeTimer;

        public ToastForm(string message)
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.BackColor = Color.Black;
            this.ForeColor = Color.White;
            this.Size = new Size(250, 40);
            this.TopMost = true;
            this.ShowInTaskbar = false;

            Label labelMessage = new Label
            {
                Text = message,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Arial", 10, FontStyle.Bold)
            };
            this.Controls.Add(labelMessage);

            closeTimer = new Timer { Interval = 3000 };
            closeTimer.Tick += (s, e) => this.Close();
            closeTimer.Start();
        }

        public static void ShowToast(Form owner, string message)
        {
            ToastForm toast = new ToastForm(message);
            toast.StartPosition = FormStartPosition.Manual;
            toast.Location = new Point(
                owner.Location.X + (owner.Width - toast.Width) / 2,
                owner.Location.Y + owner.Height - toast.Height - 60
            );
            toast.Show(owner);
        }
    }
}