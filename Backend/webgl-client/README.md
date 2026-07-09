# NPC Dedicated Server (Docker)

Runs the Unity dedicated server binary in a minimal Ubuntu 22.04 container.
Connects to your **existing host LocalAI** via Docker host networking — no
llama.cpp or GGUF models inside the container.

## Quick Start

```bash
# 1. Build the server in Unity
#    File → Build Settings → Server subtarget → Build → picks our scenes
#    Output to: Builds/Server/

# 2. Run with the server build bind-mounted
docker compose -f docker/docker-compose.yml up
```

This bind-mounts `Builds/Server/` into `/server` inside the container so you
can rebuild in Unity and just restart the container — no `docker build` needed.

## Building an immutable image (deployment)

```bash
# After Builds/Server/ is populated, build the image with the binaries baked in:
docker build -t npc-server -f docker/Dockerfile .
docker run --rm --network host npc-server
```

## Architecture

```
┌─────────────────────────────────────┐
│  Host machine                       │
│  ┌─────────────┐  ┌──────────────┐  │
│  │  LocalAI     │  │  Docker      │  │
│  │  8080 embed  │  │  Container   │  │
│  │  11435 chat  │──│  npc-server  │  │
│  │  11434 main  │  │  port 11474  │  │
│  └─────────────┘  │  (host net)  │  │
│                   └──────────────┘  │
│  ┌─────────────┐                    │
│  │  Qdrant     │  host networking   │
│  │  6333       │── localhost works  │
│  └─────────────┘  for all services  │
└─────────────────────────────────────┘
```

## Environment Variables

| Variable         | Default      | Description                        |
|------------------|--------------|------------------------------------|
| `SERVER_PORT`    | `11474`      | UDP/TCP port for Unity Transport   |
| `SERVER_ADDRESS` | `0.0.0.0`    | Listen address                     |

## Files

| File                  | Purpose                                |
|-----------------------|----------------------------------------|
| `Dockerfile`          | Container definition                   |
| `docker-compose.yml`  | Dev workflow (bind-mount + host net)   |
| `entrypoint.sh`       | Signal-safe launcher                   |
| `.dockerignore`       | Excludes build artifacts from context  |
