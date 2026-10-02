# CanliSkor — Live Football Scores

A Maçkolik-style web app for following live football scores in real time: Turkish Süper Lig plus major European leagues.
Portfolio project focused on backend design: background polling, caching, real-time push (SignalR), resilient external API integration and clean architecture.

> Status: **work in progress** (step 1: domain + ESPN provider).

## Architecture

```
backend/
  src/
    CanliSkor.Core/            Domain models + abstractions (no external dependencies)
    CanliSkor.Infrastructure/  ESPN client — the only code that knows ESPN's JSON format
    CanliSkor.Api/             ASP.NET Core host (REST, SignalR, background polling)
  tests/
    CanliSkor.Core.Tests/
    CanliSkor.Infrastructure.Tests/   mapping tests against real, saved ESPN responses
frontend/                      React + TypeScript (planned)
```

Dependencies point inward: `Api → Infrastructure → Core`. ESPN's API is unofficial and undocumented, so it sits behind
`IFootballDataProvider`; replacing it means writing one new implementation.

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

## Run

```bash
cd backend
dotnet test                                   # run all tests
dotnet run --project src/CanliSkor.Api        # http://localhost:5272
```

Try it: <http://localhost:5272/debug/scoreboard/tur.1?date=2026-09-20> (temporary debug endpoint).
