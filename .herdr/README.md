# Herdr Workflow for Unity_Linux_LLM

## Decision

Do not create `.herdr/config.toml` as the project authority. Herdr loads `~/.config/herdr/config.toml` by default, and the official docs describe `HERDR_CONFIG_PATH` as the explicit override. A repo-local config file would be inert unless every launch used that override.

Use `.herdr/` for project-local operating guidance, lightweight scripts, prompt templates, and usage snapshots. Keep global UI/key/theme defaults in `~/.config/herdr/config.toml`.

If a project-specific Herdr config becomes necessary later, launch a separate named session explicitly:

```bash
HERDR_CONFIG_PATH="$PWD/.herdr/config.toml" herdr --session unity-linux-llm
```

Do not mix that with the default session unless the separation is intentional.

## Current Workspace Layout (as of 2026-07-17)

```
Workspace: Unity_Linux_LLM (w1)
  ├── Tab 1: opencode (w1:t1) ─── 1 pane    opencode agent (idle)
  ├── Tab 2: codex (w1:t2) ────── 1 pane    codex agent (idle)
  ├── Tab 4: hermes (w1:t4) ───── 1 pane    hermes agent (working, focused)
  ├── Tab 5: logs (w1:t5) ─────── 3 panes
  │   ├── services ─── watch -n 30 .herdr/bin/service-health.sh
  │   ├── containers ─ docker ps watcher
  │   └── unity-logs ─ Unity log tail shell
  └── Tab 6: build (w1:t6) ───── 1 pane    Unity compile/test/build shell
```

**Service health tokens** (live in workspace sidebar):

| Token     | Status                |
|-----------|-----------------------|
| `localai` | up/down (auto-probed) |
| `qdrant`  | up/down               |
| `cognee`  | up/down               |
| `supabase`| up/down               |
| `herdr`   | version               |

Tokens update via `herdr workspace report-metadata`. Run a snapshot to capture a point-in-time record.

Prefer tabs for durable surfaces and panes for temporary work inside a surface. Do not create worktrees unless a task needs isolated Git state.

## Agent Routing

Use hosted coding agents for repo mutation, architectural reasoning, Unity scene wiring, and final verification. Use local models through LocalAI for bounded, low-risk work where latency and cost matter more than broad reasoning.

Good local-model candidates:

- Summarize recent logs from `Diagnostics/Logs/`, Docker, Unity server, and WebGL browser probes.
- Classify failures into buckets: Unity compile, WebGL load, LocalAI, Qdrant, Cognee, Supabase, Datadog.
- Draft candidate reproduction notes from existing logs.
- Rank candidate files from CodebaseEmbedder/Qdrant search output.
- Produce first-pass changelog or session summaries from Herdr snapshots.

Keep hosted agents on:

- Editing C#, Python, TypeScript, or shell scripts.
- Unity scene mutations and prefab wiring.
- Security-sensitive Datadog/Supabase/Auth changes.
- Debugging runtime failures with unknown root cause.
- Deciding whether local-model summaries are trustworthy.

## Integrations

Current installed integrations for this project:

| Integration | Version | Status |
|-------------|---------|--------|
| `codex`     | v6      | primary implementation agent |
| `opencode`  | v8      | alternate coding agent |
| `hermes`    | v3      | primary orchestrator (this session) |
| `claude`    | v7      | Claude Code agent |
| `cursor`    | v1      | Cursor IDE agent |
| `copilot`   | v2      | GitHub Copilot agent |

Check integration health with:

```bash
herdr integration status
herdr agent list
```

Install only the integrations you actually use:

```bash
herdr integration install codex
herdr integration install opencode
herdr integration install claude
```

## Usage Tracking

Run a snapshot before and after major agent work:

```bash
bash .herdr/bin/snapshot.sh
```

Snapshots are written under `.herdr/snapshots/` and ignored by Git. They capture Herdr status, agent/pane state, service health, live containers, endpoint probes, and latest project log names without copying full logs into the repository.

## Log Convergence

Treat Herdr as the operator surface, not the log database. Keep live log panes in the `logs` tab and keep durable logs in their native systems:

- Unity automation logs: `Diagnostics/Logs/`
- WebGL client: `docker logs npc-webgl-client`
- Dedicated server: `docker logs npc-dedicated-server`
- LocalAI: `docker logs localai-orchestrator`
- LocalAI proxy (consolidated Datadog proxy — chat-completions LLM-Obs tracing + generic passthrough tracing for everything else): `docker logs localai-proxy`
- Qdrant: `docker logs qdrant`
- Datadog agent: `docker logs dd-agent`
- Supabase stack: `Backend/supabase-stack/status` and matching Docker containers
- Cognee API: `journalctl --user -u cognee-api.service`

LocalAI can summarize logs after collection, but it should not be the only source of truth. Preserve raw logs and use summaries as triage aids.

## Practical Command Set

Inspect the current Herdr topology:

```bash
herdr workspace list
herdr tab list --workspace "$HERDR_WORKSPACE_ID"
herdr pane list --workspace "$HERDR_WORKSPACE_ID"
herdr agent list
```

Create a background command pane without stealing focus:

```bash
herdr pane split --current --direction right --no-focus
```

Read a pane transcript:

```bash
herdr pane read <pane_id> --source recent-unwrapped --lines 160
```

Report project metadata into the workspace sidebar:

```bash
herdr workspace report-metadata "$HERDR_WORKSPACE_ID" --source unity-linux-llm --token localai=up --token qdrant=up
```
