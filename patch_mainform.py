import sys

filepath = r"d:\Download\NT106_DrawingApp_Fix14\NT106_DrawingApp\DrawingClient\Forms\MainForm.cs"
with open(filepath, "r", encoding="utf-8") as f:
    content = f.read()

# Add fields
old_fields = """        private TrackBar tbPenWidth;
        private ComboBox cbCanvasSize;"""
new_fields = """        private TrackBar tbPenWidth;
        private Label lblPenWidth;
        private ToolTip colorToolTip;
        private ComboBox cbCanvasSize;"""
content = content.replace(old_fields, new_fields)

# Modify InitializeUI for OnColorPicked
old_init_1 = """            canvasManager.OnColorPicked = (color) =>
            {
                btnColorPicker.BackColor = color;
                ToastForm.ShowToast(this, "Đã hút màu");
            };"""
new_init_1 = """            canvasManager.OnColorPicked = (color) =>
            {
                btnColorPicker.BackColor = color;
                colorToolTip?.SetToolTip(btnColorPicker, $"Mã màu: #{color.R:X2}{color.G:X2}{color.B:X2}");
                ToastForm.ShowToast(this, "Đã hút màu");
            };"""
content = content.replace(old_init_1, new_init_1)

# Initialize toolTip in BuildToolPanel
old_build_1 = """        private void BuildToolPanel()
        {
            btnColorPicker = new Button { Text = "Màu nét", Location = new Point(10, 20), Size = new Size(200, 30) };"""
new_build_1 = """        private void BuildToolPanel()
        {
            colorToolTip = new ToolTip();
            btnColorPicker = new Button { Text = "Màu nét", Location = new Point(10, 20), Size = new Size(200, 30) };"""
content = content.replace(old_build_1, new_build_1)

# ColorPicker ToolTip update
old_color_click = """            btnColorPicker.Click += (s, e) =>
            {
                if (colorDialog.ShowDialog() == DialogResult.OK)
                {
                    canvasManager.CurrentColor = colorDialog.Color;
                    btnColorPicker.BackColor = colorDialog.Color;
                }
            };"""
new_color_click = """            btnColorPicker.Click += (s, e) =>
            {
                if (colorDialog.ShowDialog() == DialogResult.OK)
                {
                    canvasManager.CurrentColor = colorDialog.Color;
                    btnColorPicker.BackColor = colorDialog.Color;
                    colorToolTip.SetToolTip(btnColorPicker, $"Mã màu: #{colorDialog.Color.R:X2}{colorDialog.Color.G:X2}{colorDialog.Color.B:X2}");
                }
            };"""
content = content.replace(old_color_click, new_color_click)

# BackColor ToolTip update
old_back_click = """            btnBackColor.Click += (s, e) =>
            {
                if (colorDialog.ShowDialog() == DialogResult.OK)
                {
                    canvasManager.ChangeBackgroundColor(colorDialog.Color);
                    _network?.Send(CommandType.SET_BACKGROUND, new SetBackgroundPayload { Username = _network.CurrentUsername, ColorARGB = colorDialog.Color.ToArgb() });
                }
            };"""
new_back_click = """            btnBackColor.Click += (s, e) =>
            {
                if (colorDialog.ShowDialog() == DialogResult.OK)
                {
                    canvasManager.ChangeBackgroundColor(colorDialog.Color);
                    _network?.Send(CommandType.SET_BACKGROUND, new SetBackgroundPayload { Username = _network.CurrentUsername, ColorARGB = colorDialog.Color.ToArgb() });
                    colorToolTip.SetToolTip(btnBackColor, $"Mã màu: #{colorDialog.Color.R:X2}{colorDialog.Color.G:X2}{colorDialog.Color.B:X2}");
                }
            };"""
content = content.replace(old_back_click, new_back_click)

# Pen width
old_pen_width = """            tbPenWidth = new TrackBar { Location = new Point(10, 60), Size = new Size(200, 45), Minimum = 1, Maximum = 30, Value = 2 };
            tbPenWidth.Scroll += (s, e) => canvasManager.PenWidth = tbPenWidth.Value;"""
new_pen_width = """            tbPenWidth = new TrackBar { Location = new Point(10, 60), Size = new Size(110, 45), Minimum = 1, Maximum = 30, Value = 2 };
            lblPenWidth = new Label { Location = new Point(125, 65), Size = new Size(85, 20), Text = $"Độ dày: {tbPenWidth.Value}px", TextAlign = ContentAlignment.MiddleLeft };
            tbPenWidth.Scroll += (s, e) => 
            {
                canvasManager.PenWidth = tbPenWidth.Value;
                lblPenWidth.Text = $"Độ dày: {tbPenWidth.Value}px";
            };"""
content = content.replace(old_pen_width, new_pen_width)

# Controls add
old_tool_controls = """                btnZoomIn, btnZoomOut, cbTools, btnStickerMode, stickerPicker,
                btnStickyNote, txtFollowTarget, btnFollow, btnTurnMode, gifProgress,
                lblGifStatus, lblFollowState, btnLeaveRoom, btnToggleChat
            });"""
new_tool_controls = """                lblPenWidth, btnZoomIn, btnZoomOut, cbTools, btnStickerMode, stickerPicker,
                btnStickyNote, txtFollowTarget, btnFollow, btnTurnMode, gifProgress,
                lblGifStatus, lblFollowState, btnLeaveRoom, btnToggleChat
            });"""
content = content.replace(old_tool_controls, new_tool_controls)

with open(filepath, "w", encoding="utf-8") as f:
    f.write(content)

print("PATCH MAINFORM DONE")
