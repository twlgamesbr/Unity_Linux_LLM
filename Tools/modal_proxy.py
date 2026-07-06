"""
Smart HTTP proxy: routes to LocalAI (:8080) or Modal vLLM based on model name.
- npc-codebase-v1 -> https://andretwl--npc-codebase-v1-serve.modal.run (HTTPS)
- everything else -> http://localhost:8080 (LocalAI)
Listens on port 8081, intended as Unity's remoteHost:remotePort target.

GET /v1/models merges model lists from both upstreams.
"""
import http.server
import json
import ssl
import urllib.request
import urllib.error
import os

MODAL_UPSTREAM = os.environ.get("MODAL_UPSTREAM", "https://andretwl--npc-codebase-v1-serve.modal.run")
LOCALAI_UPSTREAM = os.environ.get("LOCALAI_UPSTREAM", "http://localhost:8080")
LOCALAI_TIMEOUT = int(os.environ.get("LOCALAI_TIMEOUT", "30"))
MODAL_TIMEOUT = int(os.environ.get("MODAL_TIMEOUT", "120"))
LISTEN_PORT = int(os.environ.get("PROXY_PORT", "8081"))

# Models served by Modal (all others go to LocalAI)
MODAL_MODELS = {"npc-codebase-v1"}


def _fetch_json(url, timeout, ssl_ctx=None):
    req = urllib.request.Request(url)
    ctx = ssl_ctx or ssl.create_default_context() if ssl_ctx is not None else None
    resp = urllib.request.urlopen(req, timeout=timeout, context=ctx)
    return json.loads(resp.read().decode())


class SmartProxyHandler(http.server.BaseHTTPRequestHandler):
    def log_message(self, format, *args):
        print(f"[proxy] {self.address_string()} - {format % args}", flush=True)

    def _read_body(self):
        length = int(self.headers.get("Content-Length", 0))
        return self.rfile.read(length) if length > 0 else b""

    def _resolve_upstream(self, model):
        if model in MODAL_MODELS:
            return MODAL_UPSTREAM, MODAL_TIMEOUT, True
        return LOCALAI_UPSTREAM, LOCALAI_TIMEOUT, False

    def _forward(self, method, body=b""):
        model = ""
        if method == "POST" and body:
            try:
                payload = json.loads(body)
                model = payload.get("model", "")
            except (json.JSONDecodeError, UnicodeDecodeError):
                pass

        base_url, timeout, use_ssl = self._resolve_upstream(model)
        upstream = f"{base_url}{self.path}"

        req = urllib.request.Request(upstream, data=body or None, method=method)
        for key, value in self.headers.items():
            kl = key.lower()
            if kl in ("host", "content-length", "connection", "transfer-encoding"):
                continue
            req.add_header(key, value)

        try:
            if use_ssl:
                ctx = ssl.create_default_context()
                ctx.check_hostname = True
                ctx.verify_mode = ssl.CERT_REQUIRED
                response = urllib.request.urlopen(req, timeout=timeout, context=ctx)
            else:
                response = urllib.request.urlopen(req, timeout=timeout)

            resp_data = response.read()
            resp_headers = dict(response.headers)
            self.send_response(response.status)
            for key, value in resp_headers.items():
                kl = key.lower()
                if kl in ("transfer-encoding", "connection", "content-encoding"):
                    continue
                self.send_header(key, value)
            self.end_headers()
            self.wfile.write(resp_data)
        except urllib.error.HTTPError as e:
            self.send_response(e.code)
            self.end_headers()
            self.wfile.write(e.read())
        except urllib.error.URLError as e:
            self._send_error(502, f"Upstream error ({model or 'unknown'}): {e.reason}")
        except Exception as e:
            self._send_error(500, f"Proxy error: {e}")

    def _send_error(self, code, message):
        self.send_response(code)
        self.send_header("Content-Type", "application/json")
        self.end_headers()
        self.wfile.write(json.dumps({"error": {"code": code, "message": message}}).encode())

    def _handle_models(self):
        """Merge model lists from LocalAI and Modal."""
        all_models = []
        seen = set()
        try:
            localai_data = _fetch_json(f"{LOCALAI_UPSTREAM}/v1/models", LOCALAI_TIMEOUT)
            for m in localai_data.get("data", []):
                mid = m.get("id", "")
                if mid not in seen:
                    seen.add(mid)
                    all_models.append(m)
        except Exception as e:
            print(f"[proxy] Failed to fetch models from LocalAI: {e}", flush=True)

        try:
            ctx = ssl.create_default_context()
            modal_data = _fetch_json(f"{MODAL_UPSTREAM}/v1/models", MODAL_TIMEOUT, ctx)
            for m in modal_data.get("data", []):
                mid = m.get("id", "")
                if mid not in seen:
                    seen.add(mid)
                    all_models.append(m)
        except Exception as e:
            print(f"[proxy] Failed to fetch models from Modal: {e}", flush=True)

        resp = json.dumps({"object": "list", "data": all_models}).encode()
        self.send_response(200)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(resp)))
        self.end_headers()
        self.wfile.write(resp)

    def do_GET(self):
        if self.path == "/v1/models":
            self._handle_models()
        else:
            body = self._read_body()
            self._forward("GET", body)

    def do_POST(self):
        body = self._read_body()
        self._forward("POST", body)

    def do_OPTIONS(self):
        self.send_response(204)
        self.send_header("Access-Control-Allow-Origin", "*")
        self.send_header("Access-Control-Allow-Methods", "GET, POST, OPTIONS")
        self.send_header("Access-Control-Allow-Headers", "Authorization, Content-Type")
        self.end_headers()


def main():
    server = http.server.HTTPServer(("0.0.0.0", LISTEN_PORT), SmartProxyHandler)
    print(f"[proxy] Listening 0.0.0.0:{LISTEN_PORT}")
    print(f"[proxy]   LocalAI : {LOCALAI_UPSTREAM}")
    print(f"[proxy]   Modal   : {MODAL_UPSTREAM} (models: {', '.join(sorted(MODAL_MODELS))})", flush=True)
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        print("[proxy] Shutting down", flush=True)
        server.server_close()


if __name__ == "__main__":
    main()
