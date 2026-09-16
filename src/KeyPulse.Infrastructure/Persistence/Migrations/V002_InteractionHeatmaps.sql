ALTER TABLE daily_mouse_stats ADD COLUMN cursor_distance_pixels REAL NOT NULL DEFAULT 0 CHECK (cursor_distance_pixels >= 0);
ALTER TABLE daily_mouse_stats ADD COLUMN estimated_distance_meters REAL NOT NULL DEFAULT 0 CHECK (estimated_distance_meters >= 0);

ALTER TABLE hourly_activity_stats ADD COLUMN cursor_distance_pixels REAL NOT NULL DEFAULT 0 CHECK (cursor_distance_pixels >= 0);
ALTER TABLE hourly_activity_stats ADD COLUMN estimated_distance_meters REAL NOT NULL DEFAULT 0 CHECK (estimated_distance_meters >= 0);

ALTER TABLE daily_app_stats ADD COLUMN cursor_distance_pixels REAL NOT NULL DEFAULT 0 CHECK (cursor_distance_pixels >= 0);
ALTER TABLE daily_app_stats ADD COLUMN estimated_distance_meters REAL NOT NULL DEFAULT 0 CHECK (estimated_distance_meters >= 0);

CREATE TABLE daily_shortcut_stats (
    stat_date TEXT NOT NULL,
    shortcut_code TEXT NOT NULL,
    press_count INTEGER NOT NULL DEFAULT 0 CHECK (press_count >= 0),
    PRIMARY KEY (stat_date, shortcut_code)
);

CREATE TABLE display_layouts (
    layout_id INTEGER PRIMARY KEY AUTOINCREMENT,
    layout_signature TEXT NOT NULL UNIQUE,
    virtual_left INTEGER NOT NULL,
    virtual_top INTEGER NOT NULL,
    virtual_width INTEGER NOT NULL CHECK (virtual_width > 0),
    virtual_height INTEGER NOT NULL CHECK (virtual_height > 0),
    created_at TEXT NOT NULL
);

CREATE TABLE display_monitors (
    layout_id INTEGER NOT NULL,
    monitor_id TEXT NOT NULL,
    left_px INTEGER NOT NULL,
    top_px INTEGER NOT NULL,
    width_px INTEGER NOT NULL CHECK (width_px > 0),
    height_px INTEGER NOT NULL CHECK (height_px > 0),
    dpi_x REAL NOT NULL CHECK (dpi_x > 0),
    dpi_y REAL NOT NULL CHECK (dpi_y > 0),
    is_primary INTEGER NOT NULL CHECK (is_primary IN (0, 1)),
    PRIMARY KEY (layout_id, monitor_id),
    FOREIGN KEY (layout_id) REFERENCES display_layouts(layout_id) ON DELETE CASCADE
);

CREATE TABLE hourly_click_points (
    stat_date TEXT NOT NULL,
    stat_hour INTEGER NOT NULL CHECK (stat_hour BETWEEN 0 AND 23),
    layout_id INTEGER NOT NULL,
    monitor_id TEXT NOT NULL,
    x_px INTEGER NOT NULL CHECK (x_px >= 0),
    y_px INTEGER NOT NULL CHECK (y_px >= 0),
    button_code TEXT NOT NULL,
    click_count INTEGER NOT NULL DEFAULT 0 CHECK (click_count >= 0),
    PRIMARY KEY (stat_date, stat_hour, layout_id, monitor_id, x_px, y_px, button_code),
    FOREIGN KEY (layout_id, monitor_id) REFERENCES display_monitors(layout_id, monitor_id) ON DELETE CASCADE
);

CREATE TABLE daily_pointer_density (
    stat_date TEXT NOT NULL,
    layout_id INTEGER NOT NULL,
    monitor_id TEXT NOT NULL,
    grid_width INTEGER NOT NULL CHECK (grid_width > 0),
    grid_height INTEGER NOT NULL CHECK (grid_height > 0),
    sample_count INTEGER NOT NULL DEFAULT 0 CHECK (sample_count >= 0),
    format_version INTEGER NOT NULL,
    compression_code TEXT NOT NULL,
    density_blob BLOB NOT NULL,
    PRIMARY KEY (stat_date, layout_id, monitor_id),
    FOREIGN KEY (layout_id, monitor_id) REFERENCES display_monitors(layout_id, monitor_id) ON DELETE CASCADE
);

CREATE TABLE pointer_occupancy_tiles (
    layout_id INTEGER NOT NULL,
    monitor_id TEXT NOT NULL,
    tile_x INTEGER NOT NULL CHECK (tile_x >= 0),
    tile_y INTEGER NOT NULL CHECK (tile_y >= 0),
    tile_width INTEGER NOT NULL CHECK (tile_width > 0),
    tile_height INTEGER NOT NULL CHECK (tile_height > 0),
    visited_count INTEGER NOT NULL DEFAULT 0 CHECK (visited_count >= 0),
    format_version INTEGER NOT NULL,
    bits_blob BLOB NOT NULL,
    PRIMARY KEY (layout_id, monitor_id, tile_x, tile_y),
    FOREIGN KEY (layout_id, monitor_id) REFERENCES display_monitors(layout_id, monitor_id) ON DELETE CASCADE
);

CREATE INDEX idx_daily_shortcut_stats_date ON daily_shortcut_stats(stat_date);
CREATE INDEX idx_hourly_click_points_date ON hourly_click_points(stat_date, layout_id);
CREATE INDEX idx_daily_pointer_density_date ON daily_pointer_density(stat_date, layout_id);

UPDATE schema_version SET version = 2, applied_at = CURRENT_TIMESTAMP WHERE id = 1;
