# Datadog Host Monitoring — NPC Platform

This directory contains the configuration for monitoring the NPC Platform stack
through Datadog. A single Datadog Agent runs on the host (`network_mode: host`)
and collects metrics, logs, APM traces, and live processes from all services.

---

## Quick Start

### 1. Set API Key

Add your Datadog API key to `Backend/.env`:

```env
DD_API_KEY=your_key_here
DD_SITE=us5.datadoghq.com   # already configured
```

### 2. Start the Agent

```bash
cd Backend/datadog-host
docker compose up -d
```

The agent auto-discovers Docker containers and starts collecting:
- System metrics (CPU, memory, disk, network, GPU via NVIDIA)
- Container metrics (per-container CPU, memory, network)
- All container stdout/stderr logs (with `container_collect_all: true`)
- Custom DogStatsD metrics from the Unity dedicated server
- APM traces from instrumented services

### 3. Import Dashboard

1. Open Datadog → Dashboards → New Dashboard → Import Dashboard JSON
2. Paste the contents of `dashboard.json`
3. Save as "NPC Platform — Unity Linux LLM"

---

## Integration Configs

Verified against the live agent (`docker exec dd-agent agent status`) on 2026-07-17:

| Directory | Service | Purpose |
|-----------|---------|---------|
| `conf.d/openmetrics.d/conf.yaml` | LocalAI + Qdrant | Single OpenMetrics check instance per service — LLM inference metrics (`localai` namespace) and vector DB collection/search metrics (`qdrant` namespace) |
| `conf.d/nginx.d/conf.yaml` | Nginx (WebGL client) | Built-in `nginx` check against `nginx.conf`'s `/nginx_status` stub_status endpoint, plus structured log tailing (source: `nginx`) |
| *(none needed)* | Unity Dedicated Server | DogStatsD custom metrics arrive via UDP `:8125` with no check config required — they show up automatically under **Metrics → Summary** once `DatadogMetricsService.Initialize()` runs. There is no `unity.d/` integration folder because DogStatsD metrics aren't a "check"; this row previously implied a config file that doesn't exist. |

There is no dedicated `localai.d/` or `qdrant.d/` folder — both are covered by the single `openmetrics.d/conf.yaml` (one Agent check, two `instances:` entries). Split them into separate folders only if you need independent `min_collection_interval` or tagging per service.

---

## Custom DogStatsD Metrics (from Unity)

The Unity dedicated server emits these metrics via `DatadogMetricsService`
(UDP port 8125). They appear automatically in Datadog under **Metrics → Summary**.

### LLM Inference
| Metric | Type | Tags |
|--------|------|------|
| `llm.request.duration` | timer | model, attempt |
| `llm.request.count` | counter | model, status |
| `llm.request.error` | counter | model, reason |

### Dialogue System
| Metric | Type | Tags |
|--------|------|------|
| `dialogue.manager.initialize.duration` | timer | profile_count, use_qdrant |
| `dialogue.manager.initialize.count` | counter | status |
| `dialogue.npc.switch.count` | counter | npc, display_name |
| `dialogue.session.turn.duration` | timer | npc, action_type, has_response |
| `dialogue.session.turn.count` | counter | npc, action_type |
| `dialogue.session.error` | counter | npc, exception |
| `dialogue.localai.request.duration` | timer | npc, model, status |
| `dialogue.localai.request.count` | counter | npc, model, status |

### Qdrant Vector Search
| Metric | Type | Tags |
|--------|------|------|
| `qdrant.search.duration` | timer | limit, result_count |
| `qdrant.search.count` | counter | result_count |
| `qdrant.search.error` | counter | reason, exception |
| `qdrant.search.result_count` | gauge | collection |

### Auth & Network
| Metric | Type | Tags |
|--------|------|------|
| `auth.login.count` | counter | mode |
| `network.server.started` | counter | listen_port, transport |
| `network.client.connected` | counter | is_server, is_client |
| `network.client.disconnected` | counter | reason |
| `network.mode.start` | counter | mode |

---

## Dashboard Widgets

The `dashboard.json` includes six widget groups:

1. **🚀 Service Overview** — Container resource usage + health checks
2. **🤖 LLM / LocalAI** — Request latency (avg/p95/p99), throughput, error rate
3. **💬 Dialogue System** — Turn volume, duration, errors, most active NPCs
4. **🗄️ Qdrant Vector DB** — Search latency, throughput, result counts, collection size
5. **🔐 Auth & Network** — Login volume, server starts, client connections, active users
6. **🖥️ Linux Host OS** — CPU, memory, disk I/O, network traffic, GPU utilization, disk space

---

## Verifying Metrics

Check that custom metrics are flowing:

```bash
# Search for Unity custom metrics in Datadog
docker exec dd-agent agent check dogstatsd  # Show recent DogStatsD metrics

# Check agent status
docker exec dd-agent agent status
```

Or in the Datadog UI: **Metrics → Summary** → search for `llm.request.count`.
