using System.Collections.Generic;

namespace SharedLib.Payloads
{
    // CMD_CREATE_ROOM
    public class CreateRoomPayload
    {
        public int CanvasWidth { get; set; } = 1280;
        public int CanvasHeight { get; set; } = 720;
    }

    // CMD_CREATE_ROOM_RESPONSE
    public class CreateRoomResponse
    {
        public bool IsSuccess { get; set; }
        public string RoomCode { get; set; } // mã 6 số
        public int CanvasWidth { get; set; }
        public int CanvasHeight { get; set; }
    }

    // CMD_JOIN_ROOM
    public class JoinRoomPayload
    {
        public string RoomCode { get; set; }
        public bool IsSpectator { get; set; } = false;
    }

    // CMD_JOIN_ROOM_RESPONSE
    public class JoinRoomResponse
    {
        public bool IsSuccess { get; set; }
        public string Message { get; set; }
        public string RoomCode { get; set; }
        public int CanvasWidth { get; set; }
        public int CanvasHeight { get; set; }
    }

    // CMD_ROOM_MEMBERS — gửi khi client mới vào phòng
    public class RoomMembersPayload
    {
        public List<MemberInfo> Members { get; set; } = new List<MemberInfo>();
    }

    public class MemberInfo
    {
        public string Username { get; set; }
        public int ColorARGB { get; set; }
        public bool IsSpectator { get; set; }
    }

    // CMD_USER_JOIN / CMD_USER_LEAVE
    public class UserJoinPayload
    {
        public string Username { get; set; }
        public int ColorARGB { get; set; }
    }

    public class UserLeavePayload
    {
        public string Username { get; set; }
    }
}