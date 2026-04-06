// ============================================================
// DrawingServer/Services/DrawService.cs
// Person C (Server) — Drawing Operations Service
// Handles saving and retrieving drawing data
// ============================================================
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DrawingServer.Database;
using SharedLib.Payloads;
using SharedLib.Logging;

namespace DrawingServer.Services
{
    /// <summary>
    /// Manages drawing operations: saves strokes, handles undo/redo, etc.
    /// Primarily used by udp server and TCP for persistence.
    /// </summary>
    public static class DrawService
    {
        /// <summary>
        /// Save a drawing stroke to database and history.
        /// Called by UDP when client draws.
        /// </summary>
        public static async Task<bool> SaveDrawStrokeAsync(string roomCode, DrawPayload payload, string username)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(roomCode) || payload == null)
                    return false;

                // Serialize payload to JSON for storage
                string strokeJson = Newtonsoft.Json.JsonConvert.SerializeObject(payload);
                
                // Save to database
                await DbManager.SaveStrokeAsync(roomCode, payload.ActionID, strokeJson, username);
                
                Logger.Info("Draw", $"Saved stroke {payload.ActionID} by {username} in room {roomCode}");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error("Draw", $"Error saving stroke: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Save text annotation to database.
        /// </summary>
        public static async Task<bool> SaveTextAsync(string roomCode, DrawPayload payload, string username)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(roomCode) || payload == null)
                    return false;

                string textJson = Newtonsoft.Json.JsonConvert.SerializeObject(payload);
                await DbManager.SaveStrokeAsync(roomCode, payload.ActionID, textJson, username);
                
                Logger.Info("Draw", $"Saved text by {username} in room {roomCode}");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error("Draw", $"Error saving text: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Save flood fill operation to database.
        /// </summary>
        public static async Task<bool> SaveFloodFillAsync(string roomCode, FloodFillPayload payload, string username)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(roomCode) || payload == null)
                    return false;

                string fillJson = Newtonsoft.Json.JsonConvert.SerializeObject(payload);
                await DbManager.SaveStrokeAsync(roomCode, payload.ActionID, fillJson, username);
                
                Logger.Info("Draw", $"Saved flood fill by {username} in room {roomCode}");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error("Draw", $"Error saving flood fill: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Save imported image to canvas.
        /// </summary>
        public static async Task<bool> SaveImportedImageAsync(string roomCode, ImportImagePayload payload, string username)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(roomCode) || payload == null)
                    return false;

                string imgJson = Newtonsoft.Json.JsonConvert.SerializeObject(payload);
                await DbManager.SaveStrokeAsync(roomCode, payload.ActionID, imgJson, username);
                
                Logger.Info("Draw", $"Saved imported image by {username} in room {roomCode}");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error("Draw", $"Error saving imported image: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Save background color change.
        /// </summary>
        public static async Task<bool> SaveBackgroundColorAsync(string roomCode, SetBackgroundPayload payload, string username)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(roomCode) || payload == null)
                    return false;

                string bgJson = Newtonsoft.Json.JsonConvert.SerializeObject(payload);
                await DbManager.SaveStrokeAsync(roomCode, Guid.NewGuid().ToString(), bgJson, username);
                
                Logger.Info("Draw", $"Changed background color in room {roomCode}");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error("Draw", $"Error saving background: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Clear all drawing content in a room.
        /// </summary>
        public static async Task<bool> ClearCanvasAsync(string roomCode, string username)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(roomCode))
                    return false;

                // Create a "clear all" action entry
                var clearAction = new { action = "CLEAR_ALL", clearedBy = username, timestamp = DateTime.UtcNow };
                string clearJson = Newtonsoft.Json.JsonConvert.SerializeObject(clearAction);
                
                await DbManager.SaveStrokeAsync(roomCode, Guid.NewGuid().ToString(), clearJson, username);
                
                Logger.Info("Draw", $"Canvas cleared in room {roomCode} by {username}");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error("Draw", $"Error clearing canvas: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Get full drawing history for a room (for sync).
        /// Already implemented in DbManager.GetRoomHistoryAsync.
        /// </summary>
        public static async Task<List<string>> GetDrawingHistoryAsync(string roomCode)
        {
            try
            {
                return await DbManager.GetRoomHistoryAsync(roomCode);
            }
            catch (Exception ex)
            {
                Logger.Error("Draw", $"Error retrieving history: {ex.Message}");
                return new List<string>();
            }
        }

        /// <summary>
        /// Count total actions in a room's drawing history.
        /// </summary>
        public static async Task<int> GetActionCountAsync(string roomCode)
        {
            try
            {
                var history = await DbManager.GetRoomHistoryAsync(roomCode);
                return history.Count;
            }
            catch (Exception ex)
            {
                Logger.Error("Draw", $"Error counting actions: {ex.Message}");
                return 0;
            }
        }
    }
}
