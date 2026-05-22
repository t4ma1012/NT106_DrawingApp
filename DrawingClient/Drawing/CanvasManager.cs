using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
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
        private readonly List<StickerPayload> stickers = new List<StickerPayload>();
        private readonly object stickerLock = new object();

        public ToolType CurrentTool { get; set; } = ToolType.Pen;
        public Color CurrentColor { get; set; } = Color.Black;
        public Color BackgroundColor { get; set; } = Color.White;
        public int PenWidth { get; set; } = 2;
        public float ZoomFactor { get; set; } = 1.0f;

        // ✅ Turn-based: false = bị chặn vẽ
        public bool IsDrawingEnabled { get; set; } = true;

        public UndoStack UndoHistory { get; private set; } = new UndoStack();
        private readonly TextTool textTool;
        public Action<Color> OnColorPicked;
        public Action<Point, Point, Color, int> OnNetworkDrawAction;
        public Action<FloodFillPayload> OnNetworkFloodFillAction;
        public Action<DrawPayload> OnNetworkTextAction;
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
            Bitmap newSurface = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(newSurface))
            {
                g.Clear(Color.Transparent);
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

        public void ApplyRemoteText(DrawPayload payload)
        {
            if (payload == null || string.IsNullOrWhiteSpace(payload.Text)) return;
            using (Font font = new Font(string.IsNullOrWhiteSpace(payload.FontName) ? "Arial" : payload.FontName, payload.FontSize <= 0 ? 14 : payload.FontSize))
            using (Brush brush = new SolidBrush(Color.FromArgb(payload.ColorARGB)))
            {
                graphics.DrawString(payload.Text, font, brush, payload.X1, payload.Y1);
            }
            canvas.Invalidate();
        }

        public void ApplyRemoteFloodFill(FloodFillPayload payload)
        {
            if (payload == null) return;
            if (payload.X < 0 || payload.Y < 0 || payload.X >= drawingSurface.Width || payload.Y >= drawingSurface.Height) return;
            FloodFillHelper.Apply(drawingSurface, new Point(payload.X, payload.Y), Color.FromArgb(payload.ColorARGB));
            canvas.Invalidate();
        }

        public void ApplyDrawAction(DrawAction action)
        {
            if (action == null) return;

            string tool = action.ToolType ?? string.Empty;
            if (tool.Equals("FloodFill", StringComparison.OrdinalIgnoreCase))
            {
                ApplyRemoteFloodFill(new FloodFillPayload { X = action.X1, Y = action.Y1, ColorARGB = action.ColorARGB });
                return;
            }

            if (tool.Equals("Text", StringComparison.OrdinalIgnoreCase))
            {
                ApplyRemoteText(new DrawPayload { X1 = action.X1, Y1 = action.Y1, Text = action.Text, FontName = action.FontName, FontSize = action.FontSize, ColorARGB = action.ColorARGB });
                return;
            }

            ApplyRemoteDraw(new DrawPayload { ToolType = tool, X1 = action.X1, Y1 = action.Y1, X2 = action.X2, Y2 = action.Y2, ColorARGB = action.ColorARGB, Thickness = action.Thickness });
        }

        public void ApplyRemoteImportImage(ImportImagePayload payload)
        {
            if (payload == null || string.IsNullOrWhiteSpace(payload.ImageData)) return;
            try
            {
                byte[] bytes = Convert.FromBase64String(payload.ImageData);
                using (var ms = new MemoryStream(bytes))
                using (var img = Image.FromStream(ms))
                {
                    graphics.DrawImage(img, new Rectangle(payload.X, payload.Y, payload.Width, payload.Height));
                }
                canvas.Invalidate();
            }
            catch { }
        }

        public void ApplyRemoteSetBackground(SetBackgroundPayload payload)
        {
            if (payload == null) return;
            // ✅ Dùng cùng logic với ChangeBackgroundColor — màu nền render trong Canvas_Paint
            BackgroundColor = Color.FromArgb(payload.ColorARGB);
            canvas.Invalidate();
        }

        public void ApplyRemoteClearAll()
        {
            try
            {
                graphics.Clear(Color.Transparent);
                ClearStickers();
                BackgroundColor = Color.White;
                canvas.Invalidate();
            }
            catch { }
        }

        public void AddSticker(StickerPayload payload)
        {
            if (payload == null) return;
            lock (stickerLock) { stickers.Add(payload); }
            canvas.Invalidate();
        }

        public void ClearStickers()
        {
            lock (stickerLock) { stickers.Clear(); }
            canvas.Invalidate();
        }

        // BỌC TRY-CATCH CHO UNDO, REDO VÀ CLEAR ALL
        public void Undo()
        {
            try
            {
                if (UndoHistory != null && UndoHistory.CanUndo)
                {
                    Bitmap current = drawingSurface;
                    Bitmap previous = UndoHistory.Undo(current);
                    if (previous != null)
                    {
                        drawingSurface = previous;
                        graphics = Graphics.FromImage(drawingSurface);
                        graphics.SmoothingMode = SmoothingMode.AntiAlias;
                        canvas.Invalidate();
                    }
                }
            }
            catch { }
        }

        public void Redo()
        {
            try
            {
                if (UndoHistory != null && UndoHistory.CanRedo)
                {
                    Bitmap current = drawingSurface;
                    Bitmap next = UndoHistory.Redo(current);
                    if (next != null)
                    {
                        drawingSurface = next;
                        graphics = Graphics.FromImage(drawingSurface);
                        graphics.SmoothingMode = SmoothingMode.AntiAlias;
                        canvas.Invalidate();
                    }
                }
            }
            catch { }
        }

        public void ClearAll()
        {
            try { UndoHistory?.Push(drawingSurface); } catch { }
            try
            {
                graphics.Clear(Color.Transparent);
                ClearStickers();
                BackgroundColor = Color.White;
                canvas.Invalidate();
            }
            catch { }
        }

        public void ImportImage(Image image, Rectangle targetRect)
        {
            if (image == null) return;
            try { UndoHistory?.Push(drawingSurface); } catch { }
            graphics.DrawImage(image, targetRect);
            canvas.Invalidate();
        }

        public void ExportImage(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || drawingSurface == null) return;

            // Tự tạo một tấm ảnh mới tinh
            using (Bitmap exportBmp = new Bitmap(drawingSurface.Width, drawingSurface.Height))
            using (Graphics g = Graphics.FromImage(exportBmp))
            {
                // Sơn màu nền hiện tại lên tấm ảnh mới
                g.Clear(BackgroundColor);
                // Đặt các nét vẽ (vốn trong suốt) đè lên trên màu nền
                g.DrawImage(drawingSurface, 0, 0);

                // Lưu thành quả
                exportBmp.Save(filePath);
            }
        }

        public void ApplyRemoteDraw(DrawPayload payload)
        {
            if (payload == null) return;
            using (Pen pen = new Pen(Color.FromArgb(payload.ColorARGB), payload.Thickness > 0 ? payload.Thickness : 2))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap   = LineCap.Round;

                Point p1 = new Point(payload.X1, payload.Y1);
                Point p2 = new Point(payload.X2, payload.Y2);

                switch (payload.ToolType?.ToLower())
                {
                    case "rectangle":
                        var rRect = BuildRectangle(p1, p2);
                        graphics.DrawRectangle(pen, rRect);
                        break;

                    case "circle":
                        var cRect = BuildRectangle(p1, p2);
                        graphics.DrawEllipse(pen, cRect);
                        break;

                    case "line":
                        graphics.DrawLine(pen, p1, p2);
                        break;

                    case "eraser":
                        using (Pen eraserPen = new Pen(BackgroundColor, payload.Thickness > 0 ? payload.Thickness * 3 : 12))
                        {
                            eraserPen.StartCap = LineCap.Round;
                            eraserPen.EndCap   = LineCap.Round;
                            graphics.DrawLine(eraserPen, p1, p2);
                        }
                        break;

                    default: // Pen, Spray, v.v.
                        graphics.DrawLine(pen, p1, p2);
                        break;
                }
            }
            canvas.Invalidate();
        }

        public void UpdateRemoteCursor(string username, Point point)
        {
            if (string.IsNullOrWhiteSpace(username)) return;
            lock (cursorLock) { remoteCursors[username] = point; }
            canvas.Invalidate();
        }

        public void RemoveRemoteCursor(string username)
        {
            if (string.IsNullOrWhiteSpace(username)) return;
            lock (cursorLock)
            {
                if (remoteCursors.ContainsKey(username)) remoteCursors.Remove(username);
            }
            canvas.Invalidate();
        }

        private void Canvas_Paint(object sender, PaintEventArgs e)
        {
            if (drawingSurface != null)
            {
                e.Graphics.ScaleTransform(ZoomFactor, ZoomFactor);
                using (var bgBrush = new SolidBrush(BackgroundColor))
                {
                    e.Graphics.FillRectangle(bgBrush, new Rectangle(0, 0, drawingSurface.Width, drawingSurface.Height));
                }
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

                lock (stickerLock)
                {
                    foreach (var sticker in stickers)
                    {
                        DrawSticker(e.Graphics, sticker);
                    }
                }
            }
        }

        private static void DrawSticker(Graphics g, StickerPayload sticker)
        {
            Rectangle rect = new Rectangle(sticker.X, sticker.Y, Math.Max(24, sticker.Width), Math.Max(24, sticker.Height));
            string glyph = GetStickerGlyph(sticker.StickerID);

            using (Font font = new Font("Segoe UI Emoji", Math.Max(12, rect.Height - 6), FontStyle.Regular, GraphicsUnit.Pixel))
            using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                if (Math.Abs(sticker.Rotation) > 0.01f)
                {
                    var state = g.Save();
                    g.TranslateTransform(rect.X + rect.Width / 2f, rect.Y + rect.Height / 2f);
                    g.RotateTransform(sticker.Rotation);
                    g.DrawString(glyph, font, Brushes.Black, new RectangleF(-rect.Width / 2f, -rect.Height / 2f, rect.Width, rect.Height), sf);
                    g.Restore(state);
                }
                else
                {
                    g.DrawString(glyph, font, Brushes.Black, rect, sf);
                }
            }
        }

        private static string GetStickerGlyph(string stickerId)
        {
            switch (stickerId)
            {
                case "heart": return "❤️";
                case "star": return "⭐";
                case "fire": return "🔥";
                case "idea": return "💡";
                case "check": return "✅";
                default: return "📌";
            }
        }

        private void DrawTextOnCanvas(string text, Point location, Color color)
        {
            try { UndoHistory?.Push(drawingSurface); } catch { }
            using (Graphics g = Graphics.FromImage(drawingSurface))
            using (Font font = new Font("Arial", 14))
            using (SolidBrush brush = new SolidBrush(color))
            {
                g.DrawString(text, font, brush, location);
            }

            OnNetworkTextAction?.Invoke(new DrawPayload
            {
                ActionID = Guid.NewGuid().ToString(),
                ToolType = ToolType.Text.ToString(),
                X1 = location.X,
                Y1 = location.Y,
                Text = text,
                FontName = "Arial",
                FontSize = 14,
                ColorARGB = color.ToArgb(),
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            });
            canvas.Invalidate();
        }

        private void Canvas_MouseDown(object sender, MouseEventArgs e)
        {
            // ✅ Turn-based: chặn vẽ nếu không phải lượt của mình
            if (!IsDrawingEnabled && e.Button == MouseButtons.Left)
                return;

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
                    try { UndoHistory?.Push(drawingSurface); } catch { }
                    FloodFillHelper.Apply(drawingSurface, actualPoint, CurrentColor);
                    OnNetworkFloodFillAction?.Invoke(new FloodFillPayload
                    {
                        ActionID = Guid.NewGuid().ToString(),
                        X = actualPoint.X,
                        Y = actualPoint.Y,
                        ColorARGB = CurrentColor.ToArgb(),
                        Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                    });
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
                try { UndoHistory?.Push(drawingSurface); } catch { }
            }

            if (e.Button == MouseButtons.Right && (Control.ModifierKeys & Keys.Shift) == Keys.Shift)
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

        public void ChangeBackgroundColor(Color color)
        {
            BackgroundColor = color;
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