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

        public CanvasManager(PictureBox pictureBox)
        {
            canvas = pictureBox;
            ResizeCanvas(800, 600);

            canvas.MouseDown += Canvas_MouseDown;
            canvas.MouseMove += Canvas_MouseMove;
            canvas.MouseUp += Canvas_MouseUp;
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
            canvas.Image = drawingSurface;
        }

        public void ClearAll()
        {
            graphics.Clear(BackgroundColor);
            canvas.Invalidate();
        }

        public void ChangeBackgroundColor(Color color)
        {
            BackgroundColor = color;
            ClearAll();
        }

        private void Canvas_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                isDrawing = true;
                previousPoint = e.Location;
            }
        }

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (isDrawing && (CurrentTool == ToolType.Pen || CurrentTool == ToolType.Eraser))
            {
                Color penColor = CurrentTool == ToolType.Eraser ? BackgroundColor : CurrentColor;
                using (Pen pen = new Pen(penColor, PenWidth))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    graphics.DrawLine(pen, previousPoint, e.Location);
                    previousPoint = e.Location;
                    canvas.Invalidate();
                }
            }
        }

        private void Canvas_MouseUp(object sender, MouseEventArgs e)
        {
            isDrawing = false;
        }
    }
}