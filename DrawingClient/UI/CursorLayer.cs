using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using SharedLib.Payloads;

namespace DrawingClient.UI
{
    public class CursorLayer
    {
        private PictureBox _canvas;
        private List<EmojiDrop> _emojis = new List<EmojiDrop>();
        private Timer _timer;

        public CursorLayer(PictureBox canvas)
        {
            _canvas = canvas;
            _canvas.Paint += Canvas_Paint;
            _timer = new Timer { Interval = 50 };
            _timer.Tick += Timer_Tick;
            _timer.Start();
        }

        public void UpdateCursor(CursorPayload payload) { }
        public void RemoveCursor(string username) { }
        public void AddEmoji(string emoji, Point pos) { _emojis.Add(new EmojiDrop { Text = emoji, Position = pos, Life = 2000 }); }

        private void Timer_Tick(object sender, EventArgs e)
        {
            bool needRedraw = false;
            for (int i = _emojis.Count - 1; i >= 0; i--)
            {
                _emojis[i].Life -= 50;
                _emojis[i].Position = new Point(_emojis[i].Position.X, _emojis[i].Position.Y - 2);
                if (_emojis[i].Life <= 0) _emojis.RemoveAt(i);
                needRedraw = true;
            }
            if (needRedraw) _canvas.Invalidate();
        }

        private void Canvas_Paint(object sender, PaintEventArgs e)
        {
            using (Font f = new Font("Segoe UI Emoji", 20))
                foreach (var em in _emojis) e.Graphics.DrawString(em.Text, f, Brushes.Black, em.Position);
        }

        private class EmojiDrop { public string Text { get; set; } public Point Position { get; set; } public int Life { get; set; } }
    }
}
