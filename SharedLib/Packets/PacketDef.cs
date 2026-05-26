// ============================================================
// SharedLib/Packets/PacketDef.cs
// PacketDef v6 FINAL � Person B (Network)
// Bao g?m t?t c? CommandType t? Tu?n 1 ? Tu?n 6
// ============================================================
using System;
using System.Text;

namespace SharedLib.Packets
{
    /// <summary>
    /// To�n b? command types cho giao th?c NT106 Drawing App.
    /// TCP: Auth, Room, Sync, Undo, Chat, Gallery, Security, AI
    /// UDP: Draw, Cursor, Reaction, Spotlight, PixelArt
    /// </summary>
    public enum CommandType : byte
    {
        // -- AUTH (TCP) ------------------------------------------
        LOGIN = 0x01,
        REGISTER = 0x02,
        LOGIN_RESPONSE = 0x03,
        REGISTER_RESPONSE = 0x04,

        // -- ROOM MANAGEMENT (TCP) -------------------------------
        CREATE_ROOM = 0x10,
        CREATE_ROOM_RESPONSE = 0x11,
        JOIN_ROOM = 0x12,
        JOIN_ROOM_RESPONSE = 0x13,
        LEAVE_ROOM = 0x14,

        // -- ROOM INFO (TCP broadcast) ---------------------------
        ROOM_MEMBERS = 0x20,
        USER_JOIN = 0x21,
        USER_LEAVE = 0x22,

        // -- DRAWING (UDP) ---------------------------------------
        DRAW = 0x30,
        FLOOD_FILL = 0x31,
        TEXT = 0x32,
        SPRAY = 0x33,
        IMPORT_IMAGE = 0x34,
        SET_BACKGROUND = 0x35,
        CLEAR_ALL = 0x36,     // Tu?n 1 � broadcast x�a canvas

        // -- SYNC (TCP) ------------------------------------------
        SYNC_BOARD = 0x40,
        CANVAS_SIZE = 0x41,

        // -- UNDO / REDO (TCP) -----------------------------------
        UNDO = 0x50,
        REDO = 0x51,

        // -- INTERACTION (TCP/UDP) -------------------------------
        CHAT = 0x60,     // TCP
        REACTION = 0x61,     // UDP
        CURSOR = 0x62,     // UDP/TCP real-time
        LASER = 0x63,     // legacy disabled
        ACTIVITY_LOG = 0x64,     // TCP
        UDP_PING = 0x65,     // UDP endpoint registration
        // -- FEATURES (TCP) --------------------------------------
        SET_TURNBASED = 0x80,
        TURN_CHANGE = 0x81,
        REQUEST_PLAYBACK = 0x82,
        PLAYBACK_RESPONSE = 0x83,

        // -- GALLERY (TCP) ---------------------------------------
        SAVE_TO_GALLERY = 0x90,
        GET_GALLERY = 0x91,
        GALLERY_RESPONSE = 0x92,
        PUBLIC_GALLERY_LINK = 0x93,     // Tu?n 7 � public link

        // -- AI FEATURES (TCP) -- Tu?n 5-6 ----------------------
        AI_TEXT_TO_IMAGE = 0xA0,     // Tu?n 5
        AI_BG_REMOVED = 0xA1,     // Tu?n 5

        // -- ADVANCED FEATURES (TCP/UDP) -- Tu?n 5-8 ------------
        STICKER = 0xB0,     // Tu?n 5 � Sticker & Shape Library
        FOLLOW_MODE = 0xB1,     // Tu?n 5 � Follow another user
        SPOTLIGHT = 0xB2,     // Tu?n 5 � UDP
        STICKY_NOTE = 0xB3,     // Tu?n 5 � Sticky note/comment
        STICKY_NOTE_REPLY = 0xB4,     // Tu?n 5
        TIMELINE_REQUEST = 0xB7,     // Tu?n 6 � Time travel
        TIMELINE_RESPONSE = 0xB8,     // Tu?n 6

        // -- PIXEL ART (TCP/UDP) --------------------------------
        PIXEL_ART_DRAW = 0xC3,     // UDP
        PIXEL_ART_SYNC = 0xC4,     // TCP

        // -- SYSTEM ----------------------------------------------
        HEARTBEAT = 0xF0,
        DISCONNECT = 0xFF
    }

    /// <summary>
    /// C?u tr�c packet: [Header=0xFF(1B)] [Cmd(1B)] [Length(4B, big-endian)] [Payload(N bytes, UTF-8 JSON)]
    /// </summary>
    public class Packet
    {
        public const byte HEADER_BYTE = 0xFF;

        public byte Header { get; set; } = HEADER_BYTE;
        public CommandType Cmd { get; set; }
        public byte[] Payload { get; set; } = Array.Empty<byte>();

        /// <summary>Chuy?n Packet th�nh byte[] d? g?i qua socket.</summary>
        public byte[] Serialize()
        {
            int payloadLen = Payload?.Length ?? 0;
            // Header(1) + Cmd(1) + Length(4) + Payload
            byte[] result = new byte[6 + payloadLen];
            result[0] = HEADER_BYTE;
            result[1] = (byte)Cmd;
            // Length big-endian
            result[2] = (byte)((payloadLen >> 24) & 0xFF);
            result[3] = (byte)((payloadLen >> 16) & 0xFF);
            result[4] = (byte)((payloadLen >> 8) & 0xFF);
            result[5] = (byte)(payloadLen & 0xFF);
            if (payloadLen > 0)
                Buffer.BlockCopy(Payload, 0, result, 6, payloadLen);
            return result;
        }

        /// <summary>Ph�n t�ch byte[] nh?n t? socket th�nh Packet.</summary>
        public static Packet Deserialize(byte[] data)
        {
            if (data == null || data.Length < 6)
                throw new ArgumentException("D? li?u packet qu� ng?n.");
            if (data[0] != HEADER_BYTE)
                throw new ArgumentException($"Header kh�ng h?p l?: 0x{data[0]:X2}");

            int payloadLen = (data[2] << 24) | (data[3] << 16) | (data[4] << 8) | data[5];
            if (data.Length < 6 + payloadLen)
                throw new ArgumentException("Payload b? c?t ng?n.");

            byte[] payload = new byte[payloadLen];
            if (payloadLen > 0)
                Buffer.BlockCopy(data, 6, payload, 0, payloadLen);

            return new Packet
            {
                Header = data[0],
                Cmd = (CommandType)data[1],
                Payload = payload
            };
        }

        public override string ToString()
            => $"Packet[{Cmd}] {Payload?.Length ?? 0} bytes";
    }
}
