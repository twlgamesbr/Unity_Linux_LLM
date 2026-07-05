# NPC Codebase Specialist — Fine-Tuning Pipeline

## Overview

Fine-tune a Qwen3-8B model on ~3K–5K codebase Q&A pairs generated from the project's Qdrant collection. The fine-tuned model replaces `llama-3.2-3b-instruct:q8_0` in the Unity NPC dialogue system, answering players with deep codebase knowledge.

## Pipeline

```
Qdrant (5,107 codebase points)
  │
  ▼  generate_dataset.py (concurrent, 4 workers, ~LocalAI calls)
ShareGPT JSONL (~3,000–5,000 pairs)
  │
  ▼  modal run modal_finetune_npc.py (A100, ~2-4 hours)
LoRA weights on Modal Volume
  │
  ▼  Merge + deploy via modal_serve_npc.py
Modal vLLM endpoint (H100)
  │
  ▼  LocalAI cloud-proxy YAML
npc-codebase-v1 model on :8080
  │
  ▼  Set NPCDialogueManager.remoteModel = "npc-codebase-v1"
Unity NPCs answer with codebase expertise
```

## Prerequisites

- LocalAI running on `localhost:8080`
- Qdrant running on `localhost:6333` with `unity_linux_llm_codebase_v1` collection
- Modal CLI installed and authenticated

## Step 1: Generate Dataset

```bash
cd Tools/FineTuning

# Default: 4 workers, 2 QA pairs per record, max 500 records per type
python3 generate_dataset.py

# Control sample size for testing:
MAX_RECORDS_PER_TYPE=10 QA_PER_RECORD=1 MAX_WORKERS=2 python3 generate_dataset.py
```

Output: `datasets/npc-codebase-qa.jsonl` (~3K–8K ShareGPT-format pairs)

## Step 2: Upload Dataset to Modal Volume

```bash
# Copy the dataset to Modal's dataset cache volume
modal volume put unsloth-dataset-cache datasets/npc-codebase-qa.jsonl /dataset_cache/npc-codebase-qa.jsonl
```

## Step 3: Run Fine-Tuning on Modal

```bash
cd Tools/FineTuning
modal run modal_finetune_npc.py --max-steps 1500
```

Key hyperparameters:
| Flag | Default | Notes |
|---|---|---|
| `--model-name` | unsloth/Qwen3-8B | Change if using different base |
| `--lora-r` | 32 | Higher = more capacity, more VRAM |
| `--batch-size` | 8 | Effective batch = batch × grad accum |
| `--max-steps` | 1500 | Enough for ~4K examples |
| `--learning-rate` | 2e-4 | Standard for LoRA fine-tune |

Checkpoints save to `unsloth-checkpoints` Volume every 200 steps.

## Step 4: Deploy Fine-Tuned Model

After training completes, deploy the vLLM inference server:

```bash
cd Tools/FineTuning
modal deploy modal_serve_npc.py
```

This deploys a Modal app called `npc-codebase-v1` with the merged model weights served via vLLM.

Get the endpoint URL:
```bash
modal app logs npc-codebase-v1
# Look for the URL like: https://workspace--npc-codebase-v1.us-east.modal.direct
```

## Step 5: Configure LocalAI Proxy

Copy `npc-codebase-v1.yaml` to the LocalAI container:

```bash
docker cp npc-codebase-v1.yaml pc-resource-localai:/models/npc-codebase-v1.yaml
docker restart pc-resource-localai
```

Verify it appears:
```bash
curl http://localhost:8080/v1/models | jq '.data[].id'
# Should list npc-codebase-v1
```

## Step 6: Update Unity Scene

1. Open `Assets/Scenes/NPCDialoguePrototype1.unity`
2. Select `NPCDialogueSystem` GameObject
3. In `NPCDialogueManager`, set `remoteModel` to `"npc-codebase-v1"`
4. Click "Fetch Models from LocalAI" to refresh the dropdown

## Interpreting Results

After deployment, the NPC dialogue system sends the same OpenAI-format chat to LocalAI. The model receives:
- System prompt from `NPCProfilePromptComposer`
- RAG context from Qdrant (still enabled)
- User question

The fine-tuned model (Qwen3-8B vs original 3B) has 3× the capacity and was trained specifically on this codebase, producing more accurate and specific answers.

## Troubleshooting

- **Dataset generation is slow**: Increase `MAX_WORKERS` or use `MAX_RECORDS_PER_TYPE` to limit samples
- **OOM during training**: Reduce `batch-size` to 4 or set `load-in-4bit` to True
- **LoRA merge fails**: The merge happens automatically after training; check logs with `modal app logs npc-codebase-finetune`
- **Model not appearing in LocalAI**: Ensure the YAML file has correct `upstream_url` pointing to the deployed Modal endpoint
