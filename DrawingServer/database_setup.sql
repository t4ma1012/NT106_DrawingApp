-- ============================================================
-- Database Initialization Script for NT106 Drawing App
-- PostgreSQL Setup
-- ============================================================
-- NOTE: Run this script to set up the initial database schema
-- You need to have PostgreSQL installed and running
-- Command: psql -U postgres -f database_setup.sql

-- Create Database
CREATE DATABASE drawingapp
    WITH
    ENCODING = 'UTF8'
    LC_COLLATE = 'en_US.UTF-8'
    LC_CTYPE = 'en_US.UTF-8';

-- Connect to the new database
\c drawingapp;

-- ============================================================
-- TABLE: Users
-- Stores user accounts (auto-register on first login)
-- ============================================================
CREATE TABLE IF NOT EXISTS Users (
    id SERIAL PRIMARY KEY,
    username VARCHAR(255) UNIQUE NOT NULL,
    password_hash VARCHAR(256) NOT NULL,   -- SHA-256 hash
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    last_login TIMESTAMP
);

-- ============================================================
-- TABLE: Rooms
-- Stores room information
-- ============================================================
CREATE TABLE IF NOT EXISTS Rooms (
    id SERIAL PRIMARY KEY,
    room_code VARCHAR(20) UNIQUE NOT NULL,
    owner_id INT NOT NULL REFERENCES Users(id),
    canvas_width INT DEFAULT 1280,
    canvas_height INT DEFAULT 720,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    is_active BOOLEAN DEFAULT TRUE
);

CREATE INDEX idx_rooms_room_code ON Rooms(room_code);
CREATE INDEX idx_rooms_owner_id ON Rooms(owner_id);

-- ============================================================
-- TABLE: DrawHistory
-- Stores drawing strokes (persistent)
-- ============================================================
CREATE TABLE IF NOT EXISTS DrawHistory (
    id SERIAL PRIMARY KEY,
    room_id INT NOT NULL REFERENCES Rooms(id),
    action_id VARCHAR(255) NOT NULL,       -- GUID for stroke
    stroke_data JSONB NOT NULL,            -- Full stroke object as JSON
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_draw_history_room_id ON DrawHistory(room_id);
CREATE INDEX idx_draw_history_action_id ON DrawHistory(action_id);

-- ============================================================
-- TABLE: Gallery
-- Stores exported drawings
-- ============================================================
CREATE TABLE IF NOT EXISTS Gallery (
    id SERIAL PRIMARY KEY,
    room_id INT NOT NULL REFERENCES Rooms(id),
    filename VARCHAR(255),
    image_data TEXT NOT NULL,              -- base64 encoded PNG
    created_by VARCHAR(255) NOT NULL,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    is_public BOOLEAN DEFAULT FALSE,
    public_token VARCHAR(255) UNIQUE       -- For public sharing
);

CREATE INDEX idx_gallery_room_id ON Gallery(room_id);
CREATE INDEX idx_gallery_public_token ON Gallery(public_token);

-- ============================================================
-- TABLE: ChatHistory
-- Stores chat messages
-- ============================================================
CREATE TABLE IF NOT EXISTS ChatHistory (
    id SERIAL PRIMARY KEY,
    room_id INT NOT NULL REFERENCES Rooms(id),
    username VARCHAR(255) NOT NULL,
    message TEXT NOT NULL,
    sent_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_chat_history_room_id ON ChatHistory(room_id);
CREATE INDEX idx_chat_history_sent_at ON ChatHistory(sent_at);

-- ============================================================
-- TABLE: ActionStack
-- Stores undo/redo actions
-- ============================================================
CREATE TABLE IF NOT EXISTS ActionStack (
    id SERIAL PRIMARY KEY,
    room_id INT NOT NULL REFERENCES Rooms(id),
    action_data JSONB NOT NULL,
    is_undo BOOLEAN DEFAULT TRUE,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_action_stack_room_id ON ActionStack(room_id);

-- ============================================================
-- Sample Data (Optional - for testing)
-- ============================================================
-- INSERT INTO Users (username, password_hash) VALUES 
--     ('alice', '2c26b46911185131006ba5d21b4d4e157c6e3fca3e1ecbb0b'),  -- sha256('password')
--     ('bob', '2c26b46911185131006ba5d21b4d4e157c6e3fca3e1ecbb0b');    -- sha256('password')

-- ============================================================
-- GRANTS (Optional - if using separate DB user)
-- ============================================================
-- GRANT CONNECT ON DATABASE drawingapp TO drawingapp_user;
-- GRANT USAGE ON SCHEMA public TO drawingapp_user;
-- GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA public TO drawingapp_user;
-- GRANT ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA public TO drawingapp_user;

-- ============================================================
-- Verify Installation
-- ============================================================
-- Run these queries to verify everything is set up:
-- SELECT * FROM pg_tables WHERE schemaname = 'public';
-- \dt                                      -- List all tables
-- SELECT COUNT(*) FROM Users;              -- Should be empty
-- SELECT COUNT(*) FROM Rooms;              -- Should be empty

COMMIT;
