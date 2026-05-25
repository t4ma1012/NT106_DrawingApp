using System;
using System.Drawing;
using System.Windows.Forms;

namespace DrawingClient.Drawing
{
    public class TextTool
    {
        private TextBox inputTextBox;
        private PictureBox canvas;
        private Action<string, Point, Color> onTextConfirmed;
        private Point clickCanvasLocation;
        private Color textColor;

        public TextTool(PictureBox pictureBox, Action<string, Point, Color> confirmAction)
        {
            canvas = pictureBox;
            onTextConfirmed = confirmAction;

            inputTextBox = new TextBox
            {
                Visible = false,
                BorderStyle = BorderStyle.FixedSingle,
                Multiline = true,
                Size = new Size(150, 50)
            };

            inputTextBox.KeyDown += InputTextBox_KeyDown;
            inputTextBox.LostFocus += InputTextBox_LostFocus;
            canvas.Controls.Add(inputTextBox);
        }

        public void StartTyping(Point canvasLocation, Point screenLocation, Color color)
        {
            clickCanvasLocation = canvasLocation;
            textColor = color;
            inputTextBox.Location = screenLocation;
            inputTextBox.Text = "";
            inputTextBox.ForeColor = color;
            inputTextBox.Visible = true;
            inputTextBox.Focus();
        }

        private void FinishTyping()
        {
            if (inputTextBox.Visible && !string.IsNullOrWhiteSpace(inputTextBox.Text))
            {
                onTextConfirmed?.Invoke(inputTextBox.Text, clickCanvasLocation, textColor);
            }
            inputTextBox.Visible = false;
        }

        private void InputTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter && !e.Shift)
            {
                e.SuppressKeyPress = true;
                FinishTyping();
            }
        }

        private void InputTextBox_LostFocus(object sender, EventArgs e)
        {
            FinishTyping();
        }
    }
}