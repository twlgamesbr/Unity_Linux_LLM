# NPCDialogue Automated Code Review

Generated: 2026-07-18 04:49:03 UTC

Scanned paths:
- `Assets/Scripts/Runtime/NPCDialogue`

Files scanned: **37** · Rules run: **21** · Findings: **538**

Tool: `Tools/NPCDialogueCodeReview` — see its README.md for usage and how to add rules.

## Summary by severity

| Severity | Count |
|---|---|
| Error | 0 |
| Warning | 197 |
| Suggestion | 178 |
| Info | 163 |

## Summary by rule

| Rule ID | Title | AGENTS.md reference | Findings |
|---|---|---|---|
| API01 | No boolean flag parameters | §1.6 Key Anti-Pattern Rules | 19 |
| ASY01 | Avoid ConfigureAwait(false) | §1.3 Async Pattern | 0 |
| CMT01 | Possible commented-out code | §1.6 Key Anti-Pattern Rules | 0 |
| DOC01 | Public API missing XML doc summary | §1.5 XML Documentation | 163 |
| FMT01 | Trailing whitespace | §1.4 Formatting Rules | 0 |
| FMT02 | Missing final newline | §1.4 Formatting Rules | 1 |
| FMT03 | Tab indentation (spaces required) | §1.4 Formatting Rules | 0 |
| FMT04 | CRLF line endings (LF required) | §1.4 Formatting Rules | 0 |
| FMT05 | Allman brace style | §1.4 Formatting Rules | 7 |
| NAM01 | Private field naming (_camelCase) | §1.1 Naming Rules | 98 |
| NAM02 | Public/internal field, property, and event naming (PascalCase) | §1.1 Naming Rules | 91 |
| NAM03 | Constant naming (PascalCase) | §1.1 Naming Rules | 0 |
| NAM04 | Method naming (PascalCase) | §1.1 Naming Rules / §7.2 Test Patterns | 0 |
| NAM05 | Parameter naming (camelCase) | §1.1 Naming Rules | 0 |
| NAM06 | Local variable naming (camelCase) | §1.1 Naming Rules | 0 |
| NAM07 | Namespace naming (PascalCase) | §1.1 Naming Rules / §2.3 Namespace Hierarchy | 0 |
| NET01 | No hard-coded "localhost" comparisons/defaults | §1.6 Key Anti-Pattern Rules | 0 |
| NET02 | (sub-check of NET01) | — | 3 |
| SER01 | [SerializeField] should pair with [FormerlySerializedAs] | §1.2 Phase 4 [SerializeField] private Pattern | 139 |
| SND01 | User-defined SendMessage hides Component.SendMessage | §2.3 Namespace Hierarchy note / §11 Known Compile Warnings (CS0108) | 0 |
| TODO01 | No TODO/FIXME/HACK markers | §1.6 Key Anti-Pattern Rules | 0 |
| VAR01 | No single-letter variables outside loop counters | §1.6 Key Anti-Pattern Rules | 17 |

## Findings

### `Assets/Scripts/Runtime/NPCDialogue/AuthUIController.cs`

| Line | Severity | Rule | Message |
|---|---|---|---|
| 10 | Info | DOC01 | Public class 'AuthUIController' has no /// <summary> doc comment. |
| 13 | Suggestion | SER01 | [SerializeField] field 'authPanel' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 15 | Warning | NAM01 | Private field 'authPanel' should be named '_authPanel' (leading underscore + camelCase). |
| 17 | Suggestion | SER01 | [SerializeField] field 'authTitle' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 18 | Warning | NAM01 | Private field 'authTitle' should be named '_authTitle' (leading underscore + camelCase). |
| 20 | Suggestion | SER01 | [SerializeField] field 'usernameInput' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 21 | Warning | NAM01 | Private field 'usernameInput' should be named '_usernameInput' (leading underscore + camelCase). |
| 23 | Suggestion | SER01 | [SerializeField] field 'passwordInput' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 24 | Warning | NAM01 | Private field 'passwordInput' should be named '_passwordInput' (leading underscore + camelCase). |
| 26 | Suggestion | SER01 | [SerializeField] field 'confirmPasswordGroup' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 27 | Warning | NAM01 | Private field 'confirmPasswordGroup' should be named '_confirmPasswordGroup' (leading underscore + camelCase). |
| 29 | Suggestion | SER01 | [SerializeField] field 'confirmPasswordInput' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 30 | Warning | NAM01 | Private field 'confirmPasswordInput' should be named '_confirmPasswordInput' (leading underscore + camelCase). |
| 32 | Suggestion | SER01 | [SerializeField] field 'rememberToggle' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 33 | Warning | NAM01 | Private field 'rememberToggle' should be named '_rememberToggle' (leading underscore + camelCase). |
| 35 | Suggestion | SER01 | [SerializeField] field 'rememberLabel' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 36 | Warning | NAM01 | Private field 'rememberLabel' should be named '_rememberLabel' (leading underscore + camelCase). |
| 38 | Suggestion | SER01 | [SerializeField] field 'submitButton' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 39 | Warning | NAM01 | Private field 'submitButton' should be named '_submitButton' (leading underscore + camelCase). |
| 41 | Suggestion | SER01 | [SerializeField] field 'submitButtonText' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 42 | Warning | NAM01 | Private field 'submitButtonText' should be named '_submitButtonText' (leading underscore + camelCase). |
| 44 | Suggestion | SER01 | [SerializeField] field 'switchModeButton' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 45 | Warning | NAM01 | Private field 'switchModeButton' should be named '_switchModeButton' (leading underscore + camelCase). |
| 47 | Suggestion | SER01 | [SerializeField] field 'switchModeText' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 48 | Warning | NAM01 | Private field 'switchModeText' should be named '_switchModeText' (leading underscore + camelCase). |
| 50 | Suggestion | SER01 | [SerializeField] field 'errorText' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 51 | Warning | NAM01 | Private field 'errorText' should be named '_errorText' (leading underscore + camelCase). |
| 53 | Suggestion | SER01 | [SerializeField] field 'authService' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 54 | Warning | NAM01 | Private field 'authService' should be named '_authService' (leading underscore + camelCase). |
| 56 | Suggestion | SER01 | [SerializeField] field 'authCanvas' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 57 | Warning | NAM01 | Private field 'authCanvas' should be named '_authCanvas' (leading underscore + camelCase). |
| 59 | Suggestion | SER01 | [SerializeField] field 'authRaycaster' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 60 | Warning | NAM01 | Private field 'authRaycaster' should be named '_authRaycaster' (leading underscore + camelCase). |
| 62 | Suggestion | SER01 | [SerializeField] field 'gameplayCanvas' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 63 | Warning | NAM01 | Private field 'gameplayCanvas' should be named '_gameplayCanvas' (leading underscore + camelCase). |
| 65 | Suggestion | SER01 | [SerializeField] field 'gameplayRaycaster' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 66 | Warning | NAM01 | Private field 'gameplayRaycaster' should be named '_gameplayRaycaster' (leading underscore + camelCase). |
| 68 | Suggestion | SER01 | [SerializeField] field 'authCanvasSortingOrder' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 69 | Warning | NAM01 | Private field 'authCanvasSortingOrder' should be named '_authCanvasSortingOrder' (leading underscore + camelCase). |
| 71 | Suggestion | SER01 | [SerializeField] field 'minUsernameLength' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 73 | Warning | NAM01 | Private field 'minUsernameLength' should be named '_minUsernameLength' (leading underscore + camelCase). |
| 75 | Suggestion | SER01 | [SerializeField] field 'minPasswordLength' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 76 | Warning | NAM01 | Private field 'minPasswordLength' should be named '_minPasswordLength' (leading underscore + camelCase). |
| 79 | Warning | NAM02 | Public/internal field 'events' should be PascalCase. |
| 85 | Info | DOC01 | Public class 'AuthEvents' has no /// <summary> doc comment. |
| 88 | Warning | NAM02 | Public/internal field 'onLoginSuccess' should be PascalCase. |
| 89 | Warning | NAM02 | Public/internal field 'onRegisterSuccess' should be PascalCase. |
| 90 | Warning | NAM02 | Public/internal field 'onError' should be PascalCase. |
| 93 | Info | DOC01 | Public class 'AuthStringEvent' has no /// <summary> doc comment. |
| 94 | Warning | FMT05 | Opening brace should be on its own line (Allman style). |
| 352 | Suggestion | API01 | Method 'ApplyMode' takes boolean flag parameter(s) [registerMode] — consider splitting into named methods, unless this signature is fixed by a delegate/event contract (e.g. UnityEvent<bool>). |
| 368 | Suggestion | API01 | Method 'ApplyCanvasFocus' takes boolean flag parameter(s) [isAuthVisible] — consider splitting into named methods, unless this signature is fixed by a delegate/event contract (e.g. UnityEvent<bool>). |
| 388 | Info | DOC01 | Public method 'ToggleMode' has no /// <summary> doc comment. |
| 394 | Info | DOC01 | Public method 'HandleFieldChanged' has no /// <summary> doc comment. |
| 399 | Suggestion | API01 | Method 'HandleToggleChanged' takes boolean flag parameter(s) [_] — consider splitting into named methods, unless this signature is fixed by a delegate/event contract (e.g. UnityEvent<bool>). |
| 399 | Info | DOC01 | Public method 'HandleToggleChanged' has no /// <summary> doc comment. |
| 404 | Info | DOC01 | Public method 'HandleSubmitPressed' has no /// <summary> doc comment. |
| 484 | Suggestion | API01 | Method 'HandleLoginAsync' takes boolean flag parameter(s) [rememberMe] — consider splitting into named methods, unless this signature is fixed by a delegate/event contract (e.g. UnityEvent<bool>). |
| 569 | Suggestion | API01 | Method 'SetInputEnabled' takes boolean flag parameter(s) [enabled] — consider splitting into named methods, unless this signature is fixed by a delegate/event contract (e.g. UnityEvent<bool>). |
| 585 | Info | DOC01 | Public method 'ClosePanel' has no /// <summary> doc comment. |

### `Assets/Scripts/Runtime/NPCDialogue/EdgeFunctionClient.cs`

| Line | Severity | Rule | Message |
|---|---|---|---|
| 22 | Suggestion | SER01 | [SerializeField] field 'referencesGroup' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 24 | Warning | NAM01 | Private field 'referencesGroup' should be named '_referencesGroup' (leading underscore + camelCase). |
| 26 | Suggestion | SER01 | [SerializeField] field '_dialogueManager' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 29 | Suggestion | SER01 | [SerializeField] field 'behaviourGroup' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 31 | Warning | NAM01 | Private field 'behaviourGroup' should be named '_behaviourGroup' (leading underscore + camelCase). |
| 33 | Suggestion | SER01 | [SerializeField] field '_edgeFunctionUrl' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 37 | Suggestion | SER01 | [SerializeField] field '_requestTimeoutSeconds' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 205 | Info | DOC01 | Public class 'ProcessTurnResult' has no /// <summary> doc comment. |
| 224 | Info | DOC01 | Public class 'SummarizeSessionResult' has no /// <summary> doc comment. |
| 237 | Info | DOC01 | Public class 'UpdateRelationshipResult' has no /// <summary> doc comment. |
| 253 | Info | DOC01 | Public class 'RoomBroadcastResult' has no /// <summary> doc comment. |

### `Assets/Scripts/Runtime/NPCDialogue/Logging/NPCFlowEvent.cs`

| Line | Severity | Rule | Message |
|---|---|---|---|
| 8 | Info | DOC01 | Public class 'NPCFlowEvent' has no /// <summary> doc comment. |
| 53 | Info | DOC01 | Public method 'ToJson' has no /// <summary> doc comment. |

### `Assets/Scripts/Runtime/NPCDialogue/Logging/NPCFlowLogger.cs`

| Line | Severity | Rule | Message |
|---|---|---|---|
| 10 | Info | DOC01 | Public class 'NPCFlowLogger' has no /// <summary> doc comment. |
| 14 | Suggestion | SER01 | [SerializeField] field '_logToUnityConsole' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 25 | Suggestion | SER01 | [SerializeField] field '_logToJsonlFile' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 30 | Suggestion | SER01 | [SerializeField] field '_includeTextSnippets' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 40 | Suggestion | SER01 | [SerializeField] field '_includeRawTextPayloads' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 44 | Suggestion | SER01 | [SerializeField] field '_maxSnippetChars' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 49 | Suggestion | SER01 | [SerializeField] field 'cacheSettingsGroup' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 51 | Warning | NAM01 | Private field 'cacheSettingsGroup' should be named '_cacheSettingsGroup' (leading underscore + camelCase). |
| 53 | Suggestion | SER01 | [SerializeField] field '_maxInMemoryEvents' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 57 | Suggestion | SER01 | [SerializeField] field 'logFileStorageGroup' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 66 | Warning | NAM01 | Private field 'logFileStorageGroup' should be named '_logFileStorageGroup' (leading underscore + camelCase). |
| 68 | Suggestion | SER01 | [SerializeField] field '_relativeLogDirectory' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 72 | Suggestion | SER01 | [SerializeField] field '_overrideAbsoluteLogDirectory' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 76 | Suggestion | SER01 | [SerializeField] field '_maxLogDays' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 80 | Suggestion | SER01 | [SerializeField] field '_maxLogDirectorySizeMB' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 84 | Suggestion | SER01 | [SerializeField] field 'suppressionGroup' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 86 | Warning | NAM01 | Private field 'suppressionGroup' should be named '_suppressionGroup' (leading underscore + camelCase). |
| 88 | Suggestion | SER01 | [SerializeField] field '_maxDuplicateEventsPerMinute' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 175 | Info | DOC01 | Public method 'FindOrCreate' has no /// <summary> doc comment. |
| 212 | Info | DOC01 | Public method 'NextRequestId' has no /// <summary> doc comment. |
| 217 | Info | DOC01 | Public method 'Log' has no /// <summary> doc comment. |
| 245 | Info | DOC01 | Public method 'Log' has no /// <summary> doc comment. |
| 275 | Info | DOC01 | Public method 'GetRecentEvents' has no /// <summary> doc comment. |
| 376 | Info | DOC01 | Public method 'Flush' has no /// <summary> doc comment. |
| 388 | Info | DOC01 | Public method 'SummarizeText' has no /// <summary> doc comment. |
| 400 | Info | DOC01 | Public method 'StageToCategory' has no /// <summary> doc comment. |
| 453 | Info | DOC01 | Public method 'SupportsPersistentFileLogging' has no /// <summary> doc comment. |
| 662 | Suggestion | VAR01 | Single-letter identifier 'a' outside a for/foreach loop counter — use a descriptive name. |
| 662 | Suggestion | VAR01 | Single-letter identifier 'b' outside a for/foreach loop counter — use a descriptive name. |
| 708 | Info | DOC01 | Public method 'LogEditorWorkflow' has no /// <summary> doc comment. |
| 788 | Warning | NAM02 | Public/internal field 'timestamps' should be PascalCase. |

### `Assets/Scripts/Runtime/NPCDialogue/Logging/NPCFlowScope.cs`

| Line | Severity | Rule | Message |
|---|---|---|---|
| 7 | Info | DOC01 | Public class 'NPCFlowScope' has no /// <summary> doc comment. |
| 42 | Info | DOC01 | Public method 'Start' has no /// <summary> doc comment. |
| 67 | Info | DOC01 | Public method 'Success' has no /// <summary> doc comment. |
| 77 | Info | DOC01 | Public method 'Fallback' has no /// <summary> doc comment. |
| 87 | Info | DOC01 | Public method 'Skipped' has no /// <summary> doc comment. |
| 97 | Info | DOC01 | Public method 'Warning' has no /// <summary> doc comment. |
| 107 | Info | DOC01 | Public method 'Error' has no /// <summary> doc comment. |
| 127 | Info | DOC01 | Public method 'Dispose' has no /// <summary> doc comment. |

### `Assets/Scripts/Runtime/NPCDialogue/Logging/NPCFlowTextSanitizer.cs`

| Line | Severity | Rule | Message |
|---|---|---|---|
| 9 | Info | DOC01 | Public class 'NPCFlowTextSanitizer' has no /// <summary> doc comment. |
| 11 | Warning | NAM01 | Private field 'Sha256' should be named '_sha256' (leading underscore + camelCase). |
| 13 | Suggestion | API01 | Method 'SummarizeText' takes boolean flag parameter(s) [includeSnippet] — consider splitting into named methods, unless this signature is fixed by a delegate/event contract (e.g. UnityEvent<bool>). |
| 13 | Info | DOC01 | Public method 'SummarizeText' has no /// <summary> doc comment. |
| 43 | Suggestion | API01 | Method 'MergeSummary' takes boolean flag parameter(s) [includeSnippet] — consider splitting into named methods, unless this signature is fixed by a delegate/event contract (e.g. UnityEvent<bool>). |
| 43 | Info | DOC01 | Public method 'MergeSummary' has no /// <summary> doc comment. |
| 64 | Info | DOC01 | Public method 'CleanDialogueText' has no /// <summary> doc comment. |
| 87 | Warning | FMT02 | File does not end with a final newline. |

### `Assets/Scripts/Runtime/NPCDialogue/NPCDialogueActionHandler.cs`

| Line | Severity | Rule | Message |
|---|---|---|---|
| 20 | Warning | NAM01 | Private field 'KnownSuspects' should be named '_knownSuspects' (leading underscore + camelCase). |
| 31 | Warning | NAM01 | Private field 'KnownLocations' should be named '_knownLocations' (leading underscore + camelCase). |
| 54 | Warning | NAM01 | Private field 'ItemIndicators' should be named '_itemIndicators' (leading underscore + camelCase). |
| 76 | Warning | NAM01 | Private field 'MoodTriggers' should be named '_moodTriggers' (leading underscore + camelCase). |
| 105 | Warning | NAM01 | Private field 'SawPattern' should be named '_sawPattern' (leading underscore + camelCase). |
| 110 | Warning | NAM01 | Private field 'KnowPattern' should be named '_knowPattern' (leading underscore + camelCase). |
| 115 | Warning | NAM01 | Private field 'GivePattern' should be named '_givePattern' (leading underscore + camelCase). |
| 369 | Suggestion | VAR01 | Single-letter identifier 's' outside a for/foreach loop counter — use a descriptive name. |
| 372 | Suggestion | VAR01 | Single-letter identifier 'l' outside a for/foreach loop counter — use a descriptive name. |
| 401 | Suggestion | VAR01 | Single-letter identifier 'i' outside a for/foreach loop counter — use a descriptive name. |

### `Assets/Scripts/Runtime/NPCDialogue/NPCDialogueActionPlanner.cs`

| Line | Severity | Rule | Message |
|---|---|---|---|
| 20 | Info | DOC01 | Public class 'NPCDialogueActionPlan' has no /// <summary> doc comment. |
| 27 | Info | DOC01 | Public method 'None' has no /// <summary> doc comment. |
| 38 | Info | DOC01 | Public class 'NPCDialogueActionPlanner' has no /// <summary> doc comment. |
| 49 | Warning | NAM01 | Private field 'HintKeywords' should be named '_hintKeywords' (leading underscore + camelCase). |
| 58 | Warning | NAM01 | Private field 'NotesKeywords' should be named '_notesKeywords' (leading underscore + camelCase). |
| 59 | Warning | NAM01 | Private field 'MapKeywords' should be named '_mapKeywords' (leading underscore + camelCase). |
| 60 | Warning | NAM01 | Private field 'SolveKeywords' should be named '_solveKeywords' (leading underscore + camelCase). |
| 61 | Warning | NAM01 | Private field 'HelpKeywords' should be named '_helpKeywords' (leading underscore + camelCase). |
| 62 | Warning | NAM01 | Private field 'EvidenceKeywords' should be named '_evidenceKeywords' (leading underscore + camelCase). |
| 71 | Info | DOC01 | Public method 'Plan' has no /// <summary> doc comment. |
| 167 | Info | DOC01 | Public method 'BuildPromptHint' has no /// <summary> doc comment. |

### `Assets/Scripts/Runtime/NPCDialogue/NPCDialogueHistoryService.cs`

| Line | Severity | Rule | Message |
|---|---|---|---|
| 31 | Suggestion | API01 | Method 'Initialize' takes boolean flag parameter(s) [persistHistory] — consider splitting into named methods, unless this signature is fixed by a delegate/event contract (e.g. UnityEvent<bool>). |

### `Assets/Scripts/Runtime/NPCDialogue/NPCDialogueManager.cs`

| Line | Severity | Rule | Message |
|---|---|---|---|
| 12 | Info | DOC01 | Public class 'NPCDialogueManager' has no /// <summary> doc comment. |
| 15 | Suggestion | SER01 | [SerializeField] field 'chatClientGroup' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 22 | Warning | NAM01 | Private field 'chatClientGroup' should be named '_chatClientGroup' (leading underscore + camelCase). |
| 26 | Warning | NAM02 | Public/internal field '_chatClient' should be PascalCase. |
| 28 | Suggestion | SER01 | [SerializeField] field 'ragServicesGroup' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 37 | Warning | NAM01 | Private field 'ragServicesGroup' should be named '_ragServicesGroup' (leading underscore + camelCase). |
| 41 | Warning | NAM02 | Public/internal field '_localRag' should be PascalCase. |
| 56 | Warning | NAM02 | Public/internal field '_qdrantRag' should be PascalCase. |
| 58 | Suggestion | SER01 | [SerializeField] field 'gameSystemsGroup' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 60 | Warning | NAM01 | Private field 'gameSystemsGroup' should be named '_gameSystemsGroup' (leading underscore + camelCase). |
| 64 | Warning | NAM02 | Public/internal field '_actionPlanner' should be PascalCase. |
| 68 | Warning | NAM02 | Public/internal field '_evidenceState' should be PascalCase. |
| 70 | Suggestion | SER01 | [SerializeField] field 'persistenceGroup' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 72 | Warning | NAM01 | Private field 'persistenceGroup' should be named '_persistenceGroup' (leading underscore + camelCase). |
| 76 | Warning | NAM02 | Public/internal field '_supabaseRepo' should be PascalCase. |
| 78 | Suggestion | SER01 | [SerializeField] field 'llmConfigGroup' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 88 | Warning | NAM01 | Private field 'llmConfigGroup' should be named '_llmConfigGroup' (leading underscore + camelCase). |
| 94 | Suggestion | NET02 | Literal "localhost" used as a default value — acceptable as a config default, but any comparison against it downstream should go through NPCNetworkUtils.IsLocalHost(host). |
| 109 | Suggestion | SER01 | [SerializeField] field '_cachedModelNames' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 116 | Suggestion | NET02 | Literal "localhost" used as a default value — acceptable as a config default, but any comparison against it downstream should go through NPCNetworkUtils.IsLocalHost(host). |
| 124 | Suggestion | SER01 | [SerializeField] field 'dialogueSettingsGroup' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 135 | Warning | NAM01 | Private field 'dialogueSettingsGroup' should be named '_dialogueSettingsGroup' (leading underscore + camelCase). |
| 166 | Suggestion | SER01 | [SerializeField] field 'eventsGroup' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 179 | Warning | NAM01 | Private field 'eventsGroup' should be named '_eventsGroup' (leading underscore + camelCase). |
| 210 | Suggestion | SER01 | [SerializeField] field 'startupGroup' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 217 | Warning | NAM01 | Private field 'startupGroup' should be named '_startupGroup' (leading underscore + camelCase). |
| 326 | Info | DOC01 | Public method 'InitializeAsync' has no /// <summary> doc comment. |
| 619 | Suggestion | VAR01 | Single-letter identifier 'm' outside a for/foreach loop counter — use a descriptive name. |
| 677 | Info | DOC01 | Public method 'GetDefaultProfileSlug' has no /// <summary> doc comment. |
| 683 | Info | DOC01 | Public method 'SwitchToNPC' has no /// <summary> doc comment. |
| 688 | Info | DOC01 | Public method 'SwitchToNPCAsync' has no /// <summary> doc comment. |
| 731 | Info | DOC01 | Public method 'SendDialogueMessage' has no /// <summary> doc comment. |
| 756 | Info | DOC01 | Public method 'SetRuntimePlayerContext' has no /// <summary> doc comment. |
| 761 | Info | DOC01 | Public method 'ClearRuntimePlayerContext' has no /// <summary> doc comment. |
| 781 | Info | DOC01 | Public method 'AddNPCKnowledge' has no /// <summary> doc comment. |
| 787 | Info | DOC01 | Public method 'SaveRAGEmbeddings' has no /// <summary> doc comment. |
| 792 | Info | DOC01 | Public method 'GetHistory' has no /// <summary> doc comment. |
| 800 | Info | DOC01 | Public method 'CaptureHistorySnapshot' has no /// <summary> doc comment. |
| 806 | Info | DOC01 | Public method 'ApplyHistorySnapshot' has no /// <summary> doc comment. |
| 811 | Info | DOC01 | Public method 'CaptureEvidenceSnapshot' has no /// <summary> doc comment. |
| 818 | Info | DOC01 | Public method 'ApplyEvidenceSnapshot' has no /// <summary> doc comment. |
| 825 | Info | DOC01 | Public method 'ClearHistory' has no /// <summary> doc comment. |
| 831 | Info | DOC01 | Public method 'CancelRequests' has no /// <summary> doc comment. |
| 836 | Info | DOC01 | Public method 'GetNPCNames' has no /// <summary> doc comment. |
| 869 | Info | DOC01 | Public class 'LocalAIModelEntry' has no /// <summary> doc comment. |
| 872 | Warning | NAM02 | Public/internal field 'id' should be PascalCase. |
| 873 | Warning | NAM02 | Public/internal field '@object' should be PascalCase. |
| 876 | Info | DOC01 | Public class 'LocalAIModelsResponse' has no /// <summary> doc comment. |
| 879 | Warning | NAM02 | Public/internal field '@object' should be PascalCase. |
| 880 | Warning | NAM02 | Public/internal field 'data' should be PascalCase. |

### `Assets/Scripts/Runtime/NPCDialogue/NPCDialogueRetrievalService.cs`

| Line | Severity | Rule | Message |
|---|---|---|---|
| 42 | Suggestion | API01 | Method 'Initialize' takes boolean flag parameter(s) [enableRAG, useQdrantRag, rebuildRagFromKnowledgeIfMissing] — consider splitting into named methods, unless this signature is fixed by a delegate/event contract (e.g. UnityEvent<bool>). |
| 118 | Suggestion | VAR01 | Single-letter identifier 'e' outside a for/foreach loop counter — use a descriptive name. |
| 166 | Suggestion | VAR01 | Single-letter identifier 'e' outside a for/foreach loop counter — use a descriptive name. |
| 292 | Suggestion | VAR01 | Single-letter identifier 'e' outside a for/foreach loop counter — use a descriptive name. |
| 404 | Suggestion | VAR01 | Single-letter identifier 'e' outside a for/foreach loop counter — use a descriptive name. |

### `Assets/Scripts/Runtime/NPCDialogue/NPCDialogueSessionService.cs`

| Line | Severity | Rule | Message |
|---|---|---|---|
| 73 | Suggestion | NET02 | Literal "localhost" used as a default value — acceptable as a config default, but any comparison against it downstream should go through NPCNetworkUtils.IsLocalHost(host). |
| 604 | Info | DOC01 | Public method 'IsTechnicalCodebaseQuestion' has no /// <summary> doc comment. |

### `Assets/Scripts/Runtime/NPCDialogue/NPCDialogueSmokeValidator.cs`

| Line | Severity | Rule | Message |
|---|---|---|---|
| 9 | Info | DOC01 | Public class 'NPCDialogueSmokeValidator' has no /// <summary> doc comment. |
| 12 | Suggestion | SER01 | [SerializeField] field 'referencesGroup' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 14 | Warning | NAM01 | Private field 'referencesGroup' should be named '_referencesGroup' (leading underscore + camelCase). |
| 18 | Warning | NAM02 | Public/internal field '_dialogueManager' should be PascalCase. |
| 22 | Warning | NAM02 | Public/internal field '_chatClient' should be PascalCase. |
| 26 | Warning | NAM02 | Public/internal field '_localRag' should be PascalCase. |
| 28 | Suggestion | SER01 | [SerializeField] field 'smokeSettingsGroup' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 30 | Warning | NAM01 | Private field 'smokeSettingsGroup' should be named '_smokeSettingsGroup' (leading underscore + camelCase). |
| 32 | Suggestion | SER01 | [SerializeField] field 'validateOnStart' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 33 | Warning | NAM02 | Public/internal field 'validateOnStart' should be PascalCase. |
| 35 | Suggestion | SER01 | [SerializeField] field 'runFirstQuestionSmokeOnStart' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 36 | Warning | NAM02 | Public/internal field 'runFirstQuestionSmokeOnStart' should be PascalCase. |
| 38 | Suggestion | SER01 | [SerializeField] field 'smokeQuestion' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 40 | Warning | NAM02 | Public/internal field 'smokeQuestion' should be PascalCase. |
| 42 | Suggestion | SER01 | [SerializeField] field 'smokeTimeoutSeconds' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 43 | Warning | NAM02 | Public/internal field 'smokeTimeoutSeconds' should be PascalCase. |
| 83 | Info | DOC01 | Public method 'ValidateConfiguration' has no /// <summary> doc comment. |
| 124 | Info | DOC01 | Public method 'RunFirstQuestionSmoke' has no /// <summary> doc comment. |
| 130 | Info | DOC01 | Public method 'RunFirstQuestionSmokeAsync' has no /// <summary> doc comment. |
| 303 | Suggestion | API01 | Method 'Require' takes boolean flag parameter(s) [condition] — consider splitting into named methods, unless this signature is fixed by a delegate/event contract (e.g. UnityEvent<bool>). |

### `Assets/Scripts/Runtime/NPCDialogue/NPCDialogueUIController.cs`

| Line | Severity | Rule | Message |
|---|---|---|---|
| 12 | Info | DOC01 | Public class 'NPCDialogueUIController' has no /// <summary> doc comment. |
| 15 | Suggestion | SER01 | [SerializeField] field '_docsGroup' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 23 | Suggestion | SER01 | [SerializeField] field 'referencesGroup' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 31 | Warning | NAM01 | Private field 'referencesGroup' should be named '_referencesGroup' (leading underscore + camelCase). |
| 45 | Suggestion | SER01 | [SerializeField] field 'dialogueUiGroup' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 54 | Warning | NAM01 | Private field 'dialogueUiGroup' should be named '_dialogueUiGroup' (leading underscore + camelCase). |
| 72 | Suggestion | SER01 | [SerializeField] field 'portraitsGroup' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 74 | Warning | NAM01 | Private field 'portraitsGroup' should be named '_portraitsGroup' (leading underscore + camelCase). |
| 88 | Suggestion | SER01 | [SerializeField] field 'notebookGroup' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 90 | Warning | NAM01 | Private field 'notebookGroup' should be named '_notebookGroup' (leading underscore + camelCase). |
| 96 | Suggestion | SER01 | [SerializeField] field 'relationshipGroup' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 98 | Warning | NAM01 | Private field 'relationshipGroup' should be named '_relationshipGroup' (leading underscore + camelCase). |
| 104 | Suggestion | SER01 | [SerializeField] field 'exitStartupGroup' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 106 | Warning | NAM01 | Private field 'exitStartupGroup' should be named '_exitStartupGroup' (leading underscore + camelCase). |
| 197 | Suggestion | API01 | Method 'SetInputEnabled' takes boolean flag parameter(s) [enabled] — consider splitting into named methods, unless this signature is fixed by a delegate/event contract (e.g. UnityEvent<bool>). |
| 197 | Info | DOC01 | Public method 'SetInputEnabled' has no /// <summary> doc comment. |
| 205 | Info | DOC01 | Public method 'SetAIText' has no /// <summary> doc comment. |
| 211 | Info | DOC01 | Public method 'GetActiveProfile' has no /// <summary> doc comment. |
| 216 | Info | DOC01 | Public method 'ToggleNotebook' has no /// <summary> doc comment. |
| 222 | Info | DOC01 | Public method 'IsAnyPanelOpen' has no /// <summary> doc comment. |
| 227 | Info | DOC01 | Public method 'InitializeOnDemandAsync' has no /// <summary> doc comment. |
| 232 | Info | DOC01 | Public method 'GetGameplayCanvas' has no /// <summary> doc comment. |

### `Assets/Scripts/Runtime/NPCDialogue/NPCEvidenceState.cs`

| Line | Severity | Rule | Message |
|---|---|---|---|
| 13 | Warning | NAM02 | Public/internal field 'npcSlug' should be PascalCase. |
| 14 | Warning | NAM02 | Public/internal field 'clueText' should be PascalCase. |
| 15 | Warning | NAM02 | Public/internal field 'category' should be PascalCase. |
| 16 | Warning | NAM02 | Public/internal field 'gameTime' should be PascalCase. |
| 30 | Warning | NAM02 | Public/internal field 'actionType' should be PascalCase. |
| 31 | Warning | NAM02 | Public/internal field 'description' should be PascalCase. |
| 32 | Warning | NAM02 | Public/internal field 'value' should be PascalCase. |
| 33 | Warning | NAM02 | Public/internal field 'npcSlug' should be PascalCase. |
| 48 | Info | DOC01 | Public method 'ToHistoryLine' has no /// <summary> doc comment. |
| 54 | Info | DOC01 | Public class 'NPCEvidenceStateSnapshot' has no /// <summary> doc comment. |
| 57 | Warning | NAM02 | Public/internal field 'discoveredClues' should be PascalCase. |
| 58 | Warning | NAM02 | Public/internal field 'obtainedItems' should be PascalCase. |
| 59 | Warning | NAM02 | Public/internal field 'visitedLocations' should be PascalCase. |
| 60 | Warning | NAM02 | Public/internal field 'npcMoodKeys' should be PascalCase. |
| 61 | Warning | NAM02 | Public/internal field 'npcMoodValues' should be PascalCase. |
| 62 | Warning | NAM02 | Public/internal field 'npcTrustKeys' should be PascalCase. |
| 63 | Warning | NAM02 | Public/internal field 'npcTrustValues' should be PascalCase. |
| 65 | Info | DOC01 | Public method 'Clone' has no /// <summary> doc comment. |
| 80 | Info | DOC01 | Public class 'NPCEvidenceState' has no /// <summary> doc comment. |
| 82 | Suggestion | SER01 | [SerializeField] field '_docsGroup' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 112 | Suggestion | VAR01 | Single-letter identifier 'x' outside a for/foreach loop counter — use a descriptive name. |
| 116 | Suggestion | SER01 | [SerializeField] field 'investigationGroup' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 124 | Warning | NAM01 | Private field 'investigationGroup' should be named '_investigationGroup' (leading underscore + camelCase). |
| 126 | Suggestion | SER01 | [SerializeField] field 'discoveredClues' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 127 | Warning | NAM02 | Public/internal field 'discoveredClues' should be PascalCase. |
| 129 | Suggestion | SER01 | [SerializeField] field 'obtainedItems' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 130 | Warning | NAM02 | Public/internal field 'obtainedItems' should be PascalCase. |
| 132 | Suggestion | SER01 | [SerializeField] field 'visitedLocations' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 133 | Warning | NAM02 | Public/internal field 'visitedLocations' should be PascalCase. |
| 135 | Suggestion | SER01 | [SerializeField] field 'npcStatesGroup' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 144 | Warning | NAM01 | Private field 'npcStatesGroup' should be named '_npcStatesGroup' (leading underscore + camelCase). |
| 146 | Suggestion | SER01 | [SerializeField] field 'npcMoodKeys' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 147 | Warning | NAM02 | Public/internal field 'npcMoodKeys' should be PascalCase. |
| 149 | Suggestion | SER01 | [SerializeField] field 'npcMoodValues' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 150 | Warning | NAM02 | Public/internal field 'npcMoodValues' should be PascalCase. |
| 152 | Suggestion | SER01 | [SerializeField] field 'npcTrustKeys' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 153 | Warning | NAM02 | Public/internal field 'npcTrustKeys' should be PascalCase. |
| 155 | Suggestion | SER01 | [SerializeField] field 'npcTrustValues' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 156 | Warning | NAM02 | Public/internal field 'npcTrustValues' should be PascalCase. |
| 183 | Suggestion | VAR01 | Single-letter identifier 'm' outside a for/foreach loop counter — use a descriptive name. |
| 189 | Suggestion | VAR01 | Single-letter identifier 't' outside a for/foreach loop counter — use a descriptive name. |
| 198 | Info | DOC01 | Public method 'RecordClue' has no /// <summary> doc comment. |
| 215 | Info | DOC01 | Public method 'HasClue' has no /// <summary> doc comment. |
| 227 | Info | DOC01 | Public method 'SetNpcMood' has no /// <summary> doc comment. |
| 236 | Info | DOC01 | Public method 'GetNpcMood' has no /// <summary> doc comment. |
| 243 | Info | DOC01 | Public method 'AdjustNpcTrust' has no /// <summary> doc comment. |
| 253 | Info | DOC01 | Public method 'GetNpcTrust' has no /// <summary> doc comment. |
| 258 | Info | DOC01 | Public method 'GetTrustLabel' has no /// <summary> doc comment. |
| 260 | Suggestion | VAR01 | Single-letter identifier 't' outside a for/foreach loop counter — use a descriptive name. |
| 274 | Info | DOC01 | Public method 'AddItem' has no /// <summary> doc comment. |
| 283 | Info | DOC01 | Public method 'AddLocation' has no /// <summary> doc comment. |
| 294 | Info | DOC01 | Public method 'BuildStateContextString' has no /// <summary> doc comment. |
| 310 | Suggestion | VAR01 | Single-letter identifier 'c' outside a for/foreach loop counter — use a descriptive name. |
| 327 | Info | DOC01 | Public method 'BuildNpcStateLine' has no /// <summary> doc comment. |
| 336 | Info | DOC01 | Public method 'CreateSnapshot' has no /// <summary> doc comment. |
| 350 | Info | DOC01 | Public method 'ApplySnapshot' has no /// <summary> doc comment. |

### `Assets/Scripts/Runtime/NPCDialogue/NPCHistoryStore.cs`

| Line | Severity | Rule | Message |
|---|---|---|---|
| 8 | Info | DOC01 | Public class 'DialogueEntry' has no /// <summary> doc comment. |
| 11 | Warning | NAM02 | Public/internal field 'role' should be PascalCase. |
| 12 | Warning | NAM02 | Public/internal field 'content' should be PascalCase. |
| 13 | Warning | NAM02 | Public/internal field 'timestampUtc' should be PascalCase. |
| 15 | Warning | FMT05 | Opening brace should be on its own line (Allman style). |
| 28 | Warning | NAM02 | Public/internal field 'entries' should be PascalCase. |
| 31 | Info | DOC01 | Public class 'NPCHistoryStore' has no /// <summary> doc comment. |
| 33 | Info | DOC01 | Public method 'Load' has no /// <summary> doc comment. |
| 122 | Info | DOC01 | Public method 'Save' has no /// <summary> doc comment. |
| 174 | Info | DOC01 | Public method 'Delete' has no /// <summary> doc comment. |
| 226 | Info | DOC01 | Public method 'GetFullPath' has no /// <summary> doc comment. |
| 235 | Info | DOC01 | Public method 'NormalizeForChatTemplate' has no /// <summary> doc comment. |

### `Assets/Scripts/Runtime/NPCDialogue/NPCNotebookStateFormatter.cs`

| Line | Severity | Rule | Message |
|---|---|---|---|
| 6 | Info | DOC01 | Public class 'NPCNotebookStateFormatter' has no /// <summary> doc comment. |
| 8 | Info | DOC01 | Public method 'Build' has no /// <summary> doc comment. |

### `Assets/Scripts/Runtime/NPCDialogue/NPCProfile.cs`

| Line | Severity | Rule | Message |
|---|---|---|---|
| 9 | Info | DOC01 | Public class 'NPCProfile' has no /// <summary> doc comment. |
| 195 | Suggestion | SER01 | [SerializeField] field 'inspectorPreview' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 196 | Warning | NAM01 | Private field 'inspectorPreview' should be named '_inspectorPreview' (leading underscore + camelCase). |
| 229 | Info | DOC01 | Public method 'GetNpcSlug' has no /// <summary> doc comment. |
| 238 | Info | DOC01 | Public method 'GetDisplayName' has no /// <summary> doc comment. |
| 245 | Info | DOC01 | Public method 'GetRagCategory' has no /// <summary> doc comment. |
| 250 | Info | DOC01 | Public method 'GetKnowledgeSourcePath' has no /// <summary> doc comment. |
| 257 | Info | DOC01 | Public method 'GetLoraAdapterPath' has no /// <summary> doc comment. |
| 264 | Info | DOC01 | Public method 'GetHistorySaveFile' has no /// <summary> doc comment. |

### `Assets/Scripts/Runtime/NPCDialogue/NPCProfilePromptComposer.cs`

| Line | Severity | Rule | Message |
|---|---|---|---|
| 16 | Warning | NAM02 | Public/internal field 'playerName' should be PascalCase. |
| 17 | Warning | NAM02 | Public/internal field 'npcSlug' should be PascalCase. |
| 18 | Warning | NAM02 | Public/internal field 'trustScore' should be PascalCase. |
| 19 | Warning | NAM02 | Public/internal field 'trustLabel' should be PascalCase. |
| 20 | Warning | NAM02 | Public/internal field 'mood' should be PascalCase. |
| 21 | Warning | NAM02 | Public/internal field 'dialogueCount' should be PascalCase. |
| 22 | Warning | NAM02 | Public/internal field 'currentLocation' should be PascalCase. |
| 23 | Warning | NAM02 | Public/internal field 'timeOfDay' should be PascalCase. |
| 50 | Info | DOC01 | Public method 'BuildSystemPrompt' has no /// <summary> doc comment. |
| 55 | Info | DOC01 | Public method 'BuildSystemPrompt' has no /// <summary> doc comment. |
| 97 | Info | DOC01 | Public method 'BuildActionPolicyText' has no /// <summary> doc comment. |
| 126 | Info | DOC01 | Public method 'BuildKnowledgeRouteText' has no /// <summary> doc comment. |

### `Assets/Scripts/Runtime/NPCDialogue/NPCRAGImporter.cs`

| Line | Severity | Rule | Message |
|---|---|---|---|
| 9 | Info | DOC01 | Public class 'NPCRAGImporter' has no /// <summary> doc comment. |
| 15 | Info | DOC01 | Public method 'RebuildAsync' has no /// <summary> doc comment. |
| 153 | Info | DOC01 | Public method 'ChunkTextByMaxSize' has no /// <summary> doc comment. |
| 185 | Info | DOC01 | Public method 'ResolveStreamingAssetPath' has no /// <summary> doc comment. |

### `Assets/Scripts/Runtime/NPCDialogue/NPCRAGMetadata.cs`

| Line | Severity | Rule | Message |
|---|---|---|---|
| 10 | Info | DOC01 | Public class 'NPCRAGMetadata' has no /// <summary> doc comment. |
| 13 | Warning | NAM02 | Public/internal field 'importerVersion' should be PascalCase. |
| 14 | Warning | NAM02 | Public/internal field 'ragPath' should be PascalCase. |
| 15 | Warning | NAM02 | Public/internal field 'embeddingModel' should be PascalCase. |
| 16 | Warning | NAM02 | Public/internal field 'embeddingLength' should be PascalCase. |
| 17 | Warning | NAM02 | Public/internal field 'chunkCharacters' should be PascalCase. |
| 18 | Warning | NAM02 | Public/internal field 'sourceCount' should be PascalCase. |
| 19 | Warning | NAM02 | Public/internal field 'chunkCount' should be PascalCase. |
| 20 | Warning | NAM02 | Public/internal field 'builtAtUtc' should be PascalCase. |
| 21 | Warning | NAM02 | Public/internal field 'sources' should be PascalCase. |
| 24 | Info | DOC01 | Public class 'NPCRAGSourceMetadata' has no /// <summary> doc comment. |
| 27 | Warning | NAM02 | Public/internal field 'npcSlug' should be PascalCase. |
| 28 | Warning | NAM02 | Public/internal field 'displayName' should be PascalCase. |
| 29 | Warning | NAM02 | Public/internal field 'sourcePath' should be PascalCase. |
| 30 | Warning | NAM02 | Public/internal field 'sha256' should be PascalCase. |
| 31 | Warning | NAM02 | Public/internal field 'byteLength' should be PascalCase. |
| 32 | Warning | NAM02 | Public/internal field 'chunkCount' should be PascalCase. |
| 35 | Info | DOC01 | Public class 'NPCRAGMetadataStore' has no /// <summary> doc comment. |
| 39 | Info | DOC01 | Public method 'GetMetadataPath' has no /// <summary> doc comment. |
| 48 | Info | DOC01 | Public method 'CreateExpected' has no /// <summary> doc comment. |
| 92 | Info | DOC01 | Public method 'TryLoad' has no /// <summary> doc comment. |
| 104 | Suggestion | VAR01 | Single-letter identifier 'e' outside a for/foreach loop counter — use a descriptive name. |
| 125 | Info | DOC01 | Public method 'Save' has no /// <summary> doc comment. |
| 140 | Info | DOC01 | Public method 'IsCurrent' has no /// <summary> doc comment. |

### `Assets/Scripts/Runtime/NPCDialogue/NPCRelationshipUIController.cs`

| Line | Severity | Rule | Message |
|---|---|---|---|
| 9 | Info | DOC01 | Public class 'NPCRelationshipUIController' has no /// <summary> doc comment. |
| 12 | Suggestion | SER01 | [SerializeField] field 'referencesGroup' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 14 | Warning | NAM01 | Private field 'referencesGroup' should be named '_referencesGroup' (leading underscore + camelCase). |
| 25 | Suggestion | SER01 | [SerializeField] field 'behaviourGroup' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 27 | Warning | NAM01 | Private field 'behaviourGroup' should be named '_behaviourGroup' (leading underscore + camelCase). |
| 29 | Suggestion | SER01 | [SerializeField] field 'autoHideWhenNoNpc' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 30 | Warning | NAM01 | Private field 'autoHideWhenNoNpc' should be named '_autoHideWhenNoNpc' (leading underscore + camelCase). |
| 52 | Info | DOC01 | Public method 'Refresh' has no /// <summary> doc comment. |
| 69 | Suggestion | API01 | Method 'UpdateTrustBar' takes boolean flag parameter(s) [hasNpc] — consider splitting into named methods, unless this signature is fixed by a delegate/event contract (e.g. UnityEvent<bool>). |
| 89 | Suggestion | API01 | Method 'UpdateMoodLabel' takes boolean flag parameter(s) [hasNpc] — consider splitting into named methods, unless this signature is fixed by a delegate/event contract (e.g. UnityEvent<bool>). |
| 106 | Suggestion | API01 | Method 'UpdateDialogueCount' takes boolean flag parameter(s) [hasNpc] — consider splitting into named methods, unless this signature is fixed by a delegate/event contract (e.g. UnityEvent<bool>). |
| 114 | Suggestion | API01 | Method 'UpdateVisibility' takes boolean flag parameter(s) [hasNpc] — consider splitting into named methods, unless this signature is fixed by a delegate/event contract (e.g. UnityEvent<bool>). |

### `Assets/Scripts/Runtime/NPCDialogue/NotebookUIController.cs`

| Line | Severity | Rule | Message |
|---|---|---|---|
| 9 | Info | DOC01 | Public class 'NotebookUIController' has no /// <summary> doc comment. |
| 11 | Suggestion | SER01 | [SerializeField] field '_docsGroup' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 19 | Suggestion | SER01 | [SerializeField] field 'referencesGroup' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 21 | Warning | NAM01 | Private field 'referencesGroup' should be named '_referencesGroup' (leading underscore + camelCase). |
| 31 | Suggestion | SER01 | [SerializeField] field 'notebookButtonsGroup' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 41 | Warning | NAM01 | Private field 'notebookButtonsGroup' should be named '_notebookButtonsGroup' (leading underscore + camelCase). |
| 63 | Suggestion | SER01 | [SerializeField] field 'notebookPanelsGroup' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 75 | Warning | NAM01 | Private field 'notebookPanelsGroup' should be named '_notebookPanelsGroup' (leading underscore + camelCase). |
| 105 | Suggestion | SER01 | [SerializeField] field 'dropdownAnswersGroup' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 107 | Warning | NAM01 | Private field 'dropdownAnswersGroup' should be named '_dropdownAnswersGroup' (leading underscore + camelCase). |
| 121 | Suggestion | SER01 | [SerializeField] field 'notesTextGroup' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 123 | Warning | NAM01 | Private field 'notesTextGroup' should be named '_notesTextGroup' (leading underscore + camelCase). |
| 356 | Info | DOC01 | Public method 'ShowNotes' has no /// <summary> doc comment. |
| 370 | Info | DOC01 | Public method 'ShowMap' has no /// <summary> doc comment. |
| 377 | Info | DOC01 | Public method 'ShowSolve' has no /// <summary> doc comment. |
| 391 | Info | DOC01 | Public method 'ShowHelp' has no /// <summary> doc comment. |
| 404 | Info | DOC01 | Public method 'HideFail' has no /// <summary> doc comment. |
| 410 | Info | DOC01 | Public method 'SubmitAnswer' has no /// <summary> doc comment. |
| 450 | Info | DOC01 | Public method 'HandleGlobalClick' has no /// <summary> doc comment. |
| 560 | Info | DOC01 | Public method 'ToggleNotebook' has no /// <summary> doc comment. |

### `Assets/Scripts/Runtime/NPCDialogue/PlayerAuthService.cs`

| Line | Severity | Rule | Message |
|---|---|---|---|
| 16 | Info | DOC01 | Public class 'PlayerAuthService' has no /// <summary> doc comment. |
| 20 | Suggestion | SER01 | [SerializeField] field 'supabaseAuthGroup' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 33 | Warning | NAM01 | Private field 'supabaseAuthGroup' should be named '_supabaseAuthGroup' (leading underscore + camelCase). |
| 35 | Suggestion | SER01 | [SerializeField] field 'supabaseUrl' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 36 | Warning | NAM01 | Private field 'supabaseUrl' should be named '_supabaseUrl' (leading underscore + camelCase). |
| 38 | Suggestion | SER01 | [SerializeField] field 'supabaseAnonKey' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 39 | Warning | NAM01 | Private field 'supabaseAnonKey' should be named '_supabaseAnonKey' (leading underscore + camelCase). |
| 41 | Suggestion | SER01 | [SerializeField] field 'restApiUrl' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 42 | Warning | NAM01 | Private field 'restApiUrl' should be named '_restApiUrl' (leading underscore + camelCase). |
| 79 | Suggestion | SER01 | [SerializeField] field 'behaviourGroup' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 87 | Warning | NAM01 | Private field 'behaviourGroup' should be named '_behaviourGroup' (leading underscore + camelCase). |
| 89 | Suggestion | SER01 | [SerializeField] field 'requestTimeoutSeconds' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 90 | Warning | NAM01 | Private field 'requestTimeoutSeconds' should be named '_requestTimeoutSeconds' (leading underscore + camelCase). |
| 92 | Suggestion | SER01 | [SerializeField] field 'validateStoredSessionOnStart' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 93 | Warning | NAM01 | Private field 'validateStoredSessionOnStart' should be named '_validateStoredSessionOnStart' (leading underscore + camelCase). |
| 95 | Suggestion | SER01 | [SerializeField] field 'restoreStoredSessionOnWebGLStart' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 96 | Warning | NAM01 | Private field 'restoreStoredSessionOnWebGLStart' should be named '_restoreStoredSessionOnWebGLStart' (leading underscore + camelCase). |
| 98 | Suggestion | SER01 | [SerializeField] field 'debugGroup' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 106 | Warning | NAM01 | Private field 'debugGroup' should be named '_debugGroup' (leading underscore + camelCase). |
| 108 | Suggestion | SER01 | [SerializeField] field 'lastAuthStatus' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 109 | Warning | NAM01 | Private field 'lastAuthStatus' should be named '_lastAuthStatus' (leading underscore + camelCase). |
| 111 | Suggestion | SER01 | [SerializeField] field 'lastAuthRoute' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 112 | Warning | NAM01 | Private field 'lastAuthRoute' should be named '_lastAuthRoute' (leading underscore + camelCase). |
| 114 | Suggestion | SER01 | [SerializeField] field 'lastAuthDurationMs' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 115 | Warning | NAM01 | Private field 'lastAuthDurationMs' should be named '_lastAuthDurationMs' (leading underscore + camelCase). |
| 188 | Info | DOC01 | Public method 'InitializeAsync' has no /// <summary> doc comment. |
| 331 | Info | DOC01 | Public method 'RegisterAsync' has no /// <summary> doc comment. |
| 607 | Suggestion | API01 | Method 'LoginAsync' takes boolean flag parameter(s) [rememberMe] — consider splitting into named methods, unless this signature is fixed by a delegate/event contract (e.g. UnityEvent<bool>). |
| 607 | Info | DOC01 | Public method 'LoginAsync' has no /// <summary> doc comment. |
| 639 | Info | DOC01 | Public method 'TryRestoreStoredSessionAsync' has no /// <summary> doc comment. |
| 749 | Info | DOC01 | Public method 'LogoutAsync' has no /// <summary> doc comment. |
| 865 | Suggestion | API01 | Method 'ShouldValidateStoredSessionOnStart' takes boolean flag parameter(s) [configuredValue, restoreOnWebGLStart] — consider splitting into named methods, unless this signature is fixed by a delegate/event contract (e.g. UnityEvent<bool>). |

### `Assets/Scripts/Runtime/NPCDialogue/PlayerAuthTypes.cs`

| Line | Severity | Rule | Message |
|---|---|---|---|
| 13 | Warning | NAM02 | Public/internal field 'playerId' should be PascalCase. |
| 14 | Warning | NAM02 | Public/internal field 'email' should be PascalCase. |
| 15 | Warning | NAM02 | Public/internal field 'username' should be PascalCase. |
| 16 | Warning | NAM02 | Public/internal field 'createdAtUtc' should be PascalCase. |
| 26 | Warning | NAM02 | Public/internal field 'sessionId' should be PascalCase. |
| 27 | Warning | NAM02 | Public/internal field 'playerId' should be PascalCase. |
| 28 | Warning | NAM02 | Public/internal field 'username' should be PascalCase. |
| 29 | Warning | NAM02 | Public/internal field 'sessionToken' should be PascalCase. |
| 30 | Warning | NAM02 | Public/internal field 'refreshToken' should be PascalCase. |
| 31 | Warning | NAM02 | Public/internal field 'createdAtUtc' should be PascalCase. |
| 32 | Warning | NAM02 | Public/internal field 'expiresAtUtc' should be PascalCase. |
| 33 | Warning | NAM02 | Public/internal field 'lastSeenAtUtc' should be PascalCase. |
| 37 | Warning | FMT05 | Opening brace should be on its own line (Allman style). |

### `Assets/Scripts/Runtime/NPCDialogue/PlayerDialogueContext.cs`

| Line | Severity | Rule | Message |
|---|---|---|---|
| 17 | Suggestion | SER01 | [SerializeField] field '_playerName' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 20 | Suggestion | SER01 | [SerializeField] field '_playerId' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 25 | Suggestion | SER01 | [SerializeField] field '_trustScore' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 28 | Suggestion | SER01 | [SerializeField] field '_currentMood' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 31 | Suggestion | SER01 | [SerializeField] field '_dialogueCount' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 36 | Suggestion | SER01 | [SerializeField] field '_knownClues' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 39 | Suggestion | SER01 | [SerializeField] field '_inventory' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 42 | Suggestion | SER01 | [SerializeField] field '_visitedLocations' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 47 | Suggestion | SER01 | [SerializeField] field '_loadedFromServer' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 221 | Info | DOC01 | Public method 'FromLocalState' has no /// <summary> doc comment. |
| 241 | Suggestion | VAR01 | Single-letter identifier 'c' outside a for/foreach loop counter — use a descriptive name. |

### `Assets/Scripts/Runtime/NPCDialogue/PlayerDialogueContextService.cs`

| Line | Severity | Rule | Message |
|---|---|---|---|
| 23 | Suggestion | SER01 | [SerializeField] field 'referencesGroup' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 25 | Warning | NAM01 | Private field 'referencesGroup' should be named '_referencesGroup' (leading underscore + camelCase). |
| 27 | Suggestion | SER01 | [SerializeField] field '_authService' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 30 | Suggestion | SER01 | [SerializeField] field '_evidenceState' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 33 | Suggestion | SER01 | [SerializeField] field 'behaviourGroup' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 35 | Warning | NAM01 | Private field 'behaviourGroup' should be named '_behaviourGroup' (leading underscore + camelCase). |
| 37 | Suggestion | SER01 | [SerializeField] field '_enableServerContext' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 44 | Suggestion | SER01 | [SerializeField] field 'debugGroup' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 46 | Warning | NAM01 | Private field 'debugGroup' should be named '_debugGroup' (leading underscore + camelCase). |
| 48 | Suggestion | SER01 | [SerializeField] field '_currentNpcSlug' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 51 | Suggestion | SER01 | [SerializeField] field '_cachedContext' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 96 | Suggestion | API01 | Method 'GetOrLoadContextAsync' takes boolean flag parameter(s) [forceRefresh] — consider splitting into named methods, unless this signature is fixed by a delegate/event contract (e.g. UnityEvent<bool>). |

### `Assets/Scripts/Runtime/NPCDialogue/PlayerSessionStore.cs`

| Line | Severity | Rule | Message |
|---|---|---|---|
| 40 | Info | DOC01 | Public method 'SaveSession' has no /// <summary> doc comment. |
| 57 | Info | DOC01 | Public method 'DestroySession' has no /// <summary> doc comment. |
| 64 | Info | DOC01 | Public method 'LoadSession' has no /// <summary> doc comment. |
| 92 | Info | DOC01 | Public method 'ToAuthSession' has no /// <summary> doc comment. |
| 119 | Info | DOC01 | Public method 'Load' has no /// <summary> doc comment. |
| 126 | Info | DOC01 | Public method 'Save' has no /// <summary> doc comment. |
| 168 | Info | DOC01 | Public method 'Clear' has no /// <summary> doc comment. |
| 174 | Info | DOC01 | Public method 'IsExpired' has no /// <summary> doc comment. |

### `Assets/Scripts/Runtime/NPCDialogue/QdrantRAGService.cs`

| Line | Severity | Rule | Message |
|---|---|---|---|
| 13 | Info | DOC01 | Public class 'QdrantRAGService' has no /// <summary> doc comment. |
| 15 | Suggestion | SER01 | [SerializeField] field 'qdrantEndpointGroup' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 17 | Warning | NAM01 | Private field 'qdrantEndpointGroup' should be named '_qdrantEndpointGroup' (leading underscore + camelCase). |
| 30 | Suggestion | SER01 | [SerializeField] field 'embedderGroup' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 32 | Warning | NAM01 | Private field 'embedderGroup' should be named '_embedderGroup' (leading underscore + camelCase). |
| 41 | Suggestion | SER01 | [SerializeField] field 'inspectorStatus' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 42 | Warning | NAM01 | Private field 'inspectorStatus' should be named '_inspectorStatus' (leading underscore + camelCase). |
| 134 | Info | DOC01 | Public method 'BuildSearchEndpoint' has no /// <summary> doc comment. |
| 181 | Info | DOC01 | Public method 'SearchAsync' has no /// <summary> doc comment. |
| 365 | Info | DOC01 | Public method 'HasValidQdrantUrl' has no /// <summary> doc comment. |
| 371 | Info | DOC01 | Public method 'HasValidCollectionName' has no /// <summary> doc comment. |
| 379 | Warning | NAM02 | Public/internal field 'result' should be PascalCase. |
| 385 | Warning | NAM02 | Public/internal field 'points' should be PascalCase. |
| 391 | Warning | NAM02 | Public/internal field 'id' should be PascalCase. |
| 397 | Warning | NAM02 | Public/internal field 'result' should be PascalCase. |
| 403 | Warning | NAM02 | Public/internal field 'id' should be PascalCase. |
| 404 | Warning | NAM02 | Public/internal field 'score' should be PascalCase. |
| 405 | Warning | NAM02 | Public/internal field 'payload' should be PascalCase. |
| 411 | Warning | NAM02 | Public/internal field 'text' should be PascalCase. |

### `Assets/Scripts/Runtime/NPCDialogue/SessionAnalyticsService.cs`

| Line | Severity | Rule | Message |
|---|---|---|---|
| 21 | Suggestion | SER01 | [SerializeField] field 'referencesGroup' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 23 | Warning | NAM01 | Private field 'referencesGroup' should be named '_referencesGroup' (leading underscore + camelCase). |
| 25 | Suggestion | SER01 | [SerializeField] field '_authService' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 28 | Suggestion | SER01 | [SerializeField] field '_dialogueRepository' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 31 | Suggestion | SER01 | [SerializeField] field 'behaviourGroup' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 33 | Warning | NAM01 | Private field 'behaviourGroup' should be named '_behaviourGroup' (leading underscore + camelCase). |
| 35 | Suggestion | SER01 | [SerializeField] field '_enableServerAnalytics' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 42 | Suggestion | SER01 | [SerializeField] field 'debugGroup' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 44 | Warning | NAM01 | Private field 'debugGroup' should be named '_debugGroup' (leading underscore + camelCase). |
| 46 | Suggestion | SER01 | [SerializeField] field '_activeSessionId' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 49 | Suggestion | SER01 | [SerializeField] field '_currentNpcSlug' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 52 | Suggestion | SER01 | [SerializeField] field '_cachedAnalyticsPreview' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 170 | Suggestion | API01 | Method 'GetAnalyticsAsync' takes boolean flag parameter(s) [forceRefresh] — consider splitting into named methods, unless this signature is fixed by a delegate/event contract (e.g. UnityEvent<bool>). |

### `Assets/Scripts/Runtime/NPCDialogue/SupabaseDialogueModels.cs`

| Line | Severity | Rule | Message |
|---|---|---|---|
| 90 | Info | DOC01 | Public class 'SessionSummaryData' has no /// <summary> doc comment. |
| 111 | Info | DOC01 | Public class 'TurnTotalsData' has no /// <summary> doc comment. |
| 126 | Info | DOC01 | Public method 'ToPromptLine' has no /// <summary> doc comment. |
| 132 | Info | DOC01 | Public class 'RecentSessionData' has no /// <summary> doc comment. |

### `Assets/Scripts/Runtime/NPCDialogue/SupabaseDialogueRepository.cs`

| Line | Severity | Rule | Message |
|---|---|---|---|
| 13 | Info | DOC01 | Public class 'SupabaseDialogueRepository' has no /// <summary> doc comment. |
| 16 | Suggestion | SER01 | [SerializeField] field 'referencesGroup' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 19 | Warning | NAM01 | Private field 'referencesGroup' should be named '_referencesGroup' (leading underscore + camelCase). |
| 24 | Suggestion | SER01 | [SerializeField] field 'behaviourGroup' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 26 | Warning | NAM01 | Private field 'behaviourGroup' should be named '_behaviourGroup' (leading underscore + camelCase). |
| 28 | Suggestion | SER01 | [SerializeField] field 'requestTimeoutSeconds' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 29 | Warning | NAM01 | Private field 'requestTimeoutSeconds' should be named '_requestTimeoutSeconds' (leading underscore + camelCase). |
| 31 | Suggestion | SER01 | [SerializeField] field 'debugGroup' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 39 | Warning | NAM01 | Private field 'debugGroup' should be named '_debugGroup' (leading underscore + camelCase). |
| 41 | Suggestion | SER01 | [SerializeField] field 'lastStatus' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 42 | Warning | NAM01 | Private field 'lastStatus' should be named '_lastStatus' (leading underscore + camelCase). |
| 44 | Suggestion | SER01 | [SerializeField] field 'lastOperation' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 45 | Warning | NAM01 | Private field 'lastOperation' should be named '_lastOperation' (leading underscore + camelCase). |
| 47 | Suggestion | SER01 | [SerializeField] field 'lastOperationDurationMs' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 48 | Warning | NAM01 | Private field 'lastOperationDurationMs' should be named '_lastOperationDurationMs' (leading underscore + camelCase). |
| 118 | Info | DOC01 | Public method 'LoadHistoryAsync' has no /// <summary> doc comment. |
| 183 | Info | DOC01 | Public method 'SaveTurnAsync' has no /// <summary> doc comment. |
| 243 | Info | DOC01 | Public method 'DeleteHistoryAsync' has no /// <summary> doc comment. |

### `Assets/Scripts/Runtime/NPCDialogue/SupabaseRealtimeService.cs`

| Line | Severity | Rule | Message |
|---|---|---|---|
| 28 | Suggestion | SER01 | [SerializeField] field 'referencesGroup' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 30 | Warning | NAM01 | Private field 'referencesGroup' should be named '_referencesGroup' (leading underscore + camelCase). |
| 32 | Suggestion | SER01 | [SerializeField] field '_authService' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 35 | Suggestion | SER01 | [SerializeField] field 'behaviourGroup' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 43 | Warning | NAM01 | Private field 'behaviourGroup' should be named '_behaviourGroup' (leading underscore + camelCase). |
| 45 | Suggestion | SER01 | [SerializeField] field '_enablePollingFallback' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 51 | Suggestion | SER01 | [SerializeField] field '_pollingIntervalSeconds' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 56 | Suggestion | SER01 | [SerializeField] field '_autoSubscribeRooms' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 62 | Suggestion | SER01 | [SerializeField] field 'debugGroup' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 64 | Warning | NAM01 | Private field 'debugGroup' should be named '_debugGroup' (leading underscore + camelCase). |
| 66 | Suggestion | SER01 | [SerializeField] field '_lastConnectionState' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 69 | Suggestion | SER01 | [SerializeField] field '_lastError' has no [FormerlySerializedAs] — confirm this field was never publicly serialized under an old PascalCase name. |
| 204 | Warning | FMT05 | Opening brace should be on its own line (Allman style). |
| 510 | Warning | FMT05 | Opening brace should be on its own line (Allman style). |
| 511 | Warning | FMT05 | Opening brace should be on its own line (Allman style). |
| 635 | Warning | FMT05 | Opening brace should be on its own line (Allman style). |

