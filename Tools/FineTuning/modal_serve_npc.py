import subprocess

import modal

vllm_image = (
    modal.Image.from_registry("nvidia/cuda:12.4.0-devel-ubuntu22.04", add_python="3.11")
    .entrypoint([])
    .uv_pip_install("vllm==0.11.2")
    .env({"HF_XET_HIGH_PERFORMANCE": "1"})
)

hf_cache_vol = modal.Volume.from_name("huggingface-cache", create_if_missing=True)
vllm_cache_vol = modal.Volume.from_name("vllm-cache", create_if_missing=True)
checkpoint_vol = modal.Volume.from_name("unsloth-checkpoints", create_if_missing=True)

MODEL_PATH = "/checkpoints/experiments"
MODEL_NAME = "npc-codebase-v1"
N_GPU = 1
MINUTES = 60
VLLM_PORT = 8000

app = modal.App("npc-codebase-v1")


@app.function(
    image=vllm_image,
    gpu=f"H100:{N_GPU}",
    scaledown_window=30 * MINUTES,
    timeout=15 * MINUTES,
    volumes={
        "/root/.cache/huggingface": hf_cache_vol,
        "/root/.cache/vllm": vllm_cache_vol,
        "/checkpoints": checkpoint_vol,
    },
    max_containers=1,
)
@modal.web_server(port=VLLM_PORT, startup_timeout=15 * MINUTES)
def serve():
    cmd = [
        "vllm",
        "serve",
        MODEL_PATH,
        "--served-model-name", MODEL_NAME,
        "--host", "0.0.0.0",
        "--port", str(VLLM_PORT),
        "--uvicorn-log-level=info",
        "--tensor-parallel-size", str(N_GPU),
        "--max-model-len", "16384",
    ]
    subprocess.Popen(" ".join(cmd), shell=True)
