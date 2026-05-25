-- 002_collaboration_features.sql
-- Collaboration tables and room metadata for multi-client sync.

ALTER TABLE Rooms
ADD COLUMN IF NOT EXISTS owner_server_id VARCHAR(64),
ADD COLUMN IF NOT EXISTS last_state_version BIGINT DEFAULT 0,
ADD COLUMN IF NOT EXISTS max_members INT DEFAULT 12;

CREATE INDEX IF NOT EXISTS idx_rooms_owner_server_id ON Rooms(owner_server_id);

-- Room members, sticky notes, and stickers are synchronized through in-memory
-- room state plus DrawHistory replay. The old standalone tables were removed
-- by 004_drop_unused_tables.sql because no active server code reads/writes them.
