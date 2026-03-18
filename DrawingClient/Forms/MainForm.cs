using System;
using System.Drawing;
using System.Windows.Forms;
using DrawingClient.Drawing;

namespace DrawingClient.Forms
{
    public class MainForm : Form
    {
        private DoubleBufferedPictureBox canvas;
        private Panel toolPanel;
        private Panel userPanel;

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
        }
    }

    public class DoubleBufferedPictureBox : PictureBox
    {
        public DoubleBufferedPictureBox()
        {
            this.DoubleBuffered = true;
        }
    }
}