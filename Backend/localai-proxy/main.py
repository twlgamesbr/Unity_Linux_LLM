"""
LocalAI Trace Proxy — instruments LLM calls with Datadog APM + LLM Observability.

Sits between Unity NPC clients and LocalAI. This is the single consolidated
Datadog proxy for LocalAI traffic (it absorbed the standalone LocalAI-project
`trace-proxy` sidecar, which duplicated the same APM-wrapping responsibility
from a separate Compose project — see Documentation/2_Architecture/
Backend_Services_Topology.md §7.5):

- `/v1/chat/completions` gets rich, gameplay-focused instrumentation: model,
  token counts, prompt/response length, latency, and LLM Observability tags
  (per-NPC prompt/response content), so Datadog can surface which dialogue
  requests are slow or expensive and need optimization.
- Every other LocalAI path (models list, embeddings, health, etc.) is
  proxied generically with a basic APM span, so nothing needs a second proxy.

Usage:
    DD_SERVICE=localai-proxy DD_ENV=production ddtrace-run python main.py
"""

import os
import time
import logging
from typing import AsyncIterable

import fastapi
import httpx
from fastapi import Request, Response
from fastapi.responses import StreamingResponse
from ddtrace import tracer
try:
    from ddtrace.contrib.asgi import TraceMiddleware
except ImportError:
    TraceMiddleware = None
from pydantic import BaseModel

logger = logging.getLogger("localai-proxy")

LOCALAI_BASE = os.getenv("LOCALAI_BASE_URL", "http://localhost:8080")
SERVICE_NAME = os.getenv("DD_SERVICE", "localai-proxy")
PORT = int(os.getenv("PROXY_PORT", "8090"))

app = fastapi.FastAPI(title="LocalAI Trace Proxy")

# ── Datadog ASGI middleware for automatic HTTP request tracing ──
# Starlette builds the middleware stack lazily on the first request, so a
# TypeError from an incompatible constructor signature would otherwise crash
# every request instead of failing fast at startup. Newer ddtrace releases
# dropped the `service=` kwarg from TraceMiddleware (service now comes from
# DD_SERVICE / ddtrace.config), so inspect the signature up front and only
# pass `service` if this ddtrace version still accepts it.
if TraceMiddleware is not None:
    import inspect

    _trace_middleware_params = inspect.signature(TraceMiddleware.__init__).parameters
    if "service" in _trace_middleware_params:
        app.add_middleware(TraceMiddleware, service=SERVICE_NAME)
    else:
        app.add_middleware(TraceMiddleware)

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


# ── Generic passthrough for everything else LocalAI exposes ──
# (models list, embeddings, TTS, OpenMetrics, etc.) — absorbs the
# responsibility the standalone `localai-trace-proxy` sidecar used to have,
# with a basic APM span per request instead of duplicating a whole service.
# FastAPI matches routes in registration order, so the specific
# `/v1/chat/completions` handler above always wins over this catch-all.

_passthrough_client = httpx.AsyncClient(base_url=LOCALAI_BASE, timeout=None)


@app.on_event("shutdown")
async def _shutdown_passthrough_client():
    await _passthrough_client.aclose()


async def _read_body(content: AsyncIterable[bytes]) -> bytes:
    chunks = []
    async for chunk in content:
        chunks.append(chunk)
    return b"".join(chunks)


@app.api_route(
    "/{path:path}",
    methods=["GET", "POST", "PUT", "DELETE", "PATCH", "HEAD", "OPTIONS"],
)
async def passthrough(request: Request, path: str):
    """Proxy any non-chat-completions LocalAI request, tagging it for APM."""
    with tracer.trace(
        "localai.proxy_passthrough",
        service=SERVICE_NAME,
        resource=f"{request.method} /{path}",
        span_type="http",
    ) as span:
        span.set_tag("localai.backend", LOCALAI_BASE)
        span.set_tag("localai.proxy_path", path)

        upstream_path = f"/{path}" if path else "/"
        if request.query_params:
            upstream_path += f"?{request.query_params}"

        body = await request.body()
        start = time.monotonic()

        try:
            resp = await _passthrough_client.request(
                method=request.method,
                url=upstream_path,
                content=body,
                headers=dict(request.headers),
            )
            span.set_tag("http.status_code", str(resp.status_code))
            span.set_tag(
                "localai.latency_ms", str((time.monotonic() - start) * 1000)
            )

            content_type = resp.headers.get("content-type", "")
            if "text/event-stream" in content_type:
                return StreamingResponse(
                    content=resp.aiter_bytes(),
                    status_code=resp.status_code,
                    headers=dict(resp.headers),
                )

            return Response(
                content=resp.content,
                status_code=resp.status_code,
                headers=dict(resp.headers),
            )

        except httpx.RequestError as e:
            span.set_tag("error", "true")
            span.set_tag("error.message", str(e))
            logger.error("Error proxying %s to LocalAI: %s", upstream_path, e)
            return fastapi.responses.JSONResponse(
                status_code=502,
                content={"error": f"LocalAI proxy error: {e}"},
            )


# ── Entrypoint ──

if __name__ == "__main__":
    import uvicorn
    uvicorn.run(app, host="0.0.0.0", port=PORT)
