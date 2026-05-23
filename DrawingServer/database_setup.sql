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
    ai_type     VARCHAR(50),   -- 'text_to_image','bg_removed','magic_erase','autocomplete'
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
