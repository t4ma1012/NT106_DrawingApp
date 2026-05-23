-- 002_collaboration_features.sql
-- Collaboration tables and room metadata for multi-client sync.

ALTER TABLE Rooms
ADD COLUMN IF NOT EXISTS owner_server_id VARCHAR(64),
ADD COLUMN IF NOT EXISTS last_state_version BIGINT DEFAULT 0,
ADD COLUMN IF NOT EXISTS max_members INT DEFAULT 12;

CREATE INDEX IF NOT EXISTS idx_rooms_owner_server_id ON Rooms(owner_server_id);

CREATE TABLE IF NOT EXISTS RoomMembers (
    room_id INT REFERENCES Rooms(id) ON DELETE CASCADE,
    username VARCHAR(50) NOT NULL,
    cursor_color_argb INT,
    role VARCHAR(20) DEFAULT 'editor',
    status VARCHAR(20) DEFAULT 'online',
    server_id VARCHAR(64),
    joined_at TIMESTAMPTZ DEFAULT NOW(),
    last_seen TIMESTAMPTZ DEFAULT NOW(),
    PRIMARY KEY (room_id, username)
);

CREATE TABLE IF NOT EXISTS StickyNotes (
    note_id UUID PRIMARY KEY,
    room_id INT REFERENCES Rooms(id) ON DELETE CASCADE,
    author_username VARCHAR(50),
    x INT NOT NULL,
    y INT NOT NULL,
    text TEXT,
    is_open BOOLEAN DEFAULT TRUE,
    updated_at TIMESTAMPTZ DEFAULT NOW(),
    created_at TIMESTAMPTZ DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS StickyNoteReplies (
    id BIGSERIAL PRIMARY KEY,
    note_id UUID REFERENCES StickyNotes(note_id) ON DELETE CASCADE,
    author_username VARCHAR(50),
    text TEXT NOT NULL,
    created_at TIMESTAMPTZ DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS Stickers (
    action_id UUID PRIMARY KEY,
    room_id INT REFERENCES Rooms(id) ON DELETE CASCADE,
    username VARCHAR(50),
    sticker_id VARCHAR(80),
    x INT,
    y INT,
    width INT,
    height INT,
    rotation REAL DEFAULT 0,
    created_at TIMESTAMPTZ DEFAULT NOW()
);
