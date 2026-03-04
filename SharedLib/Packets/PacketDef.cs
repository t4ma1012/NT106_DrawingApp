using System;

namespace SharedLib.Packets
{
    // ═══════════════════════════════════════════════════════
    // DANH SÁCH TẤT CẢ LỆNH (COMMAND) CỦA HỆ THỐNG
    // A và C đọc file này để biết gửi/nhận gì
    // ═══════════════════════════════════════════════════════
    public enum CommandType : byte
    {
        // ── Xác thực tài khoản (TCP) ──
        LOGIN = 0x01,
        REGISTER = 0x02,
        LOGIN_RESPONSE = 0x03,

        // ── Quản lý phòng (TCP) ──
        CREATE_ROOM = 0x10,
        CREATE_ROOM_RESPONSE = 0x11,
        JOIN_ROOM = 0x12,
        JOIN_ROOM_RESPONSE = 0x13,
        LEAVE_ROOM = 0x14,

        // ── Thành viên trong phòng (TCP) ──
        ROOM_MEMBERS = 0x20,
        USER_JOIN = 0x21,
        USER_LEAVE = 0x22,

        // ── Vẽ (UDP) ──
        DRAW = 0x30,
        FLOOD_FILL = 0x31,
        TEXT = 0x32,
        SPRAY = 0x33,
        IMPORT_IMAGE = 0x34,
        SET_BACKGROUND = 0x35,

        // ── Đồng bộ (TCP) ──
        SYNC_BOARD = 0x40,
        CANVAS_SIZE = 0x41,

        // ── Undo / Redo (TCP) ──
        UNDO = 0x50,
        REDO = 0x51,

        // ── Chat & Tương tác (TCP/UDP) ──
        CHAT = 0x60,
        REACTION = 0x61,
        CURSOR = 0x62,   // UDP
        LASER = 0x63,   // UDP
        ACTIVITY_LOG = 0x64,

        // ── Claim Area (TCP) ──
        CLAIM_AREA = 0x70,
        RELEASE_AREA = 0x71,
        EXTEND_CLAIM = 0x72,
        AREA_RELEASED = 0x73,

        // ── Tính năng nâng cao (TCP) ──
        SET_TURNBASED = 0x80,
        TURN_CHANGE = 0x81,
        REQUEST_PLAYBACK = 0x82,
        PLAYBACK_RESPONSE = 0x83,

        // ── Gallery (TCP) ──
        SAVE_TO_GALLERY = 0x90,
        GET_GALLERY = 0x91,
        GALLERY_RESPONSE = 0x92,

        // ── Hệ thống ──
        HEARTBEAT = 0xF0,
        DISCONNECT = 0xFF,
    }

    // ═══════════════════════════════════════════════════════
    // CẤU TRÚC GÓI TIN
    // [Header 1 byte][CMD 1 byte][Length 4 byte][Payload N byte]
    // ═══════════════════════════════════════════════════════
    public class Packet
    {
        public const byte HEADER = 0xFF;

        public byte Header { get; set; } = HEADER;
        public CommandType Cmd { get; set; }
        public byte[] Payload { get; set; } = new byte[0];

        // Chuyển Packet thành mảng byte để gửi qua mạng
        public byte[] Serialize()
        {
            int totalLen = 1 + 1 + 4 + Payload.Length;
            byte[] result = new byte[totalLen];
            int i = 0;

            result[i++] = Header;
            result[i++] = (byte)Cmd;

            // Length (4 bytes, big-endian)
            byte[] lenBytes = BitConverter.GetBytes(Payload.Length);
            result[i++] = lenBytes[3];
            result[i++] = lenBytes[2];
            result[i++] = lenBytes[1];
            result[i++] = lenBytes[0];

            Array.Copy(Payload, 0, result, i, Payload.Length);
            return result;
        }

        // Chuyển mảng byte nhận được thành Packet
        public static Packet Deserialize(byte[] data)
        {
            if (data == null || data.Length < 6)
                throw new ArgumentException("Dữ liệu quá ngắn để deserialize");

            if (data[0] != HEADER)
                throw new ArgumentException("Header không hợp lệ");

            int payloadLen = (data[2] << 24) | (data[3] << 16)
                           | (data[4] << 8) | data[5];

            byte[] payload = new byte[payloadLen];
            if (payloadLen > 0)
                Array.Copy(data, 6, payload, 0, payloadLen);

            return new Packet
            {
                Header = data[0],
                Cmd = (CommandType)data[1],
                Payload = payload
            };
        }
    }
}