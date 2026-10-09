"""Local-only mesh review server; saves the viewer's GLB to a fixed output path."""
from http.server import ThreadingHTTPServer, SimpleHTTPRequestHandler
from pathlib import Path
import json
import os

ROOT = Path(__file__).resolve().parent

class Handler(SimpleHTTPRequestHandler):
    def __init__(self, *args, **kwargs):
        super().__init__(*args, directory=str(ROOT), **kwargs)

    def do_POST(self):
        if self.path != '/export':
            return self.send_error(404)
        if self.headers.get('Origin') != 'http://127.0.0.1:8771':
            return self.send_error(403)
        try:
            length = int(self.headers.get('Content-Length', '0'))
        except ValueError:
            return self.send_error(400)
        if not 20 <= length <= 25_000_000:
            return self.send_error(413)
        data = self.rfile.read(length)
        if len(data) != length or data[:4] != b'glTF':
            return self.send_error(400)
        output = ROOT / 'MotionClub_HeroV5.glb'
        temp = ROOT / 'MotionClub_HeroV5.glb.tmp'
        temp.write_bytes(data)
        os.replace(temp, output)
        body = json.dumps({'url': '/MotionClub_HeroV5.glb'}).encode()
        self.send_response(200)
        self.send_header('Content-Type', 'application/json')
        self.send_header('Content-Length', str(len(body)))
        self.end_headers()
        self.wfile.write(body)

if __name__ == '__main__':
    ThreadingHTTPServer(('127.0.0.1', 8771), Handler).serve_forever()
