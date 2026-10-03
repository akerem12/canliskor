import { useEffect, useState } from 'react'
import type { Match } from './api/types'
import { isInPlay } from './api/types'
import { ConnectionBadge } from './components/ConnectionBadge'
import { DateNav, MaxDaysAway } from './components/DateNav'
import { LeagueSection } from './components/LeagueSection'
import { MatchDetailDialog } from './components/MatchDetailDialog'
import { useLiveScores } from './live/useLiveScores'
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
  window.history.replaceState(null, '', url)
}

interface OpenMatch {
  leagueCode: string
  matchId: string
}

/** ?league=tur.1&match=401888379 → the match whose details are open, so the link can be shared. */
function openMatchFromUrl(): OpenMatch | null {
  const params = new URLSearchParams(window.location.search)
  const leagueCode = params.get('league')
  const matchId = params.get('match')
  return leagueCode && matchId ? { leagueCode, matchId } : null
}

function writeOpenMatchToUrl(open: OpenMatch | null) {
  const url = new URL(window.location.href)
  if (open) {
    url.searchParams.set('league', open.leagueCode)
    url.searchParams.set('match', open.matchId)
    // A history entry of its own, so Back (e.g. on a phone) closes the dialog instead of leaving the page.
    window.history.pushState({ matchDialog: true }, '', url)
  } else {
    url.searchParams.delete('league')
    url.searchParams.delete('match')
    window.history.replaceState(null, '', url)
  }
}

export default function App() {
  const [dayOffset, setDayOffsetState] = useState(offsetFromUrl)
  const setDayOffset = (offset: number) => {
    setDayOffsetState(offset)
    writeOffsetToUrl(offset)
  }
  const { leagues, loaded, status, date, recentGoals, error } = useLiveScores(dayOffset)
  const [filter, setFilter] = useState<Filter>('all')
  const [openMatch, setOpenMatch] = useState(openMatchFromUrl)

  useEffect(() => {
    const onPopState = () => setOpenMatch(openMatchFromUrl())
    window.addEventListener('popstate', onPopState)
    return () => window.removeEventListener('popstate', onPopState)
  }, [])

  const showMatch = (match: Match) => {
    const open = { leagueCode: match.leagueCode, matchId: match.id }
    setOpenMatch(open)
    writeOpenMatchToUrl(open)
  }
  const closeMatch = () => {
    // Opened from the list: go back to the entry we pushed (popstate then clears the state). Opened from a link: no such entry.
    if ((window.history.state as { matchDialog?: boolean } | null)?.matchDialog) {
      window.history.back()
    } else {
      setOpenMatch(null)
      writeOpenMatchToUrl(null)
    }
  }
  const openLeague = openMatch && leagues.find(l => l.code === openMatch.leagueCode)

  // The Live tab only means something today; other days always show everything.
  const isToday = dayOffset === 0
  const effectiveFilter: Filter = isToday ? filter : 'all'

  const liveLeagues = leagues
    .map(l => ({ ...l, matches: l.matches.filter(m => isInPlay(m.status)) }))
    .filter(l => l.matches.length > 0)
  const liveCount = liveLeagues.reduce((n, l) => n + l.matches.length, 0)
  const shown = effectiveFilter === 'live' ? liveLeagues : leagues

  const emptyMessage = effectiveFilter === 'live'
    ? 'No matches in play right now.'
    : isToday ? 'No matches today.' : 'No matches on this day.'

  return (
    <div className="app">
      <header className="topbar">
        <div className="topbar__title">
          <h1>CanlıSkor</h1>
          <span className="topbar__date">{formatLongDate(date)}</span>
        </div>
        <ConnectionBadge status={status} />
      </header>

      <DateNav offset={dayOffset} onChange={setDayOffset} />

      {isToday && (
        <nav className="tabs" aria-label="Filter matches">
          <button className={filter === 'all' ? 'tab tab--active' : 'tab'} onClick={() => setFilter('all')}>
            All matches
          </button>
          <button className={filter === 'live' ? 'tab tab--active' : 'tab'} onClick={() => setFilter('live')}>
            Live <span className="tab__count">{liveCount}</span>
          </button>
        </nav>
      )}

      {/* While another day loads, the previous one stays visible but dimmed. */}
      <main className={loaded ? undefined : 'is-loading'} aria-busy={!loaded}>
        {error && status === 'offline' && <p className="notice notice--error">Can't reach the server ({error}).</p>}
        {!loaded && leagues.length === 0 ? (
          <p className="notice">Loading matches…</p>
        ) : shown.length === 0 ? (
          <p className="notice">{loaded ? emptyMessage : 'Loading matches…'}</p>
        ) : (
          shown.map(league => <LeagueSection key={league.code} league={league} recentGoals={recentGoals} onOpenMatch={showMatch} />)
        )}
      </main>

      {openMatch && (
        <MatchDetailDialog
          // A different match starts with a fresh dialog (no flash of the previous one's events).
          key={`${openMatch.leagueCode}/${openMatch.matchId}`}
          leagueCode={openMatch.leagueCode}
          matchId={openMatch.matchId}
          leagueName={openLeague?.name}
          pushed={openLeague?.matches.find(m => m.id === openMatch.matchId)}
          onClose={closeMatch}
        />
      )}

      <footer className="footer">Times in Istanbul time · Data: ESPN</footer>
    </div>
  )
}
