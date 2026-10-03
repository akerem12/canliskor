# CanliSkor — Live Football Scores

[![CI](https://github.com/akerem12/canliskor/actions/workflows/ci.yml/badge.svg)](https://github.com/akerem12/canliskor/actions/workflows/ci.yml)

A Maçkolik-style web app for following live football scores in real time: Turkish Süper Lig, major European and South American leagues, the UEFA Nations League and international friendlies.
Portfolio project focused on backend design: background polling, caching, real-time push (SignalR), resilient external API integration and clean architecture.

> Status: **work in progress** (step 17: match odds, player profiles, assists on goals and a dark navy theme).

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
frontend/                      React + TypeScript (Vite): today's matches, live tab, goal highlights, match details
  src/api/                     REST client + types mirroring the API contracts
  src/live/                    SignalR hook, match detail hook + pure state reducer (unit-tested with Vitest)
```

Dependencies point inward: `Api → Infrastructure → Core`. ESPN's API is unofficial and undocumented, so it sits behind
`IFootballDataProvider`; replacing it means writing one new implementation.

### How data flows

```
ESPN ──► ScoreboardPollingWorker ──► IMatchStore (cache) ──► REST API ──────► clients (initial state)
         (BackgroundService)    │
                                └─► MatchChangeDetector ──► SignalR hub ──► clients (changes only)
```

Only the backend calls ESPN. Today and live data are served from the cache, so that traffic never adds load on the
external API.
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

### Browsing other days

The poller only covers today (and yesterday while games run past midnight). Other days, at most a week away, are
loaded on first request by `OnDemandScoreboardLoader` and cached in the same store:

- a past day whose matches are all over is final and never fetched again;
- fixture lists are refetched at most every 30 minutes (kickoff times and postponements can change);
- only one on-demand fetch runs at a time across the app, so browsing can't burst requests at ESPN;
- if ESPN fails, the last cached copy is served.

The frontend keeps the selected day in the URL (`/?date=2026-10-10`), so links can be shared.

### Match details

Clicking a match opens its page: line-ups, events (goals with assists, penalties and own goals, cards,
substitutions), team statistics and both squads, as tabs.
They come from ESPN's match summary, loaded on request by `MatchDetailService` and cached in the store. How long
a cached detail is used depends on the match:

| Match | Refetched after |
|---|---|
| Live (or past kickoff) | `LiveInterval` (30 s): any number of viewers cost one ESPN call per interval |
| Scheduled | 30 minutes |
| Finished, postponed, cancelled | 6 hours (only late corrections can change) |

A cached detail is also stale as soon as the poller's scoreboard shows a different score or status for the match
(after a 5 s minimum, as ESPN's summary can lag its scoreboard), so a pushed goal is followed by a detail that has
the scorer. Detail fetches share the one-at-a-time gate with browsing. Only followed leagues and numeric ids are
accepted, and ESPN's summary is checked to belong to the requested league (ESPN serves any match under any league's URL).

An open match page is kept current by the server. The page subscribes to its match on the SignalR hub;
`MatchViewerRegistry` remembers which matches have viewers, and after each scoreboard poll `LiveDetailRefresher`
reloads the detail of every watched match that is in play and pushes it to that match's group. So cards,
substitutions and statistics arrive within one poll interval without the page asking, any number of viewers
cost one ESPN call per match per poll, and matches nobody is looking at cost none. The page also reloads its detail
when a score or status change is pushed, which covers kickoff and full time. The open match and tab are kept in the
URL (`/?league=esp.1&match=401882858&tab=squads`); Back returns to the match list.

### Line-ups

The same summary carries both rosters, mapped by `EspnLineupMapper`. ESPN gives every starter a position (`CD-L`,
`DM`, `AM-R`, ...) and the team a formation (`4-2-3-1`), but not who stands where, so the rows are worked out:
starters are ordered from defence to attack, cut into rows of the formation's sizes, and each row is ordered left to
right. If the formation is missing or doesn't add up to the outfield players, players of the same depth form a row.
Substitutes are all listed as `SUB`, so one who came on takes the position group of the player they replaced.
Line-ups are `null` until both are announced, about an hour before kickoff.

The frontend draws both elevens on a vertical pitch, home team at the top, with shirt number, goals, cards
and the minute a player went off; the substitutes are listed below. If both teams' shirt colours are too alike, the
away team is drawn in white or black. Clicking a player shows their minutes and statistics. Minutes played aren't in the data, so `PlayingTime` works
them out from when a player came on, went off or was sent off and how far the match is.

### Squads

The Squads tab lists both teams' registered players for the season, grouped by position, with shirt number,
nationality and age. They come from ESPN's team roster through `SquadService`, cached for 6 hours (a squad only
changes with a transfer), behind the same one-at-a-time gate and followed-leagues rule as match details. ESPN has no
usable data on coaches or on injured and suspended players, so the site doesn't show them.

### Leagues, tables and teams

The match list only shows leagues that have a match on the selected day, so a league disappears from it on days it
doesn't play (the South American leagues had no matches at all from 28 September to 1 October 2026). "Leagues &
teams" lists every followed competition at all times: its table, and from there every team.

A team page shows standing, stadium, the last five results with a W/D/L form guide, every match the team has
still to play, the table around it and the squad. Results and fixtures cover all the team's competitions (league,
cups, Europe), each labelled; matches of competitions the site doesn't follow are listed but can't be opened. Team names in a match's scoreline and in tables lead to
it; league names lead to the table. Results, fixtures and stadium come from ESPN's all-competitions team schedule (two calls, cached
15 minutes), tables from ESPN's standings API (cached 5 minutes), both through `LeagueInfoService` behind the same
one-at-a-time gate and followed-leagues rule as match details. The stadium is the venue of most of the team's home
matches; national teams have none. ESPN has no founding year for clubs, so the site doesn't show one.

Every page has a link: `/?league=tur.1` (table), `/?league=tur.1&team=1895` (team), `/?view=leagues`. While data is
on its way pages show skeleton placeholders; a failed load shows what went wrong with a retry button.

### Favourites

A star next to every team (in tables and on team pages) and every league (in the league list, on league pages and
above each league's matches) adds it to the favourites, or takes it out again. Favourites live in the browser's
`localStorage` (`canliskor.favorites.v1`), so they survive a reload and need no account; a click shows at once
everywhere on the page, and other open tabs follow.

The Favourites page collects the upcoming matches of all favourite teams (every competition they play in) and
favourite leagues (this month and next) into one list, soonest first, each match once: competition, date and time,
both teams with crests, the stadium, and links to the match and to both teams. A bar of chips at the top, one per
favourite, narrows the list to that team or league. Without favourites the page explains how to pick some; if one
favourite fails to load, the others are still shown and the page says which one is missing, with a retry.

On the day's match list the matches of favourite teams are collected in a "Your teams" section on top, and
favourite leagues come before the others (`pinned.ts`).

The code is in `frontend/src/favorites`: `favorites.ts` (data and pure operations), `FavoritesProvider.tsx` and
`useFavorites.ts` (state and storage), `FavoriteButton.tsx` (the star), `feed.ts` (merging and filtering matches)
and `FavoritesPage.tsx`.

### Match alerts

Browser notifications for goals, kick-off, half time, the second half and full time (and a goal ruled out, a match
postponed or cancelled). Two ways to get them:

- **Favourite teams:** one switch on the Favourites page turns alerts on for every match of every favourite team.
  Favourite leagues never alert, as that would be every goal of a league.
- **A single match:** the bell on a match's page ("Notify me") turns alerts on for that match only, whoever plays.
  It is forgotten a day after kickoff.

Alerts are made in the browser from the `MatchUpdated` messages the page already receives over SignalR
(`frontend/src/alerts`: `alerts.ts` decides what is news and words it, `useAlerts.ts` holds the settings in
`localStorage` and shows the notification, `AlertControls.tsx` has the switch and the bell). So they need the
browser's permission, which is asked for on first use, and they work while the site is open in a tab, also in the
background, but not once the tab or the browser is closed: that would take a service worker and Web Push.
Clicking a notification opens the match.

### Match odds

Matches that haven't kicked off show the bookmaker's 1X2 prices (home win, draw, away win) as decimal odds on the
match list, on the Favourites feed and on the match page, with the favourite marked in gold. ESPN carries one
bookmaker's lines for every followed league, on the scoreboard (`odds`) and in the match summary (`pickcenter`),
so there is no second data source and no API key. ESPN quotes American odds; `EspnOddsMapper` converts them
(-450 → 1.22, +950 → 10.50). A match without posted odds simply shows none. The odds are information, not advice.

### Player profiles

Any player's name (in the squads, the line-up sheet, the match events) opens a profile dialog: name, number,
position, nationality with flag, age, height, club, and this season's numbers with one tab per competition
(league, cups, internationals). `GET /api/leagues/{code}/players/{id}` answers from ESPN's athlete page and its
overview, cached for 30 minutes. What ESPN doesn't have is left out rather than made up: there are no minutes
played and no passing figures for footballers, substitute appearances are only known for the player's main league,
and very few players have a portrait (everyone else gets a silhouette).

### Assists

A goal names who set it up, with an "A" badge; penalties and own goals never do. For some leagues (the Süper Lig
among them) ESPN's events name only the scorer, while the line-ups say how many assists each player had in the
match. `AssistAttribution` fills the gap where that leaves no doubt: a player with one assist who could have set up
exactly one of the team's goals (on the pitch at the time, not the scorer) set up that one. Anything ambiguous stays
open; the line-ups still mark every player who assisted.

### Team search

"Leagues & teams" has a search box that finds any team of the followed leagues by name, ignoring case and accents
("besik" finds Beşiktaş, "sao" finds São Paulo). `GET /api/teams/search?q=` answers from each league's team list
(ESPN's `/{league}/teams`, one call per league on the first search, then cached for a day); a league whose list
can't be loaded is left out. A club that plays in several followed competitions is listed once, under its domestic
league, so its page opens with its league table. Names that start with the query come first; at most 20 results.

## Data source

ESPN's public JSON API (no key required), e.g.
`https://site.api.espn.com/apis/site/v2/sports/soccer/tur.1/scoreboard?dates=YYYYMMDD`.

This is an unofficial, non-commercial hobby project. It is not affiliated with or endorsed by ESPN or by any club,
league or federation; team names and crests belong to their owners and are shown straight from ESPN's servers. The
API is undocumented and not offered for third-party use, so the app is deliberately gentle with it (everything is
cached, requests run one at a time) and will be taken down on request.

| League | Code |
|---|---|
| Turkish Süper Lig | `tur.1` |
| Premier League | `eng.1` |
| La Liga | `esp.1` |
| UEFA Champions League | `uefa.champions` |
| UEFA Europa League | `uefa.europa` |
| UEFA Conference League | `uefa.europa.conf` |
| UEFA Nations League | `uefa.nations` |
| CONMEBOL Libertadores | `conmebol.libertadores` |
| CONMEBOL Sudamericana | `conmebol.sudamericana` |
| Argentine Liga Profesional | `arg.1` |
| Brazilian Série A | `bra.1` |
| Colombian Primera A | `col.1` |
| Chilean Primera División | `chi.1` |
| International friendlies | `fifa.friendly` |

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

## Deploy (Docker)

One image holds the whole app: a multi-stage build compiles the React app, publishes the backend with the UI in
`wwwroot`, and runs it on the slim ASP.NET runtime image as a non-root user.

```bash
docker compose up --build                     # http://localhost:8080
# or
docker build -t canliskor . && docker run -p 8080:8080 canliskor
```

UI, REST API and SignalR hub share one origin, so production needs no CORS either. Vite's content-hashed files under
`/assets` are cached for a year (`immutable`); `index.html` is served with `no-cache`, so a new deploy is picked up
on the next page load. `GET /health` is available for container orchestrators and load balancers.
Settings can be overridden with environment variables, e.g. `Polling__LiveInterval=00:00:20`.

### Hosting on Render (free plan)

`render.yaml` describes one free web service built from the Dockerfile. In Render: sign in with GitHub, then
**New > Blueprint**, pick this repository and **Apply**. Every push to `main` redeploys. A free service is stopped
after about 15 minutes without visitors and needs up to a minute to start again on the next visit; while stopped it
doesn't poll, so the first page after a wake-up fills in as the first poll completes.

Without Docker: `npm run build` in `frontend/`, copy `frontend/dist/*` into `backend/src/CanliSkor.Api/wwwroot/`,
then `dotnet publish backend/src/CanliSkor.Api -c Release`. Start from an empty `dist`/`wwwroot`, because stale
assets from earlier builds would be published too. If you delete `wwwroot` afterwards, also delete
`backend/src/CanliSkor.Api/obj/Release`: the build keeps a manifest pointing at it, and the app won't start without it.

## CI

`.github/workflows/ci.yml` runs on every push to `main` and on pull requests:

| Job | What it checks |
|---|---|
| Backend | Release build with warnings as errors, all .NET tests |
| Frontend | `oxlint`, Vitest, production build |
| Docker | Builds the image (layer cache via GitHub Actions), starts it and checks `/health`, `/` and `/api/leagues` |

The Docker job only runs once both test jobs pass. The image is built but not pushed anywhere.

### How the frontend stays in sync

On every (re)connect it subscribes to all leagues **first** and only then loads `GET /api/matches` +
`GET /api/matches/live`. Updates arriving while that load is in flight are buffered and replayed on top of it, so
nothing is missed or overwritten by an older response. After a reconnect (groups are per connection) it resubscribes
and reloads; at Istanbul midnight it reloads for the new day. Yesterday's games still running past midnight are
merged in from `/live`.

## API

| Endpoint | Description |
|---|---|
| `GET /health` | Liveness check (`Healthy`) |
| `GET /api/leagues` | Followed league codes, in display order |
| `GET /api/matches` | Today's matches (Istanbul date), grouped by league |
| `GET /api/matches?date=2026-10-10` | Another day, up to 7 days back or ahead (else `400`) |
| `GET /api/matches/live` | Matches currently in play |
| `GET /api/leagues/{code}/matches/{id}` | One match with its events, statistics and line-ups (`404` if unknown, `503` if ESPN is down and nothing is cached) |
| `GET /api/competitions` | Followed leagues with their names: `[{ code, name }]` |
| `GET /api/leagues/{code}/standings` | The league table: `{ leagueCode, leagueName, groups, lastUpdatedUtc }`; each group `{ name, rows }`, each row `{ rank, team, played, wins, draws, losses, goalsFor, goalsAgainst, goalDifference, points, note, noteColor }`. No groups if the competition has no table |
| `GET /api/teams/search?q=gala` | Teams whose name contains the query (two characters or more): `[{ league: { code, name }, team }]` |
| `GET /api/leagues/{code}/fixtures` | The league's matches still to be played, this month and next, soonest first: `{ leagueCode, leagueName, matches, lastUpdatedUtc }` (cached 30 minutes) |
| `GET /api/leagues/{code}/teams/{teamId}` | A team: `{ team, isNationalTeam, standingSummary, stadium, stadiumCity, recentMatches, upcomingMatches, competitions, lastUpdatedUtc }`. Matches have the same shape as in `/api/matches` and cover every competition the team plays in; `competitions` names them (`[{ code, name }]`) |
| `GET /api/leagues/{code}/players/{playerId}` | A player: `{ id, name, jersey, position, nationality, flagUrl, age, heightCm, photoUrl, team, competitions, lastUpdatedUtc }`; each competition `{ name, leagueCode, teamName, starts, substituteAppearances, goals, assists, shots, shotsOnTarget, yellowCards, redCards, foulsCommitted, foulsSuffered, offsides, cleanSheets, saves, goalsConceded }` (the last three for goalkeepers only; `404` if unknown) |
| `GET /api/leagues/{code}/teams/{teamId}/squad` | A team's squad: `{ teamId, teamName, players, lastUpdatedUtc }`, each player `{ id, name, jersey, position, age, nationality }` (`404` if unknown, `503` if ESPN is down and nothing is cached) |

Kickoff times are returned in Istanbul time (`2026-10-09T20:00:00+03:00`), statuses as strings
(`Scheduled`, `Live`, `HalfTime`, `Finished`, `Postponed`, `Cancelled`), `score` is `null` before kickoff, and
`venue` is the stadium (`null` if ESPN has none).
Each league includes `lastUpdatedUtc` so clients can detect stale data.

A match detail is `{ match, events, stats, lineups, lastUpdatedUtc }`. Events are in match order:
`{ type, clock, side, player, relatedPlayer }` with `type` one of `Goal`, `PenaltyGoal`, `OwnGoal`, `YellowCard`,
`RedCard`, `Substitution`; `side` (`Home`/`Away`) is the team the event counts for, so an own goal is on the side of
the team that benefits. `relatedPlayer` is the assist provider or the player going off. `stats` are
`{ type, home, away }` for `Possession` (percent), `Shots`, `ShotsOnTarget`, `Corners`, `Fouls`, `Offsides`,
`YellowCards`, `RedCards`, `Saves`; empty before kickoff.

`lineups` is `{ home, away }` or `null`. Each team is `{ formation, shirtColor, rows, bench }`: `rows` is the
starting eleven, goalkeeper's row first, then defence to attack, each row from the team's own left to right; `bench`
is every substitute. A player is `{ id, name, shortName, jersey, position, cameOnAt, wentOffAt, sentOffAt, minutesPlayed, stats }` with
`position` one of `Goalkeeper`, `Defender`, `Midfielder`, `Forward` (`null` for an unused substitute) and `stats`
`{ goals, assists, shots, shotsOnTarget, foulsCommitted, foulsSuffered, offsides, yellowCards, redCards, ownGoals,
saves, goalsConceded }`, where `goalsConceded` counts goals conceded while the player was on the pitch.

### Real-time: SignalR hub `/hubs/live-scores`

| Direction | Name | Payload |
|---|---|---|
| client → server | `SubscribeToLeague(leagueCode)` | e.g. `"tur.1"`; unknown leagues are rejected |
| client → server | `UnsubscribeFromLeague(leagueCode)` | |
| server → client | `MatchUpdated` | `{ match, scoreChanged, statusChanged }` |
| client → server | `SubscribeToMatch(leagueCode, matchId)` | The client has this match's page open (at most 4 per connection) |
| client → server | `UnsubscribeFromMatch(leagueCode, matchId)` | |
| server → client | `MatchDetailUpdated` | The full match detail, same shape as `GET /api/leagues/{code}/matches/{id}`; sent after each poll while the match is in play |

`match` has the same shape as in the REST API. Both flags `false` means only the clock moved.
Recommended client flow: connect, subscribe, then load `GET /api/matches` and apply updates on top.

## Configuration

`backend/src/CanliSkor.Api/appsettings.json`:

```json
"Football": { "Leagues": [ "tur.1", "eng.1", "esp.1", "uefa.champions", "uefa.europa", "uefa.europa.conf", "uefa.nations", "conmebol.libertadores", "conmebol.sudamericana", "arg.1", "bra.1", "col.1", "chi.1", "fifa.friendly" ] },
"Polling": {
  "LiveInterval": "00:00:30",
  "IdleInterval": "00:15:00",
  "KickoffLeadTime": "00:02:00",
  "ErrorRetryInterval": "00:01:00"
}
```

Options are validated at startup; invalid values stop the app with a clear error.
