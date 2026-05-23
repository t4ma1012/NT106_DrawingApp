// ============================================================
// SharedLib/Payloads/RoomPayload.cs
// ĐÃ FIX: Thêm UdpPort vào JoinRoomPayload để Server biết
//          endpoint UDP của Client để broadcast nét vẽ về
// ============================================================
using System.Collections.Generic;

namespace SharedLib.Payloads
{
    public class CreateRoomPayload
    {
        public int CanvasWidth { get; set; } = 1280;
        public int CanvasHeight { get; set; } = 720;
    }

    public class CreateRoomResponse
    {
        public bool IsSuccess { get; set; }
        public string RoomCode { get; set; }
        public int CanvasWidth { get; set; }
        public int CanvasHeight { get; set; }
        public string Message { get; set; }
    }

    public class JoinRoomPayload
    {
        public string RoomCode { get; set; }
        public bool IsSpectator { get; set; } = false;

        // ✅ FIX: Client gửi port UDP của mình để Server lưu lại
        //        dùng để broadcast nét vẽ realtime qua UDP
        public int UdpPort { get; set; } = 0;
    }

    public class JoinRoomResponse
    {
        public bool IsSuccess { get; set; }
        public string Message { get; set; }
        public string RoomCode { get; set; }
        public int CanvasWidth { get; set; }
        public int CanvasHeight { get; set; }
        public int AssignedColorARGB { get; set; }
    }

    public class RoomMembersPayload
    {
        public string RoomCode { get; set; }
        public List<MemberInfo> Members { get; set; } = new List<MemberInfo>();
    }

    public class MemberInfo
    {
        public string Username { get; set; }
        public int ColorARGB { get; set; }
        public bool IsSpectator { get; set; }
        public bool IsOnline { get; set; } = true;
    }

    public class UserJoinPayload
    {
        public string Username { get; set; }
        public int ColorARGB { get; set; }
        public bool IsSpectator { get; set; }
    }

    public class UserLeavePayload
    {
        public string Username { get; set; }
    }

    public class CanvasSizePayload
    {
        public int Width { get; set; }
        public int Height { get; set; }
    }
}
