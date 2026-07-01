# Mystery Scene Templates

This folder contains JSON templates for generating LLM dialogue mystery scenes from `Assets/Scenes/NPCDialoguePrototype.unity`. The `MysterySceneTemplateGenerator.cs` Editor tool deserializes these JSON files and generates Unity scenes with configured `NPCProfile` assets, knowledge markdown files, and solve-panel dropdowns.

---

## Quick Start

### Generate a scene from an existing template

1. In Unity, run **Tools > Mystery Scenes > Generate From Template JSON...**
2. Select any `.json` file from this folder (or your own)
3. Open the generated scene under `Assets/Scenes/GeneratedMysteries/`
4. Press Play — the `NPCDialogueManager` loads the case-specific RAG index automatically

### Generate many variations automatically

Use the batch generator script at the project root:

```bash
# Generate 10 random variations
python generate_mystery_variations.py

# Generate 25 variations with deterministic seed
python generate_mystery_variations.py --count 25 --seed 42

# Filter by theme
python generate_mystery_variations.py --themes noir,victorian --count 5

# Preview without writing files
python generate_mystery_variations.py --count 3 --dry-run

# List available themes and skeletons
python generate_mystery_variations.py --list-themes
python generate_mystery_variations.py --list-skeletons
```

Generated variations are saved under `Assets/MysteryTemplates/variations/<theme>/`.

---

## Template Reference

### Root fields

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `caseSlug` | `string` | Yes | Unique identifier for generated assets, RAG categories, and history paths. Lowercase alphanumeric with hyphens. |
| `displayName` | `string` | Yes | Human-readable title shown in the scene and logs. |
| `prototypeScenePath` | `string` | No | Source `.unity` scene to copy. Defaults to `Assets/Scenes/NPCDialoguePrototype.unity`. |
| `outputScenePath` | `string` | No | Destination path for the generated scene. Generator refuses to overwrite existing scenes. Defaults to `Assets/Scenes/GeneratedMysteries/<caseSlug>.unity`. |
| `ragEmbeddingPath` | `string` | No | StreamingAssets path to the RAG embedding index. Defaults to `RAG/<caseSlug>/NPCDialogues-minilm-chunked.rag`. |
| `correctAnswers` | `object` | Yes | The three correct solve answers (see below). |
| `choices` | `object` | Yes | Dropdown options for the solve panel (see below). |
| `npcs` | `array` | Yes | Array of 3-6 NPC templates (see below). |

### `correctAnswers` (MysterySolveAnswers)

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `culprit` | `string` | Yes | Name of the guilty party — must match one of `choices.culprits`. |
| `location` | `string` | Yes | Crime scene — must match one of `choices.locations`. |
| `evidence` | `string` | Yes | Key evidence item — must match one of `choices.evidence`. |

### `choices` (MysterySolveChoices)

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `culprits` | `string[]` | Yes | 4-6 suspect names. Must include `correctAnswers.culprit`. |
| `locations` | `string[]` | Yes | 4-6 locations. Must include `correctAnswers.location`. |
| `evidence` | `string[]` | Yes | 4-6 evidence items. Must include `correctAnswers.evidence`. |

### NPC template (`MysteryNpcTemplate`)

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `slug` | `string` | — | Unique ID for this NPC within the case. Used for profile asset names and RAG categories. |
| `displayName` | `string` | `slug` | Name shown in dialogue UI. |
| `portraitAssetPath` | `string` | `null` | Asset path to a `Texture2D` portrait image. |
| `systemPrompt` | `string` | — | System prompt that defines the NPC's personality, voice, and rules for answering. |
| `temperature` | `float` | `0.75` | LLM sampling temperature. Higher = more creative. |
| `topP` | `float` | `0.9` | Nucleus sampling threshold. |
| `minP` | `float` | `0.05` | Minimum probability threshold. |
| `topK` | `int` | `40` | Top-K sampling count. |
| `repeatPenalty` | `float` | `1.1` | Repetition penalty. |
| `maxTokens` | `int` | `180` | Maximum tokens per response. |
| `ragResults` | `int` | `3` | Number of RAG chunks retrieved per query. |
| `loraAdapterPath` | `string` | `""` | Path to a LoRA adapter `.gguf` file (relative to StreamingAssets). |
| `loraWeight` | `float` | `0.8` | LoRA adapter weight. |
| `knowledgeMarkdown` | `string` | — | Markdown text used for RAG knowledge base. 8-24 line text area in the Editor. |

---

## Available Templates

| File | NPCs | Theme | Description |
|------|------|-------|-------------|
| `welltodo-style-mystery-template.json` | 3 | Museum Heist | A stolen astrolabe at a museum gala. Three suspects: Curator, Guard, Assistant. |
| `mansion-dinner-party.json` | 5 | Victorian Manor | Lord Blackwood poisoned at his dinner party. Five interconnected NPCs with conflicting alibis, hidden motives, and a witness who saw everything. |

Generated variations appear under `Assets/MysteryTemplates/variations/<theme>/`.

---

## Authoring a Template

1. **Choose a case slug** — lowercase alphanumeric with hyphens, e.g. `ocean-liner-mystery`
2. **Define the correct answers** — the solution the player must deduce
3. **Create the choices** — dropdown options that include the correct answers plus decoys
4. **Design 3-6 NPCs** each with:
   - A distinct **voice and personality** in their `systemPrompt`
   - **Partial knowledge** — no single NPC knows everything
   - **Overlapping and conflicting facts** — create a web of clues
   - **Character-appropriate secrets** — some NPCs should lie or omit

### Knowledge markdown best practices

The `knowledgeMarkdown` field is ingested into the RAG index. Structure it as:

```markdown
# Character knowledge

## Background

Character history and context. Retrieved rarely — keep it short.

## The evening in question

- Q: What did you see?
  A: A clear, specific answer with temporal and spatial details.

- Q: Where were you at 9 PM?
  A: An alibi that another NPC may confirm or contradict.

## Observations about others

- Q: What do you think of Mr. X?
  A: Opinions and suspicions that create red herrings.
```

**Tips:**

- Use the `- Q:` / `  A:` format for RAG retrieval compatibility
- Embed **temporal anchors** (times, sequences) to help the LLM track chronology
- Include **contradictions** — NPC A says one thing, NPC B says another
- Plant **red herrings** — plausible-sounding clues that point to the wrong suspect
- Give the **culprit** a fabricated alibi and deflections
- Ensure each NPC's knowledge is **consistent with their role** — a gardener shouldn't know details only the chef would know

---

## Batch Generator

`generate_mystery_variations.py` (project root) creates many template variations from theme pools and character role templates.

### How it works

1. **Themes** define setting pools: victim names, locations, atmosphere
2. **Plot skeletons** define role assignments and which role is the culprit
3. **Character roles** are reusable templates with `{{PLACEHOLDER}}` substitutions
4. The generator picks a theme, a skeleton, assigns random names, and substitutes placeholders

### Available themes

| Theme | Setting | Victim Style |
|-------|---------|--------------|
| `victorian` | Victorian Manor | Aristocracy |
| `noir` | Noir City | Gangsters and femmes fatales |
| `sci_fi` | Starship Colony | Future explorers |
| `medieval` | Medieval Castle | Royalty and knights |
| `cozy_village` | Cozy English Village | Townsfolk |
| `theatre` | West End Theatre | Performers and directors |

### Available plot skeletons

| Skeleton | Culprit Role | Description |
|----------|--------------|-------------|
| `betrayal` | Guilty Culprit | Business partner or friend commits murder to prevent exposure |
| `inheritance` | Suspicious Heir | The heir commits murder for the inheritance |
| `vendetta` | Mysterious Stranger | Someone from the victim's past seeks revenge |

### Adding new themes

Edit `generate_mystery_variations.py` and add a new entry to the `THEMES` dict:

```python
"my_theme": {
    "name": "My Theme Name",
    "setting_victim": ["Victim Name 1", "Victim Name 2", ...],
    "setting_location": ["Location 1", "Location 2", ...],
    "setting_desc": "a brief atmospheric description",
    "atmosphere": ["detail 1", "detail 2", ...],
},
```

---

## File Organization

```
Assets/MysteryTemplates/
    README.md                       <- this file
    welltodo-style-mystery-template.json   <- original museum heist template
    mansion-dinner-party.json              <- rich 5-NPC base template
    variations/                            <- batch-generated variations
        victorian-manor/
            victorian-manor-betrayal-1234.json
            victorian-manor-inheritance-5678.json
            ...
        noir-city/
            noir-city-vendetta-9012.json
            ...
        ...
```

---

## Design Principles

- **Interconnected knowledge** — each NPC knows pieces that, assembled, reveal the truth
- **No omniscient NPCs** — every character has blind spots and biases
- **Partial truths** — NPCs may lie, omit, or mislead based on their motivations
- **Solve fairness** — the correct answer is always deducible from the combined knowledge
- **Role-appropriate knowledge** — a chef knows about the kitchen, a guard knows about security logs
