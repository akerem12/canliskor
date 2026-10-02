# CanliSkor — Live Football Scores

A Maçkolik-style web app for following live football scores in real time: Turkish Süper Lig plus major European leagues.
Portfolio project focused on backend design: background polling, caching, real-time push (SignalR), resilient external API integration and clean architecture.

> Status: **work in progress** (step 4: React frontend with live updates).

## Architecture

```
backend/
  src/
    CanliSkor.Core/            Domain models, abstractions, polling + query logic (no infrastructure dependencies)
    CanliSkor.Infrastructure/  ESPN client (the only code that knows ESPN's JSON) + in-memory store
    CanliSkor.Api/             ASP.NET Core host (REST, SignalR, background polling)
  tests/
    CanliSkor.Core.Tests/             polling interval, poller and query logic
    CanliSkor.Infrastructure.Tests/   mapping tests against real, saved ESPN responses
    CanliSkor.Api.Tests/              in-memory integration tests of the HTTP contract
frontend/                      React + TypeScript (Vite): today's matches, live tab, goal highlights
  src/api/                     REST client + types mirroring the API contracts
  src/live/                    SignalR hook + pure state reducer (unit-tested with Vitest)
```

Dependencies point inward: `Api → Infrastructure → Core`. ESPN's API is unofficial and undocumented, so it sits behind
`IFootballDataProvider`; replacing it means writing one new implementation.

### How data flows

```
ESPN ──► ScoreboardPollingWorker ──► IMatchStore (cache) ──► REST API ──────► clients (initial state)
         (BackgroundService)    │
                                └─► MatchChangeDetector ──► SignalR hub ──► clients (changes only)
```

Only the backend calls ESPN. Client requests are served from the cache, so traffic never adds load on the external API.
After each fetch the new scoreboard is compared with the cached one; only matches whose score, status or clock
changed are pushed, and only to clients subscribed to that league.

**Smart polling.** After each round the worker computes the next delay:

| Situation | Next poll |
|---|---|
| A match is live / at half time (or past kickoff but not yet flagged live) | `LiveInterval` (30 s) |
| Next kickoff is soon | just before kickoff (`KickoffLeadTime`) |
| Nothing happening | `IdleInterval` (15 min) |
| A fetch failed | at most `ErrorRetryInterval` (1 min); last good data keeps being served |

"Today" is the current date in Europe/Istanbul. Yesterday's scoreboard keeps being polled while it still has live
matches, so a game running past midnight updates until full time.

## Data source

ESPN's public JSON API (no key required), e.g.
`https://site.api.espn.com/apis/site/v2/sports/soccer/tur.1/scoreboard?dates=YYYYMMDD`.

| League | Code |
|---|---|
| Turkish Süper Lig | `tur.1` |
| Premier League | `eng.1` |
| La Liga | `esp.1` |
| UEFA Champions League | `uefa.champions` |
| UEFA Europa League | `uefa.europa` |

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) (pinned in `backend/global.json`)
- [Node.js](https://nodejs.org/) 22+ for the frontend

## Run

```bash
cd backend
dotnet test                                   # run all tests
dotnet run --project src/CanliSkor.Api        # http://localhost:5272
```

In a second terminal:

```bash
cd frontend
npm install
npm test                                      # reducer unit tests
npm run dev                                   # http://localhost:5173
```

The Vite dev server proxies `/api` and `/hubs` (including WebSockets) to the backend, so the browser only talks to one
origin and no CORS setup is needed.

### How the frontend stays in sync

On every (re)connect it subscribes to all leagues **first** and only then loads `GET /api/matches` +
`GET /api/matches/live`. Updates arriving while that load is in flight are buffered and replayed on top of it, so
nothing is missed or overwritten by an older response. After a reconnect (groups are per connection) it resubscribes
and reloads; at Istanbul midnight it reloads for the new day. Yesterday's games still running past midnight are
merged in from `/live`.

## API

| Endpoint | Description |
|---|---|
| `GET /api/leagues` | Followed league codes, in display order |
| `GET /api/matches` | Today's matches (Istanbul date), grouped by league |
| `GET /api/matches/live` | Matches currently in play |

Kickoff times are returned in Istanbul time (`2026-10-09T20:00:00+03:00`), statuses as strings
(`Scheduled`, `Live`, `HalfTime`, `Finished`, `Postponed`, `Cancelled`), and `score` is `null` before kickoff.
Each league includes `lastUpdatedUtc` so clients can detect stale data.

### Real-time: SignalR hub `/hubs/live-scores`

| Direction | Name | Payload |
|---|---|---|
| client → server | `SubscribeToLeague(leagueCode)` | e.g. `"tur.1"`; unknown leagues are rejected |
| client → server | `UnsubscribeFromLeague(leagueCode)` | |
| server → client | `MatchUpdated` | `{ match, scoreChanged, statusChanged }` |

`match` has the same shape as in the REST API. Both flags `false` means only the clock moved.
Recommended client flow: connect, subscribe, then load `GET /api/matches` and apply updates on top.

## Configuration

`backend/src/CanliSkor.Api/appsettings.json`:

```json
"Football": { "Leagues": [ "tur.1", "eng.1", "esp.1", "uefa.champions", "uefa.europa" ] },
"Polling": {
  "LiveInterval": "00:00:30",
  "IdleInterval": "00:15:00",
  "KickoffLeadTime": "00:02:00",
  "ErrorRetryInterval": "00:01:00"
}
```

Options are validated at startup; invalid values stop the app with a clear error.
