#!/usr/bin/env bash
set -Eeuo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
DEFAULT_PROJECT_ROOT="$(cd -- "$SCRIPT_DIR/../../../.." && pwd)"
PROJECT_ROOT="${UNITY_PROJECT_ROOT:-$DEFAULT_PROJECT_ROOT}"
LOG_DIR="$PROJECT_ROOT/Diagnostics/Logs"
PROJECT_VERSION_FILE="$PROJECT_ROOT/ProjectSettings/ProjectVersion.txt"

die() {
    printf 'ERROR: %s\n' "$*" >&2
    exit 1
}

usage() {
    cat <<'EOF'
Usage: unity-project.sh <command> [argument]

Commands:
  doctor                 Validate the local Unity build environment.
  compile                Import and compile the project in batch mode.
  test [filter]          Run NPCSystem.Tests EditMode tests.
  coverage               Run EditMode tests with code coverage.
  audit                  Run Project Auditor and export a report.
  build-server           Build the Linux dedicated server.
  build-webgl            Build and submodule-strip the WebGL client.
  build-all              Build server and WebGL sequentially.
  strip-status           Validate WebGL submodule stripping configuration.
  logs [name]            Tail the newest matching Diagnostics log.
  help                   Show this help.

Environment:
  UNITY_EDITOR           Override the Unity executable.
  UNITY_PROJECT_ROOT     Override the project root.
  UNITY_TIMEOUT_SECONDS  Override the per-command timeout.
EOF
}

[[ -f "$PROJECT_VERSION_FILE" ]] || die "Not a Unity project: $PROJECT_ROOT"
UNITY_VERSION="$(sed -n 's/^m_EditorVersion: //p' "$PROJECT_VERSION_FILE" | head -n 1)"
[[ -n "$UNITY_VERSION" ]] || die "Could not read the Unity version from $PROJECT_VERSION_FILE"
UNITY_EDITOR="${UNITY_EDITOR:-$HOME/Unity/Hub/Editor/$UNITY_VERSION/Editor/Unity}"

require_editor() {
    [[ -x "$UNITY_EDITOR" ]] || die "Unity $UNITY_VERSION was not found at $UNITY_EDITOR. Set UNITY_EDITOR to the matching executable."
}

require_batch_access() {
    if [[ -e "$PROJECT_ROOT/Library/UnityLockfile" ]]; then
        die "This project is locked by an Editor. Close the Editor before starting a batch command; do not delete an active lockfile."
    fi
}

new_log_path() {
    local label="$1"
    mkdir -p "$LOG_DIR"
    printf '%s/%s-%s.log\n' "$LOG_DIR" "$label" "$(date +%Y%m%d-%H%M%S)"
}

show_failure_context() {
    local log_path="$1"
    printf '\nFailure context from %s:\n' "$log_path" >&2
    rg -n -i 'error CS[0-9]+|BuildFailedException|Scripts have compiler errors|Compilation failed|build FAILED|Test run completed|Unhandled log message|Expected:|But was:' "$log_path" 2>/dev/null | tail -n 80 >&2 || true
    printf '\nFinal log lines:\n' >&2
    tail -n 80 "$log_path" >&2 || true
}

run_unity() {
    local label="$1"
    local default_timeout="$2"
    shift 2

    require_editor
    require_batch_access

    local log_path
    local timeout_seconds="${UNITY_TIMEOUT_SECONDS:-$default_timeout}"
    log_path="$(new_log_path "$label")"
    printf 'Unity:   %s\nProject: %s\nLog:     %s\n' "$UNITY_EDITOR" "$PROJECT_ROOT" "$log_path"

    set +e
    timeout --foreground "$timeout_seconds" "$UNITY_EDITOR" \
        -batchmode \
        -nographics \
        -projectPath "$PROJECT_ROOT" \
        -logFile "$log_path" \
        "$@"
    local exit_code=$?
    set -e

    if (( exit_code != 0 )); then
        if (( exit_code == 124 )); then
            printf 'Unity command timed out after %s seconds.\n' "$timeout_seconds" >&2
        fi
        show_failure_context "$log_path"
        return "$exit_code"
    fi

    if rg -n -i 'Scripts have compiler errors|Compilation failed|BuildFailedException|build FAILED' "$log_path" >/dev/null 2>&1; then
        show_failure_context "$log_path"
        return 1
    fi

    printf 'Unity command completed successfully.\n'
}

require_file() {
    local path="$1"
    local label="$2"
    [[ -f "$path" ]] || die "$label was not produced at $path"
    printf '%s: %s (%s bytes)\n' "$label" "$path" "$(stat -c %s "$path")"
}

strip_status() {
    local manifest="$PROJECT_ROOT/Packages/manifest.json"
    local package_root="$PROJECT_ROOT/Packages/com.unity.web.stripping-tool"
    local settings="$PROJECT_ROOT/Assets/DefaultSubmoduleStrippingSettings.asset"

    rg -q '"com\.unity\.web\.stripping-tool"[[:space:]]*:[[:space:]]*"1\.3\.0"' "$manifest" \
        || die "Packages/manifest.json does not pin com.unity.web.stripping-tool 1.3.0"
    [[ -d "$package_root/SubmoduleDefinitions" ]] || die "Embedded Web Stripping Tool definitions are missing"
    [[ -f "$settings" ]] || die "Missing stripping settings: $settings"

    printf 'Web Stripping Tool: 1.3.0\nSettings: %s\n' "$settings"
    rg -n 'OptimizeCodeAfterStripping|RemoveEmbeddedDebugSymbols|MissingSubmoduleErrorHandling' "$settings"

    local invalid=0
    local module
    while IFS= read -r module; do
        [[ -n "$module" ]] || continue
        if rg -F -q "\"name\": \"$module\"" "$package_root/SubmoduleDefinitions"; then
            printf '  valid: %s\n' "$module"
        else
            printf '  INVALID: %s\n' "$module" >&2
            invalid=1
        fi
    done < <(sed -n 's/^  - //p' "$settings")

    (( invalid == 0 )) || die "One or more configured submodules are not defined by the installed package"
    rg -q 'StrippingProjectSettings\.ActiveSettings' "$PROJECT_ROOT/Assets/Editor/WebGLStripPostBuild.cs" \
        || die "The WebGL stripping configuration hook is not wired to StrippingProjectSettings.ActiveSettings"
    printf 'WebGL stripping configuration is internally consistent.\n'
}

doctor() {
    require_editor
    printf 'Project: %s\nUnity version: %s\nUnity executable: %s\n' "$PROJECT_ROOT" "$UNITY_VERSION" "$UNITY_EDITOR"

    local required_file
    for required_file in \
        "Assets/Scenes/NPCDialoguePrototype1.unity" \
        "Assets/Settings/Build Profiles/Linux Server.asset" \
        "Assets/Settings/Build Profiles/WebGL - Desktop - Development.asset" \
        "Assets/Editor/NPCDialogueBuild.cs"; do
        [[ -f "$PROJECT_ROOT/$required_file" ]] || die "Missing required project file: $required_file"
        printf 'Found: %s\n' "$required_file"
    done

    rg -q '"com\.unity\.dedicated-server"' "$PROJECT_ROOT/Packages/manifest.json" \
        || die "The Dedicated Server package is not installed"
    printf 'Dedicated Server package: installed\n'

    if [[ -e "$PROJECT_ROOT/Library/UnityLockfile" ]]; then
        printf 'Editor lock: present (batch commands require the Editor to be closed)\n'
    else
        printf 'Editor lock: clear\n'
    fi

    df -h "$PROJECT_ROOT" | awk 'NR == 1 || NR == 2 { print }'
    strip_status
}

tail_logs() {
    local name="${1:-}"
    [[ -d "$LOG_DIR" ]] || die "No Diagnostics log directory exists yet"
    local log_path
    log_path="$(find "$LOG_DIR" -maxdepth 1 -type f -name "*${name}*.log" -printf '%T@ %p\n' | sort -nr | head -n 1 | cut -d' ' -f2-)"
    [[ -n "$log_path" ]] || die "No log matched '$name' in $LOG_DIR"
    printf 'Log: %s\n' "$log_path"
    tail -n 160 "$log_path"
}

run_tests() {
    local filter="${1:-}"
    local results="$LOG_DIR/EditMode-test-results-$(date +%Y%m%d-%H%M%S).xml"
    local status
    local test_args=(-runTests -testPlatform EditMode -assemblyNames NPCSystem.Tests -testResults "$results")

    mkdir -p "$LOG_DIR"
    if [[ -n "$filter" ]]; then
        test_args+=(-testFilter "$filter")
    fi

    if run_unity test 1800 "${test_args[@]}"; then
        status=0
    else
        status=$?
    fi

    if [[ -f "$results" ]]; then
        require_file "$results" "EditMode test results"
        rg -n '<test-run ' "$results" | head -n 1 || true
        if (( status != 0 )); then
            rg -n 'test-case .* result="Failed' "$results" | head -n 60 >&2 || true
        fi
    fi

    return "$status"
}

command="${1:-help}"
shift || true

case "$command" in
    doctor)
        doctor
        ;;
    compile)
        run_unity compile 1200 -quit
        ;;
    test)
        run_tests "${1:-}"
        ;;
    coverage)
        run_unity coverage 2400 -executeMethod NPCSystem.Editor.NPCDeveloperDiagnostics.RunEditModeTestsWithCoverageBatch
        require_file "$LOG_DIR/EditMode-test-results.xml" "Coverage test results"
        ;;
    audit)
        run_unity audit 2400 -executeMethod NPCSystem.Editor.NPCDeveloperDiagnostics.RunProjectAuditorBatch
        ;;
    build-server)
        run_unity build-server 5400 \
            -activeBuildProfile "Assets/Settings/Build Profiles/Linux Server.asset" \
            -executeMethod NPCDialogueBuild.PerformServerBuild \
            -quit
        require_file "$PROJECT_ROOT/Builds/Server/NPCServer.x86_64" "Dedicated server"
        ;;
    build-webgl)
        run_unity build-webgl 7200 \
            -activeBuildProfile "Assets/Settings/Build Profiles/WebGL - Desktop - Development.asset" \
            -executeMethod NPCDialogueBuild.PerformWebGLBuild \
            -quit
        require_file "$PROJECT_ROOT/Builds/WebGL_client/WebGL/index.html" "WebGL entry point"
        ;;
    build-all)
        "$0" build-server
        "$0" build-webgl
        ;;
    strip-status)
        strip_status
        ;;
    logs)
        tail_logs "${1:-}"
        ;;
    help|-h|--help)
        usage
        ;;
    *)
        usage >&2
        die "Unknown command: $command"
        ;;
esac
