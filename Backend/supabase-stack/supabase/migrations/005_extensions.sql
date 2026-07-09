-- ============================================================
-- Migration 005: Enable production-grade PostgreSQL extensions
-- ============================================================
-- Prerequisite: supabase_admin role (needed by supabase extension installers)
do $$
begin
  if not exists (select from pg_roles where rolname = 'supabase_admin') then
    create role supabase_admin with login superuser createdb createrole;
  end if;
end
$$;

-- ============================================================
-- All extension versions confirmed available in:
--   supabase/postgres:17.6.1.136
-- Installed into public schema for application access.
-- ============================================================

-- 1. pgcrypto — cryptographic functions
--    Used for: password hashing, token generation, encrypting
--    sensitive player data at rest.
create extension if not exists pgcrypto with schema public cascade;
comment on extension pgcrypto is 'Cryptographic functions for player auth tokens and data encryption';

-- 2. pg_net — async HTTP requests from PostgreSQL
--    Used for: firing webhooks from triggers (e.g., notify Java
--    game server on NPC dialogue event, player action, or
--    matchmaking signal). Non-blocking.
create extension if not exists pg_net with schema public cascade;
comment on extension pg_net is 'Async HTTP/S requests from triggers to backend game servers';

-- 3. pgmq — job queue
--    Used for: matchmaking queue, deferred event processing,
--    outbox pattern for reliable game event delivery.
create extension if not exists pgmq;
comment on extension pgmq is 'Lightweight job queue for matchmaking and event processing';

-- 4. pg_cron — scheduled job scheduler
--    Used for: daily leaderboard reset, session cleanup,
--    periodic RAG index refresh.
create extension if not exists pg_cron with schema public cascade;
grant usage on schema cron to postgres with grant option;
grant all privileges on all tables in schema cron to postgres with grant option;
grant all privileges on all sequences in schema cron to postgres with grant option;
comment on extension pg_cron is 'Scheduled job runner for maintenance and periodic tasks';

-- 5. pgjwt — JWT signing inside SQL
--    Used for: signing service-to-service JWTs, generating
--    custom access tokens for game server RPC calls.
create extension if not exists pgjwt with schema public cascade;
comment on extension pgjwt is 'JWT signing within SQL for service auth tokens';

-- 6. hstore — key-value metadata
--    Used for: flexible metadata on game entities (player
--    settings, NPC state flags) without schema changes.
create extension if not exists hstore with schema public;
comment on extension hstore is 'Key-value store for flexible entity metadata';

-- 7. vector — NPC dialogue embeddings, codebase RAG, semantic
--    search. Already enabled in earlier migrations.
-- (skip - installed separately when needed)

-- 8. uuid-ossp — UUID generation (pre-14 compat)
--    Included in supabase/postgres base image.
create extension if not exists "uuid-ossp";
comment on extension "uuid-ossp" is 'UUID generation functions for older PG compatibility';
