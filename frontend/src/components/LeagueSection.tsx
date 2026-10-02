import type { LeagueMatches } from '../api/types'
import { formatTime } from '../time'
import { MatchRow } from './MatchRow'

export function LeagueSection({ league, recentGoals }: { league: LeagueMatches; recentGoals: ReadonlySet<string> }) {
  return (
    <section className="league">
      <header className="league__header">
        <h2>{league.name}</h2>
        <span className="league__updated" title="Last fetched from the data source">
          updated {formatTime(league.lastUpdatedUtc)}
        </span>
      </header>
      <ul className="league__matches">
        {league.matches.map(match => (
          <MatchRow key={match.id} match={match} justScored={recentGoals.has(match.id)} />
        ))}
      </ul>
    </section>
  )
}
