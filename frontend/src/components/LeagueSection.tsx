import type { LeagueMatches } from '../api/types'
import { FavoriteButton } from '../favorites/FavoriteButton'
import { useI18n } from '../i18n/useI18n'
import type { Route } from '../route'
import { formatTime } from '../time'
import { MatchRow } from './MatchRow'

interface Props {
  league: LeagueMatches
  recentGoals: ReadonlySet<string>
  onNavigate: (route: Route) => void
}

export function LeagueSection({ league, recentGoals, onNavigate }: Props) {
  const { t } = useI18n()

  return (
    <section className="league">
      <header className="league__header">
        <h2>
          <button className="league__name" onClick={() => onNavigate({ view: 'league', leagueCode: league.code })} title={t.matchList.tableAndTeams}>
            {league.name} <span aria-hidden>›</span>
          </button>
          <FavoriteButton league={{ code: league.code, name: league.name }} />
        </h2>
        <span className="league__updated" title={t.matchList.fetchedTitle}>
          {t.common.updated(formatTime(league.lastUpdatedUtc))}
        </span>
      </header>
      <ul className="league__matches">
        {league.matches.map(match => (
          <MatchRow
            key={match.id}
            match={match}
            justScored={recentGoals.has(match.id)}
            onOpen={() => onNavigate({ view: 'match', leagueCode: match.leagueCode, matchId: match.id })}
          />
        ))}
      </ul>
    </section>
  )
}
