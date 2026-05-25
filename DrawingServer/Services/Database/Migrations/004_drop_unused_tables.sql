-- 004_drop_unused_tables.sql
-- Remove schema left behind by features that are no longer active.

DROP TABLE IF EXISTS StickyNoteReplies CASCADE;
DROP TABLE IF EXISTS StickyNotes CASCADE;
DROP TABLE IF EXISTS Stickers CASCADE;
DROP TABLE IF EXISTS ClientRateLimits CASCADE;
DROP TABLE IF EXISTS RoomMembers CASCADE;
DROP TABLE IF EXISTS GifExports CASCADE;
