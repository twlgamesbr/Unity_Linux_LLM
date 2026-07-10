-- ============================================================
-- Migration 008: Edge Function supporting RPCs
-- ============================================================
-- Adds helper RPCs used by the Edge Function memory
-- orchestration layer (Phase 4).
-- ============================================================

-- ─── RPC: embed_and_store_turn ──────────────────────────
-- Called by Unity client after saving a turn.
-- Generates embedding (via Edge Function) and stores in
-- dialogue_turn_vectors.
-- (The actual embedding call goes through the Edge Function;
--  this RPC is a direct insert helper for the service role.)

-- ─── RPC: upsert_npc_relationship ───────────────────────
-- Convenience RPC for the Edge Function to update trust/mood.
-- Avoids exposing player_npc_relationships table directly.
create or replace function upsert_npc_relationship(
    p_user_id uuid,
    p_npc_slug text,
    p_trust_score int,
    p_current_mood text,
    p_last_interaction_at timestamptz default now()
)
returns jsonb
language plpgsql
volatile
security definer
set search_path = public
as $$
begin
    insert into player_npc_relationships (
        user_id, npc_slug, trust_score, current_mood, last_interaction_at
    ) values (
        p_user_id, p_npc_slug, p_trust_score, p_current_mood, p_last_interaction_at
    )
    on conflict (user_id, npc_slug) do update set
        trust_score = excluded.trust_score,
        current_mood = excluded.current_mood,
        last_interaction_at = excluded.last_interaction_at,
        dialogue_count = player_npc_relationships.dialogue_count + 1,
        updated_at = now();

    return jsonb_build_object(
        'success', true,
        'user_id', p_user_id::text,
        'npc_slug', p_npc_slug,
        'trust_score', p_trust_score,
        'current_mood', p_current_mood
    );
end;
$$;

-- ─── RPC: store_turn_vector ─────────────────────────────
-- Direct insert into dialogue_turn_vectors (called by Edge Function).
create or replace function store_turn_vector(
    p_turn_id bigint,
    p_user_id uuid,
    p_npc_slug text,
    p_role text,
    p_content_hash text,
    p_embedding vector(768)
)
returns jsonb
language plpgsql
volatile
security definer
set search_path = public
as $$
begin
    insert into dialogue_turn_vectors (
        turn_id, user_id, npc_slug, role, content_hash, embedding
    ) values (
        p_turn_id, p_user_id, p_npc_slug, p_role, p_content_hash, p_embedding
    )
    on conflict (content_hash, turn_id) do nothing;

    return jsonb_build_object(
        'success', true,
        'turn_id', p_turn_id::text
    );
end;
$$;

-- ─── Grant service_role execution permissions ───────────
grant execute on function upsert_npc_relationship(uuid, text, int, text, timestamptz)
    to service_role;
grant execute on function store_turn_vector(bigint, uuid, text, text, text, vector)
    to service_role;

-- ─── Allow service_role to insert into dialogue_turn_vectors ──
-- (already covered by the RPC above, but explicit grant for direct access)
grant insert on dialogue_turn_vectors to service_role;
grant usage on sequence dialogue_turn_vectors_id_seq to service_role;
