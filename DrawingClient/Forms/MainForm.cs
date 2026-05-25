using DrawingClient.AI;
using DrawingClient.Drawing;
using DrawingClient.Network;
using DrawingClient.UI;
using SharedLib.AI;
using SharedLib.Packets;
using SharedLib.Payloads;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DrawingClient.Forms
{
    public class MainForm : Form
    {
        private readonly ClientNetwork _network;
        private readonly string _roomCode;
        private bool _isRoomOwner;
        private UdpManager _udpManager;

        private DoubleBufferedPictureBox canvas;
        private Panel toolPanel;
        private Panel userPanel;
        private CursorLayer cursorLayer;
        private Button btnColorPicker;
        private Button btnBackColor;
        private Button btnBackImage;
        private Button btnClearAll;
        private Button btnTurnMode;
        private TrackBar tbPenWidth;
        private Label lblPenWidth;
        private ToolTip colorToolTip;
        private ColorDialog colorDialog;
        private ListBox lstMembers;
        private ListBox lstChat;
        private ListBox lstLogs;
        private TextBox txtChatInput;
        private Label lblFollowState;
        private readonly HashSet<string> locallyShownChat = new HashSet<string>();
        private readonly Queue<string> locallyShownChatOrder = new Queue<string>();
        private readonly HashSet<string> displayedChat = new HashSet<string>();
        private readonly Queue<string> displayedChatOrder = new Queue<string>();

        private const float ZoomStep = 0.2f;
        private const float ZoomMin = 0.2f;
        private const float ZoomMax = 4f;

        private CanvasManager canvasManager;
        private StickerPickerControl stickerPicker;
        private TurnPanelControl turnPanel;
        private PlaybackPanelControl playbackPanel;
        private readonly Dictionary<string, StickyNoteControl> noteControls = new Dictionary<string, StickyNoteControl>();
        private string selectedStickyNoteId;
        private string selectedStickerId;
        private bool isPlacingSticker;
        private bool isStickyNoteMode;
        private bool isFollowing;
        private readonly object realtimePointerLock = new object();
        private CursorPayload pendingCursorPayload;
        private LaserPayload pendingLaserPayload;
        private bool hasPendingCursor;
        private bool hasPendingLaser;
        private System.Threading.Timer realtimePointerTimer;
        private int isFlushingRealtimePointers;
        private const int RealtimePointerFlushIntervalMs = 8;
        private readonly List<DrawAction> actionHistory = new List<DrawAction>();
        private readonly HashSet<string> undoneActionIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> ownRedoActionIds = new List<string>();

        public MainForm(ClientNetwork network, string roomCode, bool isRoomOwner = false)
        {
            _network = network;
            _roomCode = roomCode;
            _isRoomOwner = isRoomOwner;

            InitializeUI();
            canvasManager = new CanvasManager(canvas);
            ConfigureCanvasManager();
            SubscribeNetworkEvents();
            SetupUdp();

            this.FormClosed += MainForm_FormClosed;
        }

        public void RegisterUdpEndpoint()
        {
            _ = RegisterUdpEndpointBurstAsync();
        }

        private async Task RegisterUdpEndpointBurstAsync()
        {
            for (int i = 0; i < 5; i++)
            {
                _udpManager?.RegisterEndpoint(_network?.CurrentUsername, _roomCode);
                await Task.Delay(250);
            }
        }

        public void SetRoomOwner(bool isRoomOwner)
        {
            _isRoomOwner = isRoomOwner;
            if (btnTurnMode != null)
                btnTurnMode.Visible = _isRoomOwner;

            if (turnPanel != null)
                turnPanel.SetState(turnPanel.IsEnabled, turnPanel.ActiveUser, _isRoomOwner);
        }

        private void ConfigureCanvasManager()
        {
            canvasManager.OnColorPicked = (color) =>
            {
                btnColorPicker.BackColor = color;
                colorToolTip?.SetToolTip(btnColorPicker, $"Mã màu: #{color.R:X2}{color.G:X2}{color.B:X2}");
                ToastForm.ShowToast(this, "Đã hút màu");
            };

            canvasManager.OnNetworkDrawAction = payload =>
            {
                if (payload == null)
                    return;
                payload.Username = _network?.CurrentUsername;
                RecordAction(ToDrawAction(payload), true);
                _network?.SendDrawRealtime(payload);
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
                if (payload == null)
                    return;
                payload.Username = _network.CurrentUsername;
                RecordAction(ToDrawAction(payload), true);
                _network?.SendFloodFillRealtime(payload);
            };

            canvasManager.OnNetworkTextAction = payload =>
            {
                if (payload == null)
                    return;
                payload.Username = _network.CurrentUsername;
                RecordAction(ToDrawAction(payload), true);
                _network?.SendTextRealtime(payload);
            };

            canvasManager.OnNetworkImportImageAction = payload =>
            {
                if (payload == null)
                    return;

                payload.Username = _network.CurrentUsername;
                RecordAction(ToDrawAction(payload), true);
                _network?.Send(CommandType.IMPORT_IMAGE, payload);
            };

            canvasManager.OnNetworkStickerAction = payload =>
            {
                if (payload == null)
                    return;

                payload.Username = _network.CurrentUsername;
                RecordAction(ToDrawAction(payload), true);
                _network?.SendSticker(payload);
            };
        }

        private void SetupUdp()
        {
            try
            {
                if (_network?.PreferTcpRealtime == true)
                {
                    AppendLog("UDP bo qua trong che do LB relay; cursor/laser dung TCP.");
                    return;
                }

                string serverIp = string.IsNullOrWhiteSpace(_network?.ServerIp) ? "127.0.0.1" : _network.ServerIp;
                _udpManager = new UdpManager(serverIp, _network?.ServerUdpPort ?? 8889);
                _udpManager.Start();
                AppendLog($"UDP sẵn sàng trên port {_udpManager.LocalPort}, server {serverIp}:{_network?.ServerUdpPort ?? 8889}");
            }
            catch (Exception ex)
            {
                AppendLog($"UDP chưa sẵn sàng: {ex.Message}");
            }
        }



        private void SubscribeNetworkEvents()
        {
            NetworkEvents.OnCursorReceived += NetworkEvents_OnCursorReceived;
            NetworkEvents.OnRoomMembersReceived += NetworkEvents_OnRoomMembersReceived;
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
            NetworkEvents.OnTurnBasedReceived += NetworkEvents_OnTurnBasedReceived;
            NetworkEvents.OnSaveGalleryResponse += NetworkEvents_OnSaveGalleryResponse;
            NetworkEvents.OnAiTextToImageResult += NetworkEvents_OnAiTextToImageResult;
            NetworkEvents.OnAiBgRemovedResult += NetworkEvents_OnAiBgRemovedResult;
        }

        private void MainForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            NetworkEvents.OnCursorReceived -= NetworkEvents_OnCursorReceived;
            NetworkEvents.OnRoomMembersReceived -= NetworkEvents_OnRoomMembersReceived;
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
            NetworkEvents.OnTurnBasedReceived -= NetworkEvents_OnTurnBasedReceived;
            NetworkEvents.OnSaveGalleryResponse -= NetworkEvents_OnSaveGalleryResponse;
            NetworkEvents.OnAiTextToImageResult -= NetworkEvents_OnAiTextToImageResult;
            NetworkEvents.OnAiBgRemovedResult -= NetworkEvents_OnAiBgRemovedResult;
            StopRealtimePointerTimer();
            _udpManager?.Stop();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                StopRealtimePointerTimer();
                _udpManager?.Stop();
            }

            base.Dispose(disposing);
        }

        private void StopRealtimePointerTimer()
        {
            var timer = realtimePointerTimer;
            realtimePointerTimer = null;
            if (timer == null)
                return;

            try { timer.Change(Timeout.Infinite, Timeout.Infinite); } catch { }
            timer.Dispose();
        }

        private void InitializeUI()
        {
            this.Text = string.IsNullOrWhiteSpace(_roomCode) ? "Draw Together" : $"Draw Together - Room {_roomCode}";
            this.Size = new Size(1360, 840);
            this.MinimumSize = new Size(1024, 700);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = new Font("Segoe UI", 9F);

            toolPanel = new Panel
            {
                Dock = DockStyle.Left,
                Width = 270,
                BackColor = Color.FromArgb(245, 246, 248),
                AutoScroll = true,
                BorderStyle = BorderStyle.FixedSingle,
                Padding = new Padding(10)
            };
            userPanel = new Panel
            {
                Dock = DockStyle.Right,
                Width = 340,
                BackColor = Color.FromArgb(245, 246, 248),
                BorderStyle = BorderStyle.FixedSingle,
                Padding = new Padding(6)
            };
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

            canvas.TabStop = true;
            canvas.MouseEnter += (s, e) => canvas.Focus();
            canvas.MouseWheel += Canvas_MouseWheel;

            canvas.MouseMove += Canvas_MouseMove_SendCursor;
            canvas.MouseDown += Canvas_MouseDown_Custom;
            canvas.MouseMove += Canvas_MouseMove_Custom;
            canvas.MouseUp += Canvas_MouseUp_Custom;
            canvas.Paint += Canvas_Paint_Custom;

            realtimePointerTimer = new System.Threading.Timer(_ => FlushRealtimePointerState(), null, 0, RealtimePointerFlushIntervalMs);
            cursorLayer = new CursorLayer(canvas);
            this.Shown += (s, e) => canvasManager?.FitToViewport();
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
                    var payload = new SetBackgroundPayload
                    {
                    ActionID = Guid.NewGuid().ToString(),
                    RoomCode = _roomCode,
                    Username = _network.CurrentUsername,
                    ColorARGB = colorDialog.Color.ToArgb(),
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                };
                    RecordAction(ToDrawAction(payload), true);
                    _network?.Send(CommandType.SET_BACKGROUND, payload);
                    if (_network?.PreferTcpRealtime != true)
                        _udpManager?.SendSetBackground(payload);
                    colorToolTip.SetToolTip(btnBackColor, $"Mã màu: #{colorDialog.Color.R:X2}{colorDialog.Color.G:X2}{colorDialog.Color.B:X2}");
                }
            };

            btnBackImage = new Button { Text = "Anh nen", Location = new Point(10, 145), Size = new Size(200, 30) };
            btnBackImage.Click += BtnBackImage_Click;

            btnClearAll = new Button { Text = "Xóa toàn bộ", Location = new Point(10, 145), Size = new Size(200, 30) };
            btnClearAll.Click += (s, e) =>
            {
                if (!EnsureCanDraw()) return;
                canvasManager.ClearAll();
                actionHistory.Clear();
                undoneActionIds.Clear();
                ownRedoActionIds.Clear();
                _network?.SendEmpty(CommandType.CLEAR_ALL);
            };

            Button btnUndo = new Button { Text = "Hoàn tác (Ctrl+Z)", Location = new Point(10, 180), Size = new Size(200, 30) };
            btnUndo.Click += (s, e) =>
            {
                if (!EnsureCanDraw()) return;
                UndoOwnLastAction();
            };

            Button btnRedo = new Button { Text = "Làm lại (Ctrl+Y)", Location = new Point(10, 215), Size = new Size(200, 30) };
            btnRedo.Click += (s, e) =>
            {
                if (!EnsureCanDraw()) return;
                RedoOwnLastAction();
            };

            Button btnImport = new Button { Text = "Nhập ảnh", Location = new Point(10, 250), Size = new Size(200, 30) };
            btnImport.Click += BtnImport_Click;

            Button btnExport = new Button { Text = "Xuất ảnh", Location = new Point(10, 285), Size = new Size(200, 30) };
            btnExport.Click += BtnExport_Click;

            Button btnGallery = new Button { Text = "Gallery", Location = new Point(10, 355), Size = new Size(200, 30) };
            btnGallery.Click += (s, e) => new GalleryForm(_network).Show(this);

            Button btnSaveGallery = new Button { Text = "Lưu vào Gallery", Location = new Point(10, 390), Size = new Size(200, 30) };
            btnSaveGallery.Click += (s, e) => SaveCurrentCanvasToGallery();

            Button btnAiTextToDrawing = new Button { Text = "AI: Text-to-Drawing", Location = new Point(10, 425), Size = new Size(200, 30), BackColor = Color.Honeydew };
            btnAiTextToDrawing.Click += BtnAiTextToDrawing_Click;

            Button btnAiRemoveBg = new Button { Text = "AI: Xóa nền ảnh", Location = new Point(10, 460), Size = new Size(200, 30), BackColor = Color.Honeydew };
            btnAiRemoveBg.Click += BtnAiRemoveBackground_Click;

            Button btnZoomIn = new Button { Text = "Zoom +", Location = new Point(10, 535), Size = new Size(95, 30) };
            btnZoomIn.Click += (s, e) => AdjustCanvasZoom(ZoomStep);
            Button btnZoomOut = new Button { Text = "Zoom -", Location = new Point(115, 535), Size = new Size(95, 30) };
            btnZoomOut.Click += (s, e) => AdjustCanvasZoom(-ZoomStep);

            ComboBox cbTools = new ComboBox { Location = new Point(10, 570), Size = new Size(200, 30), DropDownStyle = ComboBoxStyle.DropDownList };
            cbTools.Items.AddRange(Enum.GetNames(typeof(ToolType)));
            cbTools.SelectedItem = ToolType.Pen.ToString();
            cbTools.SelectedIndexChanged += (s, e) => canvasManager.CurrentTool = (ToolType)cbTools.SelectedIndex;

            Button btnStickerMode = new Button { Text = "Đặt sticker", Location = new Point(10, 605), Size = new Size(200, 30) };
            btnStickerMode.Click += (s, e) =>
            {
                isPlacingSticker = !isPlacingSticker;
                isStickyNoteMode = false;
                ToastForm.ShowToast(this, isPlacingSticker ? "Kéo thả trên canvas để đặt kích cỡ sticker" : "Tắt đặt sticker");
            };

            stickerPicker = new StickerPickerControl { Location = new Point(10, 640), Size = new Size(200, 95) };
            stickerPicker.StickerSelected += id =>
            {
                selectedStickerId = id;
                isPlacingSticker = true;
                isStickyNoteMode = false;
            };

            Button btnStickyNote = new Button { Text = "Thêm ghi chú", Location = new Point(10, 740), Size = new Size(200, 30) };
            btnStickyNote.Click += (s, e) =>
            {
                isStickyNoteMode = !isStickyNoteMode;
                isPlacingSticker = false;
                ToastForm.ShowToast(this, isStickyNoteMode ? "Click canvas để tạo ghi chú" : "Tắt tạo ghi chú");
            };

            var txtFollowTarget = new TextBox { Location = new Point(10, 775), Size = new Size(130, 30), Text = "username" };
            var btnFollow = new Button { Text = "Follow", Location = new Point(145, 775), Size = new Size(65, 30) };
            btnFollow.Click += (s, e) =>
            {
                isFollowing = !isFollowing;
                _network?.SendFollowMode(txtFollowTarget.Text.Trim(), isFollowing);
                lblFollowState.Text = isFollowing ? $"Đang follow: {txtFollowTarget.Text.Trim()}" : "Follow: OFF";
            };

            btnTurnMode = new Button { Text = "Bật/Tắt vẽ theo lượt", Location = new Point(10, 810), Size = new Size(200, 30) };
            btnTurnMode.Visible = _isRoomOwner;
            btnTurnMode.Click += (s, e) =>
            {
                if (!_isRoomOwner)
                {
                    ToastForm.ShowToast(this, "Chỉ chủ phòng mới được bật/tắt vẽ theo lượt");
                    return;
                }

                bool enable = !turnPanel.IsEnabled;
                var payload = new TurnBasedPayload
                {
                    RoomCode = _roomCode,
                    Username = _network.CurrentUsername,
                    IsEnabled = enable,
                    ActiveUser = enable ? _network.CurrentUsername : string.Empty
                };
                ApplyTurnBasedState(payload);
                _network?.Send(CommandType.SET_TURNBASED, payload);
                _udpManager?.SendTurnBased(payload);
            };

            lblFollowState = new Label { Location = new Point(10, 895), Size = new Size(220, 26), Text = "Follow: OFF" };

            Button btnLeaveRoom = new Button { Text = "Rời phòng", Location = new Point(10, 930), Size = new Size(200, 30), BackColor = Color.LightCoral };
            btnLeaveRoom.Click += (s, e) =>
            {
                _network?.SendLeaveRoom();
                var lobby = new LobbyForm(_network, _network.CurrentUsername);
                lobby.FormClosed += (fs, fe) => this.Show();
                lobby.Show();
                this.Hide();
            };

            Button btnToggleChat = new Button { Text = "Ẩn/Hiện khung phải", Location = new Point(10, 965), Size = new Size(200, 30), BackColor = Color.LightBlue };
            btnToggleChat.Click += (s, e) =>
            {
                userPanel.Visible = !userPanel.Visible;
                canvasManager?.FitToViewport();
            };

            Label lblDrawingHeader = CreateToolHeader("Vẽ");
            Label lblHistoryHeader = CreateToolHeader("Lịch sử");
            Label lblFileHeader = CreateToolHeader("Tệp và thư viện");
            Label lblAiHeader = CreateToolHeader("AI");
            Label lblCollabHeader = CreateToolHeader("Cộng tác");
            int y = 12;

            y = PlaceToolHeader(lblDrawingHeader, y);
            y = PlaceToolControl(cbTools, y);
            y = PlaceToolControl(btnColorPicker, y);
            y = PlaceToolControl(btnBackColor, y);
            y = PlaceToolControl(btnBackImage, y);
            y = PlaceToolPair(tbPenWidth, lblPenWidth, y, 146, 74, 45);
            y = PlaceToolPair(btnZoomIn, btnZoomOut, y, 108, 108);
            y = PlaceToolControl(btnClearAll, y);

            y = PlaceToolHeader(lblHistoryHeader, y + 4);
            y = PlaceToolPair(btnUndo, btnRedo, y, 108, 108);

            y = PlaceToolHeader(lblFileHeader, y + 4);
            y = PlaceToolControl(btnImport, y);
            y = PlaceToolControl(btnExport, y);
            y = PlaceToolPair(btnGallery, btnSaveGallery, y, 108, 108);

            y = PlaceToolHeader(lblAiHeader, y + 4);
            y = PlaceToolControl(btnAiTextToDrawing, y);
            y = PlaceToolControl(btnAiRemoveBg, y);
            y = PlaceToolHeader(lblCollabHeader, y + 4);
            y = PlaceToolControl(btnStickerMode, y);
            y = PlaceToolControl(stickerPicker, y, 94);
            y = PlaceToolControl(btnStickyNote, y);
            y = PlaceToolPair(txtFollowTarget, btnFollow, y, 146, 74);
            y = PlaceToolControl(lblFollowState, y, 24);
            y = PlaceToolControl(btnTurnMode, y);
            y = PlaceToolControl(btnLeaveRoom, y);
            y = PlaceToolControl(btnToggleChat, y);

            toolPanel.Controls.AddRange(new Control[]
            {
                lblDrawingHeader, lblHistoryHeader, lblFileHeader, lblAiHeader, lblCollabHeader,
                btnColorPicker, tbPenWidth, btnBackColor, btnBackImage, btnClearAll,
                btnUndo, btnRedo, btnImport, btnExport, btnGallery, btnSaveGallery,
                btnAiTextToDrawing, btnAiRemoveBg, lblPenWidth, btnZoomIn, btnZoomOut, cbTools, btnStickerMode, stickerPicker,
                btnStickyNote, txtFollowTarget, btnFollow, btnTurnMode,
                lblFollowState, btnLeaveRoom, btnToggleChat
            });
            NormalizeToolPanelControls();
        }

        private Label CreateToolHeader(string text)
        {
            return new Label
            {
                Text = text,
                Height = 24,
                ForeColor = Color.FromArgb(45, 52, 64),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            };
        }

        private int PlaceToolHeader(Control control, int y)
        {
            control.Location = new Point(12, y);
            control.Size = new Size(226, 24);
            return y + 28;
        }

        private int PlaceToolControl(Control control, int y, int height = 32)
        {
            control.Location = new Point(12, y);
            control.Size = new Size(226, height);
            return y + height + 7;
        }

        private int PlaceToolPair(Control left, Control right, int y, int leftWidth, int rightWidth, int height = 32)
        {
            left.Location = new Point(12, y);
            left.Size = new Size(leftWidth, height);
            right.Location = new Point(12 + leftWidth + 6, y);
            right.Size = new Size(rightWidth, height);
            return y + height + 7;
        }

        private void NormalizeToolPanelControls()
        {
            foreach (Control control in toolPanel.Controls)
            {
                control.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                control.Margin = new Padding(0, 0, 0, 6);
            }

            foreach (Control control in toolPanel.Controls)
            {
                var button = control as Button;
                if (button == null)
                    continue;

                button.FlatStyle = FlatStyle.System;
                button.TextAlign = ContentAlignment.MiddleCenter;
            }

            toolPanel.AutoScrollMargin = new Size(0, 16);
        }

        private void AdjustCanvasZoom(float delta, Point? pivot = null)
        {
            if (canvasManager == null)
                return;

            Point zoomPivot = pivot ?? new Point(canvas.ClientSize.Width / 2, canvas.ClientSize.Height / 2);
            canvasManager.ZoomAt(zoomPivot, delta);
        }

        private void BuildUserPanel()
        {
            turnPanel = new TurnPanelControl { Dock = DockStyle.Top };
            turnPanel.NextTurnRequested += HandleNextTurnRequested;
            playbackPanel = new PlaybackPanelControl { Dock = DockStyle.Top };

            // FIX LỖI: Dùng hàm Send tổng quát
            playbackPanel.RequestPlayback += () => _network?.Send(CommandType.REQUEST_PLAYBACK, new PlaybackRequestPayload { RoomCode = _roomCode });

            TabControl tabs = new TabControl { Dock = DockStyle.Fill };
            TabPage tabMembers = new TabPage("Members");
            TabPage tabChat = new TabPage("Chat");
            TabPage tabLogs = new TabPage("Nhật ký");

            lstMembers = new ListBox { Dock = DockStyle.Fill };
            tabMembers.Controls.Add(lstMembers);

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

            tabs.TabPages.Add(tabMembers);
            tabs.TabPages.Add(tabChat);
            tabs.TabPages.Add(tabLogs);

            userPanel.Controls.Add(tabs);
            userPanel.Controls.Add(playbackPanel);
            userPanel.Controls.Add(turnPanel);
        }

        private void Canvas_MouseMove_SendCursor(object sender, MouseEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_network?.CurrentUsername))
                return;

            Point canvasPoint = canvasManager?.ScreenToCanvas(e.Location) ?? e.Location;
            QueueRealtimeCursor(new CursorPayload
            {
                Username = _network.CurrentUsername,
                X = canvasPoint.X,
                Y = canvasPoint.Y
            });

            if ((ModifierKeys & Keys.Alt) == Keys.Alt)
            {
                QueueRealtimeLaser(new LaserPayload
                {
                    Username = _network.CurrentUsername,
                    X = canvasPoint.X,
                    Y = canvasPoint.Y,
                    IsActive = true
                });
            }
        }

        private void QueueRealtimeCursor(CursorPayload payload)
        {
            if (payload == null)
                return;

            lock (realtimePointerLock)
            {
                pendingCursorPayload = payload;
                hasPendingCursor = true;
            }
        }

        private void QueueRealtimeLaser(LaserPayload payload)
        {
            if (payload == null)
                return;

            lock (realtimePointerLock)
            {
                pendingLaserPayload = payload;
                hasPendingLaser = true;
            }
        }

        private void FlushRealtimePointerState()
        {
            if (Interlocked.Exchange(ref isFlushingRealtimePointers, 1) == 1)
                return;

            try
            {
                CursorPayload cursor = null;
                LaserPayload laser = null;

                lock (realtimePointerLock)
                {
                    if (hasPendingCursor)
                    {
                        cursor = pendingCursorPayload;
                        hasPendingCursor = false;
                    }

                    if (hasPendingLaser)
                    {
                        laser = pendingLaserPayload;
                        hasPendingLaser = false;
                    }
                }

                if (cursor != null)
                    SendCursorRealtime(cursor);

                if (laser != null)
                    SendLaserRealtime(laser);
            }
            finally
            {
                Interlocked.Exchange(ref isFlushingRealtimePointers, 0);
            }
        }

        private void SendCursorRealtime(CursorPayload payload)
        {
            if (payload == null)
                return;
            if (string.IsNullOrWhiteSpace(payload.Username))
                payload.Username = _network?.CurrentUsername;
            if (string.IsNullOrWhiteSpace(payload.Username))
                return;

            if (_udpManager != null && _network?.PreferTcpRealtime != true)
                _udpManager.SendCursor(payload);
            else
                _network?.SendCursorRealtime(payload);
        }

        private void SendLaserRealtime(LaserPayload payload)
        {
            if (payload == null)
                return;
            if (string.IsNullOrWhiteSpace(payload.Username))
                payload.Username = _network?.CurrentUsername;
            if (string.IsNullOrWhiteSpace(payload.Username))
                return;

            if (_udpManager != null && _network?.PreferTcpRealtime != true)
                _udpManager.SendLaser(payload);
            else
                _network?.SendLaserRealtime(payload);
        }

        private void Canvas_MouseDown_Custom(object sender, MouseEventArgs e)
        {
            canvas.Focus();
            SelectStickyNote(null);

            if (e.Button != MouseButtons.Left) return;

            if (isPlacingSticker || pendingImportImage != null)
            {
                if (!EnsureCanDraw()) return;
                dragStartPoint = e.Location;
            }
            else if (isStickyNoteMode)
            {
                string noteId = Guid.NewGuid().ToString();
                var note = new StickyNoteControl { NoteId = noteId, Author = _network?.CurrentUsername, Location = e.Location };
                note.NoteChanged += StickyNoteChanged;
                note.NoteSelected += StickyNoteSelected;
                canvas.Controls.Add(note);
                note.BringToFront();
                noteControls[noteId] = note;
                SelectStickyNote(note);

                _network?.SendStickyNote(new StickyNotePayload
                {
                    NoteID = noteId,
                    AuthorUsername = _network?.CurrentUsername,
                    X = e.X,
                    Y = e.Y,
                    Width = note.Width,
                    Height = note.Height,
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
                if (!EnsureCanDraw()) return;

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
                if (_network?.PreferTcpRealtime != true)
                    _udpManager?.SendSticker(payload);

                isPlacingSticker = false;
                canvas.Invalidate();
            }
            else if (e.Button == MouseButtons.Left && pendingImportImage != null)
            {
                if (!EnsureCanDraw()) return;

                Point start = canvasManager.ScreenToCanvas(dragStartPoint);
                Point end = canvasManager.ScreenToCanvas(e.Location);
                int width = Math.Max(50, Math.Abs(end.X - start.X));
                int height = Math.Max(50, Math.Abs(end.Y - start.Y));
                int x = Math.Min(start.X, end.X);
                int y = Math.Min(start.Y, end.Y);
                Rectangle target = new Rectangle(x, y, width, height);

                ImportImageAndBroadcast(pendingImportImage, target, pendingImportAiType, pendingImportPrompt);

                pendingImportImage.Dispose();
                pendingImportImage = null;
                pendingImportAiType = null;
                pendingImportPrompt = null;
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
                Width = note.Width,
                Height = note.Height,
                Text = note.NoteText,
                IsOpen = true,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            });
        }

        private void StickyNoteSelected(StickyNoteControl note)
        {
            SelectStickyNote(note);
        }

        private void SelectStickyNote(StickyNoteControl note)
        {
            selectedStickyNoteId = note?.NoteId;
            foreach (var pair in noteControls)
            {
                bool isSelected = note != null && string.Equals(pair.Key, selectedStickyNoteId, StringComparison.OrdinalIgnoreCase);
                pair.Value.SetSelected(isSelected);
            }
        }

        private bool DeleteSelectedStickyNote()
        {
            if (string.IsNullOrWhiteSpace(selectedStickyNoteId))
                return false;

            if (!noteControls.TryGetValue(selectedStickyNoteId, out var note))
            {
                selectedStickyNoteId = null;
                return false;
            }

            string noteId = selectedStickyNoteId;
            selectedStickyNoteId = null;
            noteControls.Remove(noteId);
            canvas.Controls.Remove(note);
            note.Dispose();

            _network?.SendStickyNote(new StickyNotePayload
            {
                NoteID = noteId,
                AuthorUsername = _network.CurrentUsername,
                IsOpen = false,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            });

            return true;
        }

        private Image pendingImportImage;
        private string pendingImportAiType;
        private string pendingImportPrompt;
        private Point dragStartPoint;

        private void BtnBackImage_Click(object sender, EventArgs e)
        {
            if (!EnsureCanDraw()) return;

            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "Image files|*.png;*.jpg;*.jpeg;*.bmp";
                if (openFileDialog.ShowDialog() != DialogResult.OK)
                    return;

                using (Image image = Image.FromFile(openFileDialog.FileName))
                {
                    string imageData = EncodeBackgroundImageForNetwork(image);
                    canvasManager.ChangeBackgroundImage(image);

                    var payload = new SetBackgroundPayload
                    {
                        ActionID = Guid.NewGuid().ToString(),
                        RoomCode = _roomCode,
                        Username = _network.CurrentUsername,
                        ColorARGB = canvasManager.BackgroundColor.ToArgb(),
                        ImageData = imageData,
                        Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                    };

                    RecordAction(ToDrawAction(payload), true);
                    _network?.Send(CommandType.SET_BACKGROUND, payload);
                    ToastForm.ShowToast(this, "Da dat anh nen canvas");
                }
            }
        }

        private void BtnImport_Click(object sender, EventArgs e)
        {
            if (!EnsureCanDraw()) return;

            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "Image files|*.png;*.jpg;*.jpeg;*.bmp";
                if (openFileDialog.ShowDialog() != DialogResult.OK)
                    return;

                if (pendingImportImage != null) pendingImportImage.Dispose();
                pendingImportImage = Image.FromFile(openFileDialog.FileName);
                pendingImportAiType = null;
                pendingImportPrompt = null;
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

        private async void BtnAiTextToDrawing_Click(object sender, EventArgs e)
        {
            if (!EnsureCanDraw()) return;
            if (!ApiConfig.IsHuggingFaceConfigured())
            {
                MessageBox.Show("Chua cau hinh HF_TOKEN trong .env.", "Thieu API key");
                return;
            }

            string prompt = ShowPromptDialog("Text-to-Drawing", "Nhập prompt để AI tạo ảnh:");
            if (string.IsNullOrWhiteSpace(prompt))
                return;

            await RunButtonTaskAsync(sender as Button, "AI đang tạo ảnh...", async () =>
            {
                byte[] imageBytes = await StabilityAiClient.GenerateImageAsync(prompt.Trim());
                if (imageBytes == null || imageBytes.Length == 0)
                    throw new InvalidOperationException("Hugging Face khong tra ve anh.");

                using (Image aiImage = CreateImageFromBytes(imageBytes))
                {
                    Rectangle target = BuildCenteredImageTarget(aiImage.Size);
                    ImportImageAndBroadcast(aiImage, target, "text_to_image", prompt.Trim());
                }

                ToastForm.ShowToast(this, "Đã thêm ảnh AI vào canvas");
            });
        }

        private async void BtnAiRemoveBackground_Click(object sender, EventArgs e)
        {
            if (!EnsureCanDraw()) return;
            if (!ApiConfig.IsRemoveBgConfigured())
            {
                MessageBox.Show("Chua cau hinh REMOVE_BG_API_KEY trong .env.", "Thieu API key");
                return;
            }

            if (canvasManager.TryGetSelectedImagePayload(out var selectedImage))
            {
                await RemoveBackgroundFromCanvasImageAsync(sender as Button, selectedImage);
                return;
            }

            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "Image files|*.png;*.jpg;*.jpeg;*.bmp";
                if (openFileDialog.ShowDialog() != DialogResult.OK)
                    return;

                byte[] inputBytes = File.ReadAllBytes(openFileDialog.FileName);
                await RunButtonTaskAsync(sender as Button, "AI đang xóa nền...", async () =>
                {
                    byte[] resultBytes = await RemoveBgClient.RemoveBackgroundAsync(inputBytes);
                    if (resultBytes == null || resultBytes.Length == 0)
                        throw new InvalidOperationException("remove.bg không trả về ảnh.");

                    using (Image resultImage = CreateImageFromBytes(resultBytes))
                    {
                        Rectangle target = BuildCenteredImageTarget(resultImage.Size);
                        ImportImageAndBroadcast(resultImage, target, "bg_removed", "");
                    }

                    ToastForm.ShowToast(this, "Đã thêm ảnh đã xóa nền vào canvas");
                });
            }
        }

        private async Task RemoveBackgroundFromCanvasImageAsync(Button button, ImportImagePayload selectedImage)
        {
            if (selectedImage == null || string.IsNullOrWhiteSpace(selectedImage.ImageData))
                return;

            byte[] inputBytes = Convert.FromBase64String(selectedImage.ImageData);
            await RunButtonTaskAsync(button, "AI đang xóa nền...", async () =>
            {
                byte[] resultBytes = await RemoveBgClient.RemoveBackgroundAsync(inputBytes);
                if (resultBytes == null || resultBytes.Length == 0)
                    throw new InvalidOperationException("remove.bg không trả về ảnh.");

                using (Image resultImage = CreateImageFromBytes(resultBytes))
                {
                    var target = new Rectangle(selectedImage.X, selectedImage.Y, selectedImage.Width, selectedImage.Height);
                    ImportImageAndBroadcast(resultImage, target, "bg_removed", "", selectedImage.ActionID);
                }

                ToastForm.ShowToast(this, "Đã xóa nền ảnh đang chọn");
            });
        }

        private async Task RunButtonTaskAsync(Button button, string busyText, Func<Task> action)
        {
            string oldText = button?.Text;
            try
            {
                if (button != null)
                {
                    button.Enabled = false;
                    button.Text = busyText;
                }
                Cursor = Cursors.WaitCursor;
                await action();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không thể xử lý AI: " + ex.Message, "AI");
            }
            finally
            {
                Cursor = Cursors.Default;
                if (button != null)
                {
                    button.Text = oldText;
                    button.Enabled = true;
                }
            }
        }

        private string ShowPromptDialog(string title, string labelText)
        {
            using (Form dialog = new Form())
            using (Label label = new Label())
            using (TextBox textBox = new TextBox())
            using (Button okButton = new Button())
            using (Button cancelButton = new Button())
            {
                dialog.Text = title;
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                dialog.MinimizeBox = false;
                dialog.MaximizeBox = false;
                dialog.ClientSize = new Size(420, 145);

                label.Text = labelText;
                label.Location = new Point(12, 12);
                label.Size = new Size(396, 22);

                textBox.Location = new Point(12, 40);
                textBox.Size = new Size(396, 24);
                textBox.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;

                okButton.Text = "Tạo ảnh";
                okButton.DialogResult = DialogResult.OK;
                okButton.Location = new Point(232, 94);
                okButton.Size = new Size(84, 30);

                cancelButton.Text = "Hủy";
                cancelButton.DialogResult = DialogResult.Cancel;
                cancelButton.Location = new Point(324, 94);
                cancelButton.Size = new Size(84, 30);

                dialog.Controls.AddRange(new Control[] { label, textBox, okButton, cancelButton });
                dialog.AcceptButton = okButton;
                dialog.CancelButton = cancelButton;

                return dialog.ShowDialog(this) == DialogResult.OK ? textBox.Text.Trim() : null;
            }
        }

        private Image CreateImageFromBytes(byte[] bytes)
        {
            using (MemoryStream ms = new MemoryStream(bytes))
            using (Image image = Image.FromStream(ms))
            {
                return new Bitmap(image);
            }
        }

        private Rectangle BuildCenteredImageTarget(Size imageSize)
        {
            Size canvasSize = canvasManager.CanvasSize;
            if (canvasSize.Width <= 0 || canvasSize.Height <= 0)
                return new Rectangle(0, 0, Math.Max(50, imageSize.Width), Math.Max(50, imageSize.Height));

            float scale = Math.Min(1f, Math.Min(canvasSize.Width / (float)Math.Max(1, imageSize.Width), canvasSize.Height / (float)Math.Max(1, imageSize.Height)));
            int width = Math.Max(50, (int)(imageSize.Width * scale));
            int height = Math.Max(50, (int)(imageSize.Height * scale));
            int x = Math.Max(0, (canvasSize.Width - width) / 2);
            int y = Math.Max(0, (canvasSize.Height - height) / 2);
            return new Rectangle(x, y, width, height);
        }

        private void ImportImageAndBroadcast(Image image, Rectangle target, string aiType = null, string prompt = null, string actionIdOverride = null)
        {
            string actionId = string.IsNullOrWhiteSpace(actionIdOverride) ? Guid.NewGuid().ToString() : actionIdOverride;
            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            canvasManager.ImportImage(image, target, actionId, _network?.CurrentUsername, timestamp);

            using (MemoryStream ms = new MemoryStream())
            {
                image.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                string imageData = Convert.ToBase64String(ms.ToArray());
                RecordAction(ToDrawAction(new ImportImagePayload
                {
                    ActionID = actionId,
                    Username = _network?.CurrentUsername,
                    X = target.X,
                    Y = target.Y,
                    Width = target.Width,
                    Height = target.Height,
                    ImageData = imageData,
                    Timestamp = timestamp
                }), true);

                if (string.Equals(aiType, "text_to_image", StringComparison.OrdinalIgnoreCase))
                {
                    _network?.Send(CommandType.AI_TEXT_TO_IMAGE, new AiTextToImageResultPayload
                    {
                        ActionID = actionId,
                        RequesterUsername = _network?.CurrentUsername,
                        Prompt = prompt ?? "",
                        X = target.X,
                        Y = target.Y,
                        Width = target.Width,
                        Height = target.Height,
                        ImageData = imageData,
                        Timestamp = timestamp
                    });
                }
                else if (string.Equals(aiType, "bg_removed", StringComparison.OrdinalIgnoreCase))
                {
                    _network?.Send(CommandType.AI_BG_REMOVED, new AiBgRemovedPayload
                    {
                        ActionID = actionId,
                        RequesterUsername = _network?.CurrentUsername,
                        X = target.X,
                        Y = target.Y,
                        Width = target.Width,
                        Height = target.Height,
                        ImageData = imageData,
                        Timestamp = timestamp
                    });
                }
                else
                {
                    _network?.Send(CommandType.IMPORT_IMAGE, new ImportImagePayload
                    {
                        ActionID = actionId,
                        Username = _network?.CurrentUsername,
                        X = target.X,
                        Y = target.Y,
                        Width = target.Width,
                        Height = target.Height,
                        ImageData = imageData,
                        Timestamp = timestamp
                    });
                }
            }

            canvas.Invalidate();
        }

        private string EncodeBackgroundImageForNetwork(Image image)
        {
            Size canvasSize = canvasManager.CanvasSize;
            int width = canvasSize.Width > 0 ? canvasSize.Width : 1920;
            int height = canvasSize.Height > 0 ? canvasSize.Height : 1080;

            using (Bitmap bitmap = new Bitmap(width, height))
            using (Graphics g = Graphics.FromImage(bitmap))
            using (MemoryStream ms = new MemoryStream())
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                g.Clear(canvasManager.BackgroundColor);
                g.DrawImage(image, new Rectangle(0, 0, width, height));

                var jpegCodec = GetJpegCodec();
                if (jpegCodec != null)
                {
                    using (var encoderParameters = new System.Drawing.Imaging.EncoderParameters(1))
                    {
                        encoderParameters.Param[0] = new System.Drawing.Imaging.EncoderParameter(
                            System.Drawing.Imaging.Encoder.Quality,
                            85L);
                        bitmap.Save(ms, jpegCodec, encoderParameters);
                    }
                }
                else
                {
                    bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Jpeg);
                }

                return Convert.ToBase64String(ms.ToArray());
            }
        }

        private static System.Drawing.Imaging.ImageCodecInfo GetJpegCodec()
        {
            foreach (var codec in System.Drawing.Imaging.ImageCodecInfo.GetImageEncoders())
            {
                if (string.Equals(codec.MimeType, "image/jpeg", StringComparison.OrdinalIgnoreCase))
                    return codec;
            }

            return null;
        }

        private void SendChatMessage()
        {
            string message = txtChatInput.Text?.Trim();
            if (string.IsNullOrWhiteSpace(message)) return;

            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var payload = new ChatPayload
            {
                Username = _network.CurrentUsername,
                Message = message,
                Timestamp = timestamp
            };
            RememberLocalChat(payload);
            AppendChatMessage(payload);
            _network?.Send(CommandType.CHAT, payload);
            _udpManager?.SendChat(payload);
            txtChatInput.Clear();
        }

        private void SaveCurrentCanvasToGallery()
        {
            try
            {
                string filename = $"canvas_{DateTime.Now:yyyyMMdd_HHmmss}.png";
                string imageData = canvasManager.ExportPngBase64();
                string thumbnailData = canvasManager.ExportPngBase64(240, 160);
                _network?.SendSaveGallery(filename, imageData, thumbnailData);
                ToastForm.ShowToast(this, "Đang lưu vào Gallery...");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không thể lưu Gallery: " + ex.Message);
            }
        }

        private bool EnsureCanDraw()
        {
            if (canvasManager == null || canvasManager.IsDrawingEnabled)
                return true;

            ToastForm.ShowToast(this, "Chưa tới lượt vẽ của bạn");
            return false;
        }

        private string BuildChatKey(ChatPayload payload)
        {
            if (payload == null) return string.Empty;
            return $"{payload.Username}|{payload.Timestamp}|{payload.Message}";
        }

        private void RememberLocalChat(ChatPayload payload)
        {
            string key = BuildChatKey(payload);
            if (string.IsNullOrEmpty(key)) return;
            locallyShownChat.Add(key);
            locallyShownChatOrder.Enqueue(key);
            while (locallyShownChatOrder.Count > 50)
                locallyShownChat.Remove(locallyShownChatOrder.Dequeue());
        }

        private void AppendChatMessage(ChatPayload payload)
        {
            if (payload == null || lstChat == null)
                return;

            string key = BuildChatKey(payload);
            if (!string.IsNullOrEmpty(key) && !displayedChat.Add(key))
                return;
            if (!string.IsNullOrEmpty(key))
            {
                displayedChatOrder.Enqueue(key);
                while (displayedChatOrder.Count > 120)
                    displayedChat.Remove(displayedChatOrder.Dequeue());
            }

            long ts = payload.Timestamp > 0 ? payload.Timestamp : DateTimeOffset.Now.ToUnixTimeMilliseconds();
            DateTimeOffset time = DateTimeOffset.FromUnixTimeMilliseconds(ts).ToLocalTime();
            lstChat.Items.Add($"[{time:HH:mm}] {payload.Username}: {payload.Message}");
            lstChat.TopIndex = lstChat.Items.Count - 1;
        }

        private void AppendLog(string message)
        {
            if (lstLogs == null)
                return;

            lstLogs.Items.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
            lstLogs.TopIndex = lstLogs.Items.Count - 1;
        }

        private void RecordAction(DrawAction action, bool isOwnAction)
        {
            if (action == null || string.IsNullOrWhiteSpace(action.ActionID))
                return;

            actionHistory.Add(action);
            if (isOwnAction)
            {
                undoneActionIds.Remove(action.ActionID);
                ownRedoActionIds.Clear();
            }
        }

        private static DrawAction ToDrawAction(DrawPayload payload)
        {
            if (payload == null)
                return null;

            return new DrawAction
            {
                ActionID = payload.ActionID,
                Username = payload.Username,
                ToolType = payload.ToolType,
                X1 = payload.X1,
                Y1 = payload.Y1,
                X2 = payload.X2,
                Y2 = payload.Y2,
                ColorARGB = payload.ColorARGB,
                Thickness = payload.Thickness,
                Text = payload.Text,
                FontName = payload.FontName,
                FontSize = payload.FontSize,
                IsDeleted = payload.IsDeleted,
                Timestamp = payload.Timestamp
            };
        }

        private static DrawAction ToDrawAction(FloodFillPayload payload)
        {
            if (payload == null)
                return null;

            return new DrawAction
            {
                ActionID = payload.ActionID,
                Username = payload.Username,
                ToolType = "FloodFill",
                X1 = payload.X,
                Y1 = payload.Y,
                ColorARGB = payload.ColorARGB,
                Timestamp = payload.Timestamp
            };
        }

        private static DrawAction ToDrawAction(SetBackgroundPayload payload)
        {
            if (payload == null)
                return null;

            return new DrawAction
            {
                ActionID = payload.ActionID,
                Username = payload.Username,
                ToolType = "SetBackground",
                ColorARGB = payload.ColorARGB,
                ImageData = payload.ImageData,
                Timestamp = payload.Timestamp
            };
        }

        private static DrawAction ToDrawAction(ImportImagePayload payload)
        {
            if (payload == null)
                return null;

            return new DrawAction
            {
                ActionID = payload.ActionID,
                Username = payload.Username,
                ToolType = "ImportImage",
                X1 = payload.X,
                Y1 = payload.Y,
                ImageWidth = payload.Width,
                ImageHeight = payload.Height,
                ImageData = payload.ImageData,
                IsDeleted = payload.IsDeleted,
                Timestamp = payload.Timestamp
            };
        }

        private static DrawAction ToDrawAction(StickerPayload payload)
        {
            if (payload == null)
                return null;

            return new DrawAction
            {
                ActionID = payload.ActionID,
                Username = payload.Username,
                ToolType = "Sticker",
                Text = payload.StickerID,
                X1 = payload.X,
                Y1 = payload.Y,
                ImageWidth = payload.Width,
                ImageHeight = payload.Height,
                IsDeleted = payload.IsDeleted,
                Timestamp = payload.Timestamp
            };
        }

        private List<DrawAction> GetVisibleActions()
        {
            var visible = new List<DrawAction>();
            foreach (var action in actionHistory)
            {
                if (action == null || string.IsNullOrWhiteSpace(action.ActionID))
                    continue;
                if (!undoneActionIds.Contains(action.ActionID))
                    visible.Add(action);
            }
            return visible;
        }

        private void RenderVisibleHistory()
        {
            canvasManager.RenderActionHistory(GetVisibleActions());
        }

        private string GetLastUndoableOwnActionId()
        {
            string currentUsername = _network?.CurrentUsername ?? "";
            var seenActionIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = actionHistory.Count - 1; i >= 0; i--)
            {
                var action = actionHistory[i];
                if (action == null || string.IsNullOrWhiteSpace(action.ActionID))
                    continue;
                if (!string.Equals(action.Username ?? "", currentUsername, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (seenActionIds.Contains(action.ActionID))
                    continue;
                seenActionIds.Add(action.ActionID);
                if (!undoneActionIds.Contains(action.ActionID))
                    return action.ActionID;
            }
            return null;
        }

        private void UndoOwnLastAction()
        {
            string actionId = GetLastUndoableOwnActionId();
            if (string.IsNullOrWhiteSpace(actionId))
                return;

            undoneActionIds.Add(actionId);
            ownRedoActionIds.Add(actionId);
            RenderVisibleHistory();
            _network?.SendUndo(actionId);
        }

        private void RedoOwnLastAction()
        {
            while (ownRedoActionIds.Count > 0)
            {
                int index = ownRedoActionIds.Count - 1;
                string actionId = ownRedoActionIds[index];
                ownRedoActionIds.RemoveAt(index);
                if (string.IsNullOrWhiteSpace(actionId) || !undoneActionIds.Contains(actionId))
                    continue;

                undoneActionIds.Remove(actionId);
                RenderVisibleHistory();
                _network?.SendRedo(actionId);
                return;
            }
        }

        private void ApplyUndoFromNetwork(UndoPayload payload)
        {
            if (payload == null || string.IsNullOrWhiteSpace(payload.ActionID))
                return;
            if (!ActionBelongsToUser(payload.ActionID, payload.Username))
                return;

            bool changed = undoneActionIds.Add(payload.ActionID);
            if (changed && string.Equals(payload.Username ?? "", _network?.CurrentUsername ?? "", StringComparison.OrdinalIgnoreCase))
                ownRedoActionIds.Add(payload.ActionID);

            if (changed)
                RenderVisibleHistory();
        }

        private void ApplyRedoFromNetwork(RedoPayload payload)
        {
            if (payload == null || string.IsNullOrWhiteSpace(payload.ActionID))
                return;
            if (!ActionBelongsToUser(payload.ActionID, payload.Username))
                return;

            if (undoneActionIds.Remove(payload.ActionID))
                RenderVisibleHistory();
        }

        private bool ActionBelongsToUser(string actionId, string username)
        {
            if (string.IsNullOrWhiteSpace(actionId) || string.IsNullOrWhiteSpace(username))
                return false;

            foreach (var action in actionHistory)
            {
                if (action == null)
                    continue;
                if (string.Equals(action.ActionID, actionId, StringComparison.OrdinalIgnoreCase))
                    return string.Equals(action.Username ?? "", username, StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }

        private bool IsCurrentUser(string username)
        {
            return !string.IsNullOrWhiteSpace(username) &&
                string.Equals(username, _network?.CurrentUsername ?? "", StringComparison.OrdinalIgnoreCase);
        }

        private void NetworkEvents_OnCursorReceived(CursorPayload payload)
        {
            if (payload == null)
                return;
            if (IsCurrentUser(payload.Username))
                return;
            UIInvoke(() =>
            {
                canvasManager.UpdateRemoteCursor(payload.Username, new Point(payload.X, payload.Y));
                cursorLayer?.UpdateCursor(payload);
            });
        }

        private void NetworkEvents_OnRoomMembersReceived(RoomMembersPayload payload)
        {
            if (payload?.Members == null)
                return;

            UIInvoke(() =>
            {
                if (lstMembers == null)
                    return;

                lstMembers.BeginUpdate();
                try
                {
                    lstMembers.Items.Clear();
                    foreach (var member in payload.Members)
                    {
                        string name = string.IsNullOrWhiteSpace(member.Username) ? "unknown" : member.Username;
                        string role = member.IsSpectator ? "observer" : "editor";
                        string state = member.IsOnline ? "online" : "offline";
                        string color = member.ColorARGB == 0 ? "n/a" : $"#{member.ColorARGB & 0x00FFFFFF:X6}";
                        lstMembers.Items.Add($"{name} | {role} | {state} | {color}");
                    }
                }
                finally
                {
                    lstMembers.EndUpdate();
                }
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

        private void NetworkEvents_OnDrawReceived(DrawPayload payload) => UIInvoke(() =>
        {
            RecordAction(ToDrawAction(payload), false);
            canvasManager.ApplyRemoteDraw(payload);
        });
        private void NetworkEvents_OnSyncBoardReceived(SyncBoardPayload payload)
        {
            if (payload?.Actions == null)
                return;
            if (payload.Actions.Count >= 0)
            {
                actionHistory.Clear();
                actionHistory.AddRange(payload.Actions);
                undoneActionIds.Clear();
                ownRedoActionIds.Clear();
                UIInvoke(() =>
                {
                    RenderVisibleHistory();
                    AppendLog($"Dong bo {payload.Actions.Count} hanh dong tu phong");
                });
                return;
            }
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
                            ColorARGB = action.ColorARGB,
                            ImageData = action.ImageData
                        });
                    }
                    else if (tool.Equals("Sticker", StringComparison.OrdinalIgnoreCase))
                    {
                        // ✅ FIX: Replay sticker khi reconnect
                        canvasManager.AddSticker(new SharedLib.Payloads.StickerPayload
                        {
                            ActionID = action.ActionID,
                            Username  = action.Username,
                            StickerID = action.Text,   // StickerID được map vào field Text khi lưu
                            X         = action.X1,
                            Y         = action.Y1,
                            Width     = action.ImageWidth  > 0 ? action.ImageWidth  : 64,
                            Height    = action.ImageHeight > 0 ? action.ImageHeight : 64,
                            IsDeleted = action.IsDeleted,
                            Timestamp = action.Timestamp,
                        });
                    }
                    else if (tool.Equals("ImportImage", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(action.ImageData))
                    {
                        canvasManager.ApplyRemoteImportImage(new SharedLib.Payloads.ImportImagePayload
                        {
                            ActionID = action.ActionID,
                            Username = action.Username,
                            X = action.X1,
                            Y = action.Y1,
                            Width = action.ImageWidth > 0 ? action.ImageWidth : 400,
                            Height = action.ImageHeight > 0 ? action.ImageHeight : 300,
                            ImageData = action.ImageData,
                            IsDeleted = action.IsDeleted,
                            Timestamp = action.Timestamp
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
        private void NetworkEvents_OnFloodFillReceived(FloodFillPayload payload) => UIInvoke(() =>
        {
            RecordAction(ToDrawAction(payload), false);
            canvasManager.ApplyRemoteFloodFill(payload);
        });
        private void NetworkEvents_OnImportImageReceived(ImportImagePayload payload) => UIInvoke(() =>
        {
            RecordAction(ToDrawAction(payload), false);
            canvasManager.ApplyRemoteImportImage(payload);
        });
        private void NetworkEvents_OnAiTextToImageResult(AiTextToImageResultPayload payload)
        {
            if (payload == null)
                return;

            UIInvoke(() =>
            {
                var importPayload = new ImportImagePayload
                {
                    ActionID = payload.ActionID,
                    Username = payload.RequesterUsername,
                    X = payload.X,
                    Y = payload.Y,
                    Width = payload.Width,
                    Height = payload.Height,
                    ImageData = payload.ImageData,
                    Timestamp = payload.Timestamp
                };
                RecordAction(ToDrawAction(importPayload), false);
                canvasManager.ApplyRemoteImportImage(importPayload);
                AppendLog($"AI text-to-image: {payload.RequesterUsername}");
            });
        }

        private void NetworkEvents_OnAiBgRemovedResult(AiBgRemovedPayload payload)
        {
            if (payload == null)
                return;

            UIInvoke(() =>
            {
                var importPayload = new ImportImagePayload
                {
                    ActionID = payload.ActionID,
                    Username = payload.RequesterUsername,
                    X = payload.X,
                    Y = payload.Y,
                    Width = payload.Width,
                    Height = payload.Height,
                    ImageData = payload.ImageData,
                    Timestamp = payload.Timestamp
                };
                RecordAction(ToDrawAction(importPayload), false);
                canvasManager.ApplyRemoteImportImage(importPayload);
                AppendLog($"AI remove.bg: {payload.RequesterUsername}");
            });
        }

        private void NetworkEvents_OnSetBackgroundReceived(SetBackgroundPayload payload) => UIInvoke(() =>
        {
            RecordAction(ToDrawAction(payload), false);
            canvasManager.ApplyRemoteSetBackground(payload);
        });
        private void NetworkEvents_OnClearAllReceived() => UIInvoke(() =>
        {
            actionHistory.Clear();
            undoneActionIds.Clear();
            ownRedoActionIds.Clear();
            canvasManager.ApplyRemoteClearAll();
        });
        private void NetworkEvents_OnUndoReceived(UndoPayload payload) => UIInvoke(() => ApplyUndoFromNetwork(payload));
        private void NetworkEvents_OnRedoReceived(RedoPayload payload) => UIInvoke(() => ApplyRedoFromNetwork(payload));

        private void NetworkEvents_OnLaserReceived(LaserPayload payload)
        {
            if (payload == null || string.IsNullOrWhiteSpace(payload.Username))
                return;
            if (IsCurrentUser(payload.Username))
                return;

            UIInvoke(() =>
            {
                if (payload.IsActive)
                    canvasManager.UpdateRemoteLaser(payload.Username, new Point(payload.X, payload.Y));
                else
                    canvasManager.RemoveRemoteLaser(payload.Username);
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
                string key = BuildChatKey(payload);
                if (!string.IsNullOrEmpty(key) && locallyShownChat.Remove(key))
                    return;

                AppendChatMessage(payload);
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
            UIInvoke(() =>
            {
                RecordAction(ToDrawAction(payload), false);
                canvasManager.AddSticker(payload);
            });
        }

        private void NetworkEvents_OnStickyNoteReceived(StickyNotePayload payload)
        {
            if (payload == null || string.IsNullOrWhiteSpace(payload.NoteID))
                return;

            UIInvoke(() =>
            {
                if (!payload.IsOpen)
                {
                    if (noteControls.TryGetValue(payload.NoteID, out var deletedNote))
                    {
                        noteControls.Remove(payload.NoteID);
                        if (selectedStickyNoteId == payload.NoteID)
                            selectedStickyNoteId = null;

                        canvas.Controls.Remove(deletedNote);
                        deletedNote.Dispose();
                    }

                    return;
                }

                if (!noteControls.TryGetValue(payload.NoteID, out var note))
                {
                    note = new StickyNoteControl { NoteId = payload.NoteID, Author = payload.AuthorUsername };
                    note.NoteChanged += StickyNoteChanged;
                    note.NoteSelected += StickyNoteSelected;
                    canvas.Controls.Add(note);
                    noteControls[payload.NoteID] = note;
                }

                note.Location = new Point(payload.X, payload.Y);
                if (payload.Width > 0 && payload.Height > 0)
                    note.Size = new Size(payload.Width, payload.Height);
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

        private void NetworkEvents_OnTurnBasedReceived(TurnBasedPayload payload)
        {
            if (payload == null)
                return;

            UIInvoke(() => ApplyTurnBasedState(payload));
        }

        private void ApplyTurnBasedState(TurnBasedPayload payload)
        {
            bool isMyTurn = !payload.IsEnabled ||
                string.Equals(payload.ActiveUser, _network.CurrentUsername, StringComparison.OrdinalIgnoreCase);

            canvasManager.IsDrawingEnabled = isMyTurn;
            turnPanel.SetState(payload.IsEnabled, payload.ActiveUser, _isRoomOwner);
            AppendLog(payload.IsEnabled
                ? $"Vẽ theo lượt: bật, lượt của {payload.ActiveUser}"
                : "Vẽ theo lượt: tắt");
            ToastForm.ShowToast(this, payload.IsEnabled
                ? (isMyTurn ? "Bạn đang có quyền vẽ" : $"Đang chờ lượt của {payload.ActiveUser}")
                : "Đã tắt vẽ theo lượt");
        }

        private void HandleNextTurnRequested()
        {
            if (!_isRoomOwner)
            {
                ToastForm.ShowToast(this, "Chỉ chủ phòng mới được chuyển lượt");
                return;
            }

            if (!turnPanel.IsEnabled)
            {
                ToastForm.ShowToast(this, "Hãy bật vẽ theo lượt trước");
                return;
            }

            var payload = new TurnBasedPayload
            {
                RoomCode = _roomCode,
                Username = _network.CurrentUsername,
                IsEnabled = true,
                ActiveUser = turnPanel.ActiveUser
            };

            _network?.Send(CommandType.TURN_CHANGE, payload);
            _udpManager?.SendTurnChange(payload);
        }

        private void NetworkEvents_OnSaveGalleryResponse(SaveGalleryResponse payload)
        {
            if (payload == null)
                return;
            UIInvoke(() => ToastForm.ShowToast(this, payload.Message ?? (payload.IsSuccess ? "Đã lưu Gallery" : "Lỗi lưu Gallery")));
        }

        private void MainForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Shift) canvas.Cursor = Cursors.Cross;

            if (e.Control && (e.KeyCode == Keys.Oemplus || e.KeyCode == Keys.Add))
            {
                AdjustCanvasZoom(ZoomStep);
                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }

            if (e.Control && (e.KeyCode == Keys.OemMinus || e.KeyCode == Keys.Subtract))
            {
                AdjustCanvasZoom(-ZoomStep);
                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }

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
                var pos = canvasManager?.ScreenToCanvas(canvas.PointToClient(Cursor.Position)) ?? canvas.PointToClient(Cursor.Position);
                SendLaserRealtime(new LaserPayload { Username = _network?.CurrentUsername, X = pos.X, Y = pos.Y, IsActive = false });
            }
        }

        private void Canvas_MouseWheel(object sender, MouseEventArgs e)
        {
            if ((ModifierKeys & Keys.Control) != Keys.Control)
                return;

            AdjustCanvasZoom(e.Delta > 0 ? ZoomStep : -ZoomStep, e.Location);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Delete)
            {
                bool isTextInputFocused = ActiveControl is TextBoxBase || (ActiveControl != null && ActiveControl.GetType().Name.IndexOf("TextBox", StringComparison.OrdinalIgnoreCase) >= 0);
                if (!isTextInputFocused && EnsureCanDraw())
                {
                    if (DeleteSelectedStickyNote())
                    {
                        ToastForm.ShowToast(this, "Da xoa sticky note duoc chon");
                        return true;
                    }

                    if (canvas != null && canvas.Focused && canvasManager != null && canvasManager.DeleteSelectedObject())
                    {
                        ToastForm.ShowToast(this, "Da xoa doi tuong duoc chon");
                        return true;
                    }
                }
            }

            if (keyData == (Keys.Control | Keys.Z))
            {
                if (EnsureCanDraw())
                {
                    UndoOwnLastAction();
                }
                return true;
            }
            if (keyData == (Keys.Control | Keys.Y))
            {
                if (EnsureCanDraw())
                {
                    RedoOwnLastAction();
                }
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
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
            private bool resizing;
            private Point dragOffset;
            private Point resizeStartPoint;
            private Size resizeStartSize;
            private readonly Label header;
            private readonly TextBox txt;
            private readonly Panel resizeGrip;
            public string NoteId { get; set; }
            public string Author { get; set; }
            public string NoteText { get => txt.Text; set => txt.Text = value; }
            public event Action<StickyNoteControl> NoteChanged;
            public event Action<StickyNoteControl> NoteSelected;

            public StickyNoteControl()
            {
                Size = new Size(170, 120);
                BackColor = Color.FromArgb(255, 255, 220);
                BorderStyle = BorderStyle.FixedSingle;

                header = new Label { Dock = DockStyle.Top, Height = 20, Text = "Ghi chú", BackColor = Color.Khaki, Padding = new Padding(4, 0, 0, 0) };
                txt = new TextBox { Dock = DockStyle.Fill, Multiline = true, BorderStyle = BorderStyle.None, BackColor = BackColor };
                resizeGrip = new Panel
                {
                    Size = new Size(12, 12),
                    BackColor = Color.Goldenrod,
                    Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                    Cursor = Cursors.SizeNWSE
                };

                header.MouseDown += DragStart;
                header.MouseMove += DragMove;
                header.MouseUp += DragEnd;
                MouseDown += DragStart;
                MouseMove += DragMove;
                MouseUp += DragEnd;
                txt.Leave += (s, e) => NoteChanged?.Invoke(this);
                txt.Enter += (s, e) => NoteSelected?.Invoke(this);
                txt.MouseDown += (s, e) => NoteSelected?.Invoke(this);
                resizeGrip.MouseDown += ResizeStart;
                resizeGrip.MouseMove += ResizeMove;
                resizeGrip.MouseUp += ResizeEnd;

                Resize += (s, e) => PositionResizeGrip();

                Controls.Add(txt);
                Controls.Add(header);
                Controls.Add(resizeGrip);
                PositionResizeGrip();
            }

            private void PositionResizeGrip()
            {
                resizeGrip.Location = new Point(Math.Max(0, Width - resizeGrip.Width - 1), Math.Max(0, Height - resizeGrip.Height - 1));
                resizeGrip.BringToFront();
            }

            private void DragStart(object sender, MouseEventArgs e)
            {
                if (resizing)
                    return;

                if (e.Button != MouseButtons.Left) return;
                NoteSelected?.Invoke(this);
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

            private void ResizeStart(object sender, MouseEventArgs e)
            {
                if (e.Button != MouseButtons.Left)
                    return;

                NoteSelected?.Invoke(this);
                resizing = true;
                dragging = false;
                resizeStartPoint = PointToScreen(e.Location);
                resizeStartSize = Size;
                BringToFront();
            }

            private void ResizeMove(object sender, MouseEventArgs e)
            {
                if (!resizing)
                    return;

                Point current = PointToScreen(e.Location);
                int width = Math.Max(120, resizeStartSize.Width + (current.X - resizeStartPoint.X));
                int height = Math.Max(80, resizeStartSize.Height + (current.Y - resizeStartPoint.Y));
                Size = new Size(width, height);
                PositionResizeGrip();
            }

            private void ResizeEnd(object sender, MouseEventArgs e)
            {
                if (!resizing)
                    return;

                resizing = false;
                NoteChanged?.Invoke(this);
            }

            public void SetSelected(bool selected)
            {
                BorderStyle = selected ? BorderStyle.Fixed3D : BorderStyle.FixedSingle;
                header.BackColor = selected ? Color.Gold : Color.Khaki;
            }
        }

        private class TurnPanelControl : Panel
        {
            private readonly Label lbl;
            private readonly Button btnNextTurn;
            public bool IsEnabled { get; private set; }
            public string ActiveUser { get; private set; } = string.Empty;
            public event Action NextTurnRequested;

            public TurnPanelControl()
            {
                Height = 76;
                BackColor = Color.AliceBlue;
                BorderStyle = BorderStyle.FixedSingle;
                lbl = new Label { Dock = DockStyle.Top, Height = 28, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(8, 0, 0, 0), Text = "Turn-based: OFF" };
                btnNextTurn = new Button { Dock = DockStyle.Bottom, Height = 30, Text = "Lượt kế tiếp" };
                btnNextTurn.Click += (s, e) => NextTurnRequested?.Invoke();
                Controls.Add(btnNextTurn);
                Controls.Add(lbl);
            }

            public void SetState(bool enabled, string activeUser, bool canAdvanceTurn)
            {
                IsEnabled = enabled;
                ActiveUser = activeUser ?? string.Empty;
                lbl.Text = enabled ? $"Turn-based: ON | Lượt: {ActiveUser}" : "Turn-based: OFF";
                btnNextTurn.Visible = enabled && canAdvanceTurn;
                btnNextTurn.Enabled = enabled && canAdvanceTurn;
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
