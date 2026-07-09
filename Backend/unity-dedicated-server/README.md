# Unity NPC Dedicated Server (Docker Setup)

Runs the Unity Dedicated Server binary in a lightweight, minimal Ubuntu 22.04 container.
It communicates with your **host-based LocalAI** and other local microservices via **Docker host networking** (`localhost` loopback) so that no heavy models or GGUF dependencies are loaded inside the container.

## Quick Start (Development Workflow)

1. **Build the server in Unity**:
   - Go to `File` → `Build Settings`
   - Select `Dedicated Server` (Linux) as the Platform / subtarget
   - Set the output directory to `Builds/Server/`
2. **Launch the container**:
   ```bash
   docker compose -f docker_unity_dedicated_server/docker-compose.yml up
   ```

This launches the container with your `Builds/Server/` directory bind-mounted into `/server` inside the container. You can rebuild in Unity and simply restart the container without needing to perform a full `docker build`!

## Production / Immutable Image Build

To bake your compiled server build directly into an immutable Docker image for production deployments:

```bash
# Ensure Builds/Server/ is fully populated
docker build -t npc-dedicated-server -f docker_unity_dedicated_server/Dockerfile .

# Run the immutable container
docker run -d --restart unless-stopped --network host --name npc-dedicated-server npc-dedicated-server
```

## Architecture Diagram

```
┌─────────────────────────────────────┐
│  Host machine                       │
│  ┌─────────────┐  ┌──────────────┐  │
│  │  LocalAI     │  │  Docker      │  │
│  │  8080        │──│  Container   │  │
│  │  (and others)│  │  npc-server  │  │
│  └─────────────┘  │  port 11474  │  │
│                   │  (host net)  │  │
│  ┌─────────────┐  └──────────────┘  │
│  │  Qdrant     │  host networking   │
│  │  6333       │── localhost works  │
│  └─────────────┘  for all services  │
└─────────────────────────────────────┘
```

## Configurable Environment Variables

| Variable         | Default   | Description                                                           |
|------------------|-----------|-----------------------------------------------------------------------|
| `SERVER_PORT`    | `11474`   | The UDP and TCP port used by Unity Transport to accept connections.   |
| `SERVER_ADDRESS` | `0.0.0.0` | The address to bind/listen on inside the container.                    |
| `USE_WEBSOCKETS` | `true`    | Instructs the game to launch in WebSocket transport mode if enabled.  |

## Directory Structure

- `Dockerfile`: Multi-stage Ubuntu image definition for development and production build.
- `docker-compose.yml`: Easily launches the server container, mapping local volumes and networking.
- `entrypoint.sh`: Process launcher that forwards standard termination signals (`SIGTERM`, `SIGINT`) directly to the Unity process for graceful container teardowns.
