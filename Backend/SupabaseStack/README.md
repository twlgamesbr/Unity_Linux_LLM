# Supabase stack scaffold

Minimal self-hosted Supabase topology for local Unity backend work.

## Layout

- `docker-compose.yml` — all Supabase services pinned to stable versions
- `supabase/migrations/` — database migrations (applied in order on first boot)
  - `0001_initial.sql` — scaffold
  - `002_gameplay_schema.sql` — NPC dialogue sessions, turns, evidence
  - `003_seed_data.sql` — default NPCs (Butler, Maid, Chef)
  - `004_security_policies.sql` — RLS + RPCs
  - `005_extensions.sql` — production extensions (pgcrypto, pg_net, pgmq, pg_cron, pgjwt, hstore)
- `supabase/functions/main/` — Edge Functions entry point (Deno runtime)
- `start` / `stop` / `status` — compose helpers

## Ports

| Service       | Port  | Purpose                          |
|---------------|-------|----------------------------------|
| DB            | 55432 | PostgreSQL                       |
| Auth (Gotrue) | 8091  | User auth / JWT issuance         |
| REST          | 8092  | PostgREST auto-API               |
| Realtime      | 8093  | WebSocket subscriptions          |
| Storage       | 8094  | File storage + transforms        |
| Imgproxy      | 8095  | Image transformation proxy       |
| Meta          | 8096  | Postgres-meta admin API          |
| Studio        | 8097  | Supabase web dashboard           |
| Functions     | 8098  | Edge Runtime (Deno serverless)   |

## Run

```bash
./start
./status
./stop
```

The helper scripts just wrap `docker compose` in this folder.

## Super Admin Access

Self-hosted Studio authenticates via local Gotrue. Create an admin:

```bash
# 1. Sign up (autoconfirm enabled — no email needed)
curl -X POST http://localhost:8091/auth/v1/signup \
  -H "apikey: dev-local-anon-key" \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@npc-game.local","password":"supabase-admin-123"}'

# 2. Promote to service_role via psql
docker exec -it supabase-stack-db psql -U postgres -c "
UPDATE auth.users
SET raw_app_meta_data =
    COALESCE(raw_app_meta_data, '{}'::jsonb) || '{\"role\": \"service_role\", \"claims_admin\": true}'::jsonb
WHERE email = 'admin@npc-game.local';
"

# 3. Log in at http://localhost:8097 (use the email/password from step 1)
```

## Edge Functions

Serverless Deno functions run at **port 8098**. The entry point is `supabase/functions/main/index.ts`.

Current routes:
| Route                     | Purpose                             |
|---------------------------|-------------------------------------|
| `GET  /health`            | Health check                        |
| `POST /matchmaking/join`  | Queue a player into pgmq matchmaking |
| `POST /npc/dialogue-hook` | Forward NPC dialogue to game server  |

Add new functions by creating files under `supabase/functions/` and routing them in `main/index.ts`.
