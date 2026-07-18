# NPCDialogue Automated Code Review

A standalone Roslyn-based static analyzer that scans C# files against the
code conventions documented in [`AGENTS.md`](../../AGENTS.md) §1 (Code
Conventions), with a focus on `Assets/Scripts/Runtime/NPCDialogue/`. It is
not a Unity package — it's a plain .NET console app, so it runs without
opening the Unity Editor and without needing Unity's generated `.csproj`
files (it parses `.cs` files directly with `Microsoft.CodeAnalysis.CSharp`,
syntax-only — no semantic/type resolution, so it never needs Unity's
assemblies to build successfully).

## Why this exists

`AGENTS.md` §1 documents ~20 concrete, checkable code rules (naming,
the `[SerializeField]`/`[FormerlySerializedAs]` migration pattern, no
boolean flag parameters, no hard-coded `"localhost"`, no `TODO`/`FIXME`,
Allman braces, etc.) but nothing previously *verified* code against them
automatically. This tool closes that loop: each rule in `Rules/` maps to a
specific AGENTS.md section, and each rule has a companion self-test proving
it fires on a violating snippet and stays silent on a compliant one.

`AGENTS.md` §1 explicitly names itself the project's single, unique source of truth for code
conventions, and says any other doc that appears to contradict it is wrong and should be fixed.
This tool is how that claim stays true over time instead of drifting silently:

- `--self-test` verifies the *rules themselves* are correct (each one fires on a violating
  snippet, stays silent on a compliant one).
- `--verify-docs` verifies the *citations* are correct (every rule's `AGENTS.md §x.y` reference
  still resolves to a real heading — catches renumbering/rewrites that silently break the link
  between code and doc).

## Quick start

Run from the repository root (`Unity_Linux_LLM/`):

```bash
# Verify the verifier — run the rule self-tests (should be 100% pass)
dotnet run --project Tools/NPCDialogueCodeReview -- --self-test

# Verify every rule's AGENTS.md §-reference still resolves to a real heading
dotnet run --project Tools/NPCDialogueCodeReview -- --verify-docs

# Review NPCDialogue scripts (default path), print to console + write markdown
dotnet run --project Tools/NPCDialogueCodeReview -- \
  --path Assets/Scripts/Runtime/NPCDialogue

# Review a different/additional path
dotnet run --project Tools/NPCDialogueCodeReview -- \
  --path Assets/Scripts/Runtime/NPCDialogue \
  --path Assets/Scripts/Runtime/Networking

# CI-friendly: markdown only, don't fail the build on Suggestion/Info noise
dotnet run --project Tools/NPCDialogueCodeReview -- \
  --format markdown --fail-on Warning

# List every rule and its AGENTS.md reference
dotnet run --project Tools/NPCDialogueCodeReview -- --list-rules
```

The markdown report defaults to
`Tools/NPCDialogueCodeReview/reports/npc-dialogue-review.md` (gitignored —
regenerate on demand rather than committing stale reports).

## CLI reference

| Option | Meaning | Default |
|---|---|---|
| `--path <dir>` | Directory to scan, recursively (repeatable) | `Assets/Scripts/Runtime/NPCDialogue` |
| `--root <dir>` | Base directory relative paths are computed against | current directory |
| `--format <mode>` | `console` \| `markdown` \| `both` | `both` |
| `--out <file>` | Markdown report path | `Tools/NPCDialogueCodeReview/reports/npc-dialogue-review.md` |
| `--fail-on <severity>` | `Info` \| `Suggestion` \| `Warning` \| `Error` \| `None` — process exits 1 if any finding meets/exceeds it | `Warning` |
| `--min-severity <severity>` | Minimum severity printed to console | `Suggestion` |
| `--list-rules` | Print the rule catalog and exit | |
| `--self-test` | Run the built-in rule self-tests and exit | |
| `--verify-docs` | Check every rule's `AGENTS.md` §-reference still resolves to a real heading, then exit | |
| `--agents-md <path>` | Path to `AGENTS.md` for `--verify-docs` | `AGENTS.md` |

## Severity model

- **Error** — reserved for hard, unambiguous violations (none currently mapped; available for future strict rules).
- **Warning** — high-confidence, low-false-positive violations (naming, formatting, `ConfigureAwait(false)`, `TODO`/`FIXME`/`HACK`, hard-coded `"localhost"` comparisons).
- **Suggestion** — real signal but needs a human read (boolean flag parameters that might be a fixed delegate signature, `[SerializeField]` fields possibly missing `[FormerlySerializedAs]`, single-letter variables, commented-out-code heuristic, `"localhost"` default values).
- **Info** — guidance, not a gate (missing XML doc on public API).

## Rule catalog

Run `--list-rules` for the live list; as of this writing:

| ID | Rule | AGENTS.md reference |
|---|---|---|
| NAM01 | Private field naming (`_camelCase`) | §1.1 |
| NAM02 | Public/internal field, property, event naming (PascalCase) | §1.1 |
| NAM03 | Constant naming (PascalCase) | §1.1 |
| NAM04 | Method naming (PascalCase, with test-method underscore-segment exception) | §1.1 / §7.2 |
| NAM05 | Parameter naming (camelCase) | §1.1 |
| NAM06 | Local variable naming (camelCase) | §1.1 |
| NAM07 | Namespace naming (PascalCase) | §1.1 / §2.3 |
| SER01 | `[SerializeField]` should pair with `[FormerlySerializedAs]` | §1.2 |
| API01 | No boolean flag parameters | §1.6 |
| NET01 / NET02 | No hard-coded `"localhost"` comparisons (Warning) / defaults (Suggestion) | §1.6 |
| TODO01 | No TODO/FIXME/HACK markers | §1.6 |
| CMT01 | Possible commented-out code (heuristic) | §1.6 |
| ASY01 | Avoid `ConfigureAwait(false)` | §1.3 |
| VAR01 | No single-letter variables outside loop counters | §1.6 |
| DOC01 | Public API missing `/// <summary>` | §1.5 |
| SND01 | User-defined `SendMessage` hides `Component.SendMessage` | §2.3 / §11 (CS0108) |
| FMT01 | Trailing whitespace | §1.4 |
| FMT02 | Missing final newline | §1.4 |
| FMT03 | Tab indentation (spaces required) | §1.4 |
| FMT04 | CRLF line endings (LF required) | §1.4 |
| FMT05 | Allman brace style | §1.4 |

## Adding a new rule

1. Create `Rules/YourRule.cs` implementing `IReviewRule` (see any existing rule for the shape).
2. Register it in `Core/RuleRegistry.cs`.
3. Add at least one violating and one compliant case to `SelfTest/SelfTestRunner.cs` — the self-test suite will warn you at the end of a run if a registered rule has zero test coverage.
4. Run `--self-test` until it's green, then run a real scan to sanity-check the finding volume/wording.

## Known limitations

- **Syntax-only, no semantic model.** Rules can't reliably tell whether a `bool` parameter is forced by an inherited/delegate signature (e.g. `UnityEvent<bool>`), so `API01` is intentionally `Suggestion`-severity — read the call sites before refactoring.
- **`CMT01` (commented-out code) is a regex heuristic** over single-line comments and will have both false positives (prose that happens to end in `;`) and false negatives (multi-line commented blocks, commented code that doesn't match the shape patterns).
- **`SER01` can't know a field's serialization history.** It flags every `[SerializeField]` field without `[FormerlySerializedAs]`; only fields that were previously public and got renamed actually need the attribute.
- Not wired into CI yet — run it manually, or add a step that calls `dotnet run --project Tools/NPCDialogueCodeReview -- --format markdown --fail-on Warning` and fails the pipeline on a non-zero exit code.
