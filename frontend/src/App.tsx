import { useState } from 'react'
import { isInPlay } from './api/types'
import { ConnectionBadge } from './components/ConnectionBadge'
import { DateNav, MaxDaysAway } from './components/DateNav'
import { LeagueSection } from './components/LeagueSection'
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

export default function App() {
  const [dayOffset, setDayOffsetState] = useState(offsetFromUrl)
  const setDayOffset = (offset: number) => {
    setDayOffsetState(offset)
    writeOffsetToUrl(offset)
  }
  const { leagues, loaded, status, date, recentGoals, error } = useLiveScores(dayOffset)
  const [filter, setFilter] = useState<Filter>('all')

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
          shown.map(league => <LeagueSection key={league.code} league={league} recentGoals={recentGoals} />)
        )}
      </main>

      <footer className="footer">Times in Istanbul time · Data: ESPN</footer>
    </div>
  )
}
