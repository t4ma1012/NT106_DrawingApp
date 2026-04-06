using System.Collections.Generic;
using System.Drawing;

namespace DrawingClient.Drawing
{
    public class UndoStack
    {
        private Stack<Bitmap> history = new Stack<Bitmap>();

        public void Push(Bitmap currentCanvas)
        {
            history.Push((Bitmap)currentCanvas.Clone());
        }

        public Bitmap Pop()
        {
            if (history.Count > 0) return history.Pop();
            return null;
        }

        public bool CanUndo => history.Count > 0;
    }
}