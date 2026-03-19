using System;
using SharedLib.Payloads;

namespace DrawingClient.Network
{
	// ═══════════════════════════════════════════════════════
	// TRUNG TÂM SỰ KIỆN MẠNG
	// Người A đăng ký các event này để nhận dữ liệu từ mạng
	// Người B raise các event này khi nhận được packet
	// ═══════════════════════════════════════════════════════
	public static class NetworkEvents
	{
		// ── UDP Events ──

		// Nhận nét vẽ từ người khác (CMD_DRAW)
		public static event Action<DrawPayload> OnDrawReceived;

		// Nhận flood fill từ người khác (CMD_FLOOD_FILL)
		public static event Action<DrawPayload> OnFloodFillReceived;

		// Nhận text tool từ người khác (CMD_TEXT)
		public static event Action<DrawPayload> OnTextReceived;

		// Nhận laser pointer từ người khác (CMD_LASER)
		public static event Action<DrawPayload> OnLaserReceived;

		// Nhận reaction emoji từ người khác (CMD_REACTION)
		public static event Action<DrawPayload> OnReactionReceived;

		// ── TCP Events ──

		// Có người mới vào phòng (CMD_USER_JOIN)
		public static event Action<string> OnUserJoined;       // username

		// Có người rời phòng (CMD_USER_LEAVE)
		public static event Action<string> OnUserLeft;         // username

		// Nhận danh sách thành viên hiện tại (CMD_ROOM_MEMBERS)
		public static event Action<string[]> OnRoomMembersReceived;

		// Nhận kích thước canvas từ server (CMD_CANVAS_SIZE)
		public static event Action<int, int> OnCanvasSizeReceived; // width, height

		// ── Raise Methods (Người B gọi nội bộ) ──

		public static void RaiseDrawReceived(DrawPayload p)
			=> OnDrawReceived?.Invoke(p);

		public static void RaiseFloodFillReceived(DrawPayload p)
			=> OnFloodFillReceived?.Invoke(p);

		public static void RaiseTextReceived(DrawPayload p)
			=> OnTextReceived?.Invoke(p);

		public static void RaiseLaserReceived(DrawPayload p)
			=> OnLaserReceived?.Invoke(p);

		public static void RaiseReactionReceived(DrawPayload p)
			=> OnReactionReceived?.Invoke(p);

		public static void RaiseUserJoined(string username)
			=> OnUserJoined?.Invoke(username);

		public static void RaiseUserLeft(string username)
			=> OnUserLeft?.Invoke(username);

		public static void RaiseRoomMembersReceived(string[] members)
			=> OnRoomMembersReceived?.Invoke(members);

		public static void RaiseCanvasSizeReceived(int width, int height)
			=> OnCanvasSizeReceived?.Invoke(width, height);
	}
}