#!/usr/bin/env bash
set -euo pipefail

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
UNITY_EDITOR="${UNITY_EDITOR:-/home/athar/Unity/Hub/Editor/6000.5.1f1/Editor/Unity}"
OUTPUT_ROOT="${PROJECT_ROOT}/Diagnostics"
LOG_ROOT="${OUTPUT_ROOT}/Logs"

mkdir -p "${LOG_ROOT}"

"${UNITY_EDITOR}" \
  -batchmode \
  -quit \
  -projectPath "${PROJECT_ROOT}" \
  -executeMethod NPCSystem.Editor.NPCDeveloperDiagnostics.RunProjectAuditorBatch \
  -logFile "${LOG_ROOT}/project-auditor.log"
