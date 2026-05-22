using System;
using System.Collections.Generic;
using System.Drawing;

namespace DrawingClient.Drawing
{
    public class UndoStack
    {
        private readonly Stack<Bitmap> _undoStack = new Stack<Bitmap>();
        private readonly Stack<Bitmap> _redoStack = new Stack<Bitmap>();

        public bool CanUndo => _undoStack.Count > 0;
        public bool CanRedo => _redoStack.Count > 0;

        public void Push(Bitmap current)
        {
            if (current == null) return;

            // FIX LỖI GDI+: Tự vẽ lại ảnh thay vì dùng new Bitmap() để tránh lỗi khóa bộ nhớ
            Bitmap copy = new Bitmap(current.Width, current.Height);
            using (Graphics g = Graphics.FromImage(copy))
            {
                g.DrawImage(current, 0, 0);
            }

            _undoStack.Push(copy);
            ClearRedo();
        }

        public Bitmap Undo(Bitmap current)
        {
            if (!CanUndo) return null;

            Bitmap copy = new Bitmap(current.Width, current.Height);
            using (Graphics g = Graphics.FromImage(copy)) { g.DrawImage(current, 0, 0); }
            _redoStack.Push(copy);

            return _undoStack.Pop();
        }

        public Bitmap Redo(Bitmap current)
        {
            if (!CanRedo) return null;

            Bitmap copy = new Bitmap(current.Width, current.Height);
            using (Graphics g = Graphics.FromImage(copy)) { g.DrawImage(current, 0, 0); }
            _undoStack.Push(copy);

            return _redoStack.Pop();
        }

        private void ClearRedo()
        {
            while (_redoStack.Count > 0) _redoStack.Pop().Dispose();
        }

        public void ClearAll()
        {
            while (_undoStack.Count > 0) _undoStack.Pop().Dispose();
            ClearRedo();
        }
    }
}