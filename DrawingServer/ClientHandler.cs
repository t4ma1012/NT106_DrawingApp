using System;
using System.Net.Sockets;
using System.Text;

namespace DrawingServer
{
    public class ClientHandler
    {
        private TcpClient _client;

        public ClientHandler(TcpClient client)
        {
            _client = client;
        }

        public void HandleClient()
        {
            NetworkStream stream = _client.GetStream();
            byte[] buffer = new byte[1024];
            int bytesRead;

            try
            {
                // Nhận -> in console -> gửi lại (Echo)
                while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) != 0)
                {
                    string receivedData = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    Console.WriteLine($"[Nhận từ {_client.Client.RemoteEndPoint}]: {receivedData}");

                    // Echo gửi lại đúng những gì đã nhận
                    stream.Write(buffer, 0, bytesRead);
                }
            }
            catch (Exception ex)
            {
                // Disconnect handler: try-catch in log, không crash 
                Console.WriteLine($"[-] Client ngat ket noi hoac loi: {ex.Message}");
            }
            finally
            {
                _client.Close();
                Console.WriteLine($"[-] Da dong ket noi voi client.");
            }
        }
    }
}