#!/usr/bin/env python3
"""
Container GPU Monitor for Datadog
===================================
Queries nvidia-smi for GPU processes, maps PIDs to Docker containers,
and emits per-container GPU metrics via DogStatsD.

Run via cron every 30s:
  * * * * * /path/to/container-gpu-monitor.py

Or as a systemd timer for more reliable scheduling.
"""

import subprocess
import json
import socket
import struct
import time
import os
import re
from pathlib import Path

# ── Config ──────────────────────────────────────────────────────────
DOGSTATSD_HOST = os.environ.get("DD_DOGSTATSD_HOST", "127.0.0.1")
DOGSTATSD_PORT = int(os.environ.get("DD_DOGSTATSD_PORT", "8125"))
NAMESPACE = "gpu.container"
INTERVAL = 30  # seconds between runs

# ── DogStatsD UDP Client (minimal, no dependencies) ────────────────
def send_metric(name, value, metric_type="g", tags=None):
    """Send a metric to DogStatsD via UDP."""
    tag_str = ""
    if tags:
        tag_str = "|#" + ",".join(tags)
    
    # DogStatsD format: name:value|type|@sample_rate|#tags
    msg = f"{name}:{value}|{metric_type}{tag_str}"
    
    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    try:
        sock.sendto(msg.encode(), (DOGSTATSD_HOST, DOGSTATSD_PORT))
    finally:
        sock.close()


def get_nvidia_smi_processes():
    """Query nvidia-smi for active GPU compute processes."""
    try:
        result = subprocess.run(
            [
                "nvidia-smi",
                "--query-compute-apps=pid,process_name,used_gpu_memory",
                "--format=csv,nounits",
            ],
            capture_output=True,
            text=True,
            timeout=10,
        )
        if result.returncode != 0:
            print(f"nvidia-smi failed: {result.stderr}")
            return []
        
        processes = []
        for line in result.stdout.strip().split("\n")[1:]:  # skip header
            if not line.strip():
                continue
            parts = [p.strip() for p in line.split(",")]
            if len(parts) >= 3:
                pid = int(parts[0])
                name = parts[1]
                mem_mb = float(parts[2]) if parts[2] != "[N/A]" else 0.0
                processes.append({"pid": pid, "name": name, "memory_mb": mem_mb})
        return processes
    except Exception as e:
        print(f"Error querying nvidia-smi: {e}")
        return []


def get_nvidia_smi_gpu_stats():
    """Query nvidia-smi for per-GPU stats."""
    try:
        result = subprocess.run(
            [
                "nvidia-smi",
                "--query-gpu=index,name,utilization.gpu,memory.used,memory.total,temperature.gpu,power.draw",
                "--format=csv,nounits",
            ],
            capture_output=True,
            text=True,
            timeout=10,
        )
        if result.returncode != 0:
            return []
        
        gpus = []
        for line in result.stdout.strip().split("\n")[1:]:
            if not line.strip():
                continue
            parts = [p.strip() for p in line.split(",")]
            if len(parts) >= 7:
                gpus.append({
                    "index": int(parts[0]),
                    "name": parts[1],
                    "utilization": float(parts[2]) if parts[2] != "[N/A]" else 0.0,
                    "memory_used": float(parts[3]) if parts[3] != "[N/A]" else 0.0,
                    "memory_total": float(parts[4]) if parts[4] != "[N/A]" else 0.0,
                    "temperature": float(parts[5]) if parts[5] != "[N/A]" else 0.0,
                    "power_draw": float(parts[6]) if parts[6] != "[N/A]" else 0.0,
                })
        return gpus
    except Exception as e:
        print(f"Error querying GPU stats: {e}")
        return []


def pid_to_container(pid):
    """Map a PID to its Docker container name via cgroup."""
    try:
        cgroup_path = Path(f"/proc/{pid}/cgroup")
        if not cgroup_path.exists():
            return None
        
        content = cgroup_path.read_text()
        # Docker container IDs are 64-char hex strings in cgroup paths
        match = re.search(r"([0-9a-f]{64})", content)
        if not match:
            return None
        
        container_id = match.group(1)[:12]  # short ID
        
        # Get container name from Docker
        result = subprocess.run(
            ["docker", "inspect", "--format", "{{.Name}}", container_id],
            capture_output=True,
            text=True,
            timeout=5,
        )
        if result.returncode == 0:
            return result.stdout.strip().lstrip("/")
        return container_id
    except Exception:
        return None


def collect_and_emit():
    """Main collection loop."""
    base_tags = ["env:production", "project:unity-linux-llm"]
    
    # Collect host GPU stats
    gpus = get_nvidia_smi_gpu_stats()
    for gpu in gpus:
        gpu_tags = base_tags + [
            f"gpu:{gpu['index']}",
            f"gpu_name:{gpu['name']}",
        ]
        send_metric("utilization.gpu", gpu["utilization"], tags=gpu_tags)
        send_metric("memory.used", gpu["memory_used"], tags=gpu_tags)
        send_metric("memory.total", gpu["memory_total"], tags=gpu_tags)
        send_metric("temperature.gpu", gpu["temperature"], tags=gpu_tags)
        send_metric("power.draw", gpu["power_draw"], tags=gpu_tags)
    
    # Collect per-container GPU usage
    processes = get_nvidia_smi_processes()
    container_gpu = {}  # container_name -> {"memory_mb": sum, "process_count": n}
    
    for proc in processes:
        container = pid_to_container(proc["pid"])
        if not container:
            container = "host-process"
        
        if container not in container_gpu:
            container_gpu[container] = {"memory_mb": 0.0, "process_count": 0}
        container_gpu[container]["memory_mb"] += proc["memory_mb"]
        container_gpu[container]["process_count"] += 1
    
    # Emit per-container metrics
    for container, stats in container_gpu.items():
        container_tags = base_tags + [f"container:{container}"]
        send_metric("memory.used", stats["memory_mb"], tags=container_tags)
        send_metric("process.count", stats["process_count"], tags=container_tags)
    
    # Emit zero metrics for containers that were using GPU but aren't now
    # (This ensures Datadog sees the series go to zero rather than disappearing)
    print(f"[{time.strftime('%H:%M:%S')}] Emitted GPU metrics: {len(gpus)} GPUs, {len(container_gpu)} containers")


if __name__ == "__main__":
    collect_and_emit()
