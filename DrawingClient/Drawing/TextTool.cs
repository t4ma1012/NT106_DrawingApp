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
        private Point clickLocation;
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

        public void StartTyping(Point location, Color color)
        {
            clickLocation = location;
            textColor = color;
            inputTextBox.Location = location;
            inputTextBox.Text = "";
            inputTextBox.ForeColor = color;
            inputTextBox.Visible = true;
            inputTextBox.Focus();
        }

        private void FinishTyping()
        {
            if (inputTextBox.Visible && !string.IsNullOrWhiteSpace(inputTextBox.Text))
            {
                onTextConfirmed?.Invoke(inputTextBox.Text, clickLocation, textColor);
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