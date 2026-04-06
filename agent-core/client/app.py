"""Static file server for Protocol Agent Web UI.

Usage:
  python3 app.py [port]          # default: 8090
  python3 -m http.server 8090    # alternative (from web/ directory)

The web UI connects directly to API Gateway — no backend needed.
"""

import os
import sys
from http.server import HTTPServer, SimpleHTTPRequestHandler


def main():
    port = int(sys.argv[1]) if len(sys.argv) > 1 else 8090
    web_dir = os.path.join(os.path.dirname(__file__), "web")
    os.chdir(web_dir)
    server = HTTPServer(("0.0.0.0", port), SimpleHTTPRequestHandler)
    print(f"Protocol Agent Web UI: http://localhost:{port}")
    print("Configure API URL and API Key in the browser.")
    server.serve_forever()


if __name__ == "__main__":
    main()
