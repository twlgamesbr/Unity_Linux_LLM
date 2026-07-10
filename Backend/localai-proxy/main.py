"""
LocalAI Trace Proxy — instruments LLM calls with Datadog APM + LLM Observability.

Sits between Unity NPC clients and LocalAI's v1/chat/completions endpoint.
Captures prompts, responses, token counts, and latency for every LLM request
and sends them as Datadog APM traces + LLM Observability events.

Usage:
    DD_SERVICE=localai-proxy DD_ENV=production ddtrace-run python main.py
"""

import os
import time
import logging

import fastapi
import httpx
from ddtrace import tracer, pin
from ddtrace.contrib.asgi import TraceMiddleware
from pydantic import BaseModel

logger = logging.getLogger("localai-proxy")

LOCALAI_BASE = os.getenv("LOCALAI_BASE_URL", "http://localhost:8080")
SERVICE_NAME = os.getenv("DD_SERVICE", "localai-proxy")
PORT = int(os.getenv("PROXY_PORT", "8090"))

app = fastapi.FastAPI(title="LocalAI Trace Proxy")

# ── Datadog ASGI middleware for automatic HTTP request tracing ──
app.add_middleware(TraceMiddleware, service=SERVICE_NAME)

# ── Request/response models ──

class ChatCompletionRequest(BaseModel):
    model: str
    messages: list[dict]
    temperature: float | None = None
    top_p: float | None = None
    max_tokens: int | None = None
    stream: bool = False

class ChatCompletionResponse(BaseModel):
    id: str
    object: str
    created: int
    model: str
    choices: list[dict]
    usage: dict | None = None


# ── Proxy endpoint ──

@app.post("/v1/chat/completions")
async def chat_completions(req: ChatCompletionRequest):
    # Extract the user's last message for LLM Observability tagging
    user_message = ""
    system_prompt = ""
    for m in req.messages:
        if m.get("role") == "user":
            user_message = m.get("content", "")
        elif m.get("role") == "system":
            system_prompt = m.get("content", "")

    # Start a custom APM span for the LLM call
    with tracer.trace(
        "llm.request",
        service=SERVICE_NAME,
        resource=f"LocalAI/{req.model}",
        span_type="llm",
    ) as span:
        span.set_tag("llm.request.model", req.model)
        span.set_tag("llm.request.temperature", str(req.temperature or ""))
        span.set_tag("llm.request.max_tokens", str(req.max_tokens or ""))
        span.set_tag("llm.request.prompt_length", str(len(user_message)))
        span.set_tag("llm.request.system_length", str(len(system_prompt)))

        start = time.monotonic()

        try:
            async with httpx.AsyncClient(timeout=120) as client:
                upstream_url = f"{LOCALAI_BASE}/v1/chat/completions"
                logger.info("Proxying to %s model=%s", upstream_url, req.model)

                resp = await client.post(
                    upstream_url,
                    json=req.model_dump(exclude_none=True),
                    headers={"Content-Type": "application/json"},
                )
                resp.raise_for_status()
                data = resp.json()

            elapsed = (time.monotonic() - start) * 1000

            # Extract token usage if LocalAI returns it
            usage = data.get("usage", {})
            prompt_tokens = usage.get("prompt_tokens", 0)
            completion_tokens = usage.get("completion_tokens", 0)
            total_tokens = usage.get("total_tokens", 0)

            # Gather response text for quality tracking
            response_text = ""
            if data.get("choices"):
                response_text = data["choices"][0].get("message", {}).get("content", "")

            # APM span tags
            span.set_tag("llm.response.latency_ms", str(elapsed))
            span.set_tag("llm.response.completion_tokens", str(completion_tokens))
            span.set_tag("llm.response.prompt_tokens", str(prompt_tokens))
            span.set_tag("llm.response.total_tokens", str(total_tokens))
            span.set_tag("llm.response.response_length", str(len(response_text)))
            span.set_tag("llm.response.status", "success")

            # LLM Observability tags (Datadog LLM Obs)
            span.set_tag("ml_app", "npc-dialogue")
            span.set_tag("llm.request.system_prompt", system_prompt[:500])
            span.set_tag("llm.request.user_message", user_message[:500])
            span.set_tag("llm.response.content", response_text[:500])

            logger.info(
                "Model=%s prompt_tokens=%d completion_tokens=%d latency=%.0fms",
                req.model, prompt_tokens, completion_tokens, elapsed,
            )

            return data

        except httpx.HTTPStatusError as e:
            elapsed = (time.monotonic() - start) * 1000
            span.set_tag("llm.response.status", "error")
            span.set_tag("llm.response.error", str(e))
            span.set_tag("llm.response.latency_ms", str(elapsed))
            logger.error("LocalAI error: %s", e)
            raise fastapi.HTTPException(
                status_code=e.response.status_code,
                detail=f"LocalAI upstream error: {e.response.text}",
            )
        except httpx.RequestError as e:
            elapsed = (time.monotonic() - start) * 1000
            span.set_tag("llm.response.status", "error")
            span.set_tag("llm.response.error", f"RequestError: {e}")
            span.set_tag("llm.response.latency_ms", str(elapsed))
            logger.error("LocalAI connection error: %s", e)
            raise fastapi.HTTPException(
                status_code=502,
                detail=f"Cannot reach LocalAI at {LOCALAI_BASE}: {e}",
            )


# ── Health check ──

@app.get("/health")
async def health():
    return {"status": "ok", "proxy_to": LOCALAI_BASE}


# ── Entrypoint ──

if __name__ == "__main__":
    import uvicorn
    uvicorn.run(app, host="0.0.0.0", port=PORT)
