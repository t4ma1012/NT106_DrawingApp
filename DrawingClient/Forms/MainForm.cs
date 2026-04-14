using DrawingClient.Drawing;
using DrawingClient.Network;
using DrawingClient.UI;
using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using SharedLib.Payloads;

namespace DrawingClient.Forms
{
    public class MainForm : Form
    {
        private readonly ClientNetwork _network;
        private readonly string _roomCode;
        private SecureUdpSender _udpSender;
        private SecureUdpReceiver _udpReceiver;

        private DoubleBufferedPictureBox canvas;
        private Panel toolPanel;
        private Panel userPanel;
        private CursorLayer cursorLayer;
        private Button btnColorPicker;
        private Button btnBackColor;
        private Button btnClearAll;
        private TrackBar tbPenWidth;
        private ComboBox cbCanvasSize;
        private ColorDialog colorDialog;
        private ListBox lstChat;
        private ListBox lstLogs;
        private TextBox txtChatInput;

        private CanvasManager canvasManager;

        public MainForm(ClientNetwork network, string roomCode)
        {
            _network = network;
            _roomCode = roomCode;

            InitializeUI();
            canvasManager = new CanvasManager(canvas);
            ConfigureCanvasManager();
            SubscribeNetworkEvents();
            SetupUdp();

            this.FormClosed += MainForm_FormClosed;
        }

        private void ConfigureCanvasManager()
        {
            canvasManager.OnColorPicked = (color) =>
            {
                btnColorPicker.BackColor = color;
                ToastForm.ShowToast(this, "Đã hút màu!");
            };

            canvasManager.OnNetworkDrawAction = (p1, p2, color, width) =>
            {
                if (_udpSender == null)
                    return;

                DrawPayload payload = new DrawPayload
                {
                    ActionID = Guid.NewGuid().ToString(),
                    Username = _network?.CurrentUsername,
                    ToolType = canvasManager.CurrentTool.ToString(),
                    X1 = p1.X,
                    Y1 = p1.Y,
                    X2 = p2.X,
                    Y2 = p2.Y,
                    ColorARGB = color.ToArgb(),
                    Thickness = width,
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                };

                _udpSender.SendDraw(payload);
            };

            canvasManager.OnClaimAreaSelected = rect =>
            {
                _network?.SendClaimArea(Guid.NewGuid().ToString(), rect.Left, rect.Top, rect.Right, rect.Bottom);
                AppendLog($"Khoanh vùng sở hữu: [{rect.Left},{rect.Top}] - [{rect.Right},{rect.Bottom}]");
            };
        }

        private void SetupUdp()
        {
            try
            {
                _udpSender = new SecureUdpSender("127.0.0.1", 8889);
                _udpReceiver = new SecureUdpReceiver(8889);
                _udpReceiver.Start();
            }
            catch
            {
                AppendLog("UDP realtime chưa sẵn sàng.");
            }
        }

        private void SubscribeNetworkEvents()
        {
            NetworkEvents.OnCursorReceived += NetworkEvents_OnCursorReceived;
            NetworkEvents.OnUserJoined += NetworkEvents_OnUserJoined;
            NetworkEvents.OnUserLeft += NetworkEvents_OnUserLeft;
            NetworkEvents.OnDrawReceived += NetworkEvents_OnDrawReceived;
            NetworkEvents.OnChatReceived += NetworkEvents_OnChatReceived;
            NetworkEvents.OnActivityLogReceived += NetworkEvents_OnActivityLogReceived;
        }

        private void MainForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            NetworkEvents.OnCursorReceived -= NetworkEvents_OnCursorReceived;
            NetworkEvents.OnUserJoined -= NetworkEvents_OnUserJoined;
            NetworkEvents.OnUserLeft -= NetworkEvents_OnUserLeft;
            NetworkEvents.OnDrawReceived -= NetworkEvents_OnDrawReceived;
            NetworkEvents.OnChatReceived -= NetworkEvents_OnChatReceived;
            NetworkEvents.OnActivityLogReceived -= NetworkEvents_OnActivityLogReceived;
            _udpReceiver?.Stop();
            _udpSender?.Close();
        }

        private void NetworkEvents_OnCursorReceived(CursorPayload payload)
        {
            if (payload == null)
                return;

            if (this.IsHandleCreated && this.InvokeRequired)
            {
                this.BeginInvoke(new Action(() => NetworkEvents_OnCursorReceived(payload)));
                return;
            }

            canvasManager.UpdateRemoteCursor(payload.Username, new Point(payload.X, payload.Y));
            cursorLayer?.UpdateCursor(payload);
        }

        private void NetworkEvents_OnUserJoined(UserJoinPayload payload)
        {
            if (payload == null || string.IsNullOrWhiteSpace(payload.Username))
                return;

            if (this.IsHandleCreated && this.InvokeRequired)
            {
                this.BeginInvoke(new Action(() => NetworkEvents_OnUserJoined(payload)));
                return;
            }

            ToastForm.ShowToast(this, $"{payload.Username} đã tham gia phòng");
            AppendLog($"{payload.Username} joined room.");
        }

        private void NetworkEvents_OnUserLeft(UserLeavePayload payload)
        {
            if (payload == null || string.IsNullOrWhiteSpace(payload.Username))
                return;

            if (this.IsHandleCreated && this.InvokeRequired)
            {
                this.BeginInvoke(new Action(() => NetworkEvents_OnUserLeft(payload)));
                return;
            }

            cursorLayer?.RemoveCursor(payload.Username);
            canvasManager.RemoveRemoteCursor(payload.Username);
            ToastForm.ShowToast(this, $"{payload.Username} đã rời phòng");
            AppendLog($"{payload.Username} left room.");
        }

        private void NetworkEvents_OnDrawReceived(DrawPayload payload)
        {
            if (payload == null)
                return;

            if (this.IsHandleCreated && this.InvokeRequired)
            {
                this.BeginInvoke(new Action(() => NetworkEvents_OnDrawReceived(payload)));
                return;
            }

            canvasManager.ApplyRemoteDraw(payload);
        }

        private void NetworkEvents_OnChatReceived(ChatPayload payload)
        {
            if (payload == null)
                return;

            if (this.IsHandleCreated && this.InvokeRequired)
            {
                this.BeginInvoke(new Action(() => NetworkEvents_OnChatReceived(payload)));
                return;
            }

            lstChat.Items.Add($"[{DateTime.Now:HH:mm}] {payload.Username}: {payload.Message}");
            lstChat.TopIndex = lstChat.Items.Count - 1;
        }

        private void NetworkEvents_OnActivityLogReceived(ActivityLogPayload payload)
        {
            if (payload == null)
                return;

            if (this.IsHandleCreated && this.InvokeRequired)
            {
                this.BeginInvoke(new Action(() => NetworkEvents_OnActivityLogReceived(payload)));
                return;
            }

            AppendLog($"{payload.Username}: {payload.Action}");
        }

        private void InitializeUI()
        {
            this.Text = string.IsNullOrWhiteSpace(_roomCode) ? "Draw Together" : $"Draw Together - Room {_roomCode}";
            this.Size = new Size(1200, 800);
            this.StartPosition = FormStartPosition.CenterScreen;

            toolPanel = new Panel
            {
                Dock = DockStyle.Left,
                Width = 220,
                BackColor = Color.LightGray
            };

            userPanel = new Panel
            {
                Dock = DockStyle.Right,
                Width = 320,
                BackColor = Color.LightGray
            };

            canvas = new DoubleBufferedPictureBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White
            };

            colorDialog = new ColorDialog();

            btnColorPicker = new Button
            {
                Text = "Màu nét vẽ",
                Location = new Point(10, 20),
                Size = new Size(180, 30)
            };
            btnColorPicker.Click += (s, e) =>
            {
                if (colorDialog.ShowDialog() == DialogResult.OK)
                {
                    canvasManager.CurrentColor = colorDialog.Color;
                    btnColorPicker.BackColor = colorDialog.Color;
                }
            };

            tbPenWidth = new TrackBar
            {
                Location = new Point(10, 60),
                Size = new Size(180, 45),
                Minimum = 1,
                Maximum = 30,
                Value = 2
            };
            tbPenWidth.Scroll += (s, e) => canvasManager.PenWidth = tbPenWidth.Value;

            btnBackColor = new Button
            {
                Text = "Màu nền",
                Location = new Point(10, 110),
                Size = new Size(180, 30)
            };
            btnBackColor.Click += (s, e) =>
            {
                if (colorDialog.ShowDialog() == DialogResult.OK)
                    canvasManager.ChangeBackgroundColor(colorDialog.Color);
            };

            btnClearAll = new Button
            {
                Text = "Xóa toàn bộ",
                Location = new Point(10, 150),
                Size = new Size(180, 30)
            };
            btnClearAll.Click += (s, e) => canvasManager.ClearAll();

            cbCanvasSize = new ComboBox
            {
                Location = new Point(10, 190),
                Size = new Size(180, 30),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cbCanvasSize.Items.AddRange(new object[] { "800x600", "1280x720", "1920x1080" });
            cbCanvasSize.SelectedIndex = 0;
            cbCanvasSize.SelectedIndexChanged += (s, e) =>
            {
                string[] dims = cbCanvasSize.SelectedItem.ToString().Split('x');
                canvasManager.ResizeCanvas(int.Parse(dims[0]), int.Parse(dims[1]));
            };

            toolPanel.Controls.AddRange(new Control[] {
                btnColorPicker, tbPenWidth, btnBackColor, btnClearAll, cbCanvasSize
            });

            this.Controls.Add(canvas);
            this.Controls.Add(userPanel);
            this.Controls.Add(toolPanel);
            this.KeyPreview = true;
            this.KeyDown += MainForm_KeyDown;
            this.KeyUp += MainForm_KeyUp;

            Button btnUndo = new Button { Text = "Hoàn tác", Location = new Point(10, 230), Size = new Size(180, 30) };
            btnUndo.Click += (s, e) => canvasManager.Undo();

            Button btnRedo = new Button { Text = "Làm lại", Location = new Point(10, 265), Size = new Size(180, 30) };
            btnRedo.Click += (s, e) => canvasManager.Redo();

            Button btnImport = new Button { Text = "Nhập ảnh", Location = new Point(10, 300), Size = new Size(180, 30) };
            btnImport.Click += BtnImport_Click;

            Button btnExport = new Button { Text = "Xuất ảnh", Location = new Point(10, 335), Size = new Size(180, 30) };
            btnExport.Click += BtnExport_Click;

            Button btnGallery = new Button { Text = "Gallery", Location = new Point(10, 370), Size = new Size(180, 30) };
            btnGallery.Click += (s, e) => new GalleryForm(_network).Show(this);

            Button btnZoomIn = new Button { Text = "Zoom +", Location = new Point(10, 410), Size = new Size(85, 30) };
            btnZoomIn.Click += (s, e) => { canvasManager.ZoomFactor += 0.2f; canvas.Invalidate(); };

            Button btnZoomOut = new Button { Text = "Zoom -", Location = new Point(105, 410), Size = new Size(85, 30) };
            btnZoomOut.Click += (s, e) => { canvasManager.ZoomFactor = Math.Max(0.2f, canvasManager.ZoomFactor - 0.2f); canvas.Invalidate(); };

            ComboBox cbTools = new ComboBox { Location = new Point(10, 450), Size = new Size(180, 30), DropDownStyle = ComboBoxStyle.DropDownList };
            cbTools.Items.AddRange(Enum.GetNames(typeof(ToolType)));
            cbTools.SelectedIndex = 0;
            cbTools.SelectedIndexChanged += (s, e) => canvasManager.CurrentTool = (ToolType)cbTools.SelectedIndex;

            toolPanel.Controls.AddRange(new Control[] { btnUndo, btnRedo, btnImport, btnExport, btnGallery, btnZoomIn, btnZoomOut, cbTools });

            TabControl tabs = new TabControl
            {
                Dock = DockStyle.Fill
            };

            TabPage tabChat = new TabPage("Chat");
            TabPage tabLogs = new TabPage("Nhật ký");

            lstChat = new ListBox { Dock = DockStyle.Fill };
            Panel chatBottom = new Panel { Dock = DockStyle.Bottom, Height = 40 };
            txtChatInput = new TextBox { Dock = DockStyle.Fill };
            Button btnSendChat = new Button { Text = "Gửi", Dock = DockStyle.Right, Width = 65 };
            btnSendChat.Click += (s, e) => SendChatMessage();
            txtChatInput.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    SendChatMessage();
                }
            };
            chatBottom.Controls.Add(txtChatInput);
            chatBottom.Controls.Add(btnSendChat);
            tabChat.Controls.Add(lstChat);
            tabChat.Controls.Add(chatBottom);

            lstLogs = new ListBox { Dock = DockStyle.Fill };
            tabLogs.Controls.Add(lstLogs);

            tabs.TabPages.Add(tabChat);
            tabs.TabPages.Add(tabLogs);
            userPanel.Controls.Add(tabs);

            cursorLayer = new DrawingClient.UI.CursorLayer(canvas);
        }

        private void BtnImport_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "Image files|*.png;*.jpg;*.jpeg;*.bmp";
                if (openFileDialog.ShowDialog() != DialogResult.OK)
                    return;

                using (Image image = Image.FromFile(openFileDialog.FileName))
                {
                    Rectangle target = new Rectangle(10, 10, Math.Min(image.Width, 600), Math.Min(image.Height, 400));
                    canvasManager.ImportImage(image, target);

                    using (MemoryStream ms = new MemoryStream())
                    {
                        image.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                        string base64 = Convert.ToBase64String(ms.ToArray());
                        _network?.SendImportImage(target.X, target.Y, target.Width, target.Height, base64);
                    }
                }
            }
        }

        private void BtnExport_Click(object sender, EventArgs e)
        {
            using (SaveFileDialog saveFileDialog = new SaveFileDialog())
            {
                saveFileDialog.Filter = "PNG Image|*.png|JPEG Image|*.jpg";
                saveFileDialog.FileName = $"canvas_{DateTime.Now:yyyyMMdd_HHmmss}.png";
                if (saveFileDialog.ShowDialog() != DialogResult.OK)
                    return;

                canvasManager.ExportImage(saveFileDialog.FileName);
                ToastForm.ShowToast(this, "Đã xuất ảnh.");
            }
        }

        private void SendChatMessage()
        {
            string message = txtChatInput.Text?.Trim();
            if (string.IsNullOrWhiteSpace(message))
                return;

            _network?.SendChat(message);
            txtChatInput.Clear();
        }

        private void AppendLog(string message)
        {
            if (lstLogs == null)
                return;

            lstLogs.Items.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
            lstLogs.TopIndex = lstLogs.Items.Count - 1;
        }

        private void MainForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Alt)
            {
                // Gửi UDP lệnh CMD_LASER tại đây
                Point mousePos = canvas.PointToClient(Cursor.Position);
                cursorLayer.OtherLasers["local"] = mousePos;
            }

            if (e.KeyCode == Keys.D1) cursorLayer.AddEmoji("👍", canvas.PointToClient(Cursor.Position));
            if (e.KeyCode == Keys.D2) cursorLayer.AddEmoji("❤️", canvas.PointToClient(Cursor.Position));
            if (e.KeyCode == Keys.D3) cursorLayer.AddEmoji("😂", canvas.PointToClient(Cursor.Position));
        }

        private void MainForm_KeyUp(object sender, KeyEventArgs e)
        {
            if (!e.Alt && cursorLayer.OtherLasers.ContainsKey("local"))
            {
                cursorLayer.OtherLasers.Remove("local");
                canvas.Invalidate();
            }
        }

        // Bắt buộc bọc this.Invoke() khi nhận dữ liệu vẽ từ luồng mạng
        public void DrawFromNetwork(DrawPayload payload)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => DrawFromNetwork(payload)));
                return;
            }
            canvasManager.ApplyRemoteDraw(payload);
        }

        public class DoubleBufferedPictureBox : PictureBox
        {
            public DoubleBufferedPictureBox()
            {
                this.DoubleBuffered = true;
            }
        }
    }
}