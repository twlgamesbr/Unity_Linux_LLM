-- ============================================================
-- Migration 009: Create missing player profile RPC
-- ============================================================
-- The Unity client calls create_or_update_player_profile after
-- every login/register to sync the auth.users identity into the
-- player_profiles table. The function was originally created in
-- migration 004, but with a `player_profiles` return type.
-- This migration replaces it with a more resilient jsonb version.
-- ============================================================

-- Must drop first because return type cannot be changed with
-- `create or replace`.
drop function if exists create_or_update_player_profile(text);

create function create_or_update_player_profile(
    p_display_name text
)
returns jsonb
language plpgsql
volatile
security definer
set search_path = public
as $$
declare
    v_user_id uuid := auth.uid();
begin
    if v_user_id is null then
        return jsonb_build_object(
            'success', false,
            'error', 'Not authenticated'
        );
    end if;

    insert into player_profiles (user_id, display_name, last_login_at, is_online)
    values (v_user_id, p_display_name, now(), true)
    on conflict (user_id) do update set
        display_name = excluded.display_name,
        last_login_at = now(),
        is_online = true,
        updated_at = now();

    return jsonb_build_object(
        'success', true,
        'user_id', v_user_id::text,
        'display_name', p_display_name
    );
end;
$$;

grant execute on function create_or_update_player_profile(text) to anon;
grant execute on function create_or_update_player_profile(text) to authenticated;
grant execute on function create_or_update_player_profile(text) to service_role;
