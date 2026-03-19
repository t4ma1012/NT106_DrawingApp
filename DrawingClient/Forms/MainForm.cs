using DrawingClient.Drawing;
using DrawingClient.UI;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace DrawingClient.Forms
{
    public class MainForm : Form
    {
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

        private CanvasManager canvasManager;

        public MainForm()
        {
            InitializeUI();
            canvasManager = new CanvasManager(canvas);
            canvasManager.OnColorPicked = (color) =>
            {
                btnColorPicker.BackColor = color;
                DrawingClient.UI.ToastForm.ShowToast(this, "Đã hút màu!");
            };

            canvasManager.OnNetworkDrawAction = (p1, p2, color, width) =>
            {
                // NetworkClient.SendDrawUDP(payload); - Sẽ ghép nối sau
            };
        }

        private void InitializeUI()
        {
            this.Text = "Draw Together";
            this.Size = new Size(1200, 800);
            this.StartPosition = FormStartPosition.CenterScreen;

            toolPanel = new Panel
            {
                Dock = DockStyle.Left,
                Width = 200,
                BackColor = Color.LightGray
            };

            userPanel = new Panel
            {
                Dock = DockStyle.Right,
                Width = 250,
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

            // Thêm các nút công cụ mới vào toolPanel
            Button btnUndo = new Button { Text = "Hoàn tác", Location = new Point(10, 230), Size = new Size(180, 30) };
            btnUndo.Click += (s, e) => canvasManager.Undo();

            Button btnZoomIn = new Button { Text = "Zoom +", Location = new Point(10, 270), Size = new Size(85, 30) };
            btnZoomIn.Click += (s, e) => { canvasManager.ZoomFactor += 0.2f; canvas.Invalidate(); };

            Button btnZoomOut = new Button { Text = "Zoom -", Location = new Point(105, 270), Size = new Size(85, 30) };
            btnZoomOut.Click += (s, e) => { canvasManager.ZoomFactor = Math.Max(0.2f, canvasManager.ZoomFactor - 0.2f); canvas.Invalidate(); };

            ComboBox cbTools = new ComboBox { Location = new Point(10, 310), Size = new Size(180, 30), DropDownStyle = ComboBoxStyle.DropDownList };
            cbTools.Items.AddRange(Enum.GetNames(typeof(ToolType)));
            cbTools.SelectedIndex = 0;
            cbTools.SelectedIndexChanged += (s, e) => canvasManager.CurrentTool = (ToolType)cbTools.SelectedIndex;

            toolPanel.Controls.AddRange(new Control[] { btnUndo, btnZoomIn, btnZoomOut, cbTools });

            cursorLayer = new DrawingClient.UI.CursorLayer(canvas);

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
        public void DrawFromNetwork(/* DrawPayload p */)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => DrawFromNetwork(/* p */)));
                return;
            }
            // Thực thi vẽ dữ liệu nhận được lên canvas
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