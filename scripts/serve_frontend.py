import http.server, os, sys
from http.server import ThreadingHTTPServer

FRONTEND = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "Frontend")
PORT = 3000

MIME = {
    ".html": "text/html; charset=utf-8",
    ".css":  "text/css",
    ".js":   "application/javascript",
    ".png":  "image/png",
    ".jpg":  "image/jpeg",
    ".svg":  "image/svg+xml",
    ".ico":  "image/x-icon",
    ".json": "application/json",
}

class Handler(http.server.BaseHTTPRequestHandler):
    def do_GET(self):
        path = self.path.split("?")[0].split("#")[0]
        if path == "/" or path == "":
            path = "/index.html"
        filepath = os.path.join(FRONTEND, path.lstrip("/").replace("/", os.sep))
        # Try adding .html extension for extensionless paths
        if not os.path.isfile(filepath) and not os.path.splitext(filepath)[1]:
            if os.path.isfile(filepath + ".html"):
                filepath = filepath + ".html"
            else:
                filepath = os.path.join(FRONTEND, "index.html")
        # Directory with no trailing slash: serve its index.html
        if os.path.isdir(filepath):
            filepath = os.path.join(filepath, "index.html")
        if os.path.isfile(filepath):
            ext = os.path.splitext(filepath)[1].lower()
            ctype = MIME.get(ext, "application/octet-stream")
            with open(filepath, "rb") as f:
                data = f.read()
            self.send_response(200)
            self.send_header("Content-Type", ctype)
            self.send_header("Content-Length", str(len(data)))
            self.send_header("Access-Control-Allow-Origin", "*")
            self.end_headers()
            self.wfile.write(data)
        else:
            self.send_error(404)

    def log_message(self, fmt, *args):
        pass

os.chdir(FRONTEND)
print(f"Frontend serving at http://localhost:{PORT}")
httpd = ThreadingHTTPServer(("0.0.0.0", PORT), Handler)
httpd.serve_forever()
