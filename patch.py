import sys

target_path = r"d:\Download\NT106_DrawingApp_Fix14\NT106_DrawingApp\DrawingClient\Forms\MainForm.cs"

with open(target_path, 'r', encoding='utf-8') as f:
    lines = f.readlines()

start_idx = -1
end_idx = -1

for i, line in enumerate(lines):
    if "isPlacingSticker = false;" in line:
        start_idx = i + 2 # Skip the "return;"
        break

for i in range(start_idx, len(lines)):
    if 'string tool = action.ToolType' in lines[i]:
        end_idx = i
        break

print(f"Start: {start_idx}, End: {end_idx}")

missing_code = """            if (isStickyNoteMode)
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
        }

        private void StickyNoteChanged(StickyNoteControl note)
        {
            _network?.SendStickyNote(new StickyNotePayload
            {
                NoteID = note.NoteId,
                AuthorUsername = _network.CurrentUsername,
                X = note.Left,
                Y = note.Top,
                Text = note.NoteText,
                IsOpen = true,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            });
        }

        private void BtnImport_Click(object sender, EventArgs e)
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
        }

        private void BtnExport_Click(object sender, EventArgs e)
        {
            using (SaveFileDialog saveFileDialog = new SaveFileDialog())
            {
                saveFileDialog.Filter = "PNG Image|*.png|JPEG Image|*.jpg";
                saveFileDialog.FileName = f"canvas_{DateTime.Now:yyyyMMdd_HHmmss}.png";
                if (saveFileDialog.ShowDialog() != DialogResult.OK)
                    return;

                canvasManager.ExportImage(saveFileDialog.FileName);
                ToastForm.ShowToast(this, "Đã xuất ảnh.");
            }
        }

        private void SendChatMessage()
        {
            string message = txtChatInput.Text?.Trim();
            if (string.IsNullOrWhiteSpace(message)) return;

            _network?.SendChat(message);
            txtChatInput.Clear();
        }

        private void AppendLog(string message)
        {
            if (lstLogs == null)
                return;

            lstLogs.Items.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
            lstLogs.TopIndex = lstLogs.Items.Count - 1;
        }

        private void NetworkEvents_OnCursorReceived(CursorPayload payload)
        {
            if (payload == null)
                return;
            UIInvoke(() =>
            {
                canvasManager.UpdateRemoteCursor(payload.Username, new Point(payload.X, payload.Y));
                cursorLayer?.UpdateCursor(payload);
            });
        }

        private void NetworkEvents_OnUserJoined(UserJoinPayload payload)
        {
            if (payload == null || string.IsNullOrWhiteSpace(payload.Username))
                return;
            UIInvoke(() =>
            {
                ToastForm.ShowToast(this, f"{payload.Username} đã tham gia phòng");
                AppendLog(f"{payload.Username} đã tham gia phòng.");
            });
        }

        private void NetworkEvents_OnUserLeft(UserLeavePayload payload)
        {
            if (payload == null || string.IsNullOrWhiteSpace(payload.Username))
                return;
            UIInvoke(() =>
            {
                cursorLayer?.RemoveCursor(payload.Username);
                canvasManager.RemoveRemoteCursor(payload.Username);
                ToastForm.ShowToast(this, f"{payload.Username} đã rời phòng");
                AppendLog(f"{payload.Username} đã rời phòng.");
            });
        }

        private void NetworkEvents_OnDrawReceived(DrawPayload payload) => UIInvoke(() => canvasManager.ApplyRemoteDraw(payload));
        
        private void NetworkEvents_OnSyncBoardReceived(SyncBoardPayload payload)
        {
            if (payload?.Actions == null)
                return;
            UIInvoke(() =>
            {
                // ✅ FIX: Clear canvas trước khi replay để tránh bị chồng nét khi reconnect
                canvasManager.ClearAll();

                foreach (var action in payload.Actions)
                {
"""

# Replace f-strings in C# code that Python might incorrectly format 
# Wait, missing_code is just a string, it won't evaluate f-strings unless it has f"".
# So I should change f"..." to $"..." because it's C#.
missing_code = missing_code.replace('f"canvas_', '$"canvas_').replace('f"{payload', '$"{payload')

if start_idx != -1 and end_idx != -1:
    new_lines = lines[:start_idx] + [missing_code + "\n"] + lines[end_idx:]
    with open(target_path, 'w', encoding='utf-8') as f:
        f.writelines(new_lines)
    print("SUCCESSFULLY PATCHED VIA PYTHON!")
else:
    print("COULD NOT FIND INDEX!")
