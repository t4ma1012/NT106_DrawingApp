import sys

target_path = r"d:\Download\NT106_DrawingApp_Fix14\NT106_DrawingApp\DrawingClient\Forms\MainForm.cs"

with open(target_path, 'r', encoding='utf-8') as f:
    content = f.read()

# Replace initialization
old_init = """            canvas.MouseMove += Canvas_MouseMove_SendCursor;
            canvas.MouseDown += Canvas_MouseClick_AdvancedTools;"""
new_init = """            canvas.MouseMove += Canvas_MouseMove_SendCursor;
            canvas.MouseDown += Canvas_MouseDown_Custom;
            canvas.MouseMove += Canvas_MouseMove_Custom;
            canvas.MouseUp += Canvas_MouseUp_Custom;
            canvas.Paint += Canvas_Paint_Custom;"""
content = content.replace(old_init, new_init)

# Replace BtnImport_Click
old_import = """        private void BtnImport_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "Image files|*.png;*.jpg;*.jpeg;*.bmp";
                if (openFileDialog.ShowDialog() != DialogResult.OK)
                    return;

                using (Image image = Image.FromFile(openFileDialog.FileName))
                {
                    Rectangle target = new Rectangle(10, 10, Math.Min(image.Width, 600), Math.Min(image.Height, 400));
                    canvasManager.ImportImage(image, target);

                    using (MemoryStream ms = new MemoryStream())
                    {
                        image.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                        string base64 = Convert.ToBase64String(ms.ToArray());

                        // FIX LỖI: Dùng hàm Send tổng quát
                        _network?.Send(CommandType.IMPORT_IMAGE, new ImportImagePayload
                        {
                            ActionID = Guid.NewGuid().ToString(),
                            Username = _network.CurrentUsername,
                            X = target.X,
                            Y = target.Y,
                            Width = target.Width,
                            Height = target.Height,
                            ImageData = base64,
                            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                        });
                    }
                }
            }
        }"""
new_import = """        private Image pendingImportImage;
        private Point dragStartPoint;

        private void BtnImport_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "Image files|*.png;*.jpg;*.jpeg;*.bmp";
                if (openFileDialog.ShowDialog() != DialogResult.OK)
                    return;

                if (pendingImportImage != null) pendingImportImage.Dispose();
                pendingImportImage = Image.FromFile(openFileDialog.FileName);
                isStickyNoteMode = false;
                isPlacingSticker = false;
                ToastForm.ShowToast(this, "Kéo thả chuột trên Canvas để chọn kích cỡ ảnh");
            }
        }"""
content = content.replace(old_import, new_import)

# Replace Canvas_MouseClick_AdvancedTools
old_advanced = """        private void Canvas_MouseClick_AdvancedTools(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;

            if (isPlacingSticker)
            {
                Point actualPoint = canvasManager.ScreenToCanvas(e.Location);

                var payload = new StickerPayload
                {
                    ActionID = Guid.NewGuid().ToString(),
                    Username = _network?.CurrentUsername,
                    StickerID = string.IsNullOrWhiteSpace(selectedStickerId) ? "star" : selectedStickerId,
                    X = actualPoint.X,
                    Y = actualPoint.Y,
                    Width = 36,
                    Height = 36,
                    Rotation = 0,
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                };

                canvasManager.AddSticker(payload);
                _network?.SendSticker(payload);

                isPlacingSticker = false;
                return;
            }

            if (isStickyNoteMode)
            {
                string noteId = Guid.NewGuid().ToString();
                var note = new StickyNoteControl { NoteId = noteId, Author = _network?.CurrentUsername, Location = e.Location };
                note.NoteChanged += StickyNoteChanged;
                canvas.Controls.Add(note);
                note.BringToFront();
                noteControls[noteId] = note;

                _network?.SendStickyNote(new StickyNotePayload
                {
                    NoteID = noteId,
                    AuthorUsername = _network?.CurrentUsername,
                    X = e.X,
                    Y = e.Y,
                    Text = note.NoteText,
                    IsOpen = true,
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                });

                isStickyNoteMode = false;
                return;
            }
        }"""
new_advanced = """        private void Canvas_MouseDown_Custom(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;

            if (isPlacingSticker || pendingImportImage != null)
            {
                dragStartPoint = e.Location;
            }
            else if (isStickyNoteMode)
            {
                string noteId = Guid.NewGuid().ToString();
                var note = new StickyNoteControl { NoteId = noteId, Author = _network?.CurrentUsername, Location = e.Location };
                note.NoteChanged += StickyNoteChanged;
                canvas.Controls.Add(note);
                note.BringToFront();
                noteControls[noteId] = note;

                _network?.SendStickyNote(new StickyNotePayload
                {
                    NoteID = noteId,
                    AuthorUsername = _network?.CurrentUsername,
                    X = e.X,
                    Y = e.Y,
                    Text = note.NoteText,
                    IsOpen = true,
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                });

                isStickyNoteMode = false;
            }
        }

        private void Canvas_MouseMove_Custom(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && (isPlacingSticker || pendingImportImage != null))
            {
                canvas.Invalidate();
            }
        }

        private void Canvas_MouseUp_Custom(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && isPlacingSticker)
            {
                Point start = canvasManager.ScreenToCanvas(dragStartPoint);
                Point end = canvasManager.ScreenToCanvas(e.Location);
                int width = Math.Max(24, Math.Abs(end.X - start.X));
                int height = Math.Max(24, Math.Abs(end.Y - start.Y));
                int x = Math.Min(start.X, end.X);
                int y = Math.Min(start.Y, end.Y);

                var payload = new StickerPayload
                {
                    ActionID = Guid.NewGuid().ToString(),
                    Username = _network?.CurrentUsername,
                    StickerID = string.IsNullOrWhiteSpace(selectedStickerId) ? "star" : selectedStickerId,
                    X = x,
                    Y = y,
                    Width = width,
                    Height = height,
                    Rotation = 0,
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                };

                canvasManager.AddSticker(payload);
                _network?.SendSticker(payload);

                isPlacingSticker = false;
                canvas.Invalidate();
            }
            else if (e.Button == MouseButtons.Left && pendingImportImage != null)
            {
                Point start = canvasManager.ScreenToCanvas(dragStartPoint);
                Point end = canvasManager.ScreenToCanvas(e.Location);
                int width = Math.Max(50, Math.Abs(end.X - start.X));
                int height = Math.Max(50, Math.Abs(end.Y - start.Y));
                int x = Math.Min(start.X, end.X);
                int y = Math.Min(start.Y, end.Y);
                Rectangle target = new Rectangle(x, y, width, height);

                canvasManager.ImportImage(pendingImportImage, target);

                using (MemoryStream ms = new MemoryStream())
                {
                    pendingImportImage.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                    string base64 = Convert.ToBase64String(ms.ToArray());

                    _network?.Send(CommandType.IMPORT_IMAGE, new ImportImagePayload
                    {
                        ActionID = Guid.NewGuid().ToString(),
                        Username = _network.CurrentUsername,
                        X = target.X,
                        Y = target.Y,
                        Width = target.Width,
                        Height = target.Height,
                        ImageData = base64,
                        Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                    });
                }

                pendingImportImage.Dispose();
                pendingImportImage = null;
                canvas.Invalidate();
            }
        }

        private void Canvas_Paint_Custom(object sender, PaintEventArgs e)
        {
            if (Control.MouseButtons == MouseButtons.Left && (isPlacingSticker || pendingImportImage != null))
            {
                Point endPoint = canvas.PointToClient(Cursor.Position);
                int x = Math.Min(dragStartPoint.X, endPoint.X);
                int y = Math.Min(dragStartPoint.Y, endPoint.Y);
                int w = Math.Abs(endPoint.X - dragStartPoint.X);
                int h = Math.Abs(endPoint.Y - dragStartPoint.Y);

                using (Pen p = new Pen(Color.Red, 2) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash })
                {
                    e.Graphics.DrawRectangle(p, x, y, w, h);
                }
            }
        }"""
content = content.replace(old_advanced, new_advanced)

with open(target_path, 'w', encoding='utf-8') as f:
    f.write(content)
print("PATCHED ALL CUSTOM DRAG DROP REPLACEMENTS SUCCESSFULLY!")
