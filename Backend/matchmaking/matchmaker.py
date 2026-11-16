"""
Matchmaking rules for Tangy Trickshotter. No networking in this file, so it can be tested directly.

A player asks to play a mode and gets a ticket. Tickets wait in a queue per mode. When enough
players are waiting, the matchmaker takes them out of the queue, splits them into two teams of
even skill, and attaches a match to each ticket. The client finds out by polling its ticket.

STATUS: work in progress. See README.md for what is and is not finished.
"""
import time
import uuid
from dataclasses import dataclass, field


@dataclass(frozen=True)
class ModeConfig:
    team_size: int       # players per team in a full match
    min_team_size: int   # smallest team allowed once players have waited long enough
    map: str


MODES = {
    "tdm": ModeConfig(team_size=4, min_team_size=2, map="skyline"),
    "elimination": ModeConfig(team_size=3, min_team_size=2, map="range"),
}

SEARCHING = "searching"
MATCHED = "matched"
CANCELLED = "cancelled"
EXPIRED = "expired"


@dataclass
class Ticket:
    ticket_id: str
    player_id: str
    name: str
    mode: str
    skill: int
    created: float
    last_seen: float
    status: str = SEARCHING
    match_id: str = None
    team: str = None


@dataclass
class Match:
    match_id: str
    mode: str
    map: str
    created: float
    players: list = field(default_factory=list)  # dicts: playerId, name, team
    server: str = None                            # address of the game server, once one exists


class NoServerAllocator:
    """Placeholder. There are no game servers yet, so every match is left without an address."""

    def allocate(self, match):
        return None


class Matchmaker:
    def __init__(self, modes=None, clock=time.monotonic, ticket_ttl=15.0, relax_after=30.0,
                 match_ttl=600.0, allocator=None):
        self.modes = modes or MODES
        self.clock = clock
        self.ticket_ttl = ticket_ttl      # a ticket nobody polls for this long is dropped
        self.relax_after = relax_after    # wait this long before accepting smaller teams
        self.match_ttl = match_ttl        # finished-with matches are forgotten after this long
        self.allocator = allocator or NoServerAllocator()
        self.tickets = {}
        self.matches = {}
        self.by_player = {}

    # ------------------------------------------------------------------ player actions

    def enqueue(self, player_id, name, mode, skill=1000):
        """Puts a player in the queue for a mode and returns their ticket."""
        if mode not in self.modes:
            raise ValueError("unknown mode: %s" % mode)
        if not player_id:
            raise ValueError("playerId is required")

        # One live ticket per player: asking again replaces the old one.
        old = self.tickets.get(self.by_player.get(player_id))
        if old is not None and old.status == SEARCHING:
            old.status = CANCELLED

        now = self.clock()
        ticket = Ticket(ticket_id=uuid.uuid4().hex, player_id=player_id, name=(name or "Player")[:24],
                        mode=mode, skill=int(skill), created=now, last_seen=now)
        self.tickets[ticket.ticket_id] = ticket
        self.by_player[player_id] = ticket.ticket_id
        return ticket

    def poll(self, ticket_id):
        """Returns the ticket's current state as a dict, or None if there is no such ticket.
        Polling also counts as a heartbeat."""
        ticket = self.tickets.get(ticket_id)
        if ticket is None:
            return None
        now = self.clock()
        if ticket.status == SEARCHING:
            ticket.last_seen = now

        state = {
            "ticketId": ticket.ticket_id,
            "status": ticket.status,
            "mode": ticket.mode,
            "waited": round(now - ticket.created, 1),
            "position": self._position(ticket),
        }
        match = self.matches.get(ticket.match_id)
        if match is not None:
            state["match"] = {
                "matchId": match.match_id,
                "mode": match.mode,
                "map": match.map,
                "team": ticket.team,
                "server": match.server or "",
                "players": match.players,
            }
        return state

    def cancel(self, ticket_id):
        ticket = self.tickets.get(ticket_id)
        if ticket is None:
            return False
        if ticket.status == SEARCHING:
            ticket.status = CANCELLED
        return True

    # ------------------------------------------------------------------ the matchmaking pass

    def tick(self):
        """Run regularly. Drops stale tickets, forms every match it can, and returns the new matches."""
        now = self.clock()
        self._expire(now)

        formed = []
        for mode, config in self.modes.items():
            while True:
                waiting = self._waiting(mode)
                chosen = self._choose(waiting, config, now)
                if not chosen:
                    break
                formed.append(self._create_match(mode, config, chosen, now))
        return formed

    def stats(self):
        searching = {mode: len(self._waiting(mode)) for mode in self.modes}
        return {"searching": searching, "matches": len(self.matches), "tickets": len(self.tickets)}

    # ------------------------------------------------------------------ internals

    def _waiting(self, mode):
        tickets = [t for t in self.tickets.values() if t.status == SEARCHING and t.mode == mode]
        tickets.sort(key=lambda t: t.created)
        return tickets

    def _position(self, ticket):
        if ticket.status != SEARCHING:
            return 0
        return self._waiting(ticket.mode).index(ticket) + 1

    def _choose(self, waiting, config, now):
        """Picks the tickets for the next match, longest wait first, or [] if there are too few."""
        full = config.team_size * 2
        if len(waiting) >= full:
            return waiting[:full]

        # Not enough for full teams. Once the longest wait passes relax_after, accept smaller, even teams.
        smallest = config.min_team_size * 2
        if len(waiting) >= smallest and now - waiting[0].created >= self.relax_after:
            return waiting[:len(waiting) - len(waiting) % 2]
        return []

    def _create_match(self, mode, config, tickets, now):
        match = Match(match_id=uuid.uuid4().hex, mode=mode, map=config.map, created=now)

        # Snake draft by skill (A, B, B, A, A, B ...) so the teams come out close in total skill.
        ranked = sorted(tickets, key=lambda t: t.skill, reverse=True)
        for i, ticket in enumerate(ranked):
            team = "blue" if i % 4 in (0, 3) else "red"
            ticket.status = MATCHED
            ticket.match_id = match.match_id
            ticket.team = team
            match.players.append({"playerId": ticket.player_id, "name": ticket.name, "team": team})

        match.server = self.allocator.allocate(match)
        self.matches[match.match_id] = match
        return match

    def _expire(self, now):
        for ticket in self.tickets.values():
            if ticket.status == SEARCHING and now - ticket.last_seen > self.ticket_ttl:
                ticket.status = EXPIRED

        # Forget old matches, and tickets that are finished and no longer point at a live match.
        for match_id in [m.match_id for m in self.matches.values() if now - m.created > self.match_ttl]:
            del self.matches[match_id]
        for ticket_id, ticket in list(self.tickets.items()):
            finished = ticket.status in (CANCELLED, EXPIRED) or (
                ticket.status == MATCHED and ticket.match_id not in self.matches)
            if finished and now - ticket.last_seen > self.ticket_ttl * 4:
                del self.tickets[ticket_id]
                if self.by_player.get(ticket.player_id) == ticket_id:
                    del self.by_player[ticket.player_id]
