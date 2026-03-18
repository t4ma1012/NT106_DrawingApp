using System;
using System.Drawing;
using System.Windows.Forms;

namespace DrawingClient.Forms
{
    public class LobbyForm : Form
    {
        public LobbyForm()
        {
            this.Text = "Sảnh chờ";
            this.Size = new Size(400, 300);
            this.StartPosition = FormStartPosition.CenterScreen;
        }
    }
}