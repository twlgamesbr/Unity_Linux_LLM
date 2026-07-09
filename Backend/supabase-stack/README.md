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
# 1. Insert admin user directly into auth schema (signup endpoint may fail
#    because pop migration runner doesn't handle search_path correctly)
docker exec -i supabase-stack-db psql -U postgres <<'SQLEOF'
-- Insert into auth.users
INSERT INTO auth.users (
    id, instance_id, email, encrypted_password,
    email_confirmed_at, phone_confirmed_at,
    confirmation_sent_at, confirmed_at,
    raw_app_meta_data, raw_user_meta_data,
    created_at, updated_at, aud, role
) VALUES (
    gen_random_uuid(), '00000000-0000-0000-0000-000000000000',
    'admin@npc-game.local',
    '$2a$10$abcdefghijklmnopqrstuvwxyz0123456789ABCDEFGHIJKLMNOPQ', -- bcrypt placeholder
    now(), now(), now(), now(),
    '{"provider":"email","providers":["email"],"role":"service_role","claims_admin":true}'::jsonb,
    '{"display_name":"Super Admin"}'::jsonb,
    now(), now(), 'authenticated', 'authenticated'
)
ON CONFLICT (email) DO UPDATE
SET raw_app_meta_data =
    COALESCE(auth.users.raw_app_meta_data, '{}'::jsonb) ||
    '{"role": "service_role", "claims_admin": true}'::jsonb,
    raw_user_meta_data = '{"display_name":"Super Admin"}'::jsonb;
SQLEOF

# 2. Set a known bcrypt hash for the admin password
docker exec -i supabase-stack-db psql -U postgres \
  -c "UPDATE auth.users SET encrypted_password = '$2a$10$' || encode(gen_random_bytes(52), 'hex') || '=' WHERE email = 'admin@npc-game.local';"

# Run the Python script to set a known password:
python3 -c "
import bcrypt
import subprocess
pw = b'supabase-admin-123'
hashed = bcrypt.hashpw(pw, bcrypt.gensalt(rounds=10))
hashed_str = hashed.decode()
cmd = ['docker', 'exec', '-i', 'supabase-stack-db', 'psql', '-U', 'postgres',
       '-c', f\"UPDATE auth.users SET encrypted_password = '{hashed_str}' WHERE email = 'admin@npc-game.local';\"]
subprocess.run(cmd, check=True)
print('Password set successfully')
"

# 3. Create corresponding identity record
docker exec -i supabase-stack-db psql -U postgres <<'SQLEOF'
INSERT INTO auth.identities (
    id, user_id, identity_data, provider, provider_id,
    last_sign_in_at, created_at, updated_at
)
SELECT
    gen_random_uuid(), id,
    jsonb_build_object('sub', id::text, 'email', email),
    'email', email,
    now(), now(), now()
FROM auth.users WHERE email = 'admin@npc-game.local'
ON CONFLICT DO NOTHING;
SQLEOF

# 4. Log in at http://localhost:8097
#    email: admin@npc-game.local
#    password: supabase-admin-123
```

> **Note:** The Gotrue (auth) container connects using `?search_path=public,auth` so that
> unqualified table references (`users`, `identities`) resolve correctly at runtime.

## Edge Functions

Serverless Deno functions run at **port 8098**. The entry point is `supabase/functions/main/index.ts`.

Current routes:
| Route                     | Purpose                             |
|---------------------------|-------------------------------------|
| `GET  /health`            | Health check                        |
| `POST /matchmaking/join`  | Queue a player into pgmq matchmaking |
| `POST /npc/dialogue-hook` | Forward NPC dialogue to game server  |

Add new functions by creating files under `supabase/functions/` and routing them in `main/index.ts`.
