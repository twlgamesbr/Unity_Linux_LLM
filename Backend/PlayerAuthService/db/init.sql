CREATE EXTENSION IF NOT EXISTS pgcrypto;

CREATE TABLE IF NOT EXISTS players (
    player_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    username VARCHAR(32) NOT NULL,
    username_normalized VARCHAR(32) NOT NULL UNIQUE,
    password_hash BYTEA NOT NULL,
    password_salt BYTEA NOT NULL,
    password_iterations INTEGER NOT NULL,
    created_at_utc TIMESTAMPTZ NOT NULL DEFAULT timezone('utc', now()),
    updated_at_utc TIMESTAMPTZ NOT NULL DEFAULT timezone('utc', now())
);

CREATE TABLE IF NOT EXISTS player_sessions (
    session_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    player_id UUID NOT NULL REFERENCES players(player_id) ON DELETE CASCADE,
    session_token_hash TEXT NOT NULL UNIQUE,
    device_id TEXT,
    remember_me BOOLEAN NOT NULL DEFAULT FALSE,
    created_at_utc TIMESTAMPTZ NOT NULL DEFAULT timezone('utc', now()),
    expires_at_utc TIMESTAMPTZ NOT NULL,
    last_seen_at_utc TIMESTAMPTZ NOT NULL DEFAULT timezone('utc', now()),
    revoked_at_utc TIMESTAMPTZ
);

CREATE INDEX IF NOT EXISTS idx_player_sessions_player_id ON player_sessions(player_id);
CREATE INDEX IF NOT EXISTS idx_player_sessions_expires_at ON player_sessions(expires_at_utc);
CREATE INDEX IF NOT EXISTS idx_player_sessions_revoked_at ON player_sessions(revoked_at_utc);

-- PostgREST roles (for REST API layer)
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'web_anon') THEN
        CREATE ROLE web_anon nologin;
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'authenticator') THEN
        CREATE ROLE authenticator NOINHERIT LOGIN PASSWORD 'postgrest';
    END IF;
END
$$;

GRANT USAGE ON SCHEMA public TO web_anon;
GRANT web_anon TO authenticator;

-- ============================================================
-- Views for gameplay CRUD via PostgREST
-- Exclude sensitive auth fields from REST exposure
-- ============================================================

-- Safe player profile view (no password_hash, password_salt, password_iterations)
CREATE OR REPLACE VIEW v_players AS
SELECT player_id, username, username_normalized, created_at_utc, updated_at_utc
FROM players;

CREATE OR REPLACE FUNCTION v_players_insert() RETURNS TRIGGER SECURITY DEFINER AS $$
BEGIN
    INSERT INTO players (username, username_normalized, password_hash, password_salt, password_iterations)
    VALUES (NEW.username, NEW.username_normalized, ''::bytea, ''::bytea, 0)
    RETURNING player_id, username, username_normalized, created_at_utc, updated_at_utc
    INTO NEW.player_id, NEW.username, NEW.username_normalized, NEW.created_at_utc, NEW.updated_at_utc;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS tr_v_players_insert ON v_players;
CREATE TRIGGER tr_v_players_insert
    INSTEAD OF INSERT ON v_players
    FOR EACH ROW EXECUTE FUNCTION v_players_insert();

CREATE OR REPLACE FUNCTION v_players_update() RETURNS TRIGGER SECURITY DEFINER AS $$
BEGIN
    UPDATE players SET
        username = NEW.username,
        username_normalized = NEW.username_normalized,
        updated_at_utc = timezone('utc', now())
    WHERE player_id = OLD.player_id;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS tr_v_players_update ON v_players;
CREATE TRIGGER tr_v_players_update
    INSTEAD OF UPDATE ON v_players
    FOR EACH ROW EXECUTE FUNCTION v_players_update();

CREATE OR REPLACE FUNCTION v_players_delete() RETURNS TRIGGER SECURITY DEFINER AS $$
BEGIN
    DELETE FROM players WHERE player_id = OLD.player_id;
    RETURN OLD;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS tr_v_players_delete ON v_players;
CREATE TRIGGER tr_v_players_delete
    INSTEAD OF DELETE ON v_players
    FOR EACH ROW EXECUTE FUNCTION v_players_delete();

-- Safe session view (no session_token_hash)
CREATE OR REPLACE VIEW v_player_sessions AS
SELECT session_id, player_id, device_id, remember_me,
       created_at_utc, expires_at_utc, last_seen_at_utc, revoked_at_utc
FROM player_sessions;

CREATE OR REPLACE FUNCTION v_player_sessions_insert() RETURNS TRIGGER SECURITY DEFINER AS $$
DECLARE
    new_token TEXT;
BEGIN
    new_token := encode(gen_random_bytes(32), 'hex');
    INSERT INTO player_sessions (player_id, session_token_hash, device_id, remember_me, expires_at_utc)
    VALUES (NEW.player_id, encode(sha256(new_token::bytea), 'hex'), NEW.device_id, NEW.remember_me, NEW.expires_at_utc)
    RETURNING session_id, player_id, device_id, remember_me, created_at_utc, expires_at_utc, last_seen_at_utc, revoked_at_utc
    INTO NEW.session_id, NEW.player_id, NEW.device_id, NEW.remember_me, NEW.created_at_utc, NEW.expires_at_utc, NEW.last_seen_at_utc, NEW.revoked_at_utc;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS tr_v_player_sessions_insert ON v_player_sessions;
CREATE TRIGGER tr_v_player_sessions_insert
    INSTEAD OF INSERT ON v_player_sessions
    FOR EACH ROW EXECUTE FUNCTION v_player_sessions_insert();

CREATE OR REPLACE FUNCTION v_player_sessions_update() RETURNS TRIGGER SECURITY DEFINER AS $$
BEGIN
    UPDATE player_sessions SET
        device_id = COALESCE(NEW.device_id, OLD.device_id),
        remember_me = COALESCE(NEW.remember_me, OLD.remember_me),
        expires_at_utc = COALESCE(NEW.expires_at_utc, OLD.expires_at_utc),
        last_seen_at_utc = timezone('utc', now()),
        revoked_at_utc = COALESCE(NEW.revoked_at_utc, OLD.revoked_at_utc)
    WHERE session_id = OLD.session_id;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS tr_v_player_sessions_update ON v_player_sessions;
CREATE TRIGGER tr_v_player_sessions_update
    INSTEAD OF UPDATE ON v_player_sessions
    FOR EACH ROW EXECUTE FUNCTION v_player_sessions_update();

CREATE OR REPLACE FUNCTION v_player_sessions_delete() RETURNS TRIGGER SECURITY DEFINER AS $$
BEGIN
    DELETE FROM player_sessions WHERE session_id = OLD.session_id;
    RETURN OLD;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS tr_v_player_sessions_delete ON v_player_sessions;
CREATE TRIGGER tr_v_player_sessions_delete
    INSTEAD OF DELETE ON v_player_sessions
    FOR EACH ROW EXECUTE FUNCTION v_player_sessions_delete();

-- Grant full CRUD on views to web_anon, revoke direct table access
REVOKE ALL ON players FROM web_anon;
REVOKE ALL ON player_sessions FROM web_anon;
GRANT ALL ON v_players TO web_anon;
GRANT ALL ON v_player_sessions TO web_anon;
