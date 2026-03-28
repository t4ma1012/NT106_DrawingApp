-- Tạo bảng Users
CREATE TABLE Users (
    id SERIAL PRIMARY KEY,
    username VARCHAR(50) UNIQUE NOT NULL,
    password_hash VARCHAR(64) NOT NULL, -- Lưu chuỗi SHA-256 
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Tạo bảng Rooms
CREATE TABLE Rooms (
    id SERIAL PRIMARY KEY,
    room_code VARCHAR(6) UNIQUE NOT NULL, -- Mã phòng 6 số 
    created_by VARCHAR(50) REFERENCES Users(username),
    canvas_width INT DEFAULT 1280,
    canvas_height INT DEFAULT 720,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Tạo bảng DrawHistory (Lưu nét vẽ để Sync và Playback)
CREATE TABLE DrawHistory (
    id SERIAL PRIMARY KEY,
    room_id INT REFERENCES Rooms(id),
    action_id UUID NOT NULL, -- Dùng để Undo/Redo chính xác 
    stroke_data JSONB NOT NULL,
    timestamp TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    username VARCHAR(50) REFERENCES Users(username)
);

-- Tạo bảng GalleryImages (Lưu ảnh đã xuất)
CREATE TABLE GalleryImages (
    id SERIAL PRIMARY KEY,
    room_id INT REFERENCES Rooms(id),
    image_data BYTEA NOT NULL,
    thumbnail_data BYTEA NOT NULL,
    filename VARCHAR(255),
    saved_by VARCHAR(50) REFERENCES Users(username),
    saved_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);