# Matchmaking backend

**Status: work in progress, deliberately not switched on.** The queue and team-forming logic is
written and tested. The game does not use it yet, and nothing here is deployed.

Python 3, standard library only.

```bash
python server.py
```

```bash
python -m unittest -v
```

## How it works

1. The game sends `POST /v1/queue` with the player and the mode, and gets a ticket.
2. It polls `GET /v1/queue/<ticketId>` about once a second. Polling is also the heartbeat: a ticket
   nobody polls for 15 seconds expires.
3. Once a second the server forms every match it can. Matched tickets report `status: "matched"`
   with the match id, map, the player's team and the full player list.

| Mode | Key | Full match | Smallest match | Map |
| --- | --- | --- | --- | --- |
| Team Deathmatch | `tdm` | 4 v 4 | 2 v 2 | `skyline` |
| Elimination | `elimination` | 3 v 3 | 2 v 2 | `range` |

The longest-waiting players go first. If the queue cannot fill a full match, smaller even teams are
accepted once the longest wait passes 30 seconds. Teams are split by a snake draft on the `skill`
number each player sends (1000 if they send none), so team totals come out close.

## API

| Call | Body or result |
| --- | --- |
| `POST /v1/queue` | `{"playerId", "name", "mode", "skill"}` returns the ticket state, 201 |
| `GET /v1/queue/<ticketId>` | `{"ticketId", "status", "mode", "waited", "position", "match"}` |
| `DELETE /v1/queue/<ticketId>` | Leaves the queue |
| `GET /v1/health` | `{"ok": true}` |
| `GET /v1/stats` | Players searching per mode, live matches, tickets held |

`status` is `searching`, `matched`, `cancelled` or `expired`. A player has one live ticket: queueing
again cancels the previous one.

## Not done yet

These are what "ready" still needs. Until they exist the server binds to this machine only.

- **Game servers.** A match comes back with an empty `server` field. `NoServerAllocator` in
  `matchmaker.py` is the place to plug in whatever starts or assigns a game server. The game itself
  has no networked play yet either: enemies are local dummies.
- **Accounts and authentication.** Anyone can queue as any `playerId`, and `skill` is whatever the
  client says. Both have to come from a trusted source before this is public.
- **Persistence.** Tickets and matches live in memory and are lost on restart. One process only.
- **HTTPS, rate limiting, regions, parties.** None exist.

## Game side

`Assets/Trickshot/Scripts/MatchmakingClient.cs` talks to this API (queue, poll, cancel) and raises
`MatchFound`. It is not added to the scene and its `onlineEnabled` switch is off, so the menu still
starts the offline template modes.
