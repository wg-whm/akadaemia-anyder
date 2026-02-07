-- Migration_v2.sql: Crafting List Persistence and History Tracking
-- Adds 4 new tables for managing crafting lists, tracking history, and session data
-- Schema version: 2
-- Status: Complete

-- Table 1: crafting_lists
-- Stores user-created crafting lists with scoping by character
CREATE TABLE IF NOT EXISTS crafting_lists (
    id TEXT PRIMARY KEY,
    name TEXT NOT NULL,
    character_content_id INTEGER NOT NULL,
    created_at TEXT NOT NULL,
    last_modified_at TEXT NOT NULL
);

-- Index for character-based list queries
CREATE INDEX IF NOT EXISTS idx_crafting_lists_character ON crafting_lists(character_content_id);

-- Table 2: crafting_list_items
-- Stores individual recipe items within crafting lists
-- Tracks quantity requested and quantity completed
CREATE TABLE IF NOT EXISTS crafting_list_items (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    list_id TEXT NOT NULL REFERENCES crafting_lists(id) ON DELETE CASCADE,
    recipe_id INTEGER NOT NULL,
    recipe_name TEXT NOT NULL,
    quantity INTEGER NOT NULL DEFAULT 1,
    quantity_crafted INTEGER NOT NULL DEFAULT 0,
    craft_type INTEGER NOT NULL,
    UNIQUE(list_id, recipe_id)
);

-- Index for efficient list item queries
CREATE INDEX IF NOT EXISTS idx_crafting_list_items_list ON crafting_list_items(list_id);
CREATE INDEX IF NOT EXISTS idx_crafting_list_items_recipe ON crafting_list_items(recipe_id);

-- Table 3: crafting_history
-- Tracks lifetime crafting statistics per recipe per character
-- Supports progress tracking and achievement calculation
CREATE TABLE IF NOT EXISTS crafting_history (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    recipe_id INTEGER NOT NULL UNIQUE,
    recipe_name TEXT NOT NULL,
    character_content_id INTEGER NOT NULL,
    total_crafted INTEGER NOT NULL DEFAULT 0,
    hq_count INTEGER NOT NULL DEFAULT 0,
    first_crafted_at TEXT NOT NULL,
    last_crafted_at TEXT NOT NULL,
    UNIQUE(character_content_id, recipe_id)
);

-- Indexes for character history queries
CREATE INDEX IF NOT EXISTS idx_crafting_history_character ON crafting_history(character_content_id);
CREATE INDEX IF NOT EXISTS idx_crafting_history_recipe ON crafting_history(recipe_id);

-- Table 4: crafting_sessions
-- Tracks crafting sessions with summary statistics
-- Used for session-based analytics and progress tracking
CREATE TABLE IF NOT EXISTS crafting_sessions (
    id TEXT PRIMARY KEY,
    character_content_id INTEGER NOT NULL,
    start_time TEXT NOT NULL,
    end_time TEXT NOT NULL,
    items_crafted INTEGER NOT NULL DEFAULT 0,
    hq_count INTEGER NOT NULL DEFAULT 0,
    recipe_ids TEXT NOT NULL
);

-- Index for character session queries
CREATE INDEX IF NOT EXISTS idx_crafting_sessions_character ON crafting_sessions(character_content_id);
CREATE INDEX IF NOT EXISTS idx_crafting_sessions_start_time ON crafting_sessions(character_content_id, start_time DESC);

-- Update schema version
INSERT INTO schema_version (version, description)
VALUES (2, 'Crafting lists, history tracking, and session persistence')
ON CONFLICT(version) DO NOTHING;
