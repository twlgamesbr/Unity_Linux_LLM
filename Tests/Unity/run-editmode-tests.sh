#!/usr/bin/env bash
set -euo pipefail

PROJECT_ROOT="${PROJECT_ROOT:-$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)}"
UNITY_BIN="${UNITY_BIN:-/home/athar/Unity/Hub/Editor/6000.5.1f1/Editor/Unity}"
RESULTS="${RESULTS:-$PROJECT_ROOT/TestResults/editmode-results.xml}"
LOGFILE="${LOGFILE:-$PROJECT_ROOT/TestResults/editmode-unity.log}"

mkdir -p "$(dirname "$RESULTS")"

if [[ ! -x "$UNITY_BIN" ]]; then
  echo "ERROR: Unity executable not found or not executable: $UNITY_BIN" >&2
  echo "Set UNITY_BIN=/path/to/Unity and retry." >&2
  exit 2
fi

"$UNITY_BIN"   -batchmode   -projectPath "$PROJECT_ROOT"   -runTests   -testPlatform EditMode   -testResults "$RESULTS"   -logFile "$LOGFILE"   -quit
