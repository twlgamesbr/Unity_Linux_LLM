-- ============================================================
-- Migration 007: Dialogue session analytics & vector search
-- ============================================================
-- Enables pgvector for embedding-based dialogue turn similarity
-- search, pgmq for async session event processing, and adds
-- analytics RPCs for session metadata aggregation.
-- ============================================================

-- ─── Extensions & Tables ───────────────────────────────────

create extension if not exists vector with schema public cascade;

-- Turn vectors table (nomic-embed-text-v1.5: 768 dimensions)
create table if not exists dialogue_turn_vectors (
    id bigserial primary key,
    turn_id bigint references dialogue_turns(turn_id) on delete cascade,
    user_id uuid references auth.users(id) on delete cascade,
    npc_slug text,
    role text not null check (role in ('user', 'assistant', 'system')),
    content_hash text,
    embedding vector(768),
    created_at timestamptz default now()
);

create index if not exists idx_turn_vectors_embedding
    on dialogue_turn_vectors
    using ivfflat (embedding vector_cosine_ops)
    with (lists = 100);

create index if not exists idx_turn_vectors_user_npc
    on dialogue_turn_vectors (user_id, npc_slug);

-- pgmq session event queue
select pgmq.create('session_events');

-- ─── Trigger: auto-enqueue pgmq job on turn insert ────────

create or replace function trigger_enqueue_turn_event()
returns trigger
language plpgsql
security definer
as $$
declare
    v_npc_slug text;
    v_user_id uuid;
begin
    -- Resolve npc_slug and user_id from the parent session
    select npc_slug, user_id into v_npc_slug, v_user_id
    from dialogue_sessions
    where session_id = new.session_id;

    perform pgmq.send(
        queue_name := 'session_events',
        msg := jsonb_build_object(
            'event_type', 'turn_inserted',
            'turn_id', new.turn_id,
            'session_id', new.session_id,
            'role', new.role,
            'npc_slug', v_npc_slug,
            'user_id', v_user_id,
            'created_at', new.created_at
        )
    );
    return new;
end;
$$;

do $$
begin
    if not exists (
        select 1
        from information_schema.triggers
        where trigger_name = 'trg_turn_insert_enqueue'
          and event_object_table = 'dialogue_turns'
    ) then
        create trigger trg_turn_insert_enqueue
            after insert on dialogue_turns
            for each row
            execute function trigger_enqueue_turn_event();
    end if;
end;
$$;

-- ─── RPC: update_session_turn_count ───────────────────────

create or replace function update_session_turn_count(
    p_session_id uuid
)
returns jsonb
language plpgsql
volatile
as $$
declare
    v_turn_count int;
    v_result jsonb;
begin
    update dialogue_sessions
    set turn_count = turn_count + 1,
        ended_at = now()
    where session_id = p_session_id
    returning turn_count into v_turn_count;

    if not found then
        return jsonb_build_object(
            'success', false,
            'error', 'session not found'
        );
    end if;

    return jsonb_build_object(
        'success', true,
        'turn_count', v_turn_count,
        'session_id', p_session_id::text,
        'ended_at', now()
    );
end;
$$;

-- ─── RPC: close_dialogue_session ──────────────────────────

create or replace function close_dialogue_session(
    p_player_id uuid,
    p_npc_slug text
)
returns jsonb
language plpgsql
volatile
as $$
declare
    v_session_id uuid;
begin
    select session_id into v_session_id
    from dialogue_sessions
    where user_id = p_player_id
      and npc_slug = p_npc_slug
      and ended_at is null
    order by started_at desc
    limit 1;

    if v_session_id is null then
        return jsonb_build_object(
            'success', false,
            'error', 'no open session found'
        );
    end if;

    update dialogue_sessions
    set ended_at = now()
    where session_id = v_session_id;

    perform pgmq.send(
        queue_name := 'session_events',
        msg := jsonb_build_object(
            'event_type', 'session_ended',
            'session_id', v_session_id,
            'player_id', p_player_id,
            'npc_slug', p_npc_slug
        )
    );

    return jsonb_build_object(
        'success', true,
        'session_id', v_session_id::text
    );
end;
$$;

-- ─── RPC: get_session_analytics ───────────────────────────

create or replace function get_session_analytics(
    p_user_id uuid,
    p_npc_slug text default null
)
returns jsonb
language plpgsql
stable
as $$
declare
    v_session_summary jsonb;
    v_turn_totals jsonb;
    v_recent_sessions jsonb;
begin
    select jsonb_build_object(
        'total_sessions', count(*),
        'total_turns', coalesce(sum(turn_count), 0),
        'avg_turns_per_session', round(coalesce(avg(turn_count), 0)::numeric, 1),
        'first_session_at', min(started_at),
        'last_session_at', max(started_at)
    ) into v_session_summary
    from dialogue_sessions
    where user_id = p_user_id
      and (p_npc_slug is null or npc_slug = p_npc_slug);

    select jsonb_build_object(
        'user_turns', count(*) filter (where role = 'user'),
        'assistant_turns', count(*) filter (where role = 'assistant'),
        'system_turns', count(*) filter (where role = 'system'),
        'total_turns', count(*)
    ) into v_turn_totals
    from dialogue_turns t
    join dialogue_sessions s on t.session_id = s.session_id
    where s.user_id = p_user_id
      and (p_npc_slug is null or s.npc_slug = p_npc_slug);

    select jsonb_agg(
        jsonb_build_object(
            'session_id', session_id,
            'npc_slug', npc_slug,
            'turn_count', turn_count,
            'started_at', started_at,
            'ended_at', ended_at,
            'summary', summary
        ) order by started_at desc
    ) into v_recent_sessions
    from (
        select session_id, npc_slug, turn_count, started_at, ended_at, summary
        from dialogue_sessions
        where user_id = p_user_id
          and (p_npc_slug is null or npc_slug = p_npc_slug)
        order by started_at desc
        limit 5
    ) sub;

    return jsonb_build_object(
        'session_summary', v_session_summary,
        'turn_totals', v_turn_totals,
        'recent_sessions', coalesce(v_recent_sessions, '[]'::jsonb)
    );
end;
$$;

-- ─── RPC: search_dialogue_turns ───────────────────────────

create or replace function search_dialogue_turns(
    p_user_id uuid,
    p_query_embedding vector(768),
    p_limit int default 5
)
returns jsonb
language plpgsql
stable
as $$
declare
    v_results jsonb;
begin
    select jsonb_agg(
        jsonb_build_object(
            'turn_id', tv.turn_id,
            'role', tv.role,
            'content', dt.content,
            'npc_slug', tv.npc_slug,
            'similarity', round((1 - (tv.embedding <=> p_query_embedding))::numeric, 4),
            'created_at', tv.created_at
        ) order by (tv.embedding <=> p_query_embedding)
    ) into v_results
    from dialogue_turn_vectors tv
    join dialogue_turns dt on dt.turn_id = tv.turn_id
    where tv.user_id = p_user_id
      and tv.embedding is not null
    limit p_limit;

    return coalesce(v_results, '[]'::jsonb);
end;
$$;
