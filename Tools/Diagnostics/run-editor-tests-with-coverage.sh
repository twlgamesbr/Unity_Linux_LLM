#!/usr/bin/env bash
set -euo pipefail

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
UNITY_EDITOR="${UNITY_EDITOR:-/home/athar/Unity/Hub/Editor/6000.5.1f1/Editor/Unity}"
OUTPUT_ROOT="${PROJECT_ROOT}/Diagnostics"
COVERAGE_ROOT="${OUTPUT_ROOT}/CodeCoverage"
LOG_ROOT="${OUTPUT_ROOT}/Logs"
TEST_PLATFORM_INPUT="${1:-EditMode}"

case "${TEST_PLATFORM_INPUT}" in
  [Ee][Dd][Ii][Tt][Mm][Oo][Dd][Ee]) TEST_PLATFORM="EditMode" ;;
  [Pp][Ll][Aa][Yy][Mm][Oo][Dd][Ee]) TEST_PLATFORM="PlayMode" ;;
  *) TEST_PLATFORM="${TEST_PLATFORM_INPUT}" ;;
esac

mkdir -p "${COVERAGE_ROOT}" "${LOG_ROOT}"

set +e
"${UNITY_EDITOR}" \
  -batchmode \
  -quit \
  -projectPath "${PROJECT_ROOT}" \
  -executeMethod NPCSystem.Editor.NPCDeveloperDiagnostics.RunEditModeTestsWithCoverageBatch \
  -logFile "${LOG_ROOT}/${TEST_PLATFORM}-coverage.log" \
  -enableCodeCoverage \
  -coverageResultsPath "${COVERAGE_ROOT}" \
  -coverageOptions "generateHtmlReport;generateAdditionalMetrics;generateBadgeReport;generateAdditionalReports;assemblyFilters:+NPCSystem.Runtime,+NPCSystem.Editor,+NPCSystem.Tests,+Assembly-CSharp,+Assembly-CSharp-Editor;pathFilters:+Assets/Scripts"
EXIT_CODE=$?
set -e

if [[ ${EXIT_CODE} -eq 0 ]]; then
  if ! find "${COVERAGE_ROOT}" -type f -print -quit | grep -q .; then
    echo "Code coverage run completed without coverage artifacts in ${COVERAGE_ROOT}." >&2
    echo "Inspect ${LOG_ROOT}/${TEST_PLATFORM}-coverage.log for Unity Code Coverage package errors." >&2
    exit 2
  fi

  "${PROJECT_ROOT}/Tools/Diagnostics/generate-coverage-report.sh" "${COVERAGE_ROOT}"
fi

exit "${EXIT_CODE}"
