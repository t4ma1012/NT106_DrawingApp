using System.Collections.Generic;
using System.Drawing;

namespace DrawingClient.Drawing
{
    public class UndoStack
    {
        private readonly Stack<Bitmap> _undoHistory = new Stack<Bitmap>();
        private readonly Stack<Bitmap> _redoHistory = new Stack<Bitmap>();

        public void Push(Bitmap currentCanvas)
        {
            if (currentCanvas == null)
                return;

            _undoHistory.Push(new Bitmap(currentCanvas));
            ClearRedo();
        }

        public Bitmap Pop()
        {
            if (_undoHistory.Count > 0) return _undoHistory.Pop();
            return null;
        }

        public Bitmap Undo(Bitmap currentCanvas)
        {
            if (_undoHistory.Count == 0)
                return null;

            if (currentCanvas != null)
                _redoHistory.Push(new Bitmap(currentCanvas));

            return _undoHistory.Pop();
        }

        public Bitmap Redo(Bitmap currentCanvas)
        {
            if (_redoHistory.Count == 0)
                return null;

            if (currentCanvas != null)
                _undoHistory.Push(new Bitmap(currentCanvas));

            return _redoHistory.Pop();
        }

        public void ClearRedo()
        {
            while (_redoHistory.Count > 0)
            {
                var bmp = _redoHistory.Pop();
                bmp.Dispose();
            }
        }

        public bool CanUndo => _undoHistory.Count > 0;
        public bool CanRedo => _redoHistory.Count > 0;
    }
}