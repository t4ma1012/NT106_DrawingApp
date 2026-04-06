using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace DrawingClient.UI
{
    public class CursorLayer
    {
        private PictureBox canvas;
        public Dictionary<string, Point> OtherCursors { get; set; } = new Dictionary<string, Point>();
        public Dictionary<string, Point> OtherLasers { get; set; } = new Dictionary<string, Point>();

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

            foreach (var cursor in OtherCursors)
            {
                g.FillEllipse(Brushes.Blue, cursor.Value.X, cursor.Value.Y, 8, 8);
                g.DrawString(cursor.Key, new Font("Arial", 8), Brushes.Black, cursor.Value.X + 10, cursor.Value.Y);
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
    }
}