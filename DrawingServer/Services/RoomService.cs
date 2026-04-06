// ============================================================
// DrawingServer/Services/RoomService.cs
// Person C (Server) — Room Management Service
// Handles room lifecycle and member management
// ============================================================
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DrawingServer.Database;
using DrawingServer.Network;
using SharedLib.Payloads;
using SharedLib.Logging;

namespace DrawingServer.Services
{
    public static class RoomService
    {
        private static Dictionary<string, RoomState> _activeRooms = new Dictionary<string, RoomState>();

        public class RoomState
        {
            public string RoomCode { get; set; }
            public string OwnerId { get; set; }

            // Ép buộc dùng đúng ClientSession "hàng real" của Network
            public List<DrawingServer.Network.ClientSession> Members { get; set; } = new List<DrawingServer.Network.ClientSession>();

            public int CanvasWidth { get; set; } = 1280;
            public int CanvasHeight { get; set; } = 720;
            public DateTime CreatedTime { get; set; }
            public bool IsActive { get; set; } = true;
        }

        public static async Task<(bool Success, string RoomCode, string Message)> CreateRoomAsync(string ownerUsername, int canvasWidth = 1280, int canvasHeight = 720)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(ownerUsername))
                    return (false, "", "Invalid owner username");

                string roomCode = await DbManager.CreateRoomAsync(ownerUsername, canvasWidth, canvasHeight);

                if (string.IsNullOrEmpty(roomCode))
                    return (false, "", "Failed to create room in database");

                lock (_activeRooms)
                {
                    if (!_activeRooms.ContainsKey(roomCode))
                    {
                        _activeRooms[roomCode] = new RoomState
                        {
                            RoomCode = roomCode,
                            OwnerId = ownerUsername,
                            CanvasWidth = canvasWidth,
                            CanvasHeight = canvasHeight,
                            CreatedTime = DateTime.Now,
                            IsActive = true
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

        // Ép buộc tham số truyền vào phải là "hàng real"
        public static async Task<bool> AddMemberToRoomAsync(string roomCode, DrawingServer.Network.ClientSession clientSession)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(roomCode) || clientSession == null)
                    return false;

                bool roomExists = await DbManager.CheckRoomExistsAsync(roomCode);
                if (!roomExists)
                {
                    Logger.Warning("Room", $"Room {roomCode} does not exist in database");
                    return false;
                }

                lock (_activeRooms)
                {
                    if (!_activeRooms.ContainsKey(roomCode))
                    {
                        _activeRooms[roomCode] = new RoomState
                        {
                            RoomCode = roomCode,
                            IsActive = true
                        };
                    }

                    var room = _activeRooms[roomCode];
                    if (!room.Members.Any(m => m.Username == clientSession.Username))
                    {
                        clientSession.RoomCode = roomCode; // Sẽ không còn lỗi đỏ ở đây nữa
                        room.Members.Add(clientSession);
                        Logger.Info("Room", $"Added {clientSession.Username} to room {roomCode} ({room.Members.Count} members)");
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                Logger.Error("Room", $"Error adding member to room: {ex.Message}");
                return false;
            }
        }

        public static bool RemoveMemberFromRoom(string roomCode, string username)
        {
            try
            {
                lock (_activeRooms)
                {
                    if (!_activeRooms.ContainsKey(roomCode))
                        return false;

                    var room = _activeRooms[roomCode];
                    var member = room.Members.FirstOrDefault(m => m.Username == username);
                    if (member != null)
                    {
                        room.Members.Remove(member);
                        Logger.Info("Room", $"Removed {username} from room {roomCode} ({room.Members.Count} members left)");

                        if (room.Members.Count == 0)
                        {
                            room.IsActive = false;
                        }
                        return true;
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                Logger.Error("Room", $"Error removing member: {ex.Message}");
                return false;
            }
        }

        public static List<(string Username, string Color, bool IsOnline)> GetRoomMembers(string roomCode)
        {
            var members = new List<(string, string, bool)>();

            lock (_activeRooms)
            {
                if (!_activeRooms.ContainsKey(roomCode))
                    return members;

                var room = _activeRooms[roomCode];
                foreach (var clientSession in room.Members)
                {
                    var userSession = AuthService.GetUserSession(clientSession.Username);
                    if (userSession != null)
                    {
                        members.Add((clientSession.Username, userSession.AssignedColor, true));
                    }
                    else
                    {
                        members.Add((clientSession.Username, clientSession.AssignedColor, true));
                    }
                }
            }

            return members;
        }

        public static RoomState GetRoomState(string roomCode)
        {
            lock (_activeRooms)
            {
                if (_activeRooms.TryGetValue(roomCode, out var room))
                    return room;
            }
            return null;
        }

        public static bool IsRoomActive(string roomCode)
        {
            lock (_activeRooms)
            {
                if (_activeRooms.TryGetValue(roomCode, out var room))
                    return room.IsActive && room.Members.Count > 0;
            }
            return false;
        }

        public static int GetActiveRoomsCount()
        {
            lock (_activeRooms)
            {
                return _activeRooms.Count(r => r.Value.IsActive);
            }
        }

        public static int GetRoomMemberCount(string roomCode)
        {
            lock (_activeRooms)
            {
                if (_activeRooms.TryGetValue(roomCode, out var room))
                    return room.Members.Count;
            }
            return 0;
        }

        public static async Task BroadcastToRoomAsync(string roomCode, byte[] encryptedData)
        {
            lock (_activeRooms)
            {
                if (!_activeRooms.TryGetValue(roomCode, out var room))
                    return;

                foreach (var member in room.Members)
                {
                    if (member.UdpEndPoint != null)
                    {
                        try
                        {
                            _ = Task.Run(async () =>
                            {
                                try
                                {
                                    var udpClient = new System.Net.Sockets.UdpClient();
                                    await udpClient.SendAsync(encryptedData, encryptedData.Length, member.UdpEndPoint);
                                    udpClient.Close();
                                }
                                catch { /* ignore send errors */ }
                            });
                        }
                        catch { /* ignore */ }
                    }
                }
            }
        }

        public static List<MemberInfo> GetRoomMembersInfo(string roomCode)
        {
            var infos = new List<MemberInfo>();

            lock (_activeRooms)
            {
                if (!_activeRooms.TryGetValue(roomCode, out var room))
                    return infos;

                foreach (var client in room.Members)
                {
                    var authSession = AuthService.GetUserSession(client.Username);
                    if (authSession != null)
                    {
                        int colorARGB = int.Parse(authSession.AssignedColor.TrimStart('#'), System.Globalization.NumberStyles.HexNumber);

                        infos.Add(new MemberInfo
                        {
                            Username = client.Username,
                            ColorARGB = colorARGB,
                            IsSpectator = false,
                            IsOnline = true
                        });
                    }
                }
            }

            return infos;
        }
    }
}