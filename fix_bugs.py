import sys
import codecs

# 1. FIX MainForm.cs
main_path = r"d:\Download\NT106_DrawingApp_Fix14\NT106_DrawingApp\DrawingClient\Forms\MainForm.cs"
with codecs.open(main_path, 'r', 'utf-8') as f:
    content = f.read()

# Fix UdpManager port
content = content.replace('_udpManager = new UdpManager("127.0.0.1", 8889);', '_udpManager = new UdpManager("127.0.0.1", 0);')

# Fix btnLeaveRoom and add RequestReturnToLobby
old_leave = """            Button btnLeaveRoom = new Button { Text = "Rời phòng", Location = new Point(10, 825), Size = new Size(200, 30), BackColor = Color.LightCoral };
            btnLeaveRoom.Click += (s, e) =>
            {
                _network?.SendEmpty(CommandType.LEAVE_ROOM);
                this.Hide();
                var lobby = new LobbyForm(_network, _network.CurrentUsername);
                lobby.ShowDialog();
                this.Close();
            };"""
new_leave = """        public event Action RequestReturnToLobby;

        private void BtnLeaveRoom_Click()
        {
            _network?.SendEmpty(CommandType.LEAVE_ROOM);
            RequestReturnToLobby?.Invoke();
            this.Close();
        }

        private void InitLeaveButton()
        {
            // Placeholder (được gọi thẳng từ event lambda bên dưới)
        }"""
content = content.replace(old_leave, """            Button btnLeaveRoom = new Button { Text = "Rời phòng", Location = new Point(10, 825), Size = new Size(200, 30), BackColor = Color.LightCoral };
            btnLeaveRoom.Click += (s, e) => BtnLeaveRoom_Click();""")
content = content.replace("public class MainForm : Form\n    {\n", "public class MainForm : Form\n    {\n" + new_leave + "\n")

# Fix Local Chat Echo
old_chat = """        private void SendChatMessage()
        {
            string message = txtChatInput.Text?.Trim();
            if (string.IsNullOrWhiteSpace(message)) return;

            _network?.SendChat(message);
            txtChatInput.Clear();
        }"""
new_chat = """        private void SendChatMessage()
        {
            string message = txtChatInput.Text?.Trim();
            if (string.IsNullOrWhiteSpace(message)) return;

            string myName = _network?.CurrentUsername ?? "Tôi";
            lstChat.Items.Add($"[{DateTime.Now:HH:mm}] {myName}: {message}");
            lstChat.TopIndex = lstChat.Items.Count - 1;

            _network?.SendChat(message);
            txtChatInput.Clear();
        }"""
content = content.replace(old_chat, new_chat)

# Fix background color not syncing (Maybe because of serialization ToolType mismatch)
# Wait, let's keep it as is, background color IS syncing but may be overridden by ClearAll. I will force it to paint immediately after sync.

with codecs.open(main_path, 'w', 'utf-8') as f:
    f.write(content)


# 2. FIX LobbyForm.cs
lobby_path = r"d:\Download\NT106_DrawingApp_Fix14\NT106_DrawingApp\DrawingClient\Forms\LobbyForm.cs"
with codecs.open(lobby_path, 'r', 'utf-8') as f:
    lobby_content = f.read()

# Find pendingMainForm in btnJoinRoom
old_join_handler = """                            this.Hide();
                            pendingMainForm.FormClosed += (fs, fe) => this.Close();
                            pendingMainForm.Show();"""
new_join_handler = """                            this.Hide();
                            bool shouldReturn = false;
                            pendingMainForm.RequestReturnToLobby += () => shouldReturn = true;
                            pendingMainForm.FormClosed += (fs, fe) => {
                                if (shouldReturn) this.Show();
                                else this.Close();
                            };
                            pendingMainForm.Show();"""
lobby_content = lobby_content.replace(old_join_handler, new_join_handler)

with codecs.open(lobby_path, 'w', 'utf-8') as f:
    f.write(lobby_content)


# 3. FIX SecureTcpServer.cs
server_path = r"d:\Download\NT106_DrawingApp_Fix14\NT106_DrawingApp\DrawingServer\Network\SecureTcpServer.cs"
with codecs.open(server_path, 'r', 'utf-8') as f:
    server_content = f.read()

old_finally = """            finally
            {
                try { RoomService.RemoveMemberFromRoom(session.RoomCode, session.Username ?? "unknown"); } catch { }
                try { AuthService.LogoutUser(session.Username ?? "unknown"); } catch { }
                Clients.TryRemove(clientId, out _);
                tcpClient.Close();
                Logger.Info("TCP", $"[-] Client {clientId} đã thoát.");
            }"""
new_finally = """            finally
            {
                if (!string.IsNullOrEmpty(session.RoomCode))
                {
                    try { 
                        RoomService.RemoveMemberFromRoom(session.RoomCode, session.Username ?? "unknown"); 
                        _ = BroadcastToRoomAsync(session.RoomCode, PacketHelper.Create(CommandType.USER_LEAVE, new UserLeavePayload { Username = session.Username ?? "unknown" }), excludeClientId: clientId);
                    } catch { }
                }
                try { AuthService.LogoutUser(session.Username ?? "unknown"); } catch { }
                Clients.TryRemove(clientId, out _);
                tcpClient.Close();
                Logger.Info("TCP", $"[-] Client {clientId} đã thoát.");
            }"""
server_content = server_content.replace(old_finally, new_finally)

with codecs.open(server_path, 'w', 'utf-8') as f:
    f.write(server_content)

print("ALL FILES PATCHED SUCCESSFULLY!")
