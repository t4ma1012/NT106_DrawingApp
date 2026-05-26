using DrawingClient.Network;
using SharedLib.Payloads;
using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace DrawingClient.Forms
{
    public class GalleryForm : Form
    {
        private readonly ClientNetwork _network;
        private readonly FlowLayoutPanel galleryPanel;
        private readonly Label lblStatus;

        public GalleryForm(ClientNetwork network)
        {
            _network = network;

            Text = "Gallery";
            Size = new Size(760, 520);
            StartPosition = FormStartPosition.CenterParent;

            var topPanel = new Panel { Dock = DockStyle.Top, Height = 44, Padding = new Padding(8) };
            var btnRefresh = new Button { Text = "Làm mới", Width = 100, Height = 28, Dock = DockStyle.Left };
            btnRefresh.Click += (s, e) => LoadGallery();
            lblStatus = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(12, 0, 0, 0), Text = "Đang tải Gallery..." };
            topPanel.Controls.Add(lblStatus);
            topPanel.Controls.Add(btnRefresh);

            galleryPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                Padding = new Padding(10),
                BackColor = Color.WhiteSmoke
            };

            Controls.Add(galleryPanel);
            Controls.Add(topPanel);

            NetworkEvents.OnGalleryReceived += NetworkEvents_OnGalleryReceived;
            FormClosed += GalleryForm_FormClosed;
            Load += (s, e) => LoadGallery();
        }

        private void GalleryForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            NetworkEvents.OnGalleryReceived -= NetworkEvents_OnGalleryReceived;
        }

        private void LoadGallery()
        {
            galleryPanel.Controls.Clear();
            lblStatus.Text = "Đang tải Gallery...";
            _network?.SendGetGallery();
        }

        private void NetworkEvents_OnGalleryReceived(GalleryResponsePayload payload)
        {
            if (payload == null)
                return;

            if (IsHandleCreated && InvokeRequired)
            {
                // XU LY DA LUONG: response gallery den tu network thread, marshal ve UI thread truoc khi tao controls.
                BeginInvoke(new Action(() => NetworkEvents_OnGalleryReceived(payload)));
                return;
            }

            galleryPanel.Controls.Clear();
            lblStatus.Text = payload.Items.Count == 0 ? "Chưa có ảnh nào trong Gallery." : $"Có {payload.Items.Count} ảnh đã lưu.";

            foreach (var item in payload.Items)
                galleryPanel.Controls.Add(BuildGalleryCard(item));
        }

        private Control BuildGalleryCard(GalleryItem item)
        {
            var card = new Panel
            {
                Width = 220,
                Height = 245,
                Margin = new Padding(8),
                Padding = new Padding(8),
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };

            var picture = new PictureBox
            {
                Dock = DockStyle.Top,
                Height = 140,
                BackColor = Color.Gainsboro,
                SizeMode = PictureBoxSizeMode.Zoom
            };
            picture.Image = DecodeImage(item.ThumbnailData);

            var lblName = new Label
            {
                Dock = DockStyle.Top,
                Height = 34,
                Text = item.Filename ?? "canvas.png",
                TextAlign = ContentAlignment.MiddleLeft
            };

            var savedAt = item.SavedAt > 0
                ? DateTimeOffset.FromUnixTimeMilliseconds(item.SavedAt).ToLocalTime().ToString("dd/MM/yyyy HH:mm")
                : "";
            var lblMeta = new Label
            {
                Dock = DockStyle.Top,
                Height = 38,
                Text = $"Người lưu: {item.SavedBy}\n{savedAt}",
                ForeColor = Color.DimGray
            };

            var btnSave = new Button
            {
                Dock = DockStyle.Bottom,
                Height = 30,
                Text = "Lưu ảnh"
            };
            btnSave.Click += (s, e) => SaveGalleryImage(item);

            card.Controls.Add(btnSave);
            card.Controls.Add(lblMeta);
            card.Controls.Add(lblName);
            card.Controls.Add(picture);
            return card;
        }

        private static Image DecodeImage(string base64)
        {
            if (string.IsNullOrWhiteSpace(base64))
                return null;

            try
            {
                // I/O DU LIEU -> IMAGE: thumbnail tu server dang la base64, decode thanh bytes roi doc bang MemoryStream.
                byte[] bytes = Convert.FromBase64String(base64);
                using (var ms = new MemoryStream(bytes))
                    return Image.FromStream(ms);
            }
            catch
            {
                return null;
            }
        }

        private void SaveGalleryImage(GalleryItem item)
        {
            // I/O FILE XUAT RA MAY: nguoi dung chon vi tri luu anh gallery ve o dia.
            using (var dialog = new SaveFileDialog())
            {
                dialog.Filter = "PNG Image|*.png";
                dialog.FileName = string.IsNullOrWhiteSpace(item.Filename) ? $"gallery_{item.ID}.png" : item.Filename;
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;

                try
                {
                    // I/O DU LIEU -> FILE: anh gallery dang la base64 trong payload, decode ve bytes va ghi ra file local.
                    byte[] bytes = Convert.FromBase64String(item.ThumbnailData ?? "");
                    File.WriteAllBytes(dialog.FileName, bytes);
                    lblStatus.Text = "Đã lưu ảnh.";
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "Không thể lưu ảnh: " + ex.Message);
                }
            }
        }
    }
}
