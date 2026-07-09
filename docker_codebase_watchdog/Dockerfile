# Codebase Watcher — Dockerfile
# Runs the CodebaseEmbedder watchdog in a lightweight Python 3.11 container.
# It monitors the Unity project for file changes and syncs them to Qdrant
# via the host's LocalAI embedding endpoint.
#
# BUILD:
#   cd <project-root> && docker build -t codebase-watchdog -f docker_codebase_watchdog/Dockerfile .
#
# DEV QUICK START (bind-mount):
#   docker compose -f docker_codebase_watchdog/docker-compose.yml up
#
# The container uses host networking so it reaches Qdrant (localhost:6333)
# and LocalAI (localhost:8080) directly.

# --- Stage: base (Ubuntu 22.04 + Python 3.11) ---
FROM ubuntu:22.04 AS base

ENV DEBIAN_FRONTEND=noninteractive
ENV PATH="/opt/venv/bin:$PATH"

RUN apt-get update && apt-get install -y --no-install-recommends \
    python3.11 \
    python3.11-venv \
    python3.11-dev \
    ca-certificates \
    && rm -rf /var/lib/apt/lists/*

# Create a virtualenv so the codebase-embedder CLI is on PATH
RUN python3.11 -m venv /opt/venv && \
    /opt/venv/bin/pip install --no-cache-dir --upgrade pip

# Create non-root user
RUN groupadd -r watcher && useradd -r -g watcher -d /workspace -s /sbin/nologin watcher && \
    mkdir -p /workspace && chown watcher:watcher /workspace

WORKDIR /workspace

# --- Stage: install dependencies ---
FROM base AS deps

# Copy pyproject.toml and the embedder package
COPY Tools/CodebaseEmbedder/pyproject.toml /tmp/embedder/
COPY Tools/CodebaseEmbedder/codebase_embedder/ /tmp/embedder/codebase_embedder/

RUN /opt/venv/bin/pip install --no-cache-dir /tmp/embedder/ && \
    rm -rf /tmp/embedder

# --- Stage: runtime ---
FROM base AS runtime

COPY --from=deps /opt/venv /opt/venv
COPY --chmod=755 docker_codebase_watchdog/entrypoint.sh /entrypoint.sh

WORKDIR /workspace

# No ports to expose — this is a monitoring service, not a server.
ENTRYPOINT ["/entrypoint.sh"]
