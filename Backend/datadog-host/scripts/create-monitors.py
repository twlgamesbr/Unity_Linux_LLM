#!/usr/bin/env python3
"""
Create missing Datadog monitors for the NPC System stack.

Existing monitors (already created):
  - [NPC System] LLM Inference Latency High
  - [NPC System] Dialogue Turn Latency High
  - [NPC System] Qdrant RAG Search Errors Spike
  - [NPC System] Realtime WebSocket Server Errors

This script creates:
  1. [NPC System] NPC Server Error Logs — errors from Unity dedicated server
  2. [NPC System] Nginx 5xx Error Rate — HTTP errors from WebGL client
  3. [NPC System] Container Health — critical containers down
  4. [NPC System] Datadog APM Trace Errors — trace decode failures

Run once:
  cd Backend/datadog-host && python3 scripts/create-monitors.py
"""

import json
import os
import sys
import urllib.request
import urllib.error
from pathlib import Path


def load_env(env_path: str) -> dict:
    """Parse a simple KEY=VALUE .env file."""
    env = {}
    with open(env_path) as f:
        for line in f:
            line = line.strip()
            if not line or line.startswith("#"):
                continue
            if "=" in line:
                key, _, value = line.partition("=")
                env[key.strip()] = value.strip()
    return env


def api_call(method: str, path: str, body: dict, api_key: str, app_key: str, site: str) -> dict:
    """Make a Datadog API v1 call (monitor CRUD uses v1)."""
    url = f"https://api.{site}/api/v1/monitor{path}"
    data = json.dumps(body).encode("utf-8")
    req = urllib.request.Request(
        url,
        data=data,
        method=method,
        headers={
            "Content-Type": "application/json",
            "DD-API-KEY": api_key,
            "DD-APPLICATION-KEY": app_key,
        },
    )
    try:
        with urllib.request.urlopen(req) as resp:
            return json.loads(resp.read().decode("utf-8"))
    except urllib.error.HTTPError as e:
        error_body = e.read().decode("utf-8", errors="replace")
        print(f"  ✗ HTTP {e.code}: {error_body[:300]}")
        return {"error": True, "status_code": e.code, "body": error_body}


def list_existing_monitors(api_key: str, app_key: str, site: str) -> list:
    """List all existing monitors matching our tag."""
    url = f"https://api.{site}/api/v1/monitor?q=tag:team:npc-platform&pageSize=100"
    req = urllib.request.Request(
        url,
        headers={
            "DD-API-KEY": api_key,
            "DD-APPLICATION-KEY": app_key,
        },
    )
    try:
        with urllib.request.urlopen(req) as resp:
            data = json.loads(resp.read().decode("utf-8"))
            # v1 API returns a list directly
            if isinstance(data, list):
                return data
            return data.get("monitors", [])
    except urllib.error.HTTPError as e:
        print(f"  ✗ Failed to list monitors: HTTP {e.code}")
        return []


MONITORS = [
    # ── 1. Unity NPC Server Error Logs ──────────────────────────────
    {
        "name": "[NPC System] NPC Server Error Logs",
        "type": "log alert",
        "query": 'logs("service:npc-server env:production status:error").rollup("count").last("15m") > 5',
        "message": """{{#is_alert}}
## 🚨 NPC Server Error Log Spike

**{{value}}** error logs detected from the Unity dedicated server in the last 15 minutes — exceeding critical threshold of **{{threshold}}**.

The NPC dialogue server is encountering errors that may affect player experience.
{{/is_alert}}
{{#is_warning}}
## ⚠️ NPC Server Errors Elevated

**{{value}}** error logs detected — exceeding warning threshold of **{{warn_threshold}}**.
{{/is_warning}}
{{#is_recovery}}
## ✅ NPC Server Errors Resolved

Error log volume has returned to normal. Current count: **{{value}}**.
{{/is_recovery}}

---

## Runbook

1. Check [NPC server logs](/logs?query=service%3Anpc-server+env%3Aproduction+status%3Aerror) for error details.
2. Check [NPC server container](/containers?query=container_name%3Anpc-dedicated-server) — is it crash-looping?
3. Review [Unity NPC LLM Dashboard](/dashboard/7g5-j72-f8t) for related metrics.
4. Check if LocalAI or Qdrant backends are healthy.

### Related Links
- [Unity NPC LLM Dashboard](/dashboard/7g5-j72-f8t)
- [NPC Server Logs](/logs?query=service%3Anpc-server+env%3Aproduction)

@slack-npc-platform-handle""",
        "tags": ["team:npc-platform", "service:npc-server", "env:production"],
        "options": {
            "thresholds": {"critical": 5, "warning": 2},
            "notify_no_data": True,
            "no_data_timeframe": 30 * 60,
        },
    },
    # ── 2. Nginx 5xx Error Rate ─────────────────────────────────────
    {
        "name": "[NPC System] Nginx 5xx Error Rate",
        "type": "log alert",
        "query": 'logs("service:webgl-client env:production @http.status_code:[500 TO 599]").rollup("count").last("15m") > 20',
        "message": """{{#is_alert}}
## 🚨 Nginx 5xx Error Spike

**{{value}}** HTTP 5xx errors detected from the WebGL client nginx in the last 15 minutes — exceeding critical threshold of **{{threshold}}**.

The WebGL client is experiencing server-side errors. Players may be unable to load the game or access the Supabase API.
{{/is_alert}}
{{#is_warning}}
## ⚠️ Nginx 5xx Errors Elevated

**{{value}}** 5xx errors detected — exceeding warning threshold of **{{warn_threshold}}**.
{{/is_warning}}
{{#is_recovery}}
## ✅ Nginx 5xx Errors Resolved

Error rate has returned to normal. Current count: **{{value}}**.
{{/is_recovery}}

---

## Runbook

1. Check [nginx error logs](/logs?query=service%3Awebgl-client+env%3Aproduction+status%3Aerror) for upstream errors.
2. Verify backend services are running: `docker ps | grep -E 'supabase|realtime|kong'`
3. Check if Supabase REST/Realtime services are healthy.
4. Check [nginx access logs](/logs?query=service%3Awebgl-client+env%3Aproduction) for request patterns.

### Related Links
- [Unity NPC LLM Dashboard](/dashboard/7g5-j72-f8t)
- [WebGL Client Logs](/logs?query=service%3Awebgl-client+env%3Aproduction)

@slack-npc-platform-handle""",
        "tags": ["team:npc-platform", "service:webgl-client", "env:production"],
        "options": {
            "thresholds": {"critical": 20, "warning": 10},
            "notify_no_data": True,
            "no_data_timeframe": 30 * 60,
        },
    },
    # ── 3. Critical Container Health ────────────────────────────────
    {
        "name": "[NPC System] Critical Container Down",
        "type": "metric alert",
        "query": 'max(last_5m):min:docker.containers.running{name:(npc-dedicated-server OR npc-webgl-client OR dd-agent OR ddproxy)} by {name} < 1',
        "message": """{{#is_alert}}
## 🚨 Critical Container Down

Container **{{name}}** has been stopped for the last 5 minutes.

This is a critical service in the NPC System stack — players will be affected.
{{/is_alert}}
{{#is_recovery}}
## ✅ Container Recovered

Container **{{name}}** is running again.
{{/is_recovery}}

---

## Runbook

1. Check container status: `docker ps -a | grep {{name}}`
2. View container logs: `docker logs --tail 100 {{name}}`
3. Check if the container was OOM-killed: `docker inspect {{name}} | grep OOMKilled`
4. Restart the container: `docker compose -f Backend/*/docker-compose.yml up -d {{name}}`

@slack-npc-platform-handle""",
        "tags": ["team:npc-platform", "env:production"],
        "options": {
            "thresholds": {"critical": 1},
            "notify_no_data": True,
            "no_data_timeframe": 5 * 60,
        },
    },
    # ── 4. APM Trace Decode Errors ──────────────────────────────────
    {
        "name": "[NPC System] APM Trace Errors",
        "type": "log alert",
        "query": 'logs("service:npc-server env:production (DatadogTrace OR MessagePack OR APM OR trace_agent)").rollup("count").last("15m") > 10',
        "message": """{{#is_alert}}
## 🚨 APM Trace Submission Errors

**{{value}}** APM trace-related errors detected from the Unity server in the last 15 minutes.

The custom MessagePack encoder (DatadogTraceService) may be failing to encode or send traces to the Datadog Agent.
{{/is_alert}}
{{#is_warning}}
## ⚠️ APM Trace Errors Elevated

**{{value}}** trace errors detected — exceeding warning threshold of **{{warn_threshold}}**.
{{/is_warning}}
{{#is_recovery}}
## ✅ APM Trace Errors Resolved

Trace submission errors have returned to normal.
{{/is_recovery}}

---

## Runbook

1. Check [NPC server logs](/logs?query=service%3Anpc-server+env%3Aproduction+DatadogTrace) for trace encoding errors.
2. Verify the Datadog Agent is listening on port 8126: `curl -s http://localhost:8126/v0.5/traces | head`
3. Review the DatadogTraceService implementation in `Assets/Scripts/Runtime/Monitoring/DatadogTraceService.cs`.

### Related Links
- [Datadog APM Traces](/apm/traces?env=production)
- [Unity NPC LLM Dashboard](/dashboard/7g5-j72-f8t)

@slack-npc-platform-handle""",
        "tags": ["team:npc-platform", "service:npc-server", "env:production"],
        "options": {
            "thresholds": {"critical": 10, "warning": 5},
            "notify_no_data": True,
            "no_data_timeframe": 30 * 60,
        },
    },
]


def main():
    env_path = Path(__file__).resolve().parent.parent / ".env"
    if not env_path.exists():
        print(f"✗ .env not found at {env_path}")
        sys.exit(1)

    env = load_env(str(env_path))
    api_key = env.get("DD_API_KEY")
    app_key = env.get("DD_APP_KEY")
    site = env.get("DD_SITE", "us5.datadoghq.com")

    if not api_key or not app_key:
        print("✗ DD_API_KEY or DD_APP_KEY not set in .env")
        sys.exit(1)

    print(f"🔍 Listing existing NPC System monitors...")
    existing = list_existing_monitors(api_key, app_key, site)
    existing_names = {m["name"] for m in existing}
    print(f"   Found {len(existing)} existing monitors: {', '.join(existing_names)}")
    print()

    created = 0
    skipped = 0
    failed = 0

    for monitor_def in MONITORS:
        name = monitor_def["name"]
        if name in existing_names:
            print(f"⏭  Skipping (already exists): {name}")
            skipped += 1
            continue

        print(f"Creating: {name}")
        body = {
            "name": name,
            "type": monitor_def["type"],
            "query": monitor_def["query"],
            "message": monitor_def["message"],
            "tags": monitor_def.get("tags", []),
            "options": monitor_def.get("options", {}),
        }
        result = api_call("POST", "", body, api_key, app_key, site)
        if result.get("error"):
            print(f"  ✗ Failed to create monitor: {name}")
            failed += 1
        else:
            monitor_id = result.get("id", "unknown")
            print(f"  ✓ Created monitor {monitor_id}: {name}")
            created += 1
        print()

    print("=" * 60)
    print(f"Results: {created} created, {skipped} skipped, {failed} failed")
    print("=" * 60)

    if failed:
        sys.exit(1)


if __name__ == "__main__":
    main()
