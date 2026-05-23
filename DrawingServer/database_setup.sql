-- ============================================================
-- DrawingServer/database_setup.sql
-- Script khởi tạo toàn bộ database cho NT106 DrawingApp
-- Chạy: psql -U postgres -f database_setup.sql
-- ============================================================

-- Tạo database (bỏ qua nếu đã tồn tại)
SELECT 'CREATE DATABASE drawingapp'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'drawingapp')\gexec

\c drawingapp

-- ── BẢNG USERS ──────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS Users (
    id            SERIAL PRIMARY KEY,
    username      VARCHAR(50)  UNIQUE NOT NULL,
    password_hash VARCHAR(256) NOT NULL,
    created_at    TIMESTAMPTZ  DEFAULT NOW(),
    last_login    TIMESTAMPTZ
);

-- ── BẢNG ROOMS ──────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS Rooms (
    id            SERIAL PRIMARY KEY,
    room_code     VARCHAR(10)  UNIQUE NOT NULL,
    owner_id      INT REFERENCES Users(id),
    canvas_width  INT DEFAULT 1280,
    canvas_height INT DEFAULT 720,
    is_active     BOOLEAN DEFAULT TRUE,
    created_at    TIMESTAMPTZ DEFAULT NOW()
);

-- ── BẢNG DRAWHISTORY ────────────────────────────────────────
CREATE TABLE IF NOT EXISTS DrawHistory (
    id          SERIAL PRIMARY KEY,
    room_id     INT  REFERENCES Rooms(id) ON DELETE CASCADE,
    action_id   VARCHAR(64) NOT NULL,
    stroke_data JSONB       NOT NULL,
    username    VARCHAR(50),
    created_at  TIMESTAMPTZ DEFAULT NOW()
);

-- ── BẢNG GALLERY ────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS Gallery (
    id           SERIAL PRIMARY KEY,
    room_id      INT REFERENCES Rooms(id) ON DELETE CASCADE,
    filename     VARCHAR(256),
    image_data   TEXT,         -- base64 PNG/JPG
    created_by   VARCHAR(50),
    public_token VARCHAR(64) UNIQUE DEFAULT gen_random_uuid()::TEXT,
    created_at   TIMESTAMPTZ DEFAULT NOW()
);

-- ── BẢNG CHATHISTORY ────────────────────────────────────────
CREATE TABLE IF NOT EXISTS ChatHistory (
    id         SERIAL PRIMARY KEY,
    room_id    INT REFERENCES Rooms(id) ON DELETE CASCADE,
    username   VARCHAR(50),
    message    TEXT,
    sent_at    TIMESTAMPTZ DEFAULT NOW()
);

-- ── BẢNG ACTIONSTACK (Undo/Redo) ────────────────────────────
CREATE TABLE IF NOT EXISTS ActionStack (
    id          SERIAL PRIMARY KEY,
    room_id     INT REFERENCES Rooms(id) ON DELETE CASCADE,
    action_data JSONB,
    is_undo     BOOLEAN DEFAULT FALSE,
    created_at  TIMESTAMPTZ DEFAULT NOW()
);

-- ── BẢNG AI RESULTS ─────────────────────────────────────────
CREATE TABLE IF NOT EXISTS AiResults (
    id          SERIAL PRIMARY KEY,
    room_id     INT REFERENCES Rooms(id) ON DELETE CASCADE,
    ai_type     VARCHAR(50),   -- 'text_to_image','bg_removed','autocomplete'
    prompt      TEXT,
    image_data  TEXT,          -- base64
    username    VARCHAR(50),
    created_at  TIMESTAMPTZ DEFAULT NOW()
);

-- ── BẢNG SNAPSHOTS ─────────────────────────────────────────────
-- Lưu snapshot trạng thái canvas mỗi 5 phút (tự động qua SnapshotService)
CREATE TABLE IF NOT EXISTS Snapshots (
    id            SERIAL PRIMARY KEY,
    room_id       INT REFERENCES Rooms(id) ON DELETE CASCADE,
    snapshot_data JSONB       NOT NULL,    -- toàn bộ DrawHistory tại thời điểm chụp
    thumbnail     TEXT        DEFAULT '',  -- base64 PNG thumbnail (tùy chọn)
    taken_at      TIMESTAMPTZ DEFAULT NOW()
);

-- ── BẢNG PIXEL ART ──────────────────────────────────────────
-- Mỗi ô pixel của 1 phòng ở chế độ Pixel Art
-- PRIMARY KEY tổ hợp (room_id, row, col) đảm bảo upsert nhanh
CREATE TABLE IF NOT EXISTS PixelArtCells (
    room_id     INT REFERENCES Rooms(id) ON DELETE CASCADE,
    row         SMALLINT NOT NULL,        -- 0..63
    col         SMALLINT NOT NULL,        -- 0..63
    color_argb  INT      NOT NULL,        -- ARGB packed integer
    username    VARCHAR(50),              -- người tô ô này cuối cùng
    updated_at  TIMESTAMPTZ DEFAULT NOW(),
    PRIMARY KEY (room_id, row, col)
);

-- ── INDEXES ──────────────────────────────────────────────────
CREATE INDEX IF NOT EXISTS idx_rooms_room_code        ON Rooms(room_code);
CREATE INDEX IF NOT EXISTS idx_rooms_owner_id         ON Rooms(owner_id);
CREATE INDEX IF NOT EXISTS idx_draw_history_room_id   ON DrawHistory(room_id);
CREATE INDEX IF NOT EXISTS idx_gallery_room_id        ON Gallery(room_id);
CREATE INDEX IF NOT EXISTS idx_gallery_public_token   ON Gallery(public_token);
CREATE INDEX IF NOT EXISTS idx_chat_history_room_id   ON ChatHistory(room_id);
CREATE INDEX IF NOT EXISTS idx_chat_history_sent_at   ON ChatHistory(sent_at);
CREATE INDEX IF NOT EXISTS idx_action_stack_room_id   ON ActionStack(room_id);
CREATE INDEX IF NOT EXISTS idx_ai_results_room_id     ON AiResults(room_id);
CREATE INDEX IF NOT EXISTS idx_pixel_art_room_id      ON PixelArtCells(room_id);
CREATE INDEX IF NOT EXISTS idx_snapshots_room_id       ON Snapshots(room_id);
CREATE INDEX IF NOT EXISTS idx_snapshots_taken_at      ON Snapshots(taken_at);

-- ── VERIFY ───────────────────────────────────────────────────
-- Chạy xong kiểm tra bằng: \dt
-- Kết quả phải có 8 bảng:
--   Users, Rooms, DrawHistory, Gallery, ChatHistory,
--   ActionStack, AiResults, PixelArtCells

SELECT table_name FROM information_schema.tables
WHERE table_schema = 'public'
ORDER BY table_name;

-- ============================================================
-- Extended schema for multi-server routing/collaboration
-- ============================================================

ALTER TABLE Rooms
ADD COLUMN IF NOT EXISTS owner_server_id VARCHAR(64),
ADD COLUMN IF NOT EXISTS last_state_version BIGINT DEFAULT 0,
ADD COLUMN IF NOT EXISTS max_members INT DEFAULT 12;

CREATE INDEX IF NOT EXISTS idx_rooms_owner_server_id ON Rooms(owner_server_id);

CREATE TABLE IF NOT EXISTS ServerNodes (
    id SERIAL PRIMARY KEY,
    server_id VARCHAR(64) UNIQUE NOT NULL,
    server_name VARCHAR(100),
    host VARCHAR(255) NOT NULL,
    tcp_port INT NOT NULL,
    udp_port INT NOT NULL,
    active_connections INT DEFAULT 0,
    active_rooms INT DEFAULT 0,
    max_connections INT DEFAULT 200,
    cpu_percent REAL DEFAULT 0,
    memory_percent REAL DEFAULT 0,
    is_healthy BOOLEAN DEFAULT TRUE,
    last_heartbeat TIMESTAMPTZ DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS RoomEvents (
    id BIGSERIAL PRIMARY KEY,
    room_id INT REFERENCES Rooms(id) ON DELETE CASCADE,
    event_id UUID UNIQUE NOT NULL,
    event_type VARCHAR(50) NOT NULL,
    payload JSONB NOT NULL,
    created_by VARCHAR(50),
    source_server_id VARCHAR(64),
    created_at TIMESTAMPTZ DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_room_events_room_id_id ON RoomEvents(room_id, id);
CREATE INDEX IF NOT EXISTS idx_room_events_event_type ON RoomEvents(event_type);

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

CREATE TABLE IF NOT EXISTS GifExports (
    id BIGSERIAL PRIMARY KEY,
    room_id INT REFERENCES Rooms(id) ON DELETE CASCADE,
    requested_by VARCHAR(50),
    filename VARCHAR(255),
    status VARCHAR(30) DEFAULT 'pending',
    progress_percent INT DEFAULT 0,
    gif_data TEXT,
    error_message TEXT,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    completed_at TIMESTAMPTZ
);

CREATE TABLE IF NOT EXISTS ClientRateLimits (
    room_id INT REFERENCES Rooms(id) ON DELETE CASCADE,
    username VARCHAR(50) NOT NULL,
    window_started_at TIMESTAMPTZ NOT NULL,
    udp_packets INT DEFAULT 0,
    tcp_packets INT DEFAULT 0,
    ai_requests INT DEFAULT 0,
    PRIMARY KEY (room_id, username, window_started_at)
);

CREATE INDEX IF NOT EXISTS idx_client_rate_limits_room_user
ON ClientRateLimits(room_id, username);

ALTER TABLE AiResults
ADD COLUMN IF NOT EXISTS provider VARCHAR(40) DEFAULT 'gemini',
ADD COLUMN IF NOT EXISTS model VARCHAR(80),
ADD COLUMN IF NOT EXISTS created_by VARCHAR(50);
