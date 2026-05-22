using DrawingClient.Drawing;
using DrawingClient.Network;
using DrawingClient.UI;
using SharedLib.Packets;
using SharedLib.Payloads;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace DrawingClient.Forms
{
    public class MainForm : Form
    {
        private readonly ClientNetwork _network;
        private readonly string _roomCode;
        private UdpManager _udpManager;

        private DoubleBufferedPictureBox canvas;
        private Panel toolPanel;
        private Panel userPanel;
        private CursorLayer cursorLayer;
        private Button btnColorPicker;
        private Button btnBackColor;
        private Button btnClearAll;
        private TrackBar tbPenWidth;
        private Label lblPenWidth;
        private ToolTip colorToolTip;
        private ComboBox cbCanvasSize;
        private ColorDialog colorDialog;
        private ListBox lstChat;
        private ListBox lstLogs;
        private TextBox txtChatInput;
        private ProgressBar gifProgress;
        private Label lblGifStatus;
        private Label lblFollowState;

        private CanvasManager canvasManager;
        private StickerPickerControl stickerPicker;
        private TurnPanelControl turnPanel;
        private PlaybackPanelControl playbackPanel;
        private readonly Dictionary<string, StickyNoteControl> noteControls = new Dictionary<string, StickyNoteControl>();
        private string selectedStickerId;
        private bool isPlacingSticker;
        private bool isStickyNoteMode;
        private bool isFollowing;
        private long lastCursorSend;
        private long lastLaserSend;
        private const int CursorSendIntervalMs = 35;

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
                colorToolTip?.SetToolTip(btnColorPicker, $"Mã màu: #{color.R:X2}{color.G:X2}{color.B:X2}");
                ToastForm.ShowToast(this, "Đã hút màu");
            };

            canvasManager.OnNetworkDrawAction = (p1, p2, color, width) =>
            {
                if (_udpManager == null)
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

                _udpManager.SendDraw(payload);
            };

            canvasManager.OnClaimAreaSelected = rect =>
            {
                // FIX LỖI: Dùng hàm Send tổng quát
                _network?.Send(CommandType.CLAIM_AREA, new ClaimAreaPayload
                {
                    ClaimID = Guid.NewGuid().ToString(),
                    Username = _network.CurrentUsername,
                    X1 = rect.Left,
                    Y1 = rect.Top,
                    X2 = rect.Right,
                    Y2 = rect.Bottom,
                    DurationSeconds = 30
                });
                AppendLog($"Khoanh vùng sở hữu: [{rect.Left},{rect.Top}] - [{rect.Right},{rect.Bottom}]");
            };

            canvasManager.OnNetworkFloodFillAction = payload =>
            {
                if (_udpManager == null || payload == null)
                    return;
                payload.Username = _network.CurrentUsername;
                _udpManager.SendFloodFill(payload);
            };

            canvasManager.OnNetworkTextAction = payload =>
            {
                if (_udpManager == null || payload == null)
                    return;
                payload.Username = _network.CurrentUsername;
                _udpManager.SendDraw(payload);
            };
        }

        private void SetupUdp()
        {
            try
            {
                _udpManager = new UdpManager("127.0.0.1", 8889);
                _udpManager.Start();
                AppendLog($"UDP sẵn sàng trên port {_udpManager.LocalPort}");
            }
            catch (Exception ex)
            {
                AppendLog($"UDP chưa sẵn sàng: {ex.Message}");
            }
        }



        private void SubscribeNetworkEvents()
        {
            NetworkEvents.OnCursorReceived += NetworkEvents_OnCursorReceived;
            NetworkEvents.OnUserJoined += NetworkEvents_OnUserJoined;
            NetworkEvents.OnUserLeft += NetworkEvents_OnUserLeft;
            NetworkEvents.OnDrawReceived += NetworkEvents_OnDrawReceived;
            NetworkEvents.OnSyncBoardReceived += NetworkEvents_OnSyncBoardReceived;
            NetworkEvents.OnFloodFillReceived += NetworkEvents_OnFloodFillReceived;
            NetworkEvents.OnImportImageReceived += NetworkEvents_OnImportImageReceived;
            NetworkEvents.OnSetBackgroundReceived += NetworkEvents_OnSetBackgroundReceived;
            NetworkEvents.OnClearAllReceived += NetworkEvents_OnClearAllReceived;
            NetworkEvents.OnLaserReceived += NetworkEvents_OnLaserReceived;
            NetworkEvents.OnReactionReceived += NetworkEvents_OnReactionReceived;
            NetworkEvents.OnChatReceived += NetworkEvents_OnChatReceived;
            NetworkEvents.OnActivityLogReceived += NetworkEvents_OnActivityLogReceived;
            NetworkEvents.OnUndoReceived += NetworkEvents_OnUndoReceived;
            NetworkEvents.OnRedoReceived += NetworkEvents_OnRedoReceived;
            NetworkEvents.OnPlaybackReceived += NetworkEvents_OnPlaybackReceived;
            NetworkEvents.OnStickerReceived += NetworkEvents_OnStickerReceived;
            NetworkEvents.OnStickyNoteReceived += NetworkEvents_OnStickyNoteReceived;
            NetworkEvents.OnFollowModeReceived += NetworkEvents_OnFollowModeReceived;
            NetworkEvents.OnGifExportProgress += NetworkEvents_OnGifExportProgress;
        }

        private void MainForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            NetworkEvents.OnCursorReceived -= NetworkEvents_OnCursorReceived;
            NetworkEvents.OnUserJoined -= NetworkEvents_OnUserJoined;
            NetworkEvents.OnUserLeft -= NetworkEvents_OnUserLeft;
            NetworkEvents.OnDrawReceived -= NetworkEvents_OnDrawReceived;
            NetworkEvents.OnSyncBoardReceived -= NetworkEvents_OnSyncBoardReceived;
            NetworkEvents.OnFloodFillReceived -= NetworkEvents_OnFloodFillReceived;
            NetworkEvents.OnImportImageReceived -= NetworkEvents_OnImportImageReceived;
            NetworkEvents.OnSetBackgroundReceived -= NetworkEvents_OnSetBackgroundReceived;
            NetworkEvents.OnClearAllReceived -= NetworkEvents_OnClearAllReceived;
            NetworkEvents.OnLaserReceived -= NetworkEvents_OnLaserReceived;
            NetworkEvents.OnReactionReceived -= NetworkEvents_OnReactionReceived;
            NetworkEvents.OnChatReceived -= NetworkEvents_OnChatReceived;
            NetworkEvents.OnActivityLogReceived -= NetworkEvents_OnActivityLogReceived;
            NetworkEvents.OnUndoReceived -= NetworkEvents_OnUndoReceived;
            NetworkEvents.OnRedoReceived -= NetworkEvents_OnRedoReceived;
            NetworkEvents.OnPlaybackReceived -= NetworkEvents_OnPlaybackReceived;
            NetworkEvents.OnStickerReceived -= NetworkEvents_OnStickerReceived;
            NetworkEvents.OnStickyNoteReceived -= NetworkEvents_OnStickyNoteReceived;
            NetworkEvents.OnFollowModeReceived -= NetworkEvents_OnFollowModeReceived;
            NetworkEvents.OnGifExportProgress -= NetworkEvents_OnGifExportProgress;
            _udpManager?.Stop();
        }

        private void InitializeUI()
        {
            this.Text = string.IsNullOrWhiteSpace(_roomCode) ? "Draw Together" : $"Draw Together - Room {_roomCode}";
            this.Size = new Size(1360, 840);
            this.StartPosition = FormStartPosition.CenterScreen;

            toolPanel = new Panel { Dock = DockStyle.Left, Width = 240, BackColor = Color.LightGray, AutoScroll = true };
            userPanel = new Panel { Dock = DockStyle.Right, Width = 330, BackColor = Color.LightGray };
            canvas = new DoubleBufferedPictureBox { Dock = DockStyle.Fill, BackColor = Color.White };

            colorDialog = new ColorDialog();
            BuildToolPanel();
            BuildUserPanel();

            this.Controls.Add(canvas);
            this.Controls.Add(userPanel);
            this.Controls.Add(toolPanel);
            this.KeyPreview = true;
            this.KeyDown += MainForm_KeyDown;
            this.KeyUp += MainForm_KeyUp;

            canvas.MouseMove += Canvas_MouseMove_SendCursor;
            canvas.MouseDown += Canvas_MouseDown_Custom;
            canvas.MouseMove += Canvas_MouseMove_Custom;
            canvas.MouseUp += Canvas_MouseUp_Custom;
            canvas.Paint += Canvas_Paint_Custom;

            cursorLayer = new CursorLayer(canvas);
        }

        private void BuildToolPanel()
        {
            colorToolTip = new ToolTip();
            btnColorPicker = new Button { Text = "Màu nét", Location = new Point(10, 20), Size = new Size(200, 30) };
            btnColorPicker.Click += (s, e) =>
            {
                if (colorDialog.ShowDialog() == DialogResult.OK)
                {
                    canvasManager.CurrentColor = colorDialog.Color;
                    btnColorPicker.BackColor = colorDialog.Color;
                    colorToolTip.SetToolTip(btnColorPicker, $"Mã màu: #{colorDialog.Color.R:X2}{colorDialog.Color.G:X2}{colorDialog.Color.B:X2}");
                }
            };

            tbPenWidth = new TrackBar { Location = new Point(10, 60), Size = new Size(110, 45), Minimum = 1, Maximum = 30, Value = 2 };
            lblPenWidth = new Label { Location = new Point(125, 65), Size = new Size(85, 20), Text = $"Độ dày: {tbPenWidth.Value}px", TextAlign = ContentAlignment.MiddleLeft };
            tbPenWidth.Scroll += (s, e) => 
            {
                canvasManager.PenWidth = tbPenWidth.Value;
                lblPenWidth.Text = $"Độ dày: {tbPenWidth.Value}px";
            };

            btnBackColor = new Button { Text = "Màu nền", Location = new Point(10, 110), Size = new Size(200, 30) };
            btnBackColor.Click += (s, e) =>
            {
                if (colorDialog.ShowDialog() == DialogResult.OK)
                {
                    canvasManager.ChangeBackgroundColor(colorDialog.Color);
                    _network?.Send(CommandType.SET_BACKGROUND, new SetBackgroundPayload { Username = _network.CurrentUsername, ColorARGB = colorDialog.Color.ToArgb() });
                    colorToolTip.SetToolTip(btnBackColor, $"Mã màu: #{colorDialog.Color.R:X2}{colorDialog.Color.G:X2}{colorDialog.Color.B:X2}");
                }
            };

            btnClearAll = new Button { Text = "Xóa toàn bộ", Location = new Point(10, 145), Size = new Size(200, 30) };
            btnClearAll.Click += (s, e) =>
            {
                canvasManager.ClearAll();
                _network?.SendEmpty(CommandType.CLEAR_ALL);
            };

            cbCanvasSize = new ComboBox { Location = new Point(10, 180), Size = new Size(200, 30), DropDownStyle = ComboBoxStyle.DropDownList };
            cbCanvasSize.Items.AddRange(new object[] { "800x600", "1280x720", "1920x1080" });
            cbCanvasSize.SelectedIndex = 1;
            cbCanvasSize.SelectedIndexChanged += (s, e) =>
            {
                string[] dims = cbCanvasSize.SelectedItem.ToString().Split('x');
                canvasManager.ResizeCanvas(int.Parse(dims[0]), int.Parse(dims[1]));
            };

            Button btnUndo = new Button { Text = "Hoàn tác", Location = new Point(10, 220), Size = new Size(200, 30) };
            btnUndo.Click += (s, e) =>
            {
                canvasManager.Undo();
                _network?.SendUndo(Guid.NewGuid().ToString());
            };

            Button btnRedo = new Button { Text = "Làm lại", Location = new Point(10, 255), Size = new Size(200, 30) };
            btnRedo.Click += (s, e) =>
            {
                canvasManager.Redo();
                _network?.SendRedo(Guid.NewGuid().ToString());
            };

            Button btnImport = new Button { Text = "Nhập ảnh", Location = new Point(10, 290), Size = new Size(200, 30) };
            btnImport.Click += BtnImport_Click;

            Button btnExport = new Button { Text = "Xuất ảnh", Location = new Point(10, 325), Size = new Size(200, 30) };
            btnExport.Click += BtnExport_Click;

            Button btnExportGif = new Button { Text = "Xuất GIF", Location = new Point(10, 360), Size = new Size(200, 30) };
            btnExportGif.Click += (s, e) =>
            {
                gifProgress.Value = 0;
                lblGifStatus.Text = "GIF: Đang yêu cầu...";
                _network?.SendExportGifRequest();
            };

            Button btnGallery = new Button { Text = "Gallery", Location = new Point(10, 395), Size = new Size(200, 30) };
            btnGallery.Click += (s, e) => new GalleryForm(_network).Show(this);

            Button btnZoomIn = new Button { Text = "Zoom +", Location = new Point(10, 430), Size = new Size(95, 30) };
            btnZoomIn.Click += (s, e) => { canvasManager.ZoomFactor = Math.Min(4f, canvasManager.ZoomFactor + 0.2f); canvas.Invalidate(); };
            Button btnZoomOut = new Button { Text = "Zoom -", Location = new Point(115, 430), Size = new Size(95, 30) };
            btnZoomOut.Click += (s, e) => { canvasManager.ZoomFactor = Math.Max(0.2f, canvasManager.ZoomFactor - 0.2f); canvas.Invalidate(); };

            ComboBox cbTools = new ComboBox { Location = new Point(10, 465), Size = new Size(200, 30), DropDownStyle = ComboBoxStyle.DropDownList };
            cbTools.Items.AddRange(Enum.GetNames(typeof(ToolType)));
            cbTools.SelectedIndex = 0;
            cbTools.SelectedIndexChanged += (s, e) => canvasManager.CurrentTool = (ToolType)cbTools.SelectedIndex;

            Button btnStickerMode = new Button { Text = "Đặt sticker", Location = new Point(10, 500), Size = new Size(200, 30) };
            btnStickerMode.Click += (s, e) =>
            {
                isPlacingSticker = !isPlacingSticker;
                isStickyNoteMode = false;
                ToastForm.ShowToast(this, isPlacingSticker ? "Click canvas để đặt sticker" : "Tắt đặt sticker");
            };

            stickerPicker = new StickerPickerControl { Location = new Point(10, 535), Size = new Size(200, 95) };
            stickerPicker.StickerSelected += id =>
            {
                selectedStickerId = id;
                isPlacingSticker = true;
                isStickyNoteMode = false;
            };

            Button btnStickyNote = new Button { Text = "Thêm ghi chú", Location = new Point(10, 635), Size = new Size(200, 30) };
            btnStickyNote.Click += (s, e) =>
            {
                isStickyNoteMode = !isStickyNoteMode;
                isPlacingSticker = false;
                ToastForm.ShowToast(this, isStickyNoteMode ? "Click canvas để tạo ghi chú" : "Tắt tạo ghi chú");
            };

            var txtFollowTarget = new TextBox { Location = new Point(10, 670), Size = new Size(130, 30), Text = "username" };
            var btnFollow = new Button { Text = "Follow", Location = new Point(145, 670), Size = new Size(65, 30) };
            btnFollow.Click += (s, e) =>
            {
                isFollowing = !isFollowing;
                _network?.SendFollowMode(txtFollowTarget.Text.Trim(), isFollowing);
                lblFollowState.Text = isFollowing ? $"Đang follow: {txtFollowTarget.Text.Trim()}" : "Follow: OFF";
            };

            Button btnTurnMode = new Button { Text = "Bật/Tắt vẽ theo lượt", Location = new Point(10, 705), Size = new Size(200, 30) };
            btnTurnMode.Click += (s, e) =>
            {
                bool enable = turnPanel.IsEnabled ? false : true;
                turnPanel.SetState(enable, _network.CurrentUsername);
                _network?.Send(CommandType.SET_TURNBASED, new { RoomCode = _roomCode, IsEnabled = enable });
            };

            gifProgress = new ProgressBar { Location = new Point(10, 740), Size = new Size(200, 16), Minimum = 0, Maximum = 100 };
            lblGifStatus = new Label { Location = new Point(10, 760), Size = new Size(220, 26), Text = "GIF: sẵn sàng" };
            lblFollowState = new Label { Location = new Point(10, 790), Size = new Size(220, 26), Text = "Follow: OFF" };

            Button btnLeaveRoom = new Button { Text = "Rời phòng", Location = new Point(10, 825), Size = new Size(200, 30), BackColor = Color.LightCoral };
            btnLeaveRoom.Click += (s, e) =>
            {
                _network?.SendEmpty(CommandType.LEAVE_ROOM);
                this.Hide();
                var lobby = new LobbyForm(_network, _network.CurrentUsername);
                lobby.ShowDialog();
                this.Close();
            };

            Button btnToggleChat = new Button { Text = "Ẩn/Hiện Chat", Location = new Point(10, 860), Size = new Size(200, 30), BackColor = Color.LightBlue };
            btnToggleChat.Click += (s, e) =>
            {
                userPanel.Visible = !userPanel.Visible;
            };

            toolPanel.Controls.AddRange(new Control[]
            {
                btnColorPicker, tbPenWidth, btnBackColor, btnClearAll, cbCanvasSize,
                btnUndo, btnRedo, btnImport, btnExport, btnExportGif, btnGallery,
                lblPenWidth, btnZoomIn, btnZoomOut, cbTools, btnStickerMode, stickerPicker,
                btnStickyNote, txtFollowTarget, btnFollow, btnTurnMode, gifProgress,
                lblGifStatus, lblFollowState, btnLeaveRoom, btnToggleChat
            });
        }

        private void BuildUserPanel()
        {
            turnPanel = new TurnPanelControl { Dock = DockStyle.Top };
            playbackPanel = new PlaybackPanelControl { Dock = DockStyle.Top };

            // FIX LỖI: Dùng hàm Send tổng quát
            playbackPanel.RequestPlayback += () => _network?.Send(CommandType.REQUEST_PLAYBACK, new PlaybackRequestPayload { RoomCode = _roomCode });

            TabControl tabs = new TabControl { Dock = DockStyle.Fill };
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
            userPanel.Controls.Add(playbackPanel);
            userPanel.Controls.Add(turnPanel);
        }

        private void Canvas_MouseMove_SendCursor(object sender, MouseEventArgs e)
        {
            if (_udpManager == null || string.IsNullOrWhiteSpace(_network?.CurrentUsername))
                return;

            long now = Environment.TickCount;
            if (now - lastCursorSend >= CursorSendIntervalMs)
            {
                lastCursorSend = now;
                _udpManager.SendCursor(new CursorPayload
                {
                    Username = _network.CurrentUsername,
                    X = e.X,
                    Y = e.Y
                });
            }

            if ((ModifierKeys & Keys.Alt) == Keys.Alt && now - lastLaserSend >= CursorSendIntervalMs)
            {
                lastLaserSend = now;
                _udpManager.SendLaser(new LaserPayload
                {
                    Username = _network.CurrentUsername,
                    X = e.X,
                    Y = e.Y,
                    IsActive = true
                });
            }
        }

        private void Canvas_MouseDown_Custom(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;

            if (isPlacingSticker || pendingImportImage != null)
            {
                dragStartPoint = e.Location;
            }
            else if (isStickyNoteMode)
            {
                string noteId = Guid.NewGuid().ToString();
                var note = new StickyNoteControl { NoteId = noteId, Author = _network?.CurrentUsername, Location = e.Location };
                note.NoteChanged += StickyNoteChanged;
                canvas.Controls.Add(note);
                note.BringToFront();
                noteControls[noteId] = note;

                _network?.SendStickyNote(new StickyNotePayload
                {
                    NoteID = noteId,
                    AuthorUsername = _network?.CurrentUsername,
                    X = e.X,
                    Y = e.Y,
                    Text = note.NoteText,
                    IsOpen = true,
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                });

                isStickyNoteMode = false;
            }
        }

        private void Canvas_MouseMove_Custom(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && (isPlacingSticker || pendingImportImage != null))
            {
                canvas.Invalidate();
            }
        }

        private void Canvas_MouseUp_Custom(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && isPlacingSticker)
            {
                Point start = canvasManager.ScreenToCanvas(dragStartPoint);
                Point end = canvasManager.ScreenToCanvas(e.Location);
                int width = Math.Max(24, Math.Abs(end.X - start.X));
                int height = Math.Max(24, Math.Abs(end.Y - start.Y));
                int x = Math.Min(start.X, end.X);
                int y = Math.Min(start.Y, end.Y);

                var payload = new StickerPayload
                {
                    ActionID = Guid.NewGuid().ToString(),
                    Username = _network?.CurrentUsername,
                    StickerID = string.IsNullOrWhiteSpace(selectedStickerId) ? "star" : selectedStickerId,
                    X = x,
                    Y = y,
                    Width = width,
                    Height = height,
                    Rotation = 0,
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                };

                canvasManager.AddSticker(payload);
                _network?.SendSticker(payload);

                isPlacingSticker = false;
                canvas.Invalidate();
            }
            else if (e.Button == MouseButtons.Left && pendingImportImage != null)
            {
                Point start = canvasManager.ScreenToCanvas(dragStartPoint);
                Point end = canvasManager.ScreenToCanvas(e.Location);
                int width = Math.Max(50, Math.Abs(end.X - start.X));
                int height = Math.Max(50, Math.Abs(end.Y - start.Y));
                int x = Math.Min(start.X, end.X);
                int y = Math.Min(start.Y, end.Y);
                Rectangle target = new Rectangle(x, y, width, height);

                canvasManager.ImportImage(pendingImportImage, target);

                using (MemoryStream ms = new MemoryStream())
                {
                    pendingImportImage.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                    string base64 = Convert.ToBase64String(ms.ToArray());

                    _network?.Send(CommandType.IMPORT_IMAGE, new ImportImagePayload
                    {
                        ActionID = Guid.NewGuid().ToString(),
                        Username = _network.CurrentUsername,
                        X = target.X,
                        Y = target.Y,
                        Width = target.Width,
                        Height = target.Height,
                        ImageData = base64,
                        Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                    });
                }

                pendingImportImage.Dispose();
                pendingImportImage = null;
                canvas.Invalidate();
            }
        }

        private void Canvas_Paint_Custom(object sender, PaintEventArgs e)
        {
            if (Control.MouseButtons == MouseButtons.Left && (isPlacingSticker || pendingImportImage != null))
            {
                Point endPoint = canvas.PointToClient(Cursor.Position);
                int x = Math.Min(dragStartPoint.X, endPoint.X);
                int y = Math.Min(dragStartPoint.Y, endPoint.Y);
                int w = Math.Abs(endPoint.X - dragStartPoint.X);
                int h = Math.Abs(endPoint.Y - dragStartPoint.Y);

                using (Pen p = new Pen(Color.Red, 2) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash })
                {
                    e.Graphics.DrawRectangle(p, x, y, w, h);
                }
            }
        }

        private void StickyNoteChanged(StickyNoteControl note)
        {
            _network?.SendStickyNote(new StickyNotePayload
            {
                NoteID = note.NoteId,
                AuthorUsername = _network.CurrentUsername,
                X = note.Left,
                Y = note.Top,
                Text = note.NoteText,
                IsOpen = true,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            });
        }

        private Image pendingImportImage;
        private Point dragStartPoint;

        private void BtnImport_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "Image files|*.png;*.jpg;*.jpeg;*.bmp";
                if (openFileDialog.ShowDialog() != DialogResult.OK)
                    return;

                if (pendingImportImage != null) pendingImportImage.Dispose();
                pendingImportImage = Image.FromFile(openFileDialog.FileName);
                isStickyNoteMode = false;
                isPlacingSticker = false;
                ToastForm.ShowToast(this, "Kéo thả chuột trên Canvas để chọn kích cỡ ảnh");
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
            if (string.IsNullOrWhiteSpace(message)) return;

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

        private void NetworkEvents_OnCursorReceived(CursorPayload payload)
        {
            if (payload == null)
                return;
            UIInvoke(() =>
            {
                canvasManager.UpdateRemoteCursor(payload.Username, new Point(payload.X, payload.Y));
                cursorLayer?.UpdateCursor(payload);
            });
        }

        private void NetworkEvents_OnUserJoined(UserJoinPayload payload)
        {
            if (payload == null || string.IsNullOrWhiteSpace(payload.Username))
                return;
            UIInvoke(() =>
            {
                ToastForm.ShowToast(this, $"{payload.Username} đã tham gia phòng");
                AppendLog($"{payload.Username} đã tham gia phòng.");
            });
        }

        private void NetworkEvents_OnUserLeft(UserLeavePayload payload)
        {
            if (payload == null || string.IsNullOrWhiteSpace(payload.Username))
                return;
            UIInvoke(() =>
            {
                cursorLayer?.RemoveCursor(payload.Username);
                canvasManager.RemoveRemoteCursor(payload.Username);
                ToastForm.ShowToast(this, $"{payload.Username} đã rời phòng");
                AppendLog($"{payload.Username} đã rời phòng.");
            });
        }

        private void NetworkEvents_OnDrawReceived(DrawPayload payload) => UIInvoke(() => canvasManager.ApplyRemoteDraw(payload));
        private void NetworkEvents_OnSyncBoardReceived(SyncBoardPayload payload)
        {
            if (payload?.Actions == null)
                return;
            UIInvoke(() =>
            {
                // ✅ FIX: Clear canvas trước khi replay để tránh bị chồng nét khi reconnect
                canvasManager.ClearAll();

                foreach (var action in payload.Actions)
                {
                    string tool = action.ToolType ?? "";

                    if (tool.Equals("FloodFill", StringComparison.OrdinalIgnoreCase))
                    {
                        canvasManager.ApplyRemoteFloodFill(new SharedLib.Payloads.FloodFillPayload
                        {
                            X = action.X1,
                            Y = action.Y1,
                            ColorARGB = action.ColorARGB
                        });
                    }
                    else if (tool.Equals("SetBackground", StringComparison.OrdinalIgnoreCase))
                    {
                        // ✅ FIX: Replay màu nền khi reconnect
                        canvasManager.ApplyRemoteSetBackground(new SharedLib.Payloads.SetBackgroundPayload
                        {
                            ColorARGB = action.ColorARGB
                        });
                    }
                    else if (tool.Equals("Sticker", StringComparison.OrdinalIgnoreCase))
                    {
                        // ✅ FIX: Replay sticker khi reconnect
                        canvasManager.AddSticker(new SharedLib.Payloads.StickerPayload
                        {
                            StickerID = action.Text,   // StickerID được map vào field Text khi lưu
                            X         = action.X1,
                            Y         = action.Y1,
                            Width     = action.ImageWidth  > 0 ? action.ImageWidth  : 64,
                            Height    = action.ImageHeight > 0 ? action.ImageHeight : 64,
                        });
                    }
                    else if (tool.Equals("ImportImage", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(action.ImageData))
                    {
                        canvasManager.ApplyRemoteImportImage(new SharedLib.Payloads.ImportImagePayload
                        {
                            X = action.X1,
                            Y = action.Y1,
                            Width = action.ImageWidth > 0 ? action.ImageWidth : 400,
                            Height = action.ImageHeight > 0 ? action.ImageHeight : 300,
                            ImageData = action.ImageData
                        });
                    }
                    else
                    {
                        canvasManager.ApplyDrawAction(action);
                    }
                }
                AppendLog($"Đồng bộ {payload.Actions.Count} hành động từ phòng");
            });
        }
        private void NetworkEvents_OnFloodFillReceived(FloodFillPayload payload) => UIInvoke(() => canvasManager.ApplyRemoteFloodFill(payload));
        private void NetworkEvents_OnImportImageReceived(ImportImagePayload payload) => UIInvoke(() => canvasManager.ApplyRemoteImportImage(payload));
        private void NetworkEvents_OnSetBackgroundReceived(SetBackgroundPayload payload) => UIInvoke(() => canvasManager.ApplyRemoteSetBackground(payload));
        private void NetworkEvents_OnClearAllReceived() => UIInvoke(() => canvasManager.ApplyRemoteClearAll());
        private void NetworkEvents_OnUndoReceived(UndoPayload payload) => UIInvoke(() => canvasManager.Undo());
        private void NetworkEvents_OnRedoReceived(RedoPayload payload) => UIInvoke(() => canvasManager.Redo());

        private void NetworkEvents_OnLaserReceived(LaserPayload payload)
        {
            if (payload == null || string.IsNullOrWhiteSpace(payload.Username))
                return;

            UIInvoke(() =>
            {
                if (payload.IsActive)
                    cursorLayer.OtherLasers[payload.Username] = new Point(payload.X, payload.Y);
                else if (cursorLayer.OtherLasers.ContainsKey(payload.Username))
                    cursorLayer.OtherLasers.Remove(payload.Username);

                canvas.Invalidate();
            });
        }

        private void NetworkEvents_OnReactionReceived(ReactionPayload payload)
        {
            if (payload == null)
                return;
            UIInvoke(() => cursorLayer.AddEmoji(payload.Emoji, new Point(payload.X, payload.Y)));
        }

        private void NetworkEvents_OnChatReceived(ChatPayload payload)
        {
            if (payload == null)
                return;
            UIInvoke(() =>
            {
                lstChat.Items.Add($"[{DateTime.Now:HH:mm}] {payload.Username}: {payload.Message}");
                lstChat.TopIndex = lstChat.Items.Count - 1;
            });
        }

        private void NetworkEvents_OnActivityLogReceived(ActivityLogPayload payload)
        {
            if (payload == null)
                return;
            UIInvoke(() => AppendLog($"{payload.Username}: {payload.Action}"));
        }

        private void NetworkEvents_OnPlaybackReceived(PlaybackResponsePayload payload)
        {
            if (payload?.Actions == null)
                return;
            UIInvoke(() =>
            {
                canvasManager.ClearAll();
                foreach (var action in payload.Actions)
                    canvasManager.ApplyDrawAction(action);
                AppendLog($"Playback: {payload.Actions.Count} hành động");
            });
        }

        private void NetworkEvents_OnStickerReceived(StickerPayload payload)
        {
            if (payload == null)
                return;
            UIInvoke(() => canvasManager.AddSticker(payload));
        }

        private void NetworkEvents_OnStickyNoteReceived(StickyNotePayload payload)
        {
            if (payload == null || string.IsNullOrWhiteSpace(payload.NoteID))
                return;

            UIInvoke(() =>
            {
                if (!noteControls.TryGetValue(payload.NoteID, out var note))
                {
                    note = new StickyNoteControl { NoteId = payload.NoteID, Author = payload.AuthorUsername };
                    note.NoteChanged += StickyNoteChanged;
                    canvas.Controls.Add(note);
                    noteControls[payload.NoteID] = note;
                }

                note.Location = new Point(payload.X, payload.Y);
                note.NoteText = payload.Text ?? string.Empty;
                note.BringToFront();
            });
        }

        private void NetworkEvents_OnFollowModeReceived(FollowModePayload payload)
        {
            if (payload == null)
                return;
            UIInvoke(() =>
            {
                if (payload.IsFollowing && payload.TargetUsername == _network.CurrentUsername)
                {
                    canvasManager.ZoomFactor = payload.ZoomFactor <= 0 ? canvasManager.ZoomFactor : payload.ZoomFactor;
                    canvas.Invalidate();
                }
                AppendLog($"Follow: {payload.FollowerUsername} -> {payload.TargetUsername} ({payload.IsFollowing})");
            });
        }

        private void NetworkEvents_OnGifExportProgress(GifExportProgressPayload payload)
        {
            if (payload == null)
                return;
            UIInvoke(() =>
            {
                gifProgress.Value = Math.Max(0, Math.Min(100, payload.ProgressPercent));
                lblGifStatus.Text = $"GIF: {payload.Status} ({payload.ProgressPercent}%)";

                if (payload.Status == "completed" && !string.IsNullOrWhiteSpace(payload.GifData))
                {
                    using (SaveFileDialog saveFileDialog = new SaveFileDialog())
                    {
                        saveFileDialog.Filter = "GIF Image|*.gif";
                        saveFileDialog.FileName = $"drawing_{DateTime.Now:yyyyMMdd_HHmmss}.gif";
                        if (saveFileDialog.ShowDialog() == DialogResult.OK)
                        {
                            File.WriteAllBytes(saveFileDialog.FileName, Convert.FromBase64String(payload.GifData));
                            ToastForm.ShowToast(this, "Đã lưu GIF");
                        }
                    }
                }
            });
        }

        private void MainForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Shift) canvas.Cursor = Cursors.Cross;

            if (e.KeyCode == Keys.D1)
            {
                var pos = canvas.PointToClient(Cursor.Position);
                cursorLayer.AddEmoji("👍", pos);
                _udpManager?.SendReaction(new ReactionPayload { Username = _network.CurrentUsername, Emoji = "👍", X = pos.X, Y = pos.Y });
            }
            if (e.KeyCode == Keys.D2)
            {
                var pos = canvas.PointToClient(Cursor.Position);
                cursorLayer.AddEmoji("❤️", pos);
                _udpManager?.SendReaction(new ReactionPayload { Username = _network.CurrentUsername, Emoji = "❤️", X = pos.X, Y = pos.Y });
            }
            if (e.KeyCode == Keys.D3)
            {
                var pos = canvas.PointToClient(Cursor.Position);
                cursorLayer.AddEmoji("😂", pos);
                _udpManager?.SendReaction(new ReactionPayload { Username = _network.CurrentUsername, Emoji = "😂", X = pos.X, Y = pos.Y });
            }
        }

        private void MainForm_KeyUp(object sender, KeyEventArgs e)
        {
            if (!e.Shift)
                canvas.Cursor = Cursors.Default;

            if (e.KeyCode == Keys.Menu)
            {
                var pos = canvas.PointToClient(Cursor.Position);
                _udpManager?.SendLaser(new LaserPayload { Username = _network.CurrentUsername, X = pos.X, Y = pos.Y, IsActive = false });
            }
        }

        private void UIInvoke(Action action)
        {
            if (this.IsHandleCreated && this.InvokeRequired)
            {
                this.BeginInvoke(action);
                return;
            }
            action();
        }

        public class DoubleBufferedPictureBox : PictureBox
        {
            public DoubleBufferedPictureBox()
            {
                this.DoubleBuffered = true;
            }
        }

        private class StickerPickerControl : Panel
        {
            public event Action<string> StickerSelected;

            public StickerPickerControl()
            {
                var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(4), WrapContents = true };
                Add(flow, "❤️", "heart");
                Add(flow, "⭐", "star");
                Add(flow, "🔥", "fire");
                Add(flow, "💡", "idea");
                Add(flow, "✅", "check");
                Controls.Add(flow);
                BorderStyle = BorderStyle.FixedSingle;
                BackColor = Color.WhiteSmoke;
            }

            private void Add(FlowLayoutPanel flow, string glyph, string id)
            {
                var btn = new Button { Text = glyph, Tag = id, Width = 34, Height = 30, Font = new Font("Segoe UI Emoji", 11f) };
                btn.Click += (s, e) => StickerSelected?.Invoke((string)btn.Tag);
                flow.Controls.Add(btn);
            }
        }

        private class StickyNoteControl : Panel
        {
            private bool dragging;
            private Point dragOffset;
            private readonly TextBox txt;
            public string NoteId { get; set; }
            public string Author { get; set; }
            public string NoteText { get => txt.Text; set => txt.Text = value; }
            public event Action<StickyNoteControl> NoteChanged;

            public StickyNoteControl()
            {
                Size = new Size(170, 120);
                BackColor = Color.FromArgb(255, 255, 220);
                BorderStyle = BorderStyle.FixedSingle;

                var header = new Label { Dock = DockStyle.Top, Height = 20, Text = "Ghi chú", BackColor = Color.Khaki, Padding = new Padding(4, 0, 0, 0) };
                txt = new TextBox { Dock = DockStyle.Fill, Multiline = true, BorderStyle = BorderStyle.None, BackColor = BackColor };

                header.MouseDown += DragStart;
                header.MouseMove += DragMove;
                header.MouseUp += DragEnd;
                MouseDown += DragStart;
                MouseMove += DragMove;
                MouseUp += DragEnd;
                txt.Leave += (s, e) => NoteChanged?.Invoke(this);

                Controls.Add(txt);
                Controls.Add(header);
            }

            private void DragStart(object sender, MouseEventArgs e)
            {
                if (e.Button != MouseButtons.Left) return;
                dragging = true;
                dragOffset = e.Location;
                BringToFront();
            }

            private void DragMove(object sender, MouseEventArgs e)
            {
                if (!dragging) return;
                var p = Location;
                p.Offset(e.X - dragOffset.X, e.Y - dragOffset.Y);
                Location = p;
            }

            private void DragEnd(object sender, MouseEventArgs e)
            {
                if (!dragging) return;
                dragging = false;
                NoteChanged?.Invoke(this);
            }
        }

        private class TurnPanelControl : Panel
        {
            private readonly Label lbl;
            public bool IsEnabled { get; private set; }

            public TurnPanelControl()
            {
                Height = 44;
                BackColor = Color.AliceBlue;
                BorderStyle = BorderStyle.FixedSingle;
                lbl = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(8, 0, 0, 0), Text = "Turn-based: OFF" };
                Controls.Add(lbl);
            }

            public void SetState(bool enabled, string activeUser)
            {
                IsEnabled = enabled;
                lbl.Text = enabled ? $"Turn-based: ON | Lượt: {activeUser}" : "Turn-based: OFF";
            }
        }

        private class PlaybackPanelControl : Panel
        {
            public event Action RequestPlayback;

            public PlaybackPanelControl()
            {
                Height = 44;
                BackColor = Color.Honeydew;
                BorderStyle = BorderStyle.FixedSingle;
                var btn = new Button { Text = "Yêu cầu phát lại", Width = 140, Height = 28, Location = new Point(8, 8) };
                btn.Click += (s, e) => RequestPlayback?.Invoke();
                Controls.Add(btn);
            }
        }
    } 
}
