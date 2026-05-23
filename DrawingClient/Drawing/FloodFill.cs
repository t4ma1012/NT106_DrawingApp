using System.Collections.Generic;
using System.Drawing;

namespace DrawingClient.Drawing
{
    public static class FloodFillHelper
    {
        public static void Apply(Bitmap bitmap, Point startPoint, Color replacementColor)
        {
            if (startPoint.X < 0 || startPoint.X >= bitmap.Width || startPoint.Y < 0 || startPoint.Y >= bitmap.Height) return;

            Color targetColor = bitmap.GetPixel(startPoint.X, startPoint.Y);
            if (targetColor.ToArgb() == replacementColor.ToArgb()) return;

            Queue<Point> pixels = new Queue<Point>();
            pixels.Enqueue(startPoint);

            int width = bitmap.Width;
            int height = bitmap.Height;

            while (pixels.Count > 0)
            {
                Point pt = pixels.Dequeue();
                if (pt.X < 0 || pt.X >= width || pt.Y < 0 || pt.Y >= height) continue;

                if (bitmap.GetPixel(pt.X, pt.Y).ToArgb() == targetColor.ToArgb())
                {
                    bitmap.SetPixel(pt.X, pt.Y, replacementColor);
                    pixels.Enqueue(new Point(pt.X - 1, pt.Y));
                    pixels.Enqueue(new Point(pt.X + 1, pt.Y));
                    pixels.Enqueue(new Point(pt.X, pt.Y - 1));
                    pixels.Enqueue(new Point(pt.X, pt.Y + 1));
                }
            }
        }
    }
}