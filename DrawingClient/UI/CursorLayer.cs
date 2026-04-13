using System;
using System.Collections.Generic;
using System.Drawing;
using SharedLib.Payloads;
using System.Windows.Forms;

namespace DrawingClient.UI
{
    public class CursorLayer
    {
        private PictureBox canvas;
        public Dictionary<string, Point> OtherCursors { get; set; } = new Dictionary<string, Point>();
        public Dictionary<string, Point> OtherLasers { get; set; } = new Dictionary<string, Point>();
        private readonly object _cursorLock = new object();

        private class EmojiAnim
        {
            public string Emoji { get; set; }
            public PointF Position { get; set; }
            public float Alpha { get; set; } = 255;
        }
        private List<EmojiAnim> emojis = new List<EmojiAnim>();

        private Timer animationTimer;
        private bool isLaserVisible = true;

        public CursorLayer(PictureBox pictureBox)
        {
            canvas = pictureBox;
            canvas.Paint += Canvas_Paint;

            animationTimer = new Timer { Interval = 50 };
            animationTimer.Tick += AnimationTimer_Tick;
            animationTimer.Start();
        }

        public void AddEmoji(string emoji, Point startLocation)
        {
            emojis.Add(new EmojiAnim { Emoji = emoji, Position = startLocation });
        }

        public void UpdateCursor(CursorPayload payload)
        {
            if (payload == null || string.IsNullOrWhiteSpace(payload.Username))
                return;

            if (canvas.InvokeRequired)
            {
                canvas.BeginInvoke(new Action(() => UpdateCursor(payload)));
                return;
            }

            lock (_cursorLock)
            {
                OtherCursors[payload.Username] = new Point(payload.X, payload.Y);
            }
            canvas.Invalidate();
        }

        public void RemoveCursor(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
                return;

            if (canvas.InvokeRequired)
            {
                canvas.BeginInvoke(new Action(() => RemoveCursor(username)));
                return;
            }

            lock (_cursorLock)
            {
                if (OtherCursors.ContainsKey(username))
                    OtherCursors.Remove(username);
            }
            canvas.Invalidate();
        }

        private void AnimationTimer_Tick(object sender, EventArgs e)
        {
            isLaserVisible = !isLaserVisible;
            bool needsRedraw = OtherLasers.Count > 0 || emojis.Count > 0;

            for (int i = emojis.Count - 1; i >= 0; i--)
            {
                emojis[i].Position = new PointF(emojis[i].Position.X, emojis[i].Position.Y - 2);
                emojis[i].Alpha -= 5;
                if (emojis[i].Alpha <= 0) emojis.RemoveAt(i);
            }

            if (needsRedraw) canvas.Invalidate();
        }

        private void Canvas_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;

            lock (_cursorLock)
            {
                foreach (var cursor in OtherCursors)
                {
                    DrawRemoteCursor(g, cursor.Key, cursor.Value);
                }
            }

            if (isLaserVisible)
            {
                foreach (var laser in OtherLasers)
                {
                    g.FillEllipse(Brushes.Red, laser.Value.X - 4, laser.Value.Y - 4, 10, 10);
                }
            }

            foreach (var em in emojis)
            {
                using (SolidBrush brush = new SolidBrush(Color.FromArgb((int)em.Alpha, 0, 0, 0)))
                {
                    g.DrawString(em.Emoji, new Font("Segoe UI Emoji", 24), brush, em.Position);
                }
            }
        }

        private void DrawRemoteCursor(Graphics g, string username, Point location)
        {
            Point[] cursorShape = new[]
            {
                new Point(location.X, location.Y),
                new Point(location.X, location.Y + 14),
                new Point(location.X + 5, location.Y + 10),
                new Point(location.X + 8, location.Y + 18),
                new Point(location.X + 11, location.Y + 17),
                new Point(location.X + 8, location.Y + 9),
                new Point(location.X + 14, location.Y + 9)
            };

            using (Brush cursorBrush = new SolidBrush(Color.DeepSkyBlue))
            using (Pen borderPen = new Pen(Color.White, 1.2f))
            using (Font nameFont = new Font("Arial", 8, FontStyle.Bold))
            {
                g.FillPolygon(cursorBrush, cursorShape);
                g.DrawPolygon(borderPen, cursorShape);

                SizeF textSize = g.MeasureString(username, nameFont);
                RectangleF labelRect = new RectangleF(location.X + 16, location.Y - 1, textSize.Width + 8, textSize.Height + 2);
                using (Brush labelBack = new SolidBrush(Color.FromArgb(180, 0, 0, 0)))
                using (Brush labelText = new SolidBrush(Color.White))
                {
                    g.FillRectangle(labelBack, labelRect);
                    g.DrawString(username, nameFont, labelText, location.X + 20, location.Y);
                }
            }
        }
    }
}