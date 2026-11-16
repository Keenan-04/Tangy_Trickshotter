"""
HTTP front end for the matchmaker. Standard library only.

    python server.py                 # http://127.0.0.1:8787
    python server.py --port 9000

    POST   /v1/queue                 {"playerId", "name", "mode", "skill"}  ->  {"ticketId", ...}
    GET    /v1/queue/<ticketId>      current state; poll about once a second (this is the heartbeat)
    DELETE /v1/queue/<ticketId>      leave the queue
    GET    /v1/health
    GET    /v1/stats

STATUS: work in progress and for local testing only. It has no accounts or authentication, keeps
everything in memory, and binds to this machine only by default. Do not put it on the internet as is.
"""
import argparse
import json
import threading
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

from matchmaker import Matchmaker

MAX_BODY = 4096


def make_handler(matchmaker, lock):
    class Handler(BaseHTTPRequestHandler):
        server_version = "TangyMatchmaking/0.1"

        def _send(self, code, payload):
            body = json.dumps(payload).encode("utf-8")
            self.send_response(code)
            self.send_header("Content-Type", "application/json")
            self.send_header("Content-Length", str(len(body)))
            self.end_headers()
            self.wfile.write(body)

        def _ticket_id(self):
            parts = self.path.split("?")[0].strip("/").split("/")
            if len(parts) == 3 and parts[0] == "v1" and parts[1] == "queue":
                return parts[2]
            return None

        def do_GET(self):
            path = self.path.split("?")[0].rstrip("/")
            if path == "/v1/health":
                return self._send(200, {"ok": True})
            if path == "/v1/stats":
                with lock:
                    return self._send(200, matchmaker.stats())

            ticket_id = self._ticket_id()
            if ticket_id:
                with lock:
                    state = matchmaker.poll(ticket_id)
                if state is None:
                    return self._send(404, {"error": "no such ticket"})
                return self._send(200, state)
            self._send(404, {"error": "not found"})

        def do_POST(self):
            if self.path.split("?")[0].rstrip("/") != "/v1/queue":
                return self._send(404, {"error": "not found"})
            try:
                length = int(self.headers.get("Content-Length", "0"))
                if length <= 0 or length > MAX_BODY:
                    return self._send(400, {"error": "body required, at most %d bytes" % MAX_BODY})
                data = json.loads(self.rfile.read(length).decode("utf-8"))
                if not isinstance(data, dict):
                    raise ValueError("body must be a JSON object")
                with lock:
                    ticket = matchmaker.enqueue(str(data.get("playerId", "")), str(data.get("name", "")),
                                                str(data.get("mode", "")), int(data.get("skill", 1000)))
                    state = matchmaker.poll(ticket.ticket_id)
            except (ValueError, TypeError) as ex:
                return self._send(400, {"error": str(ex)})
            self._send(201, state)

        def do_DELETE(self):
            ticket_id = self._ticket_id()
            if not ticket_id:
                return self._send(404, {"error": "not found"})
            with lock:
                found = matchmaker.cancel(ticket_id)
            self._send(200 if found else 404, {"cancelled": found})

        def log_message(self, fmt, *args):  # keep the console quiet; polling is noisy
            pass

    return Handler


def run_ticker(matchmaker, lock, stop, interval=1.0):
    while not stop.wait(interval):
        with lock:
            for match in matchmaker.tick():
                print("match %s  %s on %s  %d players" % (match.match_id[:8], match.mode, match.map, len(match.players)))


def serve(host="127.0.0.1", port=8787, matchmaker=None):
    """Starts the server and the matchmaking loop. Returns (server, stop_event); call server.shutdown() to stop."""
    matchmaker = matchmaker or Matchmaker()
    lock = threading.Lock()
    stop = threading.Event()
    httpd = ThreadingHTTPServer((host, port), make_handler(matchmaker, lock))
    threading.Thread(target=run_ticker, args=(matchmaker, lock, stop), daemon=True).start()
    threading.Thread(target=httpd.serve_forever, daemon=True).start()
    return httpd, stop


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Tangy Trickshotter matchmaking server (work in progress)")
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=8787)
    args = parser.parse_args()

    httpd, stop = serve(args.host, args.port)
    print("matchmaking on http://%s:%d  (Ctrl+C to stop)" % (args.host, args.port))
    try:
        threading.Event().wait()
    except KeyboardInterrupt:
        pass
    finally:
        stop.set()
        httpd.shutdown()
