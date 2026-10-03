import { useCallback, useEffect, useState } from 'react'
import { isInPlay } from './api/types'
import { Skeleton, EmptyState } from './components/common'
import { ConnectionBadge } from './components/ConnectionBadge'
import { DateNav, MaxDaysAway } from './components/DateNav'
import { LeaguePage, LeaguesPage } from './components/LeaguePages'
import { LeagueSection } from './components/LeagueSection'
import { MatchPage } from './components/MatchPage'
import { TeamPage } from './components/TeamPage'
import { useLiveScores } from './live/useLiveScores'
import type { Route } from './route'
import { routeFromSearch, routeToSearch, sameRoute } from './route'
import { addDays, formatLongDate, istanbulToday } from './time'

type Filter = 'all' | 'live'

/** ?date=2026-10-10 → days from today, so a shared link or a reload opens the same day. Invalid or too far: today. */
function offsetFromUrl(): number {
  const date = new URLSearchParams(window.location.search).get('date')
  if (!date || !/^\d{4}-\d{2}-\d{2}$/.test(date)) return 0
  const offset = Math.round((Date.parse(`${date}T00:00:00Z`) - Date.parse(`${istanbulToday()}T00:00:00Z`)) / 86_400_000)
  return Math.abs(offset) <= MaxDaysAway ? offset : 0
}

function writeOffsetToUrl(offset: number) {
  const url = new URL(window.location.href)
  if (offset === 0) url.searchParams.delete('date')
  else url.searchParams.set('date', addDays(istanbulToday(), offset))
  window.history.replaceState(window.history.state, '', url)
}

/** Marks history entries we pushed, so Back inside the app can tell them from the page the visitor came from. */
interface AppHistoryState {
  inApp?: boolean
}

/** The open page, in step with the URL and the browser's Back and Forward. */
function useRoute() {
  const [route, setRoute] = useState(() => routeFromSearch(window.location.search))

  useEffect(() => {
    const onPopState = () => setRoute(routeFromSearch(window.location.search))
    window.addEventListener('popstate', onPopState)
    return () => window.removeEventListener('popstate', onPopState)
  }, [])

  const navigate = useCallback((next: Route) => {
    if (sameRoute(next, routeFromSearch(window.location.search))) return
    const url = window.location.pathname + routeToSearch(next, window.location.search)
    window.history.pushState({ inApp: true } satisfies AppHistoryState, '', url)
    setRoute(next)
    window.scrollTo(0, 0)
  }, [])

  /** Back to where the visitor was. Arrived by a link, there is no such page: the matches of the day instead. */
  const back = useCallback(() => {
    if ((window.history.state as AppHistoryState | null)?.inApp) {
      window.history.back()
    } else {
      const home: Route = { view: 'matches' }
      window.history.replaceState(null, '', window.location.pathname + routeToSearch(home, window.location.search))
      setRoute(home)
    }
  }, [])

  return { route, navigate, back }
}

export default function App() {
  const [dayOffset, setDayOffsetState] = useState(offsetFromUrl)
  const setDayOffset = (offset: number) => {
    setDayOffsetState(offset)
    writeOffsetToUrl(offset)
  }
  const { leagues, loaded, status, date, recentGoals, error, watchMatch } = useLiveScores(dayOffset)
  const [filter, setFilter] = useState<Filter>('all')
  const { route, navigate, back } = useRoute()

  const leagueOf = (code: string) => leagues.find(l => l.code === code)

  return (
    <div className="app">
      <header className="topbar">
        <div className="topbar__title">
          <h1>
            <button className="topbar__home" onClick={() => navigate({ view: 'matches' })}>CanlıSkor</button>
          </h1>
          <span className="topbar__date">{formatLongDate(date)}</span>
        </div>
        <ConnectionBadge status={status} />
      </header>

      <nav className="mainnav" aria-label="Sections">
        <button
          className={route.view === 'matches' || route.view === 'match' ? 'mainnav__item mainnav__item--active' : 'mainnav__item'}
          onClick={() => navigate({ view: 'matches' })}
        >
          Matches
        </button>
        <button
          className={route.view === 'leagues' || route.view === 'league' || route.view === 'team' ? 'mainnav__item mainnav__item--active' : 'mainnav__item'}
          onClick={() => navigate({ view: 'leagues' })}
        >
          Leagues &amp; teams
        </button>
      </nav>

      {/* Keyed by page, so each one fades in and starts with its own state. */}
      <div className="page-enter" key={JSON.stringify(route)}>
        {route.view === 'match' && (
          <MatchPage
            leagueCode={route.leagueCode}
            matchId={route.matchId}
            leagueName={leagueOf(route.leagueCode)?.name}
            pushed={leagueOf(route.leagueCode)?.matches.find(m => m.id === route.matchId)}
            watchMatch={watchMatch}
            onNavigate={navigate}
            onClose={back}
          />
        )}
        {route.view === 'team' && (
          <TeamPage
            leagueCode={route.leagueCode}
            teamId={route.teamId}
            leagueName={leagueOf(route.leagueCode)?.name}
            onNavigate={navigate}
            onBack={back}
          />
        )}
        {route.view === 'league' && (
          <LeaguePage leagueCode={route.leagueCode} leagueName={leagueOf(route.leagueCode)?.name} onNavigate={navigate} onBack={back} />
        )}
        {route.view === 'leagues' && <LeaguesPage onNavigate={navigate} />}
        {route.view === 'matches' && (
          <MatchList
            dayOffset={dayOffset}
            onDayChange={setDayOffset}
            filter={filter}
            onFilterChange={setFilter}
            leagues={leagues}
            loaded={loaded}
            offline={status === 'offline'}
            error={error}
            recentGoals={recentGoals}
            onNavigate={navigate}
          />
        )}
      </div>

      <footer className="footer">Times in Istanbul time · Data: ESPN</footer>
    </div>
  )
}

interface MatchListProps {
  dayOffset: number
  onDayChange: (offset: number) => void
  filter: Filter
  onFilterChange: (filter: Filter) => void
  leagues: ReturnType<typeof useLiveScores>['leagues']
  loaded: boolean
  offline: boolean
  error: string | null
  recentGoals: ReadonlySet<string>
  onNavigate: (route: Route) => void
}

/** The matches of one day, grouped by league. Leagues without a match that day aren't listed here. */
function MatchList({ dayOffset, onDayChange, filter, onFilterChange, leagues, loaded, offline, error, recentGoals, onNavigate }: MatchListProps) {
  // The Live tab only means something today; other days always show everything.
  const isToday = dayOffset === 0
  const effectiveFilter: Filter = isToday ? filter : 'all'

  const liveLeagues = leagues
    .map(l => ({ ...l, matches: l.matches.filter(m => isInPlay(m.status)) }))
    .filter(l => l.matches.length > 0)
  const liveCount = liveLeagues.reduce((n, l) => n + l.matches.length, 0)
  const shown = effectiveFilter === 'live' ? liveLeagues : leagues

  const emptyTitle = effectiveFilter === 'live'
    ? 'No matches in play right now.'
    : isToday ? 'No matches today.' : 'No matches on this day.'

  return (
    <>
      <DateNav offset={dayOffset} onChange={onDayChange} />

      {isToday && (
        <nav className="tabs" aria-label="Filter matches">
          <button className={filter === 'all' ? 'tab tab--active' : 'tab'} onClick={() => onFilterChange('all')}>
            All matches
          </button>
          <button className={filter === 'live' ? 'tab tab--active' : 'tab'} onClick={() => onFilterChange('live')}>
            Live <span className="tab__count">{liveCount}</span>
          </button>
        </nav>
      )}

      {/* While another day loads, the previous one stays visible but dimmed. */}
      <main className={loaded ? undefined : 'is-loading'} aria-busy={!loaded}>
        {error && offline && <p className="notice notice--error">Can't reach the server ({error}).</p>}
        {!loaded && leagues.length === 0 ? (
          <MatchListSkeleton />
        ) : shown.length === 0 ? (
          <div className="panel">
            <EmptyState
              icon="📅"
              title={emptyTitle}
              hint={effectiveFilter === 'live'
                ? 'Live matches show up here as soon as they kick off.'
                : 'Try another day, or open Leagues & teams for tables and fixtures.'}
            />
          </div>
        ) : (
          shown.map(league => (
            <LeagueSection key={league.code} league={league} recentGoals={recentGoals} onNavigate={onNavigate} />
          ))
        )}
      </main>
    </>
  )
}

function MatchListSkeleton() {
  return (
    <div aria-label="Loading matches">
      {[5, 3].map(rows => (
        <section key={rows} className="league league--skeleton">
          <Skeleton rows={rows} height={22} />
        </section>
      ))}
    </div>
  )
}
