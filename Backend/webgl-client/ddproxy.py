#!/usr/bin/env python3
"""Tiny DataDog intake proxy — handles /dd-intake?ddforward=<encodedPath> protocol."""
import urllib.parse
import http.server
import http.client
import json
import ssl

INTAKE_HOST = "browser-intake-us5-datadoghq.com"
INTAKE_PORT = 443

class DDProxyHandler(http.server.BaseHTTPRequestHandler):
    def log_message(self, format, *args):
        pass  # quiet

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
            # v6 SDK proxy protocol: ddforward=<urlencoded path+params>
            forward_path = qs["ddforward"][0]
            # The SDK encodes the path+params, urllib decodes them on parse
            target_path = forward_path
        elif parsed.path.startswith("/dd-intake/"):
            # Legacy direct path proxy
            target_path = parsed.path[len("/dd-intake"):]
            if parsed.query:
                target_path += "?" + parsed.query
        else:
            self.send_error(404)
            return

        # Read request body
        content_length = int(self.headers.get("Content-Length", 0))
        body = self.rfile.read(content_length) if content_length > 0 else None

        # Forward to actual intake
        conn = http.client.HTTPSConnection(INTAKE_HOST, INTAKE_PORT,
            context=ssl._create_unverified_context())
        try:
            # Copy relevant headers
            headers = {}
            for h in ["Content-Type", "Content-Encoding", "Origin", "Referer"]:
                v = self.headers.get(h)
                if v:
                    headers[h] = v

            conn.request(method, target_path, body=body, headers=headers)
            resp = conn.getresponse()
            resp_body = resp.read()

            self.send_response(resp.status)
            # Add CORS headers
            self.send_header("Access-Control-Allow-Origin", "*")
            self.send_header("Access-Control-Allow-Methods", "GET, POST, PUT, OPTIONS")
            self.send_header("Access-Control-Allow-Headers", "Content-Type, Authorization, Origin")
            # Copy relevant response headers
            for h in ["Content-Type", "Content-Encoding"]:
                v = resp.getheader(h)
                if v:
                    self.send_header(h, v)
            self.send_header("Content-Length", str(len(resp_body)))
            self.end_headers()
            self.wfile.write(resp_body)
        finally:
            conn.close()

    def send_cors(self, status=200):
        self.send_response(status)
        self.send_header("Access-Control-Allow-Origin", "*")
        self.send_header("Access-Control-Allow-Methods", "GET, POST, PUT, OPTIONS")
        self.send_header("Access-Control-Allow-Headers", "Content-Type, Authorization, Origin")

if __name__ == "__main__":
    port = 9090
    server = http.server.HTTPServer(("0.0.0.0", port), DDProxyHandler)
    print(f"DD proxy listening on :{port}")
    server.serve_forever()
