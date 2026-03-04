using Newtonsoft.Json;
using System.Text;

namespace SharedLib.Packets
{
    // Helper để tạo Packet từ object và ngược lại
    public static class PacketHelper
    {
        // Tạo Packet từ bất kỳ object payload nào
        public static Packet Create(CommandType cmd, object payload)
        {
            string json = JsonConvert.SerializeObject(payload);
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            return new Packet { Cmd = cmd, Payload = bytes };
        }

        // Lấy payload ra từ Packet
        public static T GetPayload<T>(Packet packet)
        {
            string json = Encoding.UTF8.GetString(packet.Payload);
            return JsonConvert.DeserializeObject<T>(json);
        }

        // Tạo Packet không có payload (heartbeat, disconnect...)
        public static Packet CreateEmpty(CommandType cmd)
        {
            return new Packet { Cmd = cmd, Payload = new byte[0] };
        }
    }
}
