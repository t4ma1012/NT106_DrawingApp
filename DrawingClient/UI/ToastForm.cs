using System;
using System.Drawing;
using System.Windows.Forms;

namespace DrawingClient.UI
{
    public class ToastForm : Form
    {
        private Timer closeTimer;
        private Timer slideTimer;
        private int _targetY;

        public ToastForm(string message)
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.BackColor = Color.Black;
            this.ForeColor = Color.White;
            this.Size = new Size(250, 40);
            this.TopMost = true;
            this.ShowInTaskbar = false;
            this.Opacity = 0.92;

            Label labelMessage = new Label
            {
                Text = message,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Arial", 10, FontStyle.Bold)
            };
            this.Controls.Add(labelMessage);

            closeTimer = new Timer { Interval = 3000 };
            closeTimer.Tick += (s, e) =>
            {
                closeTimer.Stop();
                this.Close();
            };
            closeTimer.Start();

            slideTimer = new Timer { Interval = 15 };
            slideTimer.Tick += SlideTimer_Tick;
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            slideTimer.Start();
        }

        private void SlideTimer_Tick(object sender, EventArgs e)
        {
            if (this.Top <= _targetY)
            {
                this.Top = _targetY;
                slideTimer.Stop();
                return;
            }

            this.Top -= 8;
        }

        public static void ShowToast(Form owner, string message)
        {
            if (owner != null && owner.IsHandleCreated && owner.InvokeRequired)
            {
                owner.BeginInvoke(new Action(() => ShowToast(owner, message)));
                return;
            }

            ToastForm toast = new ToastForm(message);
            toast.StartPosition = FormStartPosition.Manual;

            Rectangle bounds = owner?.RectangleToScreen(owner.ClientRectangle)
                ?? Screen.PrimaryScreen.WorkingArea;

            int x = bounds.Right - toast.Width - 16;
            int startY = bounds.Bottom + 8;
            int targetY = bounds.Bottom - toast.Height - 16;

            toast.Location = new Point(x, startY);
            toast._targetY = targetY;
            toast.Show(owner);
        }
    }
}