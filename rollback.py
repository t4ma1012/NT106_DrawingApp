import sys
import codecs

server_path = r"d:\Download\NT106_DrawingApp_Fix14\NT106_DrawingApp\DrawingServer\Network\SecureTcpServer.cs"
with codecs.open(server_path, 'r', 'utf-8') as f:
    server_content = f.read()

bad_send_packet = """        private async Task SendPacketToClientAsync(ClientSession client, Packet packet)
        {
            if (client?.SecureStream == null) return;
            byte[] data = packet.Serialize();
            byte[] encrypted = SharedLib.Security.AesHelper.Encrypt(data);
            byte[] lenBytes = BitConverter.GetBytes(encrypted.Length);
            if (BitConverter.IsLittleEndian) Array.Reverse(lenBytes);

            await client.WriteLock.WaitAsync();
            try
            {
                await client.SecureStream.WriteAsync(lenBytes, 0, 4);
                await client.SecureStream.WriteAsync(encrypted, 0, encrypted.Length);
            }"""

good_send_packet = """        private async Task SendPacketToClientAsync(ClientSession client, Packet packet)
        {
            if (client?.SecureStream == null) return;
            byte[] data = packet.Serialize();
            byte[] lenBytes = BitConverter.GetBytes(data.Length);
            if (BitConverter.IsLittleEndian) Array.Reverse(lenBytes);

            await client.WriteLock.WaitAsync();
            try
            {
                await client.SecureStream.WriteAsync(lenBytes, 0, 4);
                await client.SecureStream.WriteAsync(data, 0, data.Length);
            }"""

server_content = server_content.replace(bad_send_packet, good_send_packet)

with codecs.open(server_path, 'w', 'utf-8') as f:
    f.write(server_content)

print("ROLLBACK DONE!")
