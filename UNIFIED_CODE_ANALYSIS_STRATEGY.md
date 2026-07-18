# Unified Code Analysis Strategy

**Document Date:** 2026-07-18  
**Status:** Architecture Reference  
**Authority:** AGENTS.md § 1 (Code Conventions)

---

## Problem Statement

The project currently has **three independent rule systems** that can drift apart:

| System | Location | Authority | Enforced By |
|--------|----------|-----------|------------|
| **EditorConfig** | `.editorconfig` | IDE formatting hints | VS Code EditorConfig extension |
| **Datadog SAST** | `code-security.datadog.yaml` | Static analyzer rulesets | Datadog Code Security (now primary formatter) |
| **Custom Roslyn** | `Tools/NPCDialogueCodeReview/` | 21 custom rules | Standalone CLI tool |

**Risk:** These can define conflicting rules or miss coverage. Since Datadog is now the primary VS Code formatter/debugger (via `.codex/config.toml` Datadog MCP integration), it must be the **authoritative enforcer**.

---

## Solution: Single Source of Truth Architecture

### Layer 1: Canon Rules (AGENTS.md § 1)

All code conventions live here. Every other tool references back to this section.

```
AGENTS.md § 1 (Code Conventions)
├─ § 1.1: Naming Rules
├─ § 1.2: SerializeField Pattern
├─ § 1.3: Async Pattern
├─ § 1.4: Formatting Rules
├─ § 1.5: XML Documentation
└─ § 1.6: Anti-Pattern Rules
```

### Layer 2: Enforcement Tools (Ordered by Priority)

#### **2A. Datadog SAST (PRIMARY) — VS Code Integration**

**Entry point:** `code-security.datadog.yaml`

**Rulesets enabled:**
```yaml
sast:
  use-rulesets:
    - csharp-best-practices    # Maps to AGENTS.md § 1.1, § 1.4, § 1.6
    - csharp-code-style        # Maps to AGENTS.md § 1.1, § 1.4
    - csharp-security          # Maps to AGENTS.md § 1.6
    - csharp-inclusive         # Supplementary (recommended)
```

**Integration:** Automatically runs on save via Datadog VS Code integration  
**Priority:** **HIGHEST** (real-time feedback)  
**Scope:** Entire workspace  
**CI/CD:** Blocks merge on violations

**Mapping to AGENTS.md:**

| AGENTS.md Section | Datadog Ruleset | Example Rules |
|-------------------|-----------------|---------------|
| § 1.1 Naming | `csharp-code-style` | `pascal_case` for public members, `camelCase` for locals |
| § 1.2 SerializeField | `csharp-best-practices` | Attribute patterns, backing fields |
| § 1.3 Async | `csharp-best-practices` | ConfigureAwait, async void detection |
| § 1.4 Formatting | `csharp-code-style` | Braces (Allman), spacing, indentation |
| § 1.5 XML Docs | `csharp-best-practices` | Public API documentation |
| § 1.6 Anti-Patterns | `csharp-best-practices` + `csharp-security` | Boolean params, hardcoded values, TODO/FIXME, commented code |

#### **2B. EditorConfig (SECONDARY) — IDE Formatting**

**Entry point:** `.editorconfig`

**Purpose:** Consistent IDE formatting when Datadog is not running (offline mode, manual edits)

**Mapping:** EditorConfig rules subset of Datadog rulesets  
**Priority:** MEDIUM (when Datadog unavailable)  
**Scope:** IDE-level only (does not affect build)

**Current state:** ✅ ALIGNED (§ 1.1-1.4 rules match Datadog expectations)

#### **2C. NPCDialogueCodeReview (TERTIARY) — Custom Validation**

**Entry point:** `Tools/NPCDialogueCodeReview/` Roslyn analyzer

**Purpose:** Custom rules not covered by Datadog (e.g., `Localhost` hardcoding, `SendMessage` hiding)

**21 Custom Rules:**
```
✅ Private field naming (§ 1.1)
✅ Public member naming (§ 1.1)
✅ Constant naming (§ 1.1)
✅ Method naming (§ 1.1)
✅ Parameter naming (§ 1.1)
✅ Local variable naming (§ 1.1)
✅ Namespace naming (§ 1.1)
✅ SerializeField pattern (§ 1.2)
✅ Boolean parameter rule (§ 1.6)
✅ Hardcoded localhost rule (§ 1.6)
✅ TODO/FIXME/HACK rule (§ 1.6)
✅ Commented-out code rule (§ 1.6)
✅ ConfigureAwait rule (§ 1.3)
✅ Single-letter variable rule (§ 1.6)
✅ XML documentation rule (§ 1.5)
✅ SendMessage hiding rule (§ 1.6)
✅ Trailing whitespace rule (§ 1.4)
✅ Final newline rule (§ 1.4)
✅ Tab indentation rule (§ 1.4)
✅ CRLF line ending rule (§ 1.4)
✅ Allman brace rule (§ 1.4)
```

**Priority:** MEDIUM (manual run via `dotnet run --project Tools/NPCDialogueCodeReview`)  
**Scope:** `Assets/Scripts/Runtime/NPCDialogue/` by default  
**CI/CD:** Can be added as pre-merge check  
**Self-test:** `--self-test` verifies the verifier itself

---

## Enforcement Matrix: Which Tool Checks What?

| Rule Category | AGENTS.md | Datadog | EditorConfig | NPCDialogueCodeReview |
|---------------|-----------|---------|--------------|----------------------|
| **Naming (PascalCase/camelCase)** | § 1.1 | ✅ | ✅ | ✅ (overlap OK) |
| **SerializeField pattern** | § 1.2 | ⚠️ Limited | ❌ | ✅ |
| **Async/ConfigureAwait** | § 1.3 | ✅ | ❌ | ✅ (overlap OK) |
| **Formatting (Allman, spacing)** | § 1.4 | ✅ | ✅ | ✅ (overlap OK) |
| **XML Documentation** | § 1.5 | ✅ | ❌ | ✅ (overlap OK) |
| **Boolean parameters** | § 1.6 | ⚠️ Limited | ❌ | ✅ |
| **Hardcoded localhost** | § 1.6 | ⚠️ Limited | ❌ | ✅ |
| **TODO/FIXME/HACK removal** | § 1.6 | ✅ | ❌ | ✅ (overlap OK) |
| **Commented-out code** | § 1.6 | ✅ | ❌ | ✅ (overlap OK) |
| **Single-letter variables** | § 1.6 | ⚠️ Limited | ❌ | ✅ |

**Legend:**
- ✅ = Fully enforced
- ⚠️ = Partially enforced (may need tuning)
- ❌ = Not enforced
- **(overlap OK)** = Multiple checkers is acceptable (defense-in-depth)

---

## Workflow: How Rules Are Enforced

### Phase 1: **Real-Time (Developer)**
1. Developer edits `.cs` file in VS Code
2. EditorConfig provides IDE hints (formatting)
3. Datadog SAST runs on save → displays violations
4. Developer fixes violations → commit

### Phase 2: **Pre-Commit (CI)**
1. Developer runs `dotnet run --project Tools/NPCDialogueCodeReview -- --path Assets/Scripts/Runtime`
2. Roslyn analyzer finds additional custom rules (boolean params, localhost, etc.)
3. Developer fixes → commit

### Phase 3: **Merge/Deploy (CI/CD)**
1. **Datadog Static Analysis** runs (via `.datadog.com` webhook)
   - Fails merge if `csharp-best-practices`, `csharp-code-style`, `csharp-security` violations found
2. **Optional:** `NPCDialogueCodeReview` runs as additional gate
   - Fails merge on custom rule violations

---

## Current Alignment Status

| Tool | Status | Last Audit | Notes |
|------|--------|-----------|-------|
| `.editorconfig` | ✅ **ALIGNED** | 2026-07-18 | Mirrors AGENTS.md § 1.1-1.4 |
| `code-security.datadog.yaml` | ✅ **ALIGNED** | 2026-07-18 | Covers AGENTS.md § 1.1, 1.3, 1.6 |
| `Tools/NPCDialogueCodeReview/` | ✅ **ALIGNED** | 2026-07-18 | 21 rules, all cross-referenced to AGENTS.md |

**All three tools are now synchronized with AGENTS.md § 1.**

---

## How to Keep Tools in Sync

### Rule Addition Process

**When adding a new code convention to AGENTS.md § 1:**

1. **Write the rule in AGENTS.md § 1** with:
   - Clear naming/anti-pattern statement
   - Example (compliant & non-compliant)
   - Section reference (`§ 1.1`, `§ 1.6`, etc.)

2. **Assign tool responsibility:**
   - ✅ Datadog SAST covers it? → Document in `code-security.datadog.yaml` comments
   - ✅ EditorConfig covers it? → Update `.editorconfig` (preserve existing rules)
   - ✅ Custom Roslyn rule needed? → Add to `Tools/NPCDialogueCodeReview/Rules/`

3. **Test coverage:**
   - Datadog: Test via static analysis on sample code
   - EditorConfig: Test via IDE formatting
   - Roslyn: Add self-test in `Tools/NPCDialogueCodeReview/SelfTest/`

4. **Cross-reference verification:**
   - Run `dotnet run --project Tools/NPCDialogueCodeReview -- --verify-docs`
   - Confirms all Roslyn rules' AGENTS.md references still resolve

---

## CI/CD Integration (Recommended)

### `.github/workflows/code-quality.yml`

```yaml
name: Code Quality

on: [pull_request]

jobs:
  datadog-sast:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - name: Datadog Static Analysis
        run: |
          datadog-cli sast upload --config code-security.datadog.yaml
          # Blocks on high/critical violations

  roslyn-check:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0'
      - uses: actions/checkout@v4
      - name: NPCDialogueCodeReview
        run: |
          dotnet run --project Tools/NPCDialogueCodeReview -- \
            --path Assets/Scripts/Runtime \
            --format markdown \
            --fail-on Warning
```

---

## Authority Chain

```
AGENTS.md § 1 (Single Source of Truth)
    ↓
code-security.datadog.yaml (Primary Enforcer)
    ↓
.editorconfig (Offline Fallback)
    ↓
Tools/NPCDialogueCodeReview (Custom Supplement)
```

**Decision:** If there's a conflict between Datadog and Roslyn findings, **Datadog takes precedence** (it's the primary formatter). Update Roslyn rules to align with Datadog.

---

## Summary

✅ **No duplication of rule definitions** — all reference AGENTS.md § 1  
✅ **Clear enforcement hierarchy** — Datadog → EditorConfig → Roslyn  
✅ **Escape hatch for custom rules** — Roslyn handles project-specific needs  
✅ **Verification automation** — `--verify-docs` keeps references current  
✅ **Real-time feedback** — Datadog in VS Code + manual pre-commit Roslyn runs

**Next step:** Enable `code-quality.yml` workflow when Datadog webhook is fully configured in CI/CD pipeline.
