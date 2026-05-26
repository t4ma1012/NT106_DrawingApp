using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DrawingServer.Database;
using DrawingServer.Network;
using SharedLib.Config;
using SharedLib.Logging;
using SharedLib.Payloads;

namespace DrawingServer.Services
{
    public static class RoomService
    {
        private static readonly Dictionary<string, RoomState> ActiveRooms = new Dictionary<string, RoomState>(StringComparer.OrdinalIgnoreCase);
        private static readonly object SyncRoot = new object();

        public class RoomState
        {
            public string RoomCode { get; set; } = "";
            public string OwnerId { get; set; } = "";
            public List<DrawingServer.Network.ClientSession> Members { get; set; } = new List<DrawingServer.Network.ClientSession>();
            public int CanvasWidth { get; set; } = 1920;
            public int CanvasHeight { get; set; } = 1080;
            public DateTime CreatedTime { get; set; } = DateTime.UtcNow;
            public bool IsActive { get; set; } = true;
            public bool IsTurnBasedEnabled { get; set; }
            public string ActiveDrawingUser { get; set; } = "";
            public int MaxMembers { get; set; } = GetDefaultMaxMembers();
        }

        public static int GetDefaultMaxMembers()
        {
            return Math.Max(1, EnvLoader.GetInt("MAX_ROOM_MEMBERS", 5));
        }

        public static async Task<(bool Success, string RoomCode, string Message)> CreateRoomAsync(string ownerUsername, int canvasWidth = 1920, int canvasHeight = 1080)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(ownerUsername))
                    return (false, "", "Invalid owner username");

                // KET NOI DU LIEU/BAT DONG BO: tao phong trong PostgreSQL truoc khi cap nhat room state RAM.
                string roomCode = await DbManager.CreateRoomAsync(ownerUsername, canvasWidth, canvasHeight);
                if (string.IsNullOrEmpty(roomCode))
                    return (false, "", "Failed to create room in database");

                // XU LY DA LUONG: khoa ActiveRooms de nhieu client tao/join room khong sua dictionary cung luc.
                lock (SyncRoot)
                {
                    if (!ActiveRooms.ContainsKey(roomCode))
                    {
                        ActiveRooms[roomCode] = new RoomState
                        {
                            RoomCode = roomCode,
                            OwnerId = ownerUsername,
                            CanvasWidth = canvasWidth,
                            CanvasHeight = canvasHeight,
                            CreatedTime = DateTime.UtcNow,
                            IsActive = true,
                            MaxMembers = GetDefaultMaxMembers()
                        };
                    }
                }

                Logger.Info("Room", $"Created room {roomCode} by {ownerUsername}");
                return (true, roomCode, "Room created successfully");
            }
            catch (Exception ex)
            {
                Logger.Error("Room", $"Error creating room: {ex.Message}");
                return (false, "", ex.Message);
            }
        }

        public static async Task<(bool Success, string Message)> TryAddMemberToRoomAsync(string roomCode, DrawingServer.Network.ClientSession clientSession)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(roomCode) || clientSession == null || string.IsNullOrWhiteSpace(clientSession.Username))
                    return (false, "Invalid join request");

                // KET NOI DU LIEU/BAT DONG BO: check room ton tai trong PostgreSQL truoc khi them member.
                bool roomExists = await DbManager.CheckRoomExistsAsync(roomCode);
                if (!roomExists)
                    return (false, "Room does not exist");

                string ownerUsername = string.Empty;
                bool createNewRoomState = false;

                // XU LY DA LUONG: doc/tao RoomState trong RAM duoc bao ve bang lock.
                lock (SyncRoot)
                {
                    if (!ActiveRooms.ContainsKey(roomCode))
                        createNewRoomState = true;
                }

                if (createNewRoomState)
                    ownerUsername = await DbManager.GetRoomOwnerUsernameAsync(roomCode);

                lock (SyncRoot)
                {
                    if (!ActiveRooms.ContainsKey(roomCode))
                    {
                        ActiveRooms[roomCode] = new RoomState
                        {
                            RoomCode = roomCode,
                            OwnerId = ownerUsername,
                            IsActive = true,
                            MaxMembers = GetDefaultMaxMembers()
                        };
                    }

                    RoomState room = ActiveRooms[roomCode];
                    if (room.Members.Any(m => string.Equals(m.Username, clientSession.Username, StringComparison.OrdinalIgnoreCase)))
                    {
                        clientSession.RoomCode = roomCode;
                        return (true, "Already in room");
                    }

                    if (room.Members.Count >= room.MaxMembers)
                    {
                        return (false, $"Room is full ({room.MaxMembers} members)");
                    }

                    clientSession.RoomCode = roomCode;
                    room.Members.Add(clientSession);
                    room.IsActive = true;
                    Logger.Info("Room", $"Added {clientSession.Username} to room {roomCode} ({room.Members.Count}/{room.MaxMembers})");
                    return (true, "Joined");
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Room", $"Error adding member to room: {ex.Message}");
                return (false, ex.Message);
            }
        }

        public static bool RemoveMemberFromRoom(string roomCode, string username)
        {
            try
            {
                lock (SyncRoot)
                {
                    if (!ActiveRooms.TryGetValue(roomCode, out RoomState room))
                        return false;

                    var member = room.Members.FirstOrDefault(m => string.Equals(m.Username, username, StringComparison.OrdinalIgnoreCase));
                    if (member == null)
                        return false;

                    room.Members.Remove(member);
                    if (room.Members.Count == 0)
                        room.IsActive = false;

                    Logger.Info("Room", $"Removed {username} from room {roomCode} ({room.Members.Count}/{room.MaxMembers})");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Room", $"Error removing member: {ex.Message}");
                return false;
            }
        }

        public static bool TryAdvanceTurn(string roomCode, string requestedBy, out string activeUser, out string message)
        {
            activeUser = string.Empty;
            message = string.Empty;

            lock (SyncRoot)
            {
                if (!ActiveRooms.TryGetValue(roomCode, out RoomState room))
                {
                    message = "Room not found";
                    return false;
                }

                if (!room.IsTurnBasedEnabled)
                {
                    message = "Turn-based is disabled";
                    return false;
                }

                if (!string.Equals(room.ActiveDrawingUser, requestedBy, StringComparison.OrdinalIgnoreCase))
                {
                    message = "Only the active turn user can change turns";
                    return false;
                }

                activeUser = SelectNextTurnUser(room, room.ActiveDrawingUser);
                if (string.IsNullOrWhiteSpace(activeUser))
                {
                    message = "No eligible member found";
                    return false;
                }

                room.ActiveDrawingUser = activeUser;
                return true;
            }
        }

        public static bool TryAdvanceTurnAfterMemberRemoval(string roomCode, string removedUsername, out string activeUser, out bool turnChanged, out string message)
        {
            activeUser = string.Empty;
            turnChanged = false;
            message = string.Empty;

            lock (SyncRoot)
            {
                if (!ActiveRooms.TryGetValue(roomCode, out RoomState room))
                {
                    message = "Room not found";
                    return false;
                }

                if (!room.IsTurnBasedEnabled)
                    return true;

                if (!string.Equals(room.ActiveDrawingUser, removedUsername, StringComparison.OrdinalIgnoreCase))
                    return true;

                activeUser = SelectNextTurnUser(room, removedUsername);
                if (string.IsNullOrWhiteSpace(activeUser))
                {
                    room.ActiveDrawingUser = string.Empty;
                    return true;
                }

                room.ActiveDrawingUser = activeUser;
                turnChanged = true;
                return true;
            }
        }

        private static string SelectNextTurnUser(RoomState room, string currentActiveUser)
        {
            if (room == null || room.Members.Count == 0)
                return string.Empty;

            var members = room.Members
                .Where(member => member != null && !string.IsNullOrWhiteSpace(member.Username))
                .ToList();

            if (members.Count == 0)
                return string.Empty;

            int currentIndex = members.FindIndex(member => string.Equals(member.Username, currentActiveUser, StringComparison.OrdinalIgnoreCase));
            for (int offset = 1; offset <= members.Count; offset++)
            {
                string candidate = members[(currentIndex + offset) % members.Count]?.Username ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(candidate))
                    return candidate;
            }

            return members[0]?.Username ?? string.Empty;
        }

        public static RoomState? GetRoomState(string roomCode)
        {
            lock (SyncRoot)
            {
                ActiveRooms.TryGetValue(roomCode, out RoomState room);
                return room;
            }
        }

        public static bool IsRoomActive(string roomCode)
        {
            lock (SyncRoot)
            {
                if (!ActiveRooms.TryGetValue(roomCode, out RoomState room))
                    return false;
                return room.IsActive && room.Members.Count > 0;
            }
        }

        public static int GetActiveRoomsCount()
        {
            lock (SyncRoot)
            {
                return ActiveRooms.Count(r => r.Value.IsActive);
            }
        }

        public static int GetRoomMemberCount(string roomCode)
        {
            lock (SyncRoot)
            {
                return ActiveRooms.TryGetValue(roomCode, out RoomState room) ? room.Members.Count : 0;
            }
        }

        public static List<(string Username, string Color, bool IsOnline)> GetRoomMembers(string roomCode)
        {
            var members = new List<(string, string, bool)>();
            lock (SyncRoot)
            {
                if (!ActiveRooms.TryGetValue(roomCode, out RoomState room))
                    return members;

                foreach (DrawingServer.Network.ClientSession clientSession in room.Members)
                {
                    string username = clientSession.Username ?? "";
                    var userSession = AuthService.GetUserSession(username);
                    if (userSession != null)
                        members.Add((username, userSession.AssignedColor, true));
                    else
                        members.Add((username, clientSession.AssignedColor, true));
                }
            }
            return members;
        }

        public static List<MemberInfo> GetRoomMembersInfo(string roomCode)
        {
            var infos = new List<MemberInfo>();
            lock (SyncRoot)
            {
                if (!ActiveRooms.TryGetValue(roomCode, out RoomState room))
                    return infos;

                foreach (DrawingServer.Network.ClientSession client in room.Members)
                {
                    var authSession = AuthService.GetUserSession(client.Username ?? "");
                    int colorArgb = 0;
                    if (authSession != null && !string.IsNullOrWhiteSpace(authSession.AssignedColor))
                    {
                        int.TryParse(authSession.AssignedColor.TrimStart('#'), System.Globalization.NumberStyles.HexNumber, null, out colorArgb);
                    }

                    infos.Add(new MemberInfo
                    {
                        Username = client.Username,
                        ColorARGB = colorArgb,
                        IsSpectator = false,
                        IsOnline = true
                    });
                }
            }
            return infos;
        }
    }
}
