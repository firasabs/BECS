-- 1) Main audit table
CREATE TABLE IF NOT EXISTS audit_logs (
                                          id            INTEGER PRIMARY KEY AUTOINCREMENT,
                                          timestamp_utc TEXT    NOT NULL,          -- ISO8601 UTC
                                          user_id       TEXT,
                                          user_name     TEXT,
                                          actor_type    TEXT    NOT NULL DEFAULT 'User', -- User/System/Job
                                          action        TEXT    NOT NULL,          -- e.g. Donation.Create
                                          entity_name   TEXT,
                                          entity_id     TEXT,
                                          details_json  TEXT,
                                          success       INTEGER NOT NULL DEFAULT 1, -- 1=true,0=false
                                          correlation_id TEXT,
                                          ip_address    TEXT,
                                          user_agent    TEXT,
                                          http_method   TEXT,
                                          path          TEXT,
                                          prev_hash     TEXT,                      -- hex sha256
                                          hash          TEXT                       -- hex sha256
);

-- Helpful indexes
CREATE INDEX IF NOT EXISTS ix_audit_timestamp ON audit_logs(timestamp_utc);
CREATE INDEX IF NOT EXISTS ix_audit_entity ON audit_logs(entity_name, entity_id);
CREATE INDEX IF NOT EXISTS ix_audit_corr ON audit_logs(correlation_id);

-- 2) Optional safety: prevent updates/deletes (SQLite doesn't enforce immutability natively, but we can discourage it)
CREATE TRIGGER IF NOT EXISTS audit_logs_no_update
BEFORE UPDATE ON audit_logs
BEGIN
SELECT RAISE(ABORT, 'audit_logs are append-only');
END;

CREATE TRIGGER IF NOT EXISTS audit_logs_no_delete
BEFORE DELETE ON audit_logs
BEGIN
SELECT RAISE(ABORT, 'audit_logs are append-only');
END;
