import sys
import codecs

# 1. Patch LobbyForm.cs
lobby_path = r"d:\Download\NT106_DrawingApp_Fix14\NT106_DrawingApp\DrawingClient\Forms\LobbyForm.cs"

with codecs.open(lobby_path, 'r', 'utf-8') as f:
    lobby_content = f.read()

old_create_response = """        private void NetworkEvents_OnCreateRoomResponse(CreateRoomResponse response)
        {
            if (response == null) return;
            UIInvoke(() =>
            {
                if (response.IsSuccess)
                {
                    lblStatus.Text = "Tạo phòng thành công!";
                    MessageBox.Show($"Tạo phòng thành công! Mã phòng: {response.RoomCode}", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    // Mở form MainForm
                    this.Hide();
                    MainForm main = new MainForm(_network, response.RoomCode);
                    main.ShowDialog();
                    this.Close();
                }
                else
                {
                    lblStatus.Text = "Lỗi: " + response.Message;
                    MessageBox.Show("Không thể tạo phòng: " + response.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    btnCreateRoom.Enabled = true;
                    btnJoinRoom.Enabled = true;
                }
            });
        }"""

new_create_response = """        private void NetworkEvents_OnCreateRoomResponse(CreateRoomResponse response)
        {
            if (response == null) return;
            UIInvoke(() =>
            {
                if (response.IsSuccess)
                {
                    lblStatus.Text = "Tạo phòng thành công!";
                    MessageBox.Show($"Tạo phòng thành công! Mã phòng: {response.RoomCode}", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    
                    NetworkEvents.OnCreateRoomResponse -= NetworkEvents_OnCreateRoomResponse;
                    NetworkEvents.OnJoinRoomResponse -= NetworkEvents_OnJoinRoomResponse;
                    
                    this.Hide();
                    MainForm main = new MainForm(_network, response.RoomCode);
                    main.ShowDialog();
                    
                    if (main.DialogResult == DialogResult.Abort)
                    {
                        lblStatus.Text = "";
                        btnCreateRoom.Enabled = true;
                        btnJoinRoom.Enabled = true;
                        NetworkEvents.OnCreateRoomResponse += NetworkEvents_OnCreateRoomResponse;
                        NetworkEvents.OnJoinRoomResponse += NetworkEvents_OnJoinRoomResponse;
                        this.Show();
                    }
                    else
                    {
                        this.Close();
                    }
                }
                else
                {
                    lblStatus.Text = "Lỗi: " + response.Message;
                    MessageBox.Show("Không thể tạo phòng: " + response.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    btnCreateRoom.Enabled = true;
                    btnJoinRoom.Enabled = true;
                }
            });
        }"""

lobby_content = lobby_content.replace(old_create_response, new_create_response)


old_join_response = """        private void NetworkEvents_OnJoinRoomResponse(JoinRoomResponse response)
        {
            if (response == null) return;
            UIInvoke(() =>
            {
                if (response.IsSuccess)
                {
                    lblStatus.Text = "Tham gia thành công!";
                    this.Hide();
                    MainForm main = new MainForm(_network, response.RoomCode);
                    main.ShowDialog();
                    this.Close();
                }
                else
                {
                    lblStatus.Text = "Lỗi: " + response.Message;
                    MessageBox.Show("Không thể tham gia phòng: " + response.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    btnCreateRoom.Enabled = true;
                    btnJoinRoom.Enabled = true;
                }
            });
        }"""

new_join_response = """        private void NetworkEvents_OnJoinRoomResponse(JoinRoomResponse response)
        {
            if (response == null) return;
            UIInvoke(() =>
            {
                if (response.IsSuccess)
                {
                    lblStatus.Text = "Tham gia thành công!";
                    
                    NetworkEvents.OnCreateRoomResponse -= NetworkEvents_OnCreateRoomResponse;
                    NetworkEvents.OnJoinRoomResponse -= NetworkEvents_OnJoinRoomResponse;
                    
                    this.Hide();
                    MainForm main = new MainForm(_network, response.RoomCode);
                    main.ShowDialog();
                    
                    if (main.DialogResult == DialogResult.Abort)
                    {
                        lblStatus.Text = "";
                        btnCreateRoom.Enabled = true;
                        btnJoinRoom.Enabled = true;
                        NetworkEvents.OnCreateRoomResponse += NetworkEvents_OnCreateRoomResponse;
                        NetworkEvents.OnJoinRoomResponse += NetworkEvents_OnJoinRoomResponse;
                        this.Show();
                    }
                    else
                    {
                        this.Close();
                    }
                }
                else
                {
                    lblStatus.Text = "Lỗi: " + response.Message;
                    MessageBox.Show("Không thể tham gia phòng: " + response.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    btnCreateRoom.Enabled = true;
                    btnJoinRoom.Enabled = true;
                }
            });
        }"""

lobby_content = lobby_content.replace(old_join_response, new_join_response)

with codecs.open(lobby_path, 'w', 'utf-8') as f:
    f.write(lobby_content)

# 2. Patch MainForm.cs
main_path = r"d:\Download\NT106_DrawingApp_Fix14\NT106_DrawingApp\DrawingClient\Forms\MainForm.cs"

with codecs.open(main_path, 'r', 'utf-8') as f:
    main_content = f.read()

old_leave_btn = """            btnLeaveRoom.Click += (s, e) =>
            {
                _network?.SendEmpty(CommandType.LEAVE_ROOM);
                this.Hide();
                LobbyForm lobby = new LobbyForm(_network);
                lobby.ShowDialog();
                this.Close();
            };"""

new_leave_btn = """            btnLeaveRoom.Click += (s, e) =>
            {
                _network?.SendEmpty(CommandType.LEAVE_ROOM);
                this.DialogResult = DialogResult.Abort;
                this.Close();
            };"""

main_content = main_content.replace(old_leave_btn, new_leave_btn)

old_form_closed = """        private void MainForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            UnsubscribeNetworkEvents();
            _network?.Disconnect();
            Application.Exit();
        }"""

new_form_closed = """        private void MainForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            UnsubscribeNetworkEvents();
            if (this.DialogResult != DialogResult.Abort)
            {
                _network?.Disconnect();
                Application.Exit();
            }
        }"""

main_content = main_content.replace(old_form_closed, new_form_closed)

old_keydown = """        private void MainForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Shift) canvas.Cursor = Cursors.Cross;

            if (e.KeyCode == Keys.D1)
            {
                var pos = canvas.PointToClient(Cursor.Position);
                cursorLayer.AddEmoji("✨", pos);
                _udpManager?.SendReaction(new ReactionPayload { Username = _network.CurrentUsername, Emoji = "✨", X = pos.X, Y = pos.Y });
            }
            if (e.KeyCode == Keys.D2)
            {
                var pos = canvas.PointToClient(Cursor.Position);
                cursorLayer.AddEmoji("👍", pos);
                _udpManager?.SendReaction(new ReactionPayload { Username = _network.CurrentUsername, Emoji = "👍", X = pos.X, Y = pos.Y });
            }
            if (e.KeyCode == Keys.D3)
            {
                var pos = canvas.PointToClient(Cursor.Position);
                cursorLayer.AddEmoji("❤️", pos);
                _udpManager?.SendReaction(new ReactionPayload { Username = _network.CurrentUsername, Emoji = "❤️", X = pos.X, Y = pos.Y });
            }
        }"""

new_keydown = """        private void MainForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Shift) canvas.Cursor = Cursors.Cross;

            if (e.Control && e.KeyCode == Keys.Z)
            {
                btnUndo.PerformClick();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (e.Control && e.KeyCode == Keys.Y)
            {
                btnRedo.PerformClick();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }

            if (e.KeyCode == Keys.D1)
            {
                var pos = canvas.PointToClient(Cursor.Position);
                cursorLayer.AddEmoji("✨", pos);
                _udpManager?.SendReaction(new ReactionPayload { Username = _network.CurrentUsername, Emoji = "✨", X = pos.X, Y = pos.Y });
            }
            if (e.KeyCode == Keys.D2)
            {
                var pos = canvas.PointToClient(Cursor.Position);
                cursorLayer.AddEmoji("👍", pos);
                _udpManager?.SendReaction(new ReactionPayload { Username = _network.CurrentUsername, Emoji = "👍", X = pos.X, Y = pos.Y });
            }
            if (e.KeyCode == Keys.D3)
            {
                var pos = canvas.PointToClient(Cursor.Position);
                cursorLayer.AddEmoji("❤️", pos);
                _udpManager?.SendReaction(new ReactionPayload { Username = _network.CurrentUsername, Emoji = "❤️", X = pos.X, Y = pos.Y });
            }
        }"""

main_content = main_content.replace(old_keydown, new_keydown)

with codecs.open(main_path, 'w', 'utf-8') as f:
    f.write(main_content)

print("PATCH LOBBY AND MAINFORM DONE")
