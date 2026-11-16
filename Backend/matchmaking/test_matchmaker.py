"""Run with:  python -m unittest -v   (from this folder)"""
import json
import unittest
import urllib.error
import urllib.request

from matchmaker import CANCELLED, EXPIRED, MATCHED, SEARCHING, Matchmaker
from server import serve


class Clock:
    def __init__(self):
        self.now = 0.0

    def __call__(self):
        return self.now


def queue(mm, count, mode="tdm", skills=None):
    return [mm.enqueue("p%d" % i, "Player %d" % i, mode, skills[i] if skills else 1000) for i in range(count)]


class MatchmakerTests(unittest.TestCase):
    def setUp(self):
        self.clock = Clock()
        self.mm = Matchmaker(clock=self.clock)

    def test_full_lobby_makes_a_match_with_even_teams(self):
        tickets = queue(self.mm, 8)
        matches = self.mm.tick()
        self.assertEqual(len(matches), 1)
        teams = [p["team"] for p in matches[0].players]
        self.assertEqual(teams.count("red"), 4)
        self.assertEqual(teams.count("blue"), 4)
        self.assertEqual(matches[0].map, "skyline")
        self.assertTrue(all(self.mm.poll(t.ticket_id)["status"] == MATCHED for t in tickets))

    def test_too_few_players_keep_searching(self):
        tickets = queue(self.mm, 7)
        self.assertEqual(self.mm.tick(), [])
        state = self.mm.poll(tickets[3].ticket_id)
        self.assertEqual(state["status"], SEARCHING)
        self.assertEqual(state["position"], 4)

    def test_smaller_teams_after_a_long_wait(self):
        tickets = queue(self.mm, 5)
        self.clock.now = 29
        for t in tickets:
            self.mm.poll(t.ticket_id)
        self.assertEqual(self.mm.tick(), [])

        self.clock.now = 31
        for t in tickets:
            self.mm.poll(t.ticket_id)
        matches = self.mm.tick()
        self.assertEqual(len(matches), 1)
        self.assertEqual(len(matches[0].players), 4)  # even teams; the fifth player keeps waiting
        self.assertEqual(self.mm.poll(tickets[4].ticket_id)["status"], SEARCHING)

    def test_teams_are_balanced_by_skill(self):
        queue(self.mm, 8, skills=[2000, 1900, 1800, 1700, 1300, 1200, 1100, 1000])
        match = self.mm.tick()[0]
        skill = {"p%d" % i: s for i, s in enumerate([2000, 1900, 1800, 1700, 1300, 1200, 1100, 1000])}
        totals = {"red": 0, "blue": 0}
        for p in match.players:
            totals[p["team"]] += skill[p["playerId"]]
        self.assertLessEqual(abs(totals["red"] - totals["blue"]), 200)

    def test_modes_do_not_mix(self):
        queue(self.mm, 4, mode="tdm")
        for i in range(4):
            self.mm.enqueue("e%d" % i, "E", "elimination")
        self.assertEqual(self.mm.tick(), [])

        for i in range(4, 6):
            self.mm.enqueue("e%d" % i, "E", "elimination")
        matches = self.mm.tick()
        self.assertEqual([m.mode for m in matches], ["elimination"])
        self.assertEqual(len(matches[0].players), 6)

    def test_unpolled_ticket_expires(self):
        ticket = self.mm.enqueue("solo", "Solo", "tdm")
        self.clock.now = 16
        self.mm.tick()
        self.assertEqual(self.mm.poll(ticket.ticket_id)["status"], EXPIRED)

    def test_cancel_and_requeue(self):
        first = self.mm.enqueue("p", "P", "tdm")
        second = self.mm.enqueue("p", "P", "elimination")  # replaces the first
        self.assertEqual(self.mm.poll(first.ticket_id)["status"], CANCELLED)
        self.assertTrue(self.mm.cancel(second.ticket_id))
        self.assertEqual(self.mm.poll(second.ticket_id)["status"], CANCELLED)
        self.assertFalse(self.mm.cancel("nope"))

    def test_bad_input_is_rejected(self):
        with self.assertRaises(ValueError):
            self.mm.enqueue("p", "P", "capture-the-flag")
        with self.assertRaises(ValueError):
            self.mm.enqueue("", "P", "tdm")

    def test_match_has_no_server_yet(self):
        tickets = queue(self.mm, 8)
        self.mm.tick()
        self.assertEqual(self.mm.poll(tickets[0].ticket_id)["match"]["server"], "")


class HttpTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.httpd, cls.stop = serve("127.0.0.1", 0, Matchmaker())
        cls.base = "http://127.0.0.1:%d" % cls.httpd.server_address[1]

    @classmethod
    def tearDownClass(cls):
        cls.stop.set()
        cls.httpd.shutdown()

    def call(self, method, path, body=None):
        data = json.dumps(body).encode() if body is not None else None
        request = urllib.request.Request(self.base + path, data=data, method=method,
                                         headers={"Content-Type": "application/json"})
        try:
            with urllib.request.urlopen(request, timeout=5) as response:
                return response.status, json.loads(response.read())
        except urllib.error.HTTPError as error:
            return error.code, json.loads(error.read())

    def test_queue_poll_cancel(self):
        self.assertEqual(self.call("GET", "/v1/health"), (200, {"ok": True}))

        code, state = self.call("POST", "/v1/queue", {"playerId": "http-1", "name": "H", "mode": "tdm"})
        self.assertEqual(code, 201)
        self.assertEqual(state["status"], SEARCHING)

        code, state = self.call("GET", "/v1/queue/" + state["ticketId"])
        self.assertEqual((code, state["position"]), (200, 1))

        code, body = self.call("DELETE", "/v1/queue/" + state["ticketId"])
        self.assertEqual((code, body), (200, {"cancelled": True}))

    def test_errors(self):
        self.assertEqual(self.call("POST", "/v1/queue", {"playerId": "x", "mode": "nope"})[0], 400)
        self.assertEqual(self.call("GET", "/v1/queue/missing")[0], 404)
        self.assertEqual(self.call("GET", "/v1/elsewhere")[0], 404)


if __name__ == "__main__":
    unittest.main()
