import sys
import codecs

# 1. FIX MainForm.cs (UDP Port 8889)
main_path = r"d:\Download\NT106_DrawingApp_Fix14\NT106_DrawingApp\DrawingClient\Forms\MainForm.cs"
with codecs.open(main_path, 'r', 'utf-8') as f:
    main_content = f.read()

main_content = main_content.replace('_udpManager = new UdpManager("127.0.0.1", 0);', '_udpManager = new UdpManager("127.0.0.1", 8889);')

with codecs.open(main_path, 'w', 'utf-8') as f:
    f.write(main_content)

# 2. FIX SecureTcpServer.cs (Missing AES Encryption)
server_path = r"d:\Download\NT106_DrawingApp_Fix14\NT106_DrawingApp\DrawingServer\Network\SecureTcpServer.cs"
with codecs.open(server_path, 'r', 'utf-8') as f:
    server_content = f.read()

old_send_packet = """        private async Task SendPacketToClientAsync(ClientSession client, Packet packet)
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

new_send_packet = """        private async Task SendPacketToClientAsync(ClientSession client, Packet packet)
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

server_content = server_content.replace(old_send_packet, new_send_packet)

with codecs.open(server_path, 'w', 'utf-8') as f:
    f.write(server_content)

print("PATCHED BOTH FILES!")
