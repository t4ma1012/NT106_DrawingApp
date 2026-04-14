using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using SharedLib.Payloads;

namespace DrawingClient.Drawing
{
    public class CanvasManager
    {
        private readonly PictureBox canvas;
        private Bitmap drawingSurface;
        private Graphics graphics;
        private Point previousPoint;
        private Point currentPoint;
        private bool isDrawing;
        private bool isClaimSelecting;
        private Point claimStart;
        private Point claimEnd;
        private readonly Dictionary<string, Point> remoteCursors = new Dictionary<string, Point>();
        private readonly object cursorLock = new object();

        public ToolType CurrentTool { get; set; } = ToolType.Pen;
        public Color CurrentColor { get; set; } = Color.Black;
        public Color BackgroundColor { get; set; } = Color.White;
        public int PenWidth { get; set; } = 2;
        public float ZoomFactor { get; set; } = 1.0f;

        public UndoStack UndoHistory { get; private set; } = new UndoStack();
        private readonly TextTool textTool;
        public Action<Color> OnColorPicked;
        public Action<Point, Point, Color, int> OnNetworkDrawAction;
        public Action<Rectangle> OnClaimAreaSelected;

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
                drawingSurface = UndoHistory.Undo(drawingSurface);
                graphics = Graphics.FromImage(drawingSurface);
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                canvas.Invalidate();
            }
        }

        public void Redo()
        {
            if (UndoHistory.CanRedo)
            {
                drawingSurface.Dispose();
                drawingSurface = UndoHistory.Redo(drawingSurface);
                graphics = Graphics.FromImage(drawingSurface);
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                canvas.Invalidate();
            }
        }

        public void ImportImage(Image image, Rectangle targetRect)
        {
            if (image == null)
                return;

            UndoHistory.Push(drawingSurface);
            graphics.DrawImage(image, targetRect);
            canvas.Invalidate();
        }

        public void ExportImage(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || drawingSurface == null)
                return;

            drawingSurface.Save(filePath);
        }

        public void ApplyRemoteDraw(DrawPayload payload)
        {
            if (payload == null)
                return;

            using (Pen pen = new Pen(Color.FromArgb(payload.ColorARGB), payload.Thickness > 0 ? payload.Thickness : 2))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                graphics.DrawLine(pen, payload.X1, payload.Y1, payload.X2, payload.Y2);
            }

            canvas.Invalidate();
        }

        public void UpdateRemoteCursor(string username, Point point)
        {
            if (string.IsNullOrWhiteSpace(username))
                return;

            lock (cursorLock)
            {
                remoteCursors[username] = point;
            }
            canvas.Invalidate();
        }

        public void RemoveRemoteCursor(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
                return;

            lock (cursorLock)
            {
                if (remoteCursors.ContainsKey(username))
                    remoteCursors.Remove(username);
            }
            canvas.Invalidate();
        }

        private void Canvas_Paint(object sender, PaintEventArgs e)
        {
            if (drawingSurface != null)
            {
                e.Graphics.ScaleTransform(ZoomFactor, ZoomFactor);
                e.Graphics.DrawImage(drawingSurface, Point.Empty);

                if (isDrawing && (CurrentTool == ToolType.Line || CurrentTool == ToolType.Rectangle || CurrentTool == ToolType.Circle))
                {
                    using (Pen previewPen = new Pen(Color.FromArgb(160, CurrentColor), PenWidth))
                    {
                        previewPen.DashStyle = DashStyle.Dash;
                        DrawShape(e.Graphics, previewPen, previousPoint, currentPoint, CurrentTool);
                    }
                }

                if (isClaimSelecting)
                {
                    Rectangle claimRect = BuildRectangle(claimStart, claimEnd);
                    using (Brush fill = new SolidBrush(Color.FromArgb(45, Color.DeepSkyBlue)))
                    using (Pen border = new Pen(Color.FromArgb(170, Color.DeepSkyBlue), 1.5f))
                    {
                        border.DashStyle = DashStyle.Dash;
                        e.Graphics.FillRectangle(fill, claimRect);
                        e.Graphics.DrawRectangle(border, claimRect);
                    }
                }

                lock (cursorLock)
                {
                    foreach (var cursor in remoteCursors)
                    {
                        using (Brush b = new SolidBrush(Color.FromArgb(180, Color.MediumPurple)))
                        using (Font f = new Font("Arial", 8, FontStyle.Bold))
                        using (Brush t = new SolidBrush(Color.Black))
                        {
                            e.Graphics.FillEllipse(b, cursor.Value.X - 4, cursor.Value.Y - 4, 8, 8);
                            e.Graphics.DrawString(cursor.Key, f, t, cursor.Value.X + 8, cursor.Value.Y - 6);
                        }
                    }
                }
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
                currentPoint = actualPoint;
                UndoHistory.Push(drawingSurface);
            }

            if (e.Button == MouseButtons.Right)
            {
                isClaimSelecting = true;
                claimStart = ScreenToCanvas(e.Location);
                claimEnd = claimStart;
            }
        }

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            Point actualPoint = ScreenToCanvas(e.Location);
            currentPoint = actualPoint;

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
            else if (isDrawing && (CurrentTool == ToolType.Line || CurrentTool == ToolType.Rectangle || CurrentTool == ToolType.Circle))
            {
                canvas.Invalidate();
            }

            if (isClaimSelecting)
            {
                claimEnd = actualPoint;
                canvas.Invalidate();
            }
        }

        private void Canvas_MouseUp(object sender, MouseEventArgs e)
        {
            if (isDrawing && (CurrentTool == ToolType.Line || CurrentTool == ToolType.Rectangle || CurrentTool == ToolType.Circle))
            {
                using (Pen pen = new Pen(CurrentColor, PenWidth))
                {
                    DrawShape(graphics, pen, previousPoint, currentPoint, CurrentTool);
                }
                OnNetworkDrawAction?.Invoke(previousPoint, currentPoint, CurrentColor, PenWidth);
                canvas.Invalidate();
            }

            isDrawing = false;

            if (e.Button == MouseButtons.Right && isClaimSelecting)
            {
                isClaimSelecting = false;
                Rectangle rect = BuildRectangle(claimStart, claimEnd);
                if (rect.Width > 2 && rect.Height > 2)
                    OnClaimAreaSelected?.Invoke(rect);
                canvas.Invalidate();
            }
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

        private static Rectangle BuildRectangle(Point p1, Point p2)
        {
            int x = Math.Min(p1.X, p2.X);
            int y = Math.Min(p1.Y, p2.Y);
            int w = Math.Abs(p2.X - p1.X);
            int h = Math.Abs(p2.Y - p1.Y);
            return new Rectangle(x, y, w, h);
        }

        private static void DrawShape(Graphics g, Pen pen, Point p1, Point p2, ToolType tool)
        {
            switch (tool)
            {
                case ToolType.Line:
                    g.DrawLine(pen, p1, p2);
                    break;
                case ToolType.Rectangle:
                    g.DrawRectangle(pen, BuildRectangle(p1, p2));
                    break;
                case ToolType.Circle:
                    g.DrawEllipse(pen, BuildRectangle(p1, p2));
                    break;
            }
        }
    }
}