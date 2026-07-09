-- Gameplay schema for Unity NPC dialogue prototype
-- Linked to auth.users for identity, RLS-enabled for player isolation

-- Player profiles (linked to auth.users)
CREATE TABLE IF NOT EXISTS player_profiles (
    user_id uuid PRIMARY KEY REFERENCES auth.users(id) ON DELETE CASCADE,
    display_name text NOT NULL,
    avatar_url text,
    last_login_at timestamptz,
    is_online boolean DEFAULT false,
    created_at timestamptz DEFAULT now(),
    updated_at timestamptz DEFAULT now()
);

-- NPC catalog (canonical definitions, not per-player state)
CREATE TABLE IF NOT EXISTS npc_definitions (
    npc_slug text PRIMARY KEY,
    display_name text NOT NULL,
    description text,
    personality_tags text[],
    base_mood text DEFAULT 'neutral',
    portrait_url text,
    knowledge_path text,
    is_active boolean DEFAULT true,
    created_at timestamptz DEFAULT now()
);

-- Per-player NPC relationship state (maps NPCEvidenceState mood/trust)
CREATE TABLE IF NOT EXISTS player_npc_relationships (
    user_id uuid REFERENCES auth.users(id) ON DELETE CASCADE,
    npc_slug text REFERENCES npc_definitions(npc_slug),
    trust_score int DEFAULT 50 CHECK (trust_score >= 0 AND trust_score <= 100),
    current_mood text DEFAULT 'neutral',
    dialogue_count int DEFAULT 0,
    last_interaction_at timestamptz,
    created_at timestamptz DEFAULT now(),
    updated_at timestamptz DEFAULT now(),
    PRIMARY KEY (user_id, npc_slug)
);

-- Multiplayer rooms
CREATE TABLE IF NOT EXISTS multiplayer_rooms (
    room_id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    room_name text NOT NULL,
    owner_user_id uuid REFERENCES auth.users(id),
    room_code text UNIQUE,
    max_players int DEFAULT 8,
    is_active boolean DEFAULT true,
    game_state jsonb DEFAULT '{}'::jsonb,
    started_at timestamptz,
    ended_at timestamptz,
    created_at timestamptz DEFAULT now(),
    updated_at timestamptz DEFAULT now()
);

-- Room memberships
CREATE TABLE IF NOT EXISTS room_memberships (
    room_id uuid REFERENCES multiplayer_rooms(room_id) ON DELETE CASCADE,
    user_id uuid REFERENCES auth.users(id) ON DELETE CASCADE,
    joined_at timestamptz DEFAULT now(),
    left_at timestamptz,
    is_ready boolean DEFAULT false,
    PRIMARY KEY (room_id, user_id)
);

-- Dialogue sessions (maps NPCHistoryStore sessions)
CREATE TABLE IF NOT EXISTS dialogue_sessions (
    session_id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    room_id uuid REFERENCES multiplayer_rooms(room_id) ON DELETE SET NULL,
    user_id uuid REFERENCES auth.users(id) NOT NULL,
    npc_slug text REFERENCES npc_definitions(npc_slug) NOT NULL,
    started_at timestamptz DEFAULT now(),
    ended_at timestamptz,
    turn_count int DEFAULT 0,
    summary text,
    created_at timestamptz DEFAULT now()
);

-- Dialogue turns (maps NPCHistoryStore DialogueEntry)
CREATE TABLE IF NOT EXISTS dialogue_turns (
    turn_id bigserial PRIMARY KEY,
    session_id uuid REFERENCES dialogue_sessions(session_id) ON DELETE CASCADE,
    player_id uuid REFERENCES auth.users(id),
    role text NOT NULL CHECK (role IN ('user', 'assistant', 'system')),
    content text NOT NULL,
    npc_mood_snapshot text,
    npc_trust_snapshot int,
    action_type text,
    action_result text,
    created_at timestamptz DEFAULT now()
);

-- Player clues (maps NPCEvidenceState.discoveredClues)
CREATE TABLE IF NOT EXISTS player_clues (
    clue_id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id uuid REFERENCES auth.users(id) ON DELETE CASCADE,
    npc_slug text REFERENCES npc_definitions(npc_slug),
    clue_text text NOT NULL,
    category text DEFAULT 'general',
    game_time float,
    discovered_at timestamptz DEFAULT now(),
    UNIQUE (user_id, clue_text)
);

-- Player items (maps NPCEvidenceState.obtainedItems)
CREATE TABLE IF NOT EXISTS player_items (
    item_id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id uuid REFERENCES auth.users(id) ON DELETE CASCADE,
    item_slug text NOT NULL,
    item_name text NOT NULL,
    description text,
    npc_source_slug text REFERENCES npc_definitions(npc_slug),
    acquired_at timestamptz DEFAULT now(),
    UNIQUE (user_id, item_slug)
);

-- Player locations (maps NPCEvidenceState.visitedLocations)
CREATE TABLE IF NOT EXISTS player_locations (
    location_id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id uuid REFERENCES auth.users(id) ON DELETE CASCADE,
    location_name text NOT NULL,
    npc_slug text REFERENCES npc_definitions(npc_slug),
    visited_at timestamptz DEFAULT now(),
    UNIQUE (user_id, location_name)
);

-- Indexes
CREATE INDEX IF NOT EXISTS idx_dialogue_sessions_user_npc ON dialogue_sessions(user_id, npc_slug);
CREATE INDEX IF NOT EXISTS idx_dialogue_turns_session ON dialogue_turns(session_id, created_at);
CREATE INDEX IF NOT EXISTS idx_player_npc_relationships_user ON player_npc_relationships(user_id);
CREATE INDEX IF NOT EXISTS idx_room_memberships_room ON room_memberships(room_id);
CREATE INDEX IF NOT EXISTS idx_dialogue_sessions_room ON dialogue_sessions(room_id);
