# Mystery Scene Template System - Quick Start Guide

## Overview

This system lets you create complete LLM-powered mystery-solving scenes in Unity by filling out a JSON template. The template generator (`MysterySceneTemplateGenerator.cs`) handles:
- Copying the prototype scene
- Creating NPC profiles (ScriptableObjects)
- Writing knowledge files for RAG
- Configuring solve panel answers
- Setting up the scene automatically

## Quick Start

1. **Open Unity Editor**
2. **Tools → Mystery Scenes → Generate Example Template** - Creates a template from the base
3. **Edit the template JSON** with your mystery details
4. **Tools → Mystery Scenes → Generate From Template JSON...** - Select your edited template
5. **Open the generated scene** and test!

## Template Structure

### Required Fields

| Field | Description |
|-------|-------------|
| `caseSlug` | Unique identifier (lowercase, hyphens only) |
| `displayName` | Player-facing mystery title |
| `correctAnswers.culprit` | Who did it |
| `correctAnswers.location` | Where it happened |
| `correctAnswers.evidence` | Key evidence |
| `choices.culprits[]` | 5-6 suspect names for dropdown |
| `choices.locations[]` | 4-5 location names for dropdown |
| `choices.evidence[]` | 4-5 evidence names for dropdown |
| `npcs[]` | Array of 3+ NPC definitions |

### NPC Definition Fields

| Field | Description | Default |
|-------|-------------|---------|
| `slug` | Unique NPC ID (lowercase, hyphens) | Required |
| `displayName` | Name shown to player | Required |
| `archetype` | witness/suspect/authority/informant | Optional |
| `portraitAssetPath` | Path to portrait texture | Uses existing images |
| `systemPrompt` | LLM personality instructions | Required |
| `temperature` | Creativity (0.7-0.85) | 0.75 |
| `maxTokens` | Response length | 180 |
| `knowledgeMarkdown` | Q&A pairs for RAG | Required |

### Writing Knowledge (RAG)

The `knowledgeMarkdown` field uses **Q&A pairs** that the LLM retrieves during conversation:

```markdown
## What They Know

- Q: Where were you during the theft?
  A: I was in the kitchen preparing dessert.
- Q: Did you see anyone suspicious?
  A: I saw Professor Plum near the study.
```

**Tips:**
- Write 15-30 Q&A pairs per NPC
- Include their alibi, observations, secrets, and personality
- Use natural language questions players might ask
- Keep answers in character
- Add `{{any}}` fallback at the end

### Progression System (Optional)

The `progression` field helps design clue flow:

```json
"progression": {
  "startingNpc": "butler",
  "cluesToUnlock": [
    {
      "clue": "The safe combination is in the study",
      "revealedBy": "butler",
      "triggerQuestion": "What's in the study?",
      "unlocks": ["maid"]
    }
  ],
  "redHerrings": [
    {
      "description": "The gardener had muddy boots",
      "source": "maid",
      "truth": "He was in the greenhouse all night"
    }
  ]
}
```

## Creating a New Mystery - Step by Step

### 1. Plan Your Mystery
- **Core puzzle**: Who, Where, What (evidence)
- **6 suspects** with motives/opportunities
- **5 rooms/locations** with hiding spots
- **3-5 NPCs** with unique perspectives
- **Timeline** of events

### 2. Copy the Base Template
```bash
cp Assets/MysteryTemplates/mystery-scene-template-base.json \
   Assets/MysteryTemplates/my-mystery.json
```

### 3. Fill in the Template
Replace all placeholder values. Key sections:

**Case Metadata** - Sets tone and context
**Correct Answers** - The solution
**Choices** - Dropdown options for player
**NPCs** - Each needs unique voice and knowledge

### 4. Generate the Scene
Unity Menu: **Tools → Mystery Scenes → Generate From Template JSON...**

### 5. Test and Iterate
- Play the generated scene
- Talk to NPCs, verify knowledge works
- Check solve panel answers
- Adjust template and regenerate

## NPC Archetype Guide

| Archetype | Personality | Temperature | Example |
|-----------|-------------|-------------|---------|
| **Authority** | Composed, precise, protective | 0.70-0.75 | Butler, Guard, Curator |
| **Witness** | Observant, nervous, eager | 0.75-0.80 | Maid, Assistant, Neighbor |
| **Suspect** | Defensive, evasive, charming | 0.78-0.85 | Guest, Rival, Heir |
| **Informant** | Cryptic, knowledgeable, wary | 0.72-0.78 | Bartender, Informant, Ghost |

## Portrait Assets

Use existing portraits or add your own:
- `Assets/LLMUnity/Samples/KnowledgeBaseGame/Images/butler.png`
- `Assets/LLMUnity/Samples/KnowledgeBaseGame/Images/maid.png`
- `Assets/LLMUnity/Samples/KnowledgeBaseGame/Images/chef.png`

For custom portraits, place in `Assets/StreamingAssets/NPCs/[caseSlug]/[npcSlug]/portrait.png`

## LoRA Adapters (Optional)

For distinct NPC voices, train LoRA adapters:
1. Place at `Assets/StreamingAssets/NPCs/[caseSlug]/[npcSlug]/adapter.gguf`
2. Set `loraAdapterPath` in template
3. Adjust `loraWeight` (0.7-0.9)

## RAG Embeddings

After generating, you'll need to build embeddings:
1. Run the RAG ingestion script on knowledge files
2. Output to `Assets/StreamingAssets/RAG/[caseSlug]/`
3. Update `ragEmbeddingPath` in template

## Common Patterns

### Classic Whodunit (Welltodo Style)
- 6 guests, 1 victim, 5 rooms, 4 events
- 3 NPCs (butler, maid, chef) with overlapping knowledge
- Each NPC witnessed different events

### Supernatural Mystery
- Add "ghost" NPC with cryptic knowledge
- Higher temperature (0.85+) for otherworldly feel
- Include "impossible" clues

### Noir Detective
- Cynical narrator NPC
- Low temperature (0.65-0.70) for gritty responses
- Focus on motives and alibis

## Validation Checklist

Before generating:
- [ ] `caseSlug` is unique and slug-format
- [ ] All 3 correct answers match entries in choices arrays
- [ ] At least 3 NPCs defined
- [ ] Each NPC has unique slug and knowledgeMarkdown
- [ ] Portrait paths exist
- [ ] Output scene path doesn't exist yet

## Troubleshooting

| Issue | Fix |
|-------|-----|
| "Template not found" | Check file path, use forward slashes |
| "Prototype scene not found" | Verify `Assets/Scenes/NPCDialoguePrototype1.unity` exists |
| "Output scene already exists" | Delete or rename existing scene |
| NPCs not loading | Check NPCProfile ScriptableObjects created in `Assets/Data/MysteryScenes/` |
| Knowledge not working | Verify markdown format, check RAG embeddings built |

## Menu Commands

- **Generate Example Template** - Creates base template in `Assets/MysteryTemplates/`
- **Generate From Template JSON...** - File picker for any template JSON

## File Structure After Generation

```
Assets/
├── Scenes/GeneratedMysteries/
│   └── your-mystery-slug.unity          # Generated scene
├── Data/MysteryScenes/your-mystery-slug/
│   └── NPCProfiles/
│       ├── npc-slug-1.asset             # NPC profiles
│       ├── npc-slug-2.asset
│       └── npc-slug-3.asset
└── StreamingAssets/NPCs/your-mystery-slug/
    ├── npc-slug-1/knowledge.md          # Knowledge files
    ├── npc-slug-2/knowledge.md
    └── npc-slug-3/knowledge.md
```

## Advanced: Extending the Generator

Modify `MysterySceneTemplateGenerator.cs` to:
- Add custom scene setup (lighting, props)
- Generate timeline UI from `events` field
- Create room map from `rooms` field
- Auto-generate hint system from `progression`

## Example Templates Included

- `welltodo-style-mystery-template.json` - Classic mansion mystery
- `museum-clockwork-case` - Museum theft (in template)
- `mystery-scene-template-base.json` - This comprehensive base template

---

**Next Steps:** Edit `mystery-scene-template-base.json` with your mystery details, then generate!
