using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace DrawingClient.Drawing
{
    public class CanvasManager
    {
        private PictureBox canvas;
        private Bitmap drawingSurface;
        private Graphics graphics;
        private Point previousPoint;
        private bool isDrawing;

        public ToolType CurrentTool { get; set; } = ToolType.Pen;
        public Color CurrentColor { get; set; } = Color.Black;
        public Color BackgroundColor { get; set; } = Color.White;
        public int PenWidth { get; set; } = 2;
        public float ZoomFactor { get; set; } = 1.0f;

        public UndoStack UndoHistory { get; private set; } = new UndoStack();
        private TextTool textTool;
        public Action<Color> OnColorPicked;
        public Action<Point, Point, Color, int> OnNetworkDrawAction;

        public CanvasManager(PictureBox pictureBox)
        {
            canvas = pictureBox;
            ResizeCanvas(800, 600);

            textTool = new TextTool(canvas, DrawTextOnCanvas);

            canvas.MouseDown += Canvas_MouseDown;
            canvas.MouseMove += Canvas_MouseMove;
            canvas.MouseUp += Canvas_MouseUp;
            canvas.Paint += Canvas_Paint;
        }

        public Point ScreenToCanvas(Point screenPoint)
        {
            return new Point((int)(screenPoint.X / ZoomFactor), (int)(screenPoint.Y / ZoomFactor));
        }

        public void ResizeCanvas(int width, int height)
        {
            Bitmap newSurface = new Bitmap(width, height);
            using (Graphics g = Graphics.FromImage(newSurface))
            {
                g.Clear(BackgroundColor);
                if (drawingSurface != null)
                {
                    g.DrawImage(drawingSurface, 0, 0);
                    drawingSurface.Dispose();
                }
            }
            drawingSurface = newSurface;
            graphics = Graphics.FromImage(drawingSurface);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            canvas.Invalidate();
        }

        public void Undo()
        {
            if (UndoHistory.CanUndo)
            {
                drawingSurface.Dispose();
                drawingSurface = UndoHistory.Pop();
                graphics = Graphics.FromImage(drawingSurface);
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                canvas.Invalidate();
            }
        }

        private void Canvas_Paint(object sender, PaintEventArgs e)
        {
            if (drawingSurface != null)
            {
                e.Graphics.ScaleTransform(ZoomFactor, ZoomFactor);
                e.Graphics.DrawImage(drawingSurface, Point.Empty);
            }
        }

        private void DrawTextOnCanvas(string text, Point location, Color color)
        {
            UndoHistory.Push(drawingSurface);
            using (Graphics g = Graphics.FromImage(drawingSurface))
            using (Font font = new Font("Arial", 14))
            using (SolidBrush brush = new SolidBrush(color))
            {
                g.DrawString(text, font, brush, location);
            }
            canvas.Invalidate();
        }

        private void Canvas_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                Point actualPoint = ScreenToCanvas(e.Location);

                if (CurrentTool == ToolType.Pipette)
                {
                    if (actualPoint.X >= 0 && actualPoint.X < drawingSurface.Width && actualPoint.Y >= 0 && actualPoint.Y < drawingSurface.Height)
                    {
                        Color pickedColor = drawingSurface.GetPixel(actualPoint.X, actualPoint.Y);
                        CurrentColor = pickedColor;
                        OnColorPicked?.Invoke(pickedColor);
                    }
                    return;
                }

                if (CurrentTool == ToolType.FloodFill)
                {
                    UndoHistory.Push(drawingSurface);
                    FloodFillHelper.Apply(drawingSurface, actualPoint, CurrentColor);
                    canvas.Invalidate();
                    return;
                }

                if (CurrentTool == ToolType.Text)
                {
                    textTool.StartTyping(e.Location, CurrentColor);
                    return;
                }

                isDrawing = true;
                previousPoint = actualPoint;
                UndoHistory.Push(drawingSurface);
            }
        }

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            Point actualPoint = ScreenToCanvas(e.Location);

            if (isDrawing && (CurrentTool == ToolType.Pen || CurrentTool == ToolType.Eraser))
            {
                Color penColor = CurrentTool == ToolType.Eraser ? BackgroundColor : CurrentColor;
                using (Pen pen = new Pen(penColor, PenWidth))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    graphics.DrawLine(pen, previousPoint, actualPoint);
                }

                OnNetworkDrawAction?.Invoke(previousPoint, actualPoint, penColor, PenWidth);

                previousPoint = actualPoint;
                canvas.Invalidate();
            }
        }

        private void Canvas_MouseUp(object sender, MouseEventArgs e)
        {
            isDrawing = false;
        }

        public void ClearAll()
        {
            UndoHistory.Push(drawingSurface);
            graphics.Clear(BackgroundColor);
            canvas.Invalidate();
        }

        public void ChangeBackgroundColor(Color color)
        {
            UndoHistory.Push(drawingSurface);
            BackgroundColor = color;
            graphics.Clear(BackgroundColor);
            canvas.Invalidate();
        }
    }
}