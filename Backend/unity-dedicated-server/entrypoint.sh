#!/bin/bash
# Entrypoint for the NPC dedicated server container.
# Forwards signals to the Unity process so Docker stop works cleanly.
set -e

SERVER_PORT="${SERVER_PORT:-11474}"
SERVER_ADDRESS="${SERVER_ADDRESS:-0.0.0.0}"

# Locate the server binary — try common names
if [ -f "/Server/LinuxDedicatedServer.x86_64" ]; then
    SERVER_BINARY="/Server/LinuxDedicatedServer.x86_64"
elif [ -f "/Server1/Server1.x86_64" ]; then
    SERVER_BINARY="/Server/ServerWS.x86_64"
elif [ -f "/Server/NPCServer.x86_64" ]; then
    SERVER_BINARY="/Server/NPCServer.x86_64"
elif [ -f "/Server/Server.x86_64" ]; then
    SERVER_BINARY="/Server/Server.x86_64"
elif [ -f "/Server/Linux.x86_64" ]; then
    SERVER_BINARY="/Server/Linux.x86_64"
elif ls /Server/*.x86_64 1>/dev/null 2>&1; then
    SERVER_BINARY="$(ls /Server/*.x86_64 | head -n 1)"
else
    echo "ERROR: No server binary found in /Server/"
    echo "Bind-mount or COPY your Unity dedicated server build to /Server/."
    echo "Expected files: LinuxDedicatedServer.x86_64 (or NPCServer.x86_64), UnityPlayer.so, GameAssembly.so, and the matching *_Data/ folder"
    ls -la /Server/ 2>/dev/null || echo "  /Server/ is empty or missing"
    exit 1
fi

USE_WEBSOCKETS="${USE_WEBSOCKETS:-false}"
EXTRA_ARGS=""
if [ "$USE_WEBSOCKETS" = "true" ]; then
    EXTRA_ARGS="-npc-websockets"
fi

echo "=== NPC Dedicated Server Container ==="
echo "Binary:     $SERVER_BINARY"
echo "Port:       $SERVER_PORT"
echo "Address:    $SERVER_ADDRESS"
echo "WebSockets: $USE_WEBSOCKETS"
echo "LocalAI:    localhost:8080 (via host networking)"
echo "======================================"

# Start server in background so we can trap signals.
# -batchmode enables CLI arg processing in the standalone player.
# Unknown args are passed through to the game code.
"$SERVER_BINARY" \
    -batchmode \
    -nographics \
    -npc-server \
    -port "$SERVER_PORT" \
    -address "$SERVER_ADDRESS" \
    $EXTRA_ARGS \
    -logFile /dev/stdout &
SERVER_PID=$!

# Forward shutdown signals to Unity process
trap 'echo "Shutting down server (PID $SERVER_PID)..."; kill -TERM $SERVER_PID 2>/dev/null; wait $SERVER_PID; exit 0' SIGTERM SIGINT

wait $SERVER_PID
EXIT_CODE=$?
echo "Server exited with code $EXIT_CODE"
exit $EXIT_CODE
