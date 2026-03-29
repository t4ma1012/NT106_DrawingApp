// ============================================================
// DrawingClient/Network/NetworkEvents.cs
// Tuần 2→8 — Tất cả events từ network → UI
// Person A subscribe events này trong MainForm/LobbyForm
// ============================================================
using System;
using SharedLib.Payloads;

namespace DrawingClient.Network
{
    /// <summary>
    /// Static event hub: network layer raise, UI layer subscribe.
    /// Tất cả events đều có thể được gọi từ background thread
    /// → Person A phải dùng this.Invoke() khi cập nhật UI.
    /// </summary>
    public static class NetworkEvents
    {
        // ── AUTH ────────────────────────────────────────────────
        public static event Action<LoginResponse> OnLoginResponse;
        public static event Action<RegisterResponse> OnRegisterResponse;

        // ── ROOM ────────────────────────────────────────────────
        public static event Action<CreateRoomResponse> OnCreateRoomResponse;
        public static event Action<JoinRoomResponse> OnJoinRoomResponse;
        public static event Action<RoomMembersPayload> OnRoomMembersReceived;
        public static event Action<UserJoinPayload> OnUserJoined;
        public static event Action<UserLeavePayload> OnUserLeft;
        public static event Action<CanvasSizePayload> OnCanvasSizeReceived;

        // ── DRAWING (UDP) ───────────────────────────────────────
        public static event Action<DrawPayload> OnDrawReceived;
        public static event Action<FloodFillPayload> OnFloodFillReceived;
        public static event Action<ImportImagePayload> OnImportImageReceived;
        public static event Action<SetBackgroundPayload> OnSetBackgroundReceived;
        public static event Action OnClearAllReceived;

        // ── SYNC / UNDO ──────────────────────────────────────────
        public static event Action<SyncBoardPayload> OnSyncBoardReceived;
        public static event Action<UndoPayload> OnUndoReceived;
        public static event Action<RedoPayload> OnRedoReceived;
        public static event Action<PlaybackResponsePayload> OnPlaybackReceived;

        // ── INTERACTION (UDP) ───────────────────────────────────
        public static event Action<CursorPayload> OnCursorReceived;
        public static event Action<LaserPayload> OnLaserReceived;
        public static event Action<ReactionPayload> OnReactionReceived;

        // ── CHAT / ACTIVITY ─────────────────────────────────────
        public static event Action<ChatPayload> OnChatReceived;
        public static event Action<ActivityLogPayload> OnActivityLogReceived;

        // ── CLAIM AREA ──────────────────────────────────────────
        public static event Action<ClaimAreaPayload> OnClaimAreaReceived;
        public static event Action<ReleaseAreaPayload> OnReleaseAreaReceived;

        // ── GALLERY ──────────────────────────────────────────────
        public static event Action<GalleryResponsePayload> OnGalleryReceived;
        public static event Action<PublicGalleryLinkPayload> OnPublicLinkReceived;

        // ── AI FEATURES (Tuần 5-6) ──────────────────────────────
        public static event Action<AiTextToImageResultPayload> OnAiTextToImageResult;
        public static event Action<AiBgRemovedPayload> OnAiBgRemovedResult;
        public static event Action<AiMagicEraseResultPayload> OnAiMagicEraseResult;
        public static event Action<AiAutoCompleteResultPayload> OnAiAutoCompleteResult;

        // ── ADVANCED FEATURES (Tuần 5-6) ────────────────────────
        public static event Action<StickerPayload> OnStickerReceived;
        public static event Action<FollowModePayload> OnFollowModeReceived;
        public static event Action<SpotlightPayload> OnSpotlightReceived;
        public static event Action<StickyNotePayload> OnStickyNoteReceived;
        public static event Action<StickyNoteReplyPayload> OnStickyNoteReplyReceived;
        public static event Action<VoteResponsePayload> OnVoteResponse;
        public static event Action<TimelineResponsePayload> OnTimelineResponse;
        public static event Action<SnapshotListPayload> OnSnapshotListReceived;

        // ── GAMIFICATION (Tuần 7-8) ─────────────────────────────
        public static event Action<DrawingPromptPayload> OnDrawingPromptReceived;
        public static event Action<BlindDrawPayload> OnBlindDrawReceived;
        public static event Action<PixelArtDrawPayload> OnPixelArtDrawReceived;
        public static event Action<PixelArtSyncPayload> OnPixelArtSyncReceived;
        public static event Action<GifExportProgressPayload> OnGifExportProgress;

        // ── CONNECTION ──────────────────────────────────────────
        public static event Action OnDisconnected;
        public static event Action OnConnected;

        // ── RAISE METHODS ────────────────────────────────────────
        public static void RaiseLoginResponse(LoginResponse p) => OnLoginResponse?.Invoke(p);
        public static void RaiseRegisterResponse(RegisterResponse p) => OnRegisterResponse?.Invoke(p);
        public static void RaiseCreateRoomResponse(CreateRoomResponse p) => OnCreateRoomResponse?.Invoke(p);
        public static void RaiseJoinRoomResponse(JoinRoomResponse p) => OnJoinRoomResponse?.Invoke(p);
        public static void RaiseRoomMembersReceived(RoomMembersPayload p) => OnRoomMembersReceived?.Invoke(p);
        public static void RaiseUserJoined(UserJoinPayload p) => OnUserJoined?.Invoke(p);
        public static void RaiseUserLeft(UserLeavePayload p) => OnUserLeft?.Invoke(p);
        public static void RaiseCanvasSizeReceived(CanvasSizePayload p) => OnCanvasSizeReceived?.Invoke(p);
        public static void RaiseDrawReceived(DrawPayload p) => OnDrawReceived?.Invoke(p);
        public static void RaiseFloodFillReceived(FloodFillPayload p) => OnFloodFillReceived?.Invoke(p);
        public static void RaiseImportImageReceived(ImportImagePayload p) => OnImportImageReceived?.Invoke(p);
        public static void RaiseSetBackgroundReceived(SetBackgroundPayload p) => OnSetBackgroundReceived?.Invoke(p);
        public static void RaiseClearAll() => OnClearAllReceived?.Invoke();
        public static void RaiseSyncBoardReceived(SyncBoardPayload p) => OnSyncBoardReceived?.Invoke(p);
        public static void RaiseUndoReceived(UndoPayload p) => OnUndoReceived?.Invoke(p);
        public static void RaiseRedoReceived(RedoPayload p) => OnRedoReceived?.Invoke(p);
        public static void RaisePlaybackReceived(PlaybackResponsePayload p) => OnPlaybackReceived?.Invoke(p);
        public static void RaiseCursorReceived(CursorPayload p) => OnCursorReceived?.Invoke(p);
        public static void RaiseLaserReceived(LaserPayload p) => OnLaserReceived?.Invoke(p);
        public static void RaiseReactionReceived(ReactionPayload p) => OnReactionReceived?.Invoke(p);
        public static void RaiseChatReceived(ChatPayload p) => OnChatReceived?.Invoke(p);
        public static void RaiseActivityLogReceived(ActivityLogPayload p) => OnActivityLogReceived?.Invoke(p);
        public static void RaiseClaimAreaReceived(ClaimAreaPayload p) => OnClaimAreaReceived?.Invoke(p);
        public static void RaiseReleaseAreaReceived(ReleaseAreaPayload p) => OnReleaseAreaReceived?.Invoke(p);
        public static void RaiseGalleryReceived(GalleryResponsePayload p) => OnGalleryReceived?.Invoke(p);
        public static void RaisePublicLinkReceived(PublicGalleryLinkPayload p) => OnPublicLinkReceived?.Invoke(p);
        public static void RaiseAiTextToImageResult(AiTextToImageResultPayload p) => OnAiTextToImageResult?.Invoke(p);
        public static void RaiseAiBgRemovedResult(AiBgRemovedPayload p) => OnAiBgRemovedResult?.Invoke(p);
        public static void RaiseAiMagicEraseResult(AiMagicEraseResultPayload p) => OnAiMagicEraseResult?.Invoke(p);
        public static void RaiseAiAutoCompleteResult(AiAutoCompleteResultPayload p) => OnAiAutoCompleteResult?.Invoke(p);
        public static void RaiseStickerReceived(StickerPayload p) => OnStickerReceived?.Invoke(p);
        public static void RaiseFollowModeReceived(FollowModePayload p) => OnFollowModeReceived?.Invoke(p);
        public static void RaiseSpotlightReceived(SpotlightPayload p) => OnSpotlightReceived?.Invoke(p);
        public static void RaiseStickyNoteReceived(StickyNotePayload p) => OnStickyNoteReceived?.Invoke(p);
        public static void RaiseStickyNoteReplyReceived(StickyNoteReplyPayload p) => OnStickyNoteReplyReceived?.Invoke(p);
        public static void RaiseVoteResponse(VoteResponsePayload p) => OnVoteResponse?.Invoke(p);
        public static void RaiseTimelineResponse(TimelineResponsePayload p) => OnTimelineResponse?.Invoke(p);
        public static void RaiseSnapshotListReceived(SnapshotListPayload p) => OnSnapshotListReceived?.Invoke(p);
        public static void RaiseDrawingPromptReceived(DrawingPromptPayload p) => OnDrawingPromptReceived?.Invoke(p);
        public static void RaiseBlindDrawReceived(BlindDrawPayload p) => OnBlindDrawReceived?.Invoke(p);
        public static void RaisePixelArtDrawReceived(PixelArtDrawPayload p) => OnPixelArtDrawReceived?.Invoke(p);
        public static void RaisePixelArtSyncReceived(PixelArtSyncPayload p) => OnPixelArtSyncReceived?.Invoke(p);
        public static void RaiseGifExportProgress(GifExportProgressPayload p) => OnGifExportProgress?.Invoke(p);
        public static void RaiseDisconnected() => OnDisconnected?.Invoke();
        public static void RaiseConnected() => OnConnected?.Invoke();
    }
}
