ALTER TABLE daily_app_stats
ADD COLUMN effective_active_seconds INTEGER NOT NULL DEFAULT 0 CHECK (effective_active_seconds >= 0);

CREATE TABLE activity_sessions (
    session_id TEXT PRIMARY KEY,
    stat_date TEXT NOT NULL,
    started_at TEXT NOT NULL,
    ended_at TEXT NOT NULL,
    effective_seconds INTEGER NOT NULL DEFAULT 0 CHECK (effective_seconds >= 0),
    key_press_count INTEGER NOT NULL DEFAULT 0 CHECK (key_press_count >= 0),
    mouse_click_count INTEGER NOT NULL DEFAULT 0 CHECK (mouse_click_count >= 0),
    wheel_event_count INTEGER NOT NULL DEFAULT 0 CHECK (wheel_event_count >= 0)
);

CREATE INDEX idx_activity_sessions_date ON activity_sessions(stat_date, started_at);

INSERT INTO settings_meta (meta_key, meta_value)
VALUES ('effective_tracking_started_at', CURRENT_TIMESTAMP)
ON CONFLICT(meta_key) DO NOTHING;

UPDATE schema_version SET version = 3, applied_at = CURRENT_TIMESTAMP WHERE id = 1;
