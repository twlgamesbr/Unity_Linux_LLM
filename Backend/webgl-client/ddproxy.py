#!/usr/bin/env python3
"""Tiny DataDog intake proxy — handles /dd-intake?ddforward=<encodedPath> protocol."""
import urllib.parse
import http.server
import http.client
import json
import ssl
import os
import logging

# Initialize ddtrace if available
try:
    from ddtrace import tracer, patch
    from ddtrace.contrib.trace_utils import set_http_meta
    patch(http_client=True)
    DDTRACE_AVAILABLE = True
except ImportError:
    DDTRACE_AVAILABLE = False

logger = logging.getLogger("ddproxy")
if DDTRACE_AVAILABLE:
    logger.info("ddtrace instrumentation active")

INTAKE_HOST = "browser-intake-us5-datadoghq.com"
INTAKE_PORT = 443

class DDProxyHandler(http.server.BaseHTTPRequestHandler):
    def log_message(self, format, *args):
        pass  # quiet to /var/log/

    def do_OPTIONS(self):
        self.send_cors(204)
        self.end_headers()

    def do_GET(self):
        self.handle_request("GET")

    def do_POST(self):
        self.handle_request("POST")

    def do_PUT(self):
        self.handle_request("PUT")

    def handle_request(self, method):
        parsed = urllib.parse.urlparse(self.path)
        qs = urllib.parse.parse_qs(parsed.query)

        if parsed.path == "/dd-intake" and "ddforward" in qs:
            forward_path = qs["ddforward"][0]
            target_path = forward_path
        elif parsed.path.startswith("/dd-intake/"):
            target_path = parsed.path[len("/dd-intake"):]
            if parsed.query:
                target_path += "?" + parsed.query
        else:
            self.send_error(404)
            return

        # Read request body
        content_length = int(self.headers.get("Content-Length", 0))
        body = self.rfile.read(content_length) if content_length > 0 else None

        # Trace the upstream call
        span = None
        if DDTRACE_AVAILABLE:
            span = tracer.trace(
                "ddproxy.upstream_request",
                service="ddproxy",
                resource=f"{method} /dd-intake",
                span_type="http",
            )
            span.set_tag("http.method", method)
            span.set_tag("http.url", f"https://{INTAKE_HOST}{target_path}")
            span.set_tag("ddproxy.forward_path", target_path)

        # Forward to actual intake
        conn = http.client.HTTPSConnection(INTAKE_HOST, INTAKE_PORT,
            context=ssl._create_unverified_context())
        try:
            headers = {}
            for h in ["Content-Type", "Content-Encoding", "Origin", "Referer"]:
                v = self.headers.get(h)
                if v:
                    headers[h] = v

            conn.request(method, target_path, body=body, headers=headers)
            resp = conn.getresponse()
            resp_body = resp.read()

            if span:
                span.set_tag("http.status_code", resp.status)
                if resp.status >= 400:
                    span.error = 1
                span.finish()

            self.send_response(resp.status)
            self.send_header("Access-Control-Allow-Origin", "*")
            self.send_header("Access-Control-Allow-Methods", "GET, POST, PUT, OPTIONS")
            self.send_header("Access-Control-Allow-Headers", "Content-Type, Authorization, Origin")
            for h in ["Content-Type", "Content-Encoding"]:
                v = resp.getheader(h)
                if v:
                    self.send_header(h, v)
            self.send_header("Content-Length", str(len(resp_body)))
            self.end_headers()
            self.wfile.write(resp_body)
        except Exception as e:
            if span:
                span.set_tag("error", str(e))
                span.error = 1
                span.finish()
            self.send_error(502, f"Upstream error: {e}")
        finally:
            conn.close()

    def send_cors(self, status=200):
        self.send_response(status)
        self.send_header("Access-Control-Allow-Origin", "*")
        self.send_header("Access-Control-Allow-Methods", "GET, POST, PUT, OPTIONS")
        self.send_header("Access-Control-Allow-Headers", "Content-Type, Authorization, Origin")

if __name__ == "__main__":
    logging.basicConfig(level=logging.INFO)
    port = 9090
    server = http.server.HTTPServer(("0.0.0.0", port), DDProxyHandler)
    logger.info(f"DD proxy listening on :{port}")
    server.serve_forever()
