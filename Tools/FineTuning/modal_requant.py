import pathlib
import modal

EXPERIMENT = "Llama-3.2-3B-Instruct-r32-20260706-025345"
GGUF_DIR = f"/gguf/{EXPERIMENT}"

app = modal.App("npc-llama32-requant")

image = (
    modal.Image.debian_slim(python_version="3.11")
    .apt_install("git")
    .uv_pip_install(
        "unsloth[cu128-torch270]==2025.7.8",
        "unsloth_zoo==2025.7.10",
        "transformers==4.54.0",
        "torch==2.7.0",
        "accelerate==1.9.0",
        "trl==0.19.1",
        "peft==0.16.0",
        "sentencepiece==0.2.0",
        "protobuf==5.29.4",
        "hf-transfer==0.1.9",
        "huggingface_hub==0.34.2",
    )
    .env({"HF_HOME": "/model_cache"})
)

gguf_volume = modal.Volume.from_name("unsloth-gguf", create_if_missing=True)
model_cache_volume = modal.Volume.from_name("unsloth-model-cache", create_if_missing=True)


@app.function(
    image=image,
    gpu="A100-40GB",
    volumes={
        "/gguf": gguf_volume,
        "/model_cache": model_cache_volume,
    },
    timeout=30 * 60,
)
def requantize(quant_method: str = "q8_0"):
    import torch
    from unsloth import FastLanguageModel

    print(f"CUDA available: {torch.cuda.is_available()}")
    print(f"GPU: {torch.cuda.get_device_name(0) if torch.cuda.is_available() else 'none'}")

    gguf_dir = pathlib.Path(GGUF_DIR)
    output_path = gguf_dir / f"unsloth.{quant_method.upper()}.gguf"

    if output_path.exists():
        size_mb = output_path.stat().st_size / (1024**2)
        print(f"{output_path.name} already exists ({size_mb:.0f} MB)")
        return

    # Verify safetensors exist
    safetensors = list(gguf_dir.glob("model-*.safetensors"))
    if not safetensors:
        print(f"No safetensors found in {gguf_dir}")
        return
    print(f"Found {len(safetensors)} safetensors shard(s)")

    print(f"Loading merged model from safetensors: {gguf_dir}")
    model, tokenizer = FastLanguageModel.from_pretrained(
        model_name=str(gguf_dir),
        max_seq_length=8192,
        dtype=None,
        load_in_4bit=False,
    )

    # Remove any truncated GGUFs
    for f in gguf_dir.glob("unsloth.*.gguf"):
        size_mb = f.stat().st_size / (1024**2)
        if size_mb < 500:
            f.unlink()
            print(f"Removed truncated {f.name} ({size_mb:.0f} MB)")

    print(f"Exporting to {quant_method}...")
    model.save_pretrained_gguf(
        str(gguf_dir),
        tokenizer,
        quantization_method=quant_method,
    )

    gguf_volume.commit()
    print(f"Done. Files in {gguf_dir}:")
    for f in sorted(gguf_dir.glob("*.gguf")):
        size_mb = f.stat().st_size / (1024**2)
        print(f"  {f.name}: {size_mb:.0f} MB")


@app.local_entrypoint()
def main(quant_method: str = "q8_0"):
    print(f"Re-quantizing {EXPERIMENT} safetensors -> {quant_method}")
    requantize.remote(quant_method)
