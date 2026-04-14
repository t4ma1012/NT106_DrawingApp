using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using DrawingClient.Network;
using SharedLib.Payloads;

namespace DrawingClient.Forms
{
    public class GalleryForm : Form
    {
        private readonly ClientNetwork _network;
        private readonly ListView _listView;
        private readonly ImageList _thumbs;

        public GalleryForm(ClientNetwork network)
        {
            _network = network;

            this.Text = "Gallery";
            this.StartPosition = FormStartPosition.CenterParent;
            this.Size = new Size(840, 520);

            _thumbs = new ImageList { ImageSize = new Size(180, 120), ColorDepth = ColorDepth.Depth32Bit };
            _listView = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.LargeIcon,
                LargeImageList = _thumbs
            };

            this.Controls.Add(_listView);

            this.Load += GalleryForm_Load;
            this.FormClosed += GalleryForm_FormClosed;
            NetworkEvents.OnGalleryReceived += NetworkEvents_OnGalleryReceived;
        }

        private void GalleryForm_Load(object sender, EventArgs e)
        {
            _network.SendGetGallery();
        }

        private void GalleryForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            NetworkEvents.OnGalleryReceived -= NetworkEvents_OnGalleryReceived;
        }

        private void NetworkEvents_OnGalleryReceived(GalleryResponsePayload payload)
        {
            if (this.IsHandleCreated && this.InvokeRequired)
            {
                this.BeginInvoke(new Action(() => NetworkEvents_OnGalleryReceived(payload)));
                return;
            }

            _thumbs.Images.Clear();
            _listView.Items.Clear();

            if (payload?.Items == null)
                return;

            int index = 0;
            foreach (var item in payload.Items)
            {
                Image img = DecodeImage(item.ThumbnailData) ?? CreatePlaceholder();
                _thumbs.Images.Add(img);

                DateTime savedAt = DateTimeOffset.FromUnixTimeMilliseconds(item.SavedAt).LocalDateTime;
                string text = string.Format("{0}\n{1} - {2:dd/MM HH:mm}", item.Filename, item.SavedBy, savedAt);

                ListViewItem row = new ListViewItem(text, index)
                {
                    Tag = item
                };

                _listView.Items.Add(row);
                index++;
            }
        }

        private static Image DecodeImage(string base64)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(base64))
                    return null;

                byte[] data = Convert.FromBase64String(base64);
                using (MemoryStream ms = new MemoryStream(data))
                {
                    return Image.FromStream(ms);
                }
            }
            catch
            {
                return null;
            }
        }

        private static Image CreatePlaceholder()
        {
            Bitmap bmp = new Bitmap(180, 120);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Gainsboro);
                g.DrawString("No preview", SystemFonts.DefaultFont, Brushes.DimGray, new PointF(45, 52));
            }
            return bmp;
        }
    }
}
