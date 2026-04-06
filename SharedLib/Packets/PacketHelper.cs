// ============================================================
// SharedLib/Packets/PacketHelper.cs
// Person B (Network) — helper tiện ích tạo/đọc packet
// ============================================================
using System;
using System.Text;
using Newtonsoft.Json;

namespace SharedLib.Packets
{
    public static class PacketHelper
    {
        private static readonly JsonSerializerSettings _settings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            DateFormatHandling = DateFormatHandling.IsoDateFormat
        };

        /// <summary>Tạo Packet từ CommandType + object payload (serialize JSON).</summary>
        public static Packet Create(CommandType cmd, object payload)
        {
            string json = JsonConvert.SerializeObject(payload, _settings);
            return new Packet
            {
                Cmd = cmd,
                Payload = Encoding.UTF8.GetBytes(json)
            };
        }

        /// <summary>Tạo Packet không có payload.</summary>
        public static Packet CreateEmpty(CommandType cmd)
            => new Packet { Cmd = cmd, Payload = Array.Empty<byte>() };

        /// <summary>Đọc payload của Packet thành object kiểu T.</summary>
        public static T GetPayload<T>(Packet packet)
        {
            if (packet.Payload == null || packet.Payload.Length == 0)
                return default;
            string json = Encoding.UTF8.GetString(packet.Payload);
            return JsonConvert.DeserializeObject<T>(json, _settings);
        }

        /// <summary>Đọc payload thành string JSON (debug).</summary>
        public static string GetRawJson(Packet packet)
            => packet.Payload == null ? "" : Encoding.UTF8.GetString(packet.Payload);
    }
}
