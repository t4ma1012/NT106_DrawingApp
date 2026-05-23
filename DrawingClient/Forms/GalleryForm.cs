using System.Windows.Forms;
using DrawingClient.Network;

namespace DrawingClient.Forms
{
    public class GalleryForm : Form
    {
        public GalleryForm(ClientNetwork network)
        {
            this.Text = "Thư viện ảnh (Đang thi công...)";
            this.Size = new System.Drawing.Size(400, 300);
            this.StartPosition = FormStartPosition.CenterScreen;
        }
    }
}