CREATE TABLE schema_version (
    id INTEGER PRIMARY KEY CHECK (id = 1),
    version INTEGER NOT NULL,
    applied_at TEXT NOT NULL
);

CREATE TABLE daily_key_stats (
    stat_date TEXT NOT NULL,
    key_code TEXT NOT NULL,
    press_count INTEGER NOT NULL DEFAULT 0 CHECK (press_count >= 0),
    PRIMARY KEY (stat_date, key_code)
);

CREATE TABLE daily_mouse_stats (
    stat_date TEXT PRIMARY KEY,
    left_click_count INTEGER NOT NULL DEFAULT 0 CHECK (left_click_count >= 0),
    right_click_count INTEGER NOT NULL DEFAULT 0 CHECK (right_click_count >= 0),
    middle_click_count INTEGER NOT NULL DEFAULT 0 CHECK (middle_click_count >= 0),
    xbutton1_click_count INTEGER NOT NULL DEFAULT 0 CHECK (xbutton1_click_count >= 0),
    xbutton2_click_count INTEGER NOT NULL DEFAULT 0 CHECK (xbutton2_click_count >= 0),
    wheel_up_count INTEGER NOT NULL DEFAULT 0 CHECK (wheel_up_count >= 0),
    wheel_down_count INTEGER NOT NULL DEFAULT 0 CHECK (wheel_down_count >= 0),
    wheel_left_count INTEGER NOT NULL DEFAULT 0 CHECK (wheel_left_count >= 0),
    wheel_right_count INTEGER NOT NULL DEFAULT 0 CHECK (wheel_right_count >= 0),
    mouse_distance_pixels REAL NOT NULL DEFAULT 0 CHECK (mouse_distance_pixels >= 0)
);

CREATE TABLE hourly_activity_stats (
    stat_date TEXT NOT NULL,
    stat_hour INTEGER NOT NULL CHECK (stat_hour BETWEEN 0 AND 23),
    key_press_count INTEGER NOT NULL DEFAULT 0 CHECK (key_press_count >= 0),
    mouse_click_count INTEGER NOT NULL DEFAULT 0 CHECK (mouse_click_count >= 0),
    wheel_event_count INTEGER NOT NULL DEFAULT 0 CHECK (wheel_event_count >= 0),
    mouse_distance_pixels REAL NOT NULL DEFAULT 0 CHECK (mouse_distance_pixels >= 0),
    active_seconds INTEGER NOT NULL DEFAULT 0 CHECK (active_seconds >= 0),
    PRIMARY KEY (stat_date, stat_hour)
);

CREATE TABLE app_registry (
    app_id INTEGER PRIMARY KEY AUTOINCREMENT,
    process_name TEXT NOT NULL UNIQUE,
    display_name TEXT,
    first_seen_at TEXT NOT NULL,
    last_seen_at TEXT NOT NULL
);

CREATE TABLE daily_app_stats (
    stat_date TEXT NOT NULL,
    app_id INTEGER NOT NULL,
    key_press_count INTEGER NOT NULL DEFAULT 0 CHECK (key_press_count >= 0),
    mouse_click_count INTEGER NOT NULL DEFAULT 0 CHECK (mouse_click_count >= 0),
    wheel_event_count INTEGER NOT NULL DEFAULT 0 CHECK (wheel_event_count >= 0),
    mouse_distance_pixels REAL NOT NULL DEFAULT 0 CHECK (mouse_distance_pixels >= 0),
    active_seconds INTEGER NOT NULL DEFAULT 0 CHECK (active_seconds >= 0),
    PRIMARY KEY (stat_date, app_id),
    FOREIGN KEY (app_id)
        REFERENCES app_registry(app_id)
        ON DELETE CASCADE
);

CREATE TABLE settings_meta (
    meta_key TEXT PRIMARY KEY,
    meta_value TEXT NOT NULL
);

CREATE INDEX idx_daily_key_stats_key
ON daily_key_stats(key_code, stat_date);

CREATE INDEX idx_daily_app_stats_app
ON daily_app_stats(app_id, stat_date);

CREATE INDEX idx_hourly_activity_date
ON hourly_activity_stats(stat_date);

INSERT INTO schema_version (id, version, applied_at)
VALUES (1, 1, CURRENT_TIMESTAMP);
