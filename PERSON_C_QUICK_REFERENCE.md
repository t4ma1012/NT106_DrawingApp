# 🎯 PERSON C - QUICK REFERENCE GUIDE

## 📋 FILES YOU CREATED/MODIFIED

### ✨ NEW FILES (3 Service Classes)

| File | Lines | Location | Purpose |
|------|-------|----------|---------|
| **AuthService.cs** | 200+ | `DrawingServer/Services/` | User auth & sessions |
| **DrawService.cs** | 180+ | `DrawingServer/Services/` | Save/retrieve drawings |
| **RoomService.cs** | 280+ | `DrawingServer/Services/` | Room management |

### 🔧 EXTENDED DATABASE

| File | Change | Location |
|------|--------|----------|
| **DbManager.cs** | +10 methods | `DrawingServer/Services/Database/` |
| **database_setup.sql** | New | `DrawingServer/` |

### ⚡ EXPANDED NETWORK

| File | Change | Location |
|------|--------|----------|
| **SecureTcpServer.cs** | +10 handlers | `DrawingServer/Network/` |
| **GalleryPayload.cs** | +1 class | `SharedLib/Payloads/` |

---

## 🚀 QUICK START

### 1. Set Up Database (First Time Only)
```bash
cd DrawingServer
psql -U postgres < database_setup.sql
```

### 2. Compile
```bash
cd ..
dotnet build
# ✅ Should have 0 errors
```

### 3. Run Server
```bash
cd DrawingServer
dotnet run
# Output: "TCP Server running on 8888..."
#         "UDP Server running on 8889..."
```

### 4. Run Client (Another Terminal)
```bash
cd DrawingClient
dotnet run
# WinForms window opens
```

---

## 🎓 KEY CLASSES & METHODS

### AuthService - User Management
```csharp
// Authenticate user (auto-register)
var (success, userId, username, msg) = 
    await AuthService.AuthenticateUserAsync("alice", "hash123");

// Get user's assigned color (for UI)
string color = AuthService.GetUserColor(userId); // "#FF0000"

// Track online users
bool isOnline = AuthService.IsUserOnline("alice");
List<string> roomUsers = AuthService.GetRoomUsers("123456");
```

### RoomService - Room Lifecycle
```csharp
// Create new room
var (success, roomCode, msg) = 
    await RoomService.CreateRoomAsync("alice", 1280, 720);
// Returns: (true, "456789", "Room created")

// Add user to room
await RoomService.AddMemberToRoomAsync(roomCode, clientSession);

// Get members
List<MemberInfo> members = RoomService.GetRoomMembersInfo(roomCode);

// Remove when user leaves
RoomService.RemoveMemberFromRoom(roomCode, "alice");
```

### DrawService - Drawing Persistence
```csharp
// Save drawing stroke
await DrawService.SaveDrawStrokeAsync(roomCode, payload, username);

// Get drawing history (for sync on join)
List<string> history = await DrawService.GetDrawingHistoryAsync(roomCode);

// Clear canvas
await DrawService.ClearCanvasAsync(roomCode, username);
```

### DbManager - Database Access
```csharp
// Get user ID
int userId = await DbManager.GetUserIdAsync("alice");

// Save to gallery
await DbManager.SaveGalleryItemAsync(roomCode, "MyDrawing.png", imageData, "alice");

// Get chat history
var messages = await DbManager.GetChatMessagesAsync(roomCode, limit: 50);

// Save action for undo/redo
await DbManager.SaveActionStackAsync(roomCode, actionJson, isUndo: true);
```

---

## 🔄 FLOW EXAMPLES

### User Login Flow
```
Client sends: LoginPayload {username, passwordHash}
         ↓
SecureTcpServer.HandleClientAsync()
         ↓
AuthService.AuthenticateUserAsync()
         ↓
DbManager.LoginAsync() (checks/creates in DB)
         ↓
AuthService.CreateUserSession() (cache user)
         ↓
Server responds: LoginResponse {success, userId}
         ↓
Client updates UI
```

### Drawing Save & Sync Flow
```
User A draws (UDP) → DrawPayload
         ↓
SecureUdpServer receives
         ↓
DrawService.SaveDrawStrokeAsync()
         ↓
DbManager.SaveStrokeAsync() (persist)
         ↓
Broadcast to Room via UDP → User B also draws
```

### Room Join & History Sync
```
User joins: JoinRoomPayload {roomCode}
         ↓
SecureTcpServer.HandleClientAsync() - JOIN_ROOM case
         ↓
RoomService.AddMemberToRoomAsync()
         ↓
DrawService.GetDrawingHistoryAsync()
         ↓
Send SYNC_BOARD packet with history JSON
         ↓
Client draws all previous strokes
```

---

## ⚠️ IMPORTANT CONFIGURATION

### Database Connection String
**File**: `DrawingServer/Services/Database/DbManager.cs`, Line 12
```csharp
private static readonly string connString = 
    "Host=127.0.0.1;Port=5432;Database=drawingapp;Username=postgres;Password=123456";
```

**Change if**:
- PostgreSQL on different host/port
- Different username/password
- Database name changed

### Server TLS Certificate
**File**: `DrawingServer/Program.cs`, Lines 19-21
```csharp
string pfxPath = "server.pfx";              // Modify if file moved
string pfxPassword = "123456";              // Match cert password
```

---

## 🧪 SIMPLE TEST SCRIPT

```csharp
// Test each service (add to a console app or test project)

// Test 1: AuthService
var auth = await AuthService.AuthenticateUserAsync("testuser", "hash123");
Console.WriteLine($"Auth: {auth.IsSuccess}"); // true

// Test 2: RoomService  
var room = await RoomService.CreateRoomAsync("testuser", 1280, 720);
Console.WriteLine($"Room: {room.RoomCode}"); // "123456"

// Test 3: DrawService
await DrawService.SaveDrawStrokeAsync(room.RoomCode, payload, "testuser");
var history = await DrawService.GetDrawingHistoryAsync(room.RoomCode);
Console.WriteLine($"History count: {history.Count}"); // 1

// Test 4: DbManager
await DbManager.SaveChatMessageAsync(room.RoomCode, "testuser", "Hello!");
var messages = await DbManager.GetChatMessagesAsync(room.RoomCode);
Console.WriteLine($"Messages: {messages.Count}"); // 1
```

---

## 📊 HANDLER ROUTING

When client sends CommandType, SecureTcpServer routes to:

```
LOGIN             → AuthService.AuthenticateUserAsync()
CREATE_ROOM       → RoomService.CreateRoomAsync()
JOIN_ROOM         → RoomService.AddMemberToRoomAsync()
LEAVE_ROOM        → RoomService.RemoveMemberFromRoom()
CHAT              → DbManager.SaveChatMessageAsync()
UNDO/REDO         → DbManager.SaveActionStackAsync()
SAVE_TO_GALLERY   → DbManager.SaveGalleryItemAsync()
GET_GALLERY       → DbManager.GetGalleryItemsAsync()
AI_* commands     → (Logged & acknowledged, actual processing delegated)
```

---

## 📈 DATABASE SIZES (Example)

After 8 hours of use:

| Table | Rows | Size |
|-------|------|------|
| Users | 50 | 5 KB |
| Rooms | 12 | 2 KB |
| DrawHistory | 50,000 | 50 MB (depends on 1280x720 strokes) |
| ChatHistory | 2,000 | 500 KB |
| Gallery | 300 | 1 GB (large PNG exports) |
| ActionStack | 10,000 | 5 MB |

→ Consider archiving old data or partitioning table for production

---

## ❌ WHAT NOT TO DO

| ❌ DON'T | ✅ DO INSTEAD |
|---------|--------------|
| Call async without await | Use `await` or fire-and-forget with `_ = Task.Run()` |
| Access Dictionary without lock | Use `lock (_lock)` around collection access |
| Store passwords plaintext | Always hash with SHA-256 |
| Send unencrypted UDP | Use AesHelper.Encrypt() |
| Direct SQL queries | Use DbManager methods |
| Modify Clients dict | Use RoomService/AuthService |
| Ignore exceptions | At minimum, log with Logger.Error() |

---

## 🎯 NEXT STEPS FOR TEAM

1. **Person A**: Integrate UI with NetworkEvents (already defined)
2. **Person B**: No changes needed (work is complete)
3. **Person C**: All done! Ready for testing
4. **Team**: Integration test the full flow

---

## 📞 TROUBLESHOOTING

### "Table 'Users' does not exist"
```bash
# Forgot to run database setup
cd DrawingServer
psql -U postgres < database_setup.sql
```

### "TLS Handshake failed"
```bash
# Certificate missing or password wrong
# Regenerate: OpenSSL req -x509 -newkey rsa:2048 -keyout server.key -out server.crt
```

### "Connection refused on 8888"
```bash
# Server not running
# Check if port already in use:
netstat -an | grep 8888
# Kill process if needed:
lsof -i :8888 | grep LISTEN | awk '{print $2}' | xargs kill
```

### "No rows affected" in SaveStroke
```bash
# Room doesn't exist
# Verify roomCode exists:
SELECT * FROM Rooms WHERE room_code = 'YOUR_CODE';
```

---

**Last Updated**: March 30, 2026  
**Implementation Status**: ✅ COMPLETE  
**Ready for**: Integration Testing  

