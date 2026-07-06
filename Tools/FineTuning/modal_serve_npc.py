import pathlib

import modal

EXPERIMENTS_ROOT = pathlib.Path("/checkpoints/experiments")
MODEL_NAME = "npc-codebase-v1"
N_GPU = 1

image = (
    modal.Image.from_registry("nvidia/cuda:12.4.0-devel-ubuntu22.04", add_python="3.11")
    .entrypoint([])
    .uv_pip_install("vllm==0.11.2")
    .env({"HF_XET_HIGH_PERFORMANCE": "1"})
)

volumes = {
    "/root/.cache/huggingface": modal.Volume.from_name("huggingface-cache", create_if_missing=True),
    "/root/.cache/vllm": modal.Volume.from_name("vllm-cache", create_if_missing=True),
    "/checkpoints": modal.Volume.from_name("unsloth-checkpoints", create_if_missing=True),
}

app = modal.App("npc-codebase-v1")


@app.function(
    image=image,
    gpu=f"A100:{N_GPU}",
    scaledown_window=30 * 60,
    timeout=20 * 60,
    volumes=volumes,
    max_containers=1,
)
@modal.asgi_app()
def serve():
    from contextlib import asynccontextmanager
    from vllm.entrypoints.openai.api_server import (
        FlexibleArgumentParser,
        build_app,
        build_async_engine_client,
        init_app_state,
        make_arg_parser,
    )

    if not EXPERIMENTS_ROOT.exists():
        raise FileNotFoundError(f"{EXPERIMENTS_ROOT} not found")

    candidates = sorted(EXPERIMENTS_ROOT.iterdir())
    model_path = None
    for candidate in reversed(candidates):
        merged = candidate / "merged_model"
        if merged.exists():
            model_path = str(merged)
            break

    if not model_path:
        raise FileNotFoundError(f"No merged model under {EXPERIMENTS_ROOT}")

    print(f"Model: {model_path}")

    parser = make_arg_parser(FlexibleArgumentParser("vllm serve"))
    args = parser.parse_args([
        model_path,
        "--served-model-name", MODEL_NAME,
        "--host", "0.0.0.0",
        "--port", "8000",
        "--tensor-parallel-size", str(N_GPU),
        "--max-model-len", "16384",
        "--dtype", "bfloat16",
    ])

    fastapi_app = build_app(args)

    orig_lifespan = fastapi_app.router.lifespan_context

    @asynccontextmanager
    async def engine_lifespan(app):
        async with build_async_engine_client(args) as engine_client:
            await init_app_state(engine_client, app.state, args)
            async with orig_lifespan(app) as maybe_state:
                yield maybe_state

    fastapi_app.router.lifespan_context = engine_lifespan
    print("Model loaded, server ready")
    return fastapi_app
