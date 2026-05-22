import sys
import codecs

server_path = r"d:\Download\NT106_DrawingApp_Fix14\NT106_DrawingApp\DrawingServer\Network\SecureTcpServer.cs"

with codecs.open(server_path, 'r', 'utf-8') as f:
    content = f.read()

bad_send = """        public async Task SendPacketToClientAsync(UserSession session, Packet packet)
        {
            if (session.TcpClient != null && session.TcpClient.Connected)
            {
                try
                {
                    string json = JsonConvert.SerializeObject(packet);
                    byte[] data = Encoding.UTF8.GetBytes(json + "\\n");
                    var stream = session.TcpClient.GetStream();
                    await stream.WriteAsync(data, 0, data.Length);
                }
                catch { }
            }
        }"""

good_send = """        public async Task SendPacketToClientAsync(UserSession session, Packet packet)
        {
            if (session.TcpClient != null && session.TcpClient.Connected)
            {
                try
                {
                    byte[] data = packet.Serialize();
                    byte[] lenBytes = BitConverter.GetBytes(data.Length);
                    if (BitConverter.IsLittleEndian) Array.Reverse(lenBytes);
                    
                    var stream = session.TcpClient.GetStream();
                    await stream.WriteAsync(lenBytes, 0, 4);
                    await stream.WriteAsync(data, 0, data.Length);
                }
                catch (Exception ex) 
                {
                    SharedLib.Logging.Logger.Error("TCP", "Lỗi gửi packet: " + ex.Message);
                }
            }
        }"""

content = content.replace(bad_send, good_send)

bad_broadcast = """        public async Task BroadcastToRoomAsync(string roomCode, Packet packet, string excludeClientId = null)
        {
            if (!_rooms.ContainsKey(roomCode)) return;
            string json = JsonConvert.SerializeObject(packet);
            byte[] data = Encoding.UTF8.GetBytes(json + "\\n");

            foreach (var clientId in _rooms[roomCode].ToList())
            {
                if (clientId == excludeClientId) continue;
                if (_clients.TryGetValue(clientId, out var session) && session.TcpClient != null && session.TcpClient.Connected)
                {
                    try
                    {
                        var stream = session.TcpClient.GetStream();
                        await stream.WriteAsync(data, 0, data.Length);
                    }
                    catch { }
                }
            }
        }"""

good_broadcast = """        public async Task BroadcastToRoomAsync(string roomCode, Packet packet, string excludeClientId = null)
        {
            if (!_rooms.ContainsKey(roomCode)) return;
            
            byte[] data = packet.Serialize();
            byte[] lenBytes = BitConverter.GetBytes(data.Length);
            if (BitConverter.IsLittleEndian) Array.Reverse(lenBytes);

            foreach (var clientId in _rooms[roomCode].ToList())
            {
                if (clientId == excludeClientId) continue;
                if (_clients.TryGetValue(clientId, out var session) && session.TcpClient != null && session.TcpClient.Connected)
                {
                    try
                    {
                        var stream = session.TcpClient.GetStream();
                        await stream.WriteAsync(lenBytes, 0, 4);
                        await stream.WriteAsync(data, 0, data.Length);
                    }
                    catch (Exception ex) 
                    {
                        SharedLib.Logging.Logger.Error("TCP", "Lỗi broadcast: " + ex.Message);
                    }
                }
            }
        }"""

content = content.replace(bad_broadcast, good_broadcast)

with codecs.open(server_path, 'w', 'utf-8') as f:
    f.write(content)

print("PATCH SECURE_TCP_SERVER DONE")
