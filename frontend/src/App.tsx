import { useState } from 'react'
import { isInPlay } from './api/types'
import { ConnectionBadge } from './components/ConnectionBadge'
import { LeagueSection } from './components/LeagueSection'
import { useLiveScores } from './live/useLiveScores'
import { formatLongDate } from './time'

type Filter = 'all' | 'live'

export default function App() {
  const { leagues, loaded, status, date, recentGoals, error } = useLiveScores()
  const [filter, setFilter] = useState<Filter>('all')

  const liveLeagues = leagues
    .map(l => ({ ...l, matches: l.matches.filter(m => isInPlay(m.status)) }))
    .filter(l => l.matches.length > 0)
  const liveCount = liveLeagues.reduce((n, l) => n + l.matches.length, 0)
  const shown = filter === 'live' ? liveLeagues : leagues

  return (
    <div className="app">
      <header className="topbar">
        <div className="topbar__title">
          <h1>CanlıSkor</h1>
          <span className="topbar__date">{formatLongDate(date)}</span>
        </div>
        <ConnectionBadge status={status} />
      </header>

      <nav className="tabs" aria-label="Filter matches">
        <button className={filter === 'all' ? 'tab tab--active' : 'tab'} onClick={() => setFilter('all')}>
          All matches
        </button>
        <button className={filter === 'live' ? 'tab tab--active' : 'tab'} onClick={() => setFilter('live')}>
          Live <span className="tab__count">{liveCount}</span>
        </button>
      </nav>

      <main>
        {error && status === 'offline' && <p className="notice notice--error">Can't reach the server ({error}).</p>}
        {!loaded && leagues.length === 0 ? (
          <p className="notice">Loading matches…</p>
        ) : shown.length === 0 ? (
          <p className="notice">{filter === 'live' ? 'No matches in play right now.' : 'No matches today.'}</p>
        ) : (
          shown.map(league => <LeagueSection key={league.code} league={league} recentGoals={recentGoals} />)
        )}
      </main>

      <footer className="footer">Times in Istanbul time · Data: ESPN</footer>
    </div>
  )
}
