-- 003_ai_gemini_and_server_routing.sql
-- Server registry, event log, and AI metadata.

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

-- ClientRateLimits was a placeholder table and is not used by active code.
-- Existing databases drop it in 004_drop_unused_tables.sql.

ALTER TABLE AiResults
ADD COLUMN IF NOT EXISTS provider VARCHAR(40) DEFAULT 'gemini',
ADD COLUMN IF NOT EXISTS model VARCHAR(80),
ADD COLUMN IF NOT EXISTS created_by VARCHAR(50),
ADD COLUMN IF NOT EXISTS username VARCHAR(50);
