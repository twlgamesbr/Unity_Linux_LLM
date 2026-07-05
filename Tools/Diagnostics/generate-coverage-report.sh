#!/usr/bin/env bash
set -euo pipefail

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
COVERAGE_ROOT="${1:-}"
REPORT_ROOT=""

if ! command -v dotnet >/dev/null 2>&1; then
  echo "dotnet is required to run ReportGenerator." >&2
  exit 1
fi

DOTNET_TOOL_MANIFEST="${PROJECT_ROOT}/dotnet-tools.json"
if [[ ! -f "${DOTNET_TOOL_MANIFEST}" ]]; then
  echo "dotnet tool manifest not found at ${DOTNET_TOOL_MANIFEST}." >&2
  exit 1
fi

if [[ -z "${COVERAGE_ROOT}" ]]; then
  for candidate in "${PROJECT_ROOT}/CodeCoverage" "${PROJECT_ROOT}/Diagnostics/CodeCoverage"; do
    if [[ -d "${candidate}" ]] && find "${candidate}" -type f \( -name '*.xml' -o -name '*.info' \) -print -quit | grep -q .; then
      COVERAGE_ROOT="${candidate}"
      break
    fi
  done
fi

if [[ -z "${COVERAGE_ROOT}" ]]; then
  echo "No coverage output was found. Run Unity tests with coverage enabled first." >&2
  exit 2
fi

REPORT_ROOT="${COVERAGE_ROOT}/Report"
mkdir -p "${REPORT_ROOT}"

cd "${PROJECT_ROOT}"

mapfile -d '' REPORT_FILES < <(find "${COVERAGE_ROOT}" -type f \( -name '*.xml' -o -name '*.info' \) -print0 | sort -z)

if [[ ${#REPORT_FILES[@]} -eq 0 ]]; then
  echo "No report files were found under ${COVERAGE_ROOT}." >&2
  exit 2
fi

REPORT_ARGS=()
for report_file in "${REPORT_FILES[@]}"; do
  REPORT_ARGS+=("-reports:${report_file}")
done

dotnet tool run reportgenerator "${REPORT_ARGS[@]}" \
  -targetdir:"${REPORT_ROOT}" \
  -reporttypes:Html;HtmlSummary;Badges;Cobertura;JsonSummary;TextSummary \
  -historydir:"${REPORT_ROOT}/History"

echo "Coverage report generated at ${REPORT_ROOT}/index.html"
