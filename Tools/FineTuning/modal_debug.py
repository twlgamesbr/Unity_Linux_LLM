import pathlib

import modal

image = (
    modal.Image.from_registry("nvidia/cuda:12.4.0-devel-ubuntu22.04", add_python="3.11")
    .entrypoint([])
    .uv_pip_install("vllm==0.11.2")
    .env({"HF_XET_HIGH_PERFORMANCE": "1"})
)

checkpoints = modal.Volume.from_name("unsloth-checkpoints", create_if_missing=True)

app = modal.App("npc-codebase-debug")


@app.function(
    image=image,
    gpu="A100:1",
    timeout=300,
    volumes={"/checkpoints": checkpoints},
)
def debug():
    import os
    import shutil
    import subprocess

    root = pathlib.Path("/checkpoints/experiments")

    print(f"Python: {os.sys.version}")
    print(f"vllm in PATH: {shutil.which('vllm')}")
    print(f"Root exists: {root.exists()}")

    if root.exists():
        for c in sorted(root.iterdir()):
            print(f"  {c.name}")
            merged = c / "merged_model"
            if merged.exists():
                files = list(merged.iterdir())
                print(f"    merged_model/ ({len(files)} files)")
                for f in files:
                    print(f"      {f.name} ({f.stat().st_size / 1024 / 1024:.1f} MB)")

    model = "/checkpoints/experiments/npc-codebase-v1-r32-1500/merged_model"
    print(f"\nModel path exists: {pathlib.Path(model).exists()}")

    print("\nTesting Python imports...")
    import subprocess, sys

    tests = [
        "import torch; print('torch:', torch.__version__, 'CUDA:', torch.cuda.is_available())",
        "import vllm; print('vllm:', vllm.__version__)",
        "from vllm.entrypoints.openai.api_server import make_arg_parser, make_asgi_app; print('make_asgi_app OK')",
        "from vllm.entrypoints.openai.api_server import FlexibleArgumentParser, make_arg_parser, make_asgi_app; p = make_arg_parser(FlexibleArgumentParser('vllm serve')); args = p.parse_args(['/checkpoints/experiments/npc-codebase-v1-r32-1500/merged_model', '--served-model-name', 'test', '--host', '0.0.0.0', '--port', '8000']); fn = make_asgi_app(args); print('Has __call__:', hasattr(fn, '__call__')); print('Dir:', [x for x in dir(fn) if not x.startswith('_')][:10])",
    ]
    for test in tests:
        r = subprocess.run([sys.executable, "-c", test], capture_output=True, text=True, timeout=30)
        print(f"  {r.stdout.strip() or r.stderr.strip()}")
    print("All imports OK")
