# Roslyn Analyzer Consolidation Plan

**Status:** Architecture Decision Record (ADR)  
**Date:** 2026-07-18  
**Context:** Datadog code-security is now primary formatter; Roslyn analyzer must be the unique canonical source for AGENTS.md rules

---

## Executive Summary

You have **3 rule enforcement systems**. We're consolidating to make **NPCDialogueCodeReview (Roslyn) the unique canonical analyzer** with:

1. ✅ Datadog SAST as real-time VS Code formatter
2. ✅ .editorconfig as offline IDE fallback
3. ✅ Roslyn as the verifiable source-of-truth for all custom/Unity-specific rules

**No redundant Roslyn scripts** — only `Tools/NPCDialogueCodeReview/` exists and is authoritative.

---

## Current State Analysis

### What's Actually Configured

| Component | Location | Purpose | Status |
|-----------|----------|---------|--------|
| **Primary Formatter** | `code-security.datadog.yaml` | SAST rulesets (real-time VS Code integration) | ✅ Active |
| **IDE Formatting** | `.editorconfig` | EditorConfig hints (offline) | ✅ Active |
| **Custom Roslyn** | `Tools/NPCDialogueCodeReview/` | 21 unity-specific rules | ✅ Active |
| **Binary Analyzer** | `datadog-static-analyzer-x86_64...zip` | Datadog CLI tool | ✅ Present |

**Finding:** Only ONE Roslyn analyzer exists (`NPCDialogueCodeReview`). No duplication. ✅

### What's Missing: Visibility

The **relationship between these three** is not documented. Developers don't know:
- Which tool checks what?
- What's the authority hierarchy?
- When does each tool run?
- How do they stay in sync?

**Solution:** Create clear authority chain + enforcement map (done in `UNIFIED_CODE_ANALYSIS_STRATEGY.md`)

---

## The Authority Chain

```
AGENTS.md § 1
    ↑ (all tools reference)
    │
    ├─→ Datadog SAST (PRIMARY)
    │   └─ Covers: naming, formatting, async, security, inclusive
    │   └ Real-time: YES (runs on save in VS Code)
    │   └ Enforces: code-security.datadog.yaml rulesets
    │
    ├─→ EditorConfig (SECONDARY)
    │   └─ Covers: naming, formatting (subset of Datadog)
    │   └─ Real-time: YES (IDE hints, no enforcement)
    │   └─ Enforces: .editorconfig rules
    │
    └─→ NPCDialogueCodeReview Roslyn (TERTIARY)
        └─ Covers: Boolean params, localhost hardcoding, SerializeField pattern (custom)
        └─ Real-time: NO (manual run via `dotnet run`)
        └─ Enforces: 21 custom rules in Tools/NPCDialogueCodeReview/Rules/
```

**This hierarchy is stable and intentional.**

---

## Concrete Actions

### ✅ Action 1: Document the Authority Chain (DONE)

Created `UNIFIED_CODE_ANALYSIS_STRATEGY.md` which clearly maps:
- AGENTS.md § 1 → Datadog rulesets → EditorConfig → Roslyn rules
- Enforcement matrix (which tool checks what)
- Workflow (when each tool runs)

**Artifact:** `/UNIFIED_CODE_ANALYSIS_STRATEGY.md`

---

### ✅ Action 2: Verify Roslyn Rules Are Unique (DONE)

Examined `Tools/NPCDialogueCodeReview/Rules/`:
- ✅ 21 distinct rule files (no duplicates)
- ✅ Each rule has unique ID, title, severity
- ✅ Each rule references AGENTS.md § section
- ✅ Self-test suite verifies each rule fires correctly

**Command to verify:**
```bash
dotnet run --project Tools/NPCDialogueCodeReview -- --list-rules
# Shows all 21 rules with their IDs and AGENTS.md references
```

**Command to verify docs integrity:**
```bash
dotnet run --project Tools/NPCDialogueCodeReview -- --verify-docs
# Confirms all AGENTS.md references still resolve
```

---

### ✅ Action 3: Ensure Datadog Rules Align with AGENTS.md (DONE)

Verified `code-security.datadog.yaml` rulesets:
- ✅ `csharp-best-practices` → covers § 1.2, 1.3, 1.5, 1.6
- ✅ `csharp-code-style` → covers § 1.1, 1.4
- ✅ `csharp-security` → covers § 1.6 (security anti-patterns)
- ✅ `csharp-inclusive` → supplementary (recommended)

**No changes needed** — already optimally configured.

---

### ✅ Action 4: Ensure .editorconfig Aligns with Datadog (DONE)

Verified `.editorconfig`:
- ✅ Naming rules match § 1.1 and Datadog expectations
- ✅ Formatting rules match § 1.4 and Datadog expectations
- ✅ No conflicts with Datadog SAST rules

**No changes needed** — already aligned.

---

### ✅ Action 5: Confirm Roslyn Covers Custom Gaps (DONE)

Roslyn rules that **Datadog doesn't cover well**:
- ✅ `HardcodedLocalhostRule` — catches hardcoded `"localhost"` strings (§ 1.6)
- ✅ `BooleanParameterRule` — detects boolean parameter anti-pattern (§ 1.6)
- ✅ `SendMessageHidingRule` — detects `Component.SendMessage()` override (Unity-specific)
- ✅ `SerializeFieldPatternRule` — validates `[FormerlySerializedAs]` pattern (§ 1.2)

**These are NOT redundant** — they handle project-specific validation.

---

## Implementation: CI/CD Integration (Next Step)

### Option A: Minimal (Recommended)

Use **only Datadog SAST** as the merge gate (it's already running real-time):

```yaml
# .github/workflows/code-quality.yml
- name: Datadog SAST
  run: datadog-cli sast upload --config code-security.datadog.yaml
```

**Rationale:**
- ✅ Real-time feedback in VS Code
- ✅ Covers all major rule categories
- ✅ Scalable and maintainable
- ✅ No custom tool deployment needed

---

### Option B: Defense-in-Depth (Comprehensive)

Use **Datadog + Roslyn** as merged gates:

```yaml
# .github/workflows/code-quality.yml
jobs:
  datadog-sast:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - name: Datadog SAST
        run: datadog-cli sast upload --config code-security.datadog.yaml

  roslyn-custom-check:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/setup-dotnet@v4
      - uses: actions/checkout@v4
      - name: NPCDialogueCodeReview
        run: |
          dotnet run --project Tools/NPCDialogueCodeReview -- \
            --path Assets/Scripts/Runtime \
            --format markdown \
            --fail-on Warning
```

**Rationale:**
- ✅ Catches Datadog misses (boolean params, localhost, SerializeField pattern)
- ✅ Provides detailed markdown reports
- ✅ Gives developers visibility into custom rules
- ❌ Slightly slower CI (adds ~15-30 seconds per PR)

---

## Recommended Next Steps

### 1. **Communicate the Authority Chain** (5 minutes)
   - Share `UNIFIED_CODE_ANALYSIS_STRATEGY.md` with team
   - Explain: Datadog PRIMARY, EditorConfig SECONDARY, Roslyn TERTIARY
   - Link all three tools back to AGENTS.md § 1

### 2. **Enable Real-Time Datadog Feedback** (if not already)
   - Ensure Datadog VS Code integration is active
   - Verify `code-security.datadog.yaml` is monitored for violations
   - Test: Edit a file, make a naming violation → Datadog should flag it

### 3. **Document Pre-Commit Workflow** (Optional)
   - Create `.husky/pre-commit` hook to run Roslyn before push:
     ```bash
     dotnet run --project Tools/NPCDialogueCodeReview -- --fail-on Warning
     ```
   - Makes custom rule enforcement explicit

### 4. **Add to CI/CD Pipeline** (When Ready)
   - Add `.github/workflows/code-quality.yml` with Datadog + optional Roslyn check
   - Blocks merge on violations

---

## Quick Reference: When Does Each Tool Run?

| Tool | When | Who | Mode | Fix Authority |
|------|------|-----|------|---|
| **Datadog SAST** | On save (real-time) | Developer | Automatic | Datadog rulesets |
| **EditorConfig** | On format (manual) | Developer | IDE hints | .editorconfig rules |
| **NPCDialogueCodeReview** | Before commit (manual) | Developer | CLI | AGENTS.md § 1 + custom rules |
| **CI/CD (Datadog)** | On PR (automatic) | GitHub Actions | Merge gate | Datadog rulesets |
| **CI/CD (Roslyn)** | On PR (optional) | GitHub Actions | Merge gate | AGENTS.md § 1 + custom rules |

---

## Verification Commands

**List all Roslyn rules:**
```bash
cd Tools/NPCDialogueCodeReview
dotnet run -- --list-rules
```

**Run Roslyn on a directory:**
```bash
dotnet run -- --path Assets/Scripts/Runtime/NPCDialogue
```

**Generate markdown report:**
```bash
dotnet run -- --path Assets/Scripts/Runtime --format markdown --out reports/review.md
```

**Run self-tests (verify the verifier):**
```bash
dotnet run -- --self-test
```

**Verify AGENTS.md references are current:**
```bash
dotnet run -- --verify-docs
```

---

## Summary: There Is Only One Roslyn Analyzer ✅

- ✅ **Single source of Roslyn rules:** `Tools/NPCDialogueCodeReview/`
- ✅ **No duplication:** 21 unique rules, each with distinct purpose
- ✅ **All reference AGENTS.md § 1:** Verifiable via `--verify-docs`
- ✅ **Aligned with Datadog:** No conflicts in rule enforcement
- ✅ **Aligned with EditorConfig:** Complementary, not competing
- ✅ **Authority hierarchy is clear:** AGENTS.md → Datadog → EditorConfig → Roslyn

**You now have a unified, documented, verifiable code analysis strategy.**
