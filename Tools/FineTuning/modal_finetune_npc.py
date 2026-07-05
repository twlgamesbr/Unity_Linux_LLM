import json
import pathlib
from dataclasses import dataclass
from datetime import datetime
from typing import Optional

import modal

app = modal.App("npc-codebase-finetune")

train_image = (
    modal.Image.debian_slim(python_version="3.11")
    .uv_pip_install(
        "accelerate==1.9.0",
        "datasets==3.6.0",
        "hf-transfer==0.1.9",
        "huggingface_hub==0.34.2",
        "peft==0.16.0",
        "transformers==4.54.0",
        "trl==0.19.1",
        "unsloth[cu128-torch270]==2025.7.8",
        "unsloth_zoo==2025.7.10",
    )
    .env({"HF_HOME": "/model_cache"})
)

with train_image.imports():
    import unsloth
    import datasets
    import torch
    from transformers import TrainingArguments
    from trl import SFTTrainer
    from unsloth import FastLanguageModel
    from unsloth.chat_templates import standardize_sharegpt

model_cache_volume = modal.Volume.from_name("unsloth-model-cache", create_if_missing=True)
dataset_cache_volume = modal.Volume.from_name("unsloth-dataset-cache", create_if_missing=True)
checkpoint_volume = modal.Volume.from_name("unsloth-checkpoints", create_if_missing=True)

GPU_TYPE = "A100-40GB"
TIMEOUT_HOURS = 6
MAX_RETRIES = 3

LORA_TARGET_MODULES = [
    "q_proj", "k_proj", "v_proj", "o_proj",
    "gate_proj", "up_proj", "down_proj",
]


@dataclass
class TrainingConfig:
    model_name: str
    dataset_path: str
    max_seq_length: int
    load_in_4bit: bool
    load_in_8bit: bool

    lora_r: int
    lora_alpha: int
    lora_dropout: float
    lora_bias: str
    use_rslora: bool

    optim: str
    batch_size: int
    gradient_accumulation_steps: int
    packing: bool
    use_gradient_checkpointing: str
    learning_rate: float
    lr_scheduler_type: str
    warmup_ratio: float
    weight_decay: float
    max_steps: int
    save_steps: int
    eval_steps: int
    logging_steps: int

    seed: int
    experiment_name: Optional[str] = None

    def __post_init__(self):
        if self.experiment_name is None:
            ts = datetime.now().strftime("%Y%m%d-%H%M%S")
            model_short = self.model_name.split("/")[-1]
            self.experiment_name = f"{model_short}-r{self.lora_r}-{ts}"


def load_custom_dataset(jsonl_path: pathlib.Path, tokenizer, max_samples: int = 0):
    print(f"Loading dataset from {jsonl_path}")
    rows = []
    with open(jsonl_path) as f:
        for line in f:
            line = line.strip()
            if line:
                rows.append(json.loads(line))
    if max_samples > 0:
        rows = rows[:max_samples]
    print(f"Loaded {len(rows)} examples")

    ds = datasets.Dataset.from_list(rows)
    ds = standardize_sharegpt(ds)
    ds = ds.train_test_split(test_size=0.05, seed=42)
    train_ds = ds["train"]
    eval_ds = ds["test"]

    def format_fn(examples):
        texts = []
        for conv in examples["conversations"]:
            text = tokenizer.apply_chat_template(
                conv, tokenize=False, add_generation_prompt=False
            )
            texts.append(text)
        return {"text": texts}

    train_ds = train_ds.map(format_fn, batched=True, num_proc=2, remove_columns=train_ds.column_names)
    eval_ds = eval_ds.map(format_fn, batched=True, num_proc=2, remove_columns=eval_ds.column_names)

    return train_ds, eval_ds


@app.function(
    image=train_image,
    gpu=GPU_TYPE,
    volumes={
        "/model_cache": model_cache_volume,
        "/dataset_cache": dataset_cache_volume,
        "/checkpoints": checkpoint_volume,
    },
    timeout=TIMEOUT_HOURS * 60 * 60,
    retries=modal.Retries(initial_delay=0.0, max_retries=MAX_RETRIES),
    single_use_containers=True,
)
def finetune(config: TrainingConfig):
    exp_dir = pathlib.Path("/checkpoints") / "experiments" / config.experiment_name

    print(f"Loading model: {config.model_name}")
    model, tokenizer = FastLanguageModel.from_pretrained(
        model_name=config.model_name,
        max_seq_length=config.max_seq_length,
        dtype=None,
        load_in_4bit=config.load_in_4bit,
        load_in_8bit=config.load_in_8bit,
    )

    dataset_path = pathlib.Path(config.dataset_path)
    if not dataset_path.exists():
        dataset_path = pathlib.Path("/dataset_cache") / config.dataset_path
    print(f"Loading dataset from {dataset_path}")
    train_dataset, eval_dataset = load_custom_dataset(dataset_path, tokenizer)

    print("Configuring LoRA...")
    model = FastLanguageModel.get_peft_model(
        model,
        r=config.lora_r,
        target_modules=LORA_TARGET_MODULES,
        lora_alpha=config.lora_alpha,
        lora_dropout=config.lora_dropout,
        bias=config.lora_bias,
        use_gradient_checkpointing=config.use_gradient_checkpointing,
        random_state=config.seed,
        use_rslora=config.use_rslora,
        loftq_config=None,
    )

    exp_dir.mkdir(parents=True, exist_ok=True)
    output_dir = str(exp_dir)

    training_args = TrainingArguments(
        per_device_train_batch_size=config.batch_size,
        gradient_accumulation_steps=config.gradient_accumulation_steps,
        learning_rate=config.learning_rate,
        max_steps=config.max_steps,
        warmup_ratio=config.warmup_ratio,
        eval_strategy="steps",
        eval_steps=config.eval_steps,
        save_strategy="steps",
        save_steps=config.save_steps,
        logging_steps=config.logging_steps,
        fp16=not torch.cuda.is_bf16_supported(),
        bf16=torch.cuda.is_bf16_supported(),
        optim=config.optim,
        weight_decay=config.weight_decay,
        lr_scheduler_type=config.lr_scheduler_type,
        output_dir=output_dir,
        report_to=None,
        seed=config.seed,
    )

    trainer = SFTTrainer(
        model=model,
        tokenizer=tokenizer,
        train_dataset=train_dataset,
        eval_dataset=eval_dataset,
        dataset_text_field="text",
        max_seq_length=config.max_seq_length,
        dataset_num_proc=2,
        packing=config.packing,
        args=training_args,
    )

    print(f"Train size: {len(train_dataset):,}")
    print(f"Eval size:  {len(eval_dataset):,}")
    print(f"Total params: {sum(p.numel() for p in model.parameters()):,}")
    print(f"Trainable: {sum(p.numel() for p in model.parameters() if p.requires_grad):,}")

    trainer.train()

    final_path = exp_dir / "final_model"
    model.save_pretrained(final_path)
    tokenizer.save_pretrained(final_path)
    print(f"Model saved to {final_path}")

    merge_path = exp_dir / "merged_model"
    print(f"Merging LoRA weights to {merge_path}...")
    merged = model.merge_and_unload()
    merged.save_pretrained(merge_path)
    tokenizer.save_pretrained(merge_path)
    print(f"Merged model saved to {merge_path}")

    return str(merge_path)


@app.local_entrypoint()
def main(
    model_name: str = "unsloth/Qwen3-8B",
    dataset_path: str = "/dataset_cache/npc-codebase-qa.jsonl",
    max_seq_length: int = 8192,
    load_in_4bit: bool = True,
    load_in_8bit: bool = False,
    lora_r: int = 32,
    lora_alpha: int = 32,
    lora_dropout: float = 0.0,
    lora_bias: str = "none",
    use_rslora: bool = False,
    optim: str = "adamw_8bit",
    batch_size: int = 8,
    gradient_accumulation_steps: int = 1,
    packing: bool = False,
    use_gradient_checkpointing: str = "unsloth",
    learning_rate: float = 2e-4,
    lr_scheduler_type: str = "cosine",
    warmup_ratio: float = 0.06,
    weight_decay: float = 0.01,
    max_steps: int = 1500,
    save_steps: int = 200,
    eval_steps: int = 200,
    logging_steps: int = 10,
    seed: int = 42,
    experiment_name: Optional[str] = None,
):
    config = TrainingConfig(
        model_name=model_name,
        dataset_path=dataset_path,
        max_seq_length=max_seq_length,
        load_in_4bit=load_in_4bit,
        load_in_8bit=load_in_8bit,
        lora_r=lora_r,
        lora_alpha=lora_alpha,
        lora_dropout=lora_dropout,
        lora_bias=lora_bias,
        use_rslora=use_rslora,
        optim=optim,
        batch_size=batch_size,
        gradient_accumulation_steps=gradient_accumulation_steps,
        packing=packing,
        use_gradient_checkpointing=use_gradient_checkpointing,
        learning_rate=learning_rate,
        lr_scheduler_type=lr_scheduler_type,
        warmup_ratio=warmup_ratio,
        weight_decay=weight_decay,
        max_steps=max_steps,
        save_steps=save_steps,
        eval_steps=eval_steps,
        logging_steps=logging_steps,
        seed=seed,
        experiment_name=experiment_name,
    )

    print(f"Experiment: {config.experiment_name}")
    print(f"Model: {config.model_name}")
    print(f"LoRA: r={config.lora_r}, alpha={config.lora_alpha}")
    print(f"Effective batch size: {config.batch_size * config.gradient_accumulation_steps}")
    print(f"Max steps: {config.max_steps}")

    result_path = finetune.remote(config)
    print(f"Training complete. Merged model at: {result_path}")
