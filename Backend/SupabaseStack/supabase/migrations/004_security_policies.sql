-- RLS policies and RPC functions for the Unity NPC dialogue schema
-- All policies are JWT-scoped via auth.uid() — no service-role in client paths

-- Enable RLS on all player-facing tables
ALTER TABLE player_profiles ENABLE ROW LEVEL SECURITY;
ALTER TABLE player_npc_relationships ENABLE ROW LEVEL SECURITY;
ALTER TABLE multiplayer_rooms ENABLE ROW LEVEL SECURITY;
ALTER TABLE room_memberships ENABLE ROW LEVEL SECURITY;
ALTER TABLE dialogue_sessions ENABLE ROW LEVEL SECURITY;
ALTER TABLE dialogue_turns ENABLE ROW LEVEL SECURITY;
ALTER TABLE player_clues ENABLE ROW LEVEL SECURITY;
ALTER TABLE player_items ENABLE ROW LEVEL SECURITY;
ALTER TABLE player_locations ENABLE ROW LEVEL SECURITY;

-- ============================================================
-- RLS POLICIES
-- ============================================================

-- player_profiles: each user owns their profile
CREATE POLICY player_profiles_owner ON player_profiles
    FOR ALL
    USING (user_id = auth.uid())
    WITH CHECK (user_id = auth.uid());

-- player_npc_relationships: each user owns their NPC relationships
CREATE POLICY player_npc_relationships_owner ON player_npc_relationships
    FOR ALL
    USING (user_id = auth.uid())
    WITH CHECK (user_id = auth.uid());

-- multiplayer_rooms: any authenticated user can see active rooms
CREATE POLICY multiplayer_rooms_select ON multiplayer_rooms
    FOR SELECT
    USING (auth.role() = 'authenticated' AND is_active = true);

-- Room creators can update their own rooms
CREATE POLICY multiplayer_rooms_owner_update ON multiplayer_rooms
    FOR UPDATE
    USING (owner_user_id = auth.uid())
    WITH CHECK (owner_user_id = auth.uid());

-- Any authenticated user can create a room
CREATE POLICY multiplayer_rooms_insert ON multiplayer_rooms
    FOR INSERT
    WITH CHECK (auth.role() = 'authenticated');

-- room_memberships: users see and manage their own memberships
CREATE POLICY room_memberships_owner ON room_memberships
    FOR ALL
    USING (user_id = auth.uid())
    WITH CHECK (user_id = auth.uid());

-- Room members can see other members in the same room
CREATE POLICY room_memberships_roommates ON room_memberships
    FOR SELECT
    USING (
        EXISTS (
            SELECT 1 FROM room_memberships AS rm
            WHERE rm.room_id = room_memberships.room_id
            AND rm.user_id = auth.uid()
            AND rm.left_at IS NULL
        )
    );

-- dialogue_sessions: users own their sessions
CREATE POLICY dialogue_sessions_owner ON dialogue_sessions
    FOR ALL
    USING (user_id = auth.uid())
    WITH CHECK (user_id = auth.uid());

-- dialogue_turns: users can read/write turns in their sessions
CREATE POLICY dialogue_turns_owner ON dialogue_turns
    FOR ALL
    USING (
        EXISTS (
            SELECT 1 FROM dialogue_sessions
            WHERE session_id = dialogue_turns.session_id
            AND user_id = auth.uid()
        )
    );

-- player_clues: users own their clues
CREATE POLICY player_clues_owner ON player_clues
    FOR ALL
    USING (user_id = auth.uid())
    WITH CHECK (user_id = auth.uid());

-- player_items: users own their items
CREATE POLICY player_items_owner ON player_items
    FOR ALL
    USING (user_id = auth.uid())
    WITH CHECK (user_id = auth.uid());

-- player_locations: users own their locations
CREATE POLICY player_locations_owner ON player_locations
    FOR ALL
    USING (user_id = auth.uid())
    WITH CHECK (user_id = auth.uid());

-- ============================================================
-- RPC FUNCTIONS (privileged mutations, SECURITY DEFINER)
-- ============================================================

-- Create or update player profile on first login
CREATE OR REPLACE FUNCTION create_or_update_player_profile(p_display_name text)
RETURNS player_profiles
LANGUAGE plpgsql SECURITY DEFINER SET search_path = 'public'
AS $$
DECLARE
    v_profile player_profiles;
BEGIN
    INSERT INTO player_profiles (user_id, display_name, last_login_at, is_online)
    VALUES (auth.uid(), p_display_name, now(), true)
    ON CONFLICT (user_id) DO UPDATE SET
        display_name = CASE WHEN p_display_name IS NOT NULL AND p_display_name <> ''
            THEN p_display_name ELSE player_profiles.display_name END,
        last_login_at = now(),
        is_online = true
    RETURNING * INTO v_profile;
    RETURN v_profile;
END;
$$;

-- Join a multiplayer room (checks capacity)
CREATE OR REPLACE FUNCTION join_room(p_room_id uuid)
RETURNS room_memberships
LANGUAGE plpgsql SECURITY DEFINER SET search_path = 'public'
AS $$
DECLARE
    v_membership room_memberships;
    v_count int;
    v_max int;
BEGIN
    SELECT max_players INTO v_max FROM multiplayer_rooms WHERE room_id = p_room_id;
    IF NOT FOUND THEN
        RAISE EXCEPTION 'ROOM_NOT_FOUND' USING HINT = 'Room does not exist';
    END IF;

    SELECT count(*) INTO v_count
    FROM room_memberships
    WHERE room_id = p_room_id AND left_at IS NULL;

    IF v_count >= v_max THEN
        RAISE EXCEPTION 'ROOM_FULL' USING HINT = 'Room has reached max_players';
    END IF;

    INSERT INTO room_memberships (room_id, user_id, joined_at, is_ready)
    VALUES (p_room_id, auth.uid(), now(), false)
    ON CONFLICT (room_id, user_id) DO UPDATE SET
        left_at = NULL,
        joined_at = now(),
        is_ready = false
    RETURNING * INTO v_membership;

    RETURN v_membership;
END;
$$;

-- Leave a multiplayer room
CREATE OR REPLACE FUNCTION leave_room(p_room_id uuid)
RETURNS void
LANGUAGE plpgsql SECURITY DEFINER SET search_path = 'public'
AS $$
BEGIN
    UPDATE room_memberships
    SET left_at = now(), is_ready = false
    WHERE room_id = p_room_id AND user_id = auth.uid();
END;
$$;

-- Record a dialogue turn and update session + NPC relationship
CREATE OR REPLACE FUNCTION record_dialogue_turn(
    p_session_id uuid,
    p_role text,
    p_content text,
    p_npc_slug text DEFAULT NULL,
    p_mood_snapshot text DEFAULT NULL,
    p_trust_snapshot int DEFAULT NULL,
    p_action_type text DEFAULT NULL,
    p_action_result text DEFAULT NULL
)
RETURNS dialogue_turns
LANGUAGE plpgsql SECURITY DEFINER SET search_path = 'public'
AS $$
DECLARE
    v_turn dialogue_turns;
BEGIN
    INSERT INTO dialogue_turns (session_id, role, content, npc_mood_snapshot, npc_trust_snapshot, action_type, action_result)
    VALUES (p_session_id, p_role, p_content, p_mood_snapshot, p_trust_snapshot, p_action_type, p_action_result)
    RETURNING * INTO v_turn;

    UPDATE dialogue_sessions
    SET turn_count = turn_count + 1
    WHERE session_id = p_session_id;

    IF p_npc_slug IS NOT NULL AND auth.uid() IS NOT NULL THEN
        INSERT INTO player_npc_relationships (user_id, npc_slug, dialogue_count, last_interaction_at, current_mood, trust_score)
        VALUES (auth.uid(), p_npc_slug, 1, now(),
            COALESCE(NULLIF(p_mood_snapshot, ''), 'neutral'),
            COALESCE(p_trust_snapshot, 50))
        ON CONFLICT (user_id, npc_slug) DO UPDATE SET
            dialogue_count = player_npc_relationships.dialogue_count + 1,
            last_interaction_at = now(),
            current_mood = CASE WHEN p_mood_snapshot IS NOT NULL AND p_mood_snapshot <> ''
                THEN p_mood_snapshot ELSE player_npc_relationships.current_mood END,
            trust_score = CASE WHEN p_trust_snapshot IS NOT NULL
                THEN p_trust_snapshot ELSE player_npc_relationships.trust_score END;
    END IF;

    RETURN v_turn;
END;
$$;

-- Set player online/offline status
CREATE OR REPLACE FUNCTION set_online_status(p_online boolean)
RETURNS void
LANGUAGE plpgsql SECURITY DEFINER SET search_path = 'public'
AS $$
BEGIN
    UPDATE player_profiles
    SET is_online = p_online,
        last_login_at = CASE WHEN p_online THEN now() ELSE last_login_at END
    WHERE user_id = auth.uid();
END;
$$;

-- ============================================================
-- GRANTS
-- ============================================================

-- Schema usage
GRANT USAGE ON SCHEMA public TO anon, authenticated;

-- RPC execution
GRANT EXECUTE ON FUNCTION create_or_update_player_profile TO anon, authenticated;
GRANT EXECUTE ON FUNCTION join_room TO authenticated;
GRANT EXECUTE ON FUNCTION leave_room TO authenticated;
GRANT EXECUTE ON FUNCTION record_dialogue_turn TO authenticated;
GRANT EXECUTE ON FUNCTION set_online_status TO authenticated;

-- Table SELECT (mutations go through RPC)
GRANT SELECT ON player_profiles TO authenticated;
GRANT SELECT ON player_npc_relationships TO authenticated;
GRANT SELECT ON multiplayer_rooms TO authenticated;
GRANT SELECT ON room_memberships TO authenticated;
GRANT SELECT ON dialogue_sessions TO authenticated;
GRANT SELECT ON dialogue_turns TO authenticated;
GRANT SELECT ON player_clues TO authenticated;
GRANT SELECT ON player_items TO authenticated;
GRANT SELECT ON player_locations TO authenticated;
