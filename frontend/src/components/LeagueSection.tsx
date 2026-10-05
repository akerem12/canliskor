import type { LeagueMatches } from '../api/types'
import { FavoriteButton } from '../favorites/FavoriteButton'
import { useI18n } from '../i18n/useI18n'
import type { Route } from '../route'
import { formatTime } from '../time'
import { MatchRow } from './MatchRow'

/** "tur.1" → Türkiye's flag. Competitions across countries ("uefa.champions") have none. */
function countryFlagUrl(leagueCode: string): string | null {
  const country = /^([a-z]{3})\.\d+$/.exec(leagueCode)?.[1]
  return country ? `https://a.espncdn.com/i/teamlogos/countries/500/${country}.png` : null
}

interface Props {
  league: LeagueMatches
  recentGoals: ReadonlySet<string>
  showOdds: boolean
  onNavigate: (route: Route) => void
}

export function LeagueSection({ league, recentGoals, showOdds, onNavigate }: Props) {
  const { t } = useI18n()
  const flagUrl = countryFlagUrl(league.code)

  return (
    <section className="league">
      <header className="league__header">
        <h2>
          {flagUrl
            ? <img className="league__icon" src={flagUrl} alt="" width={16} height={16} loading="lazy" />
            : <span className="league__icon" aria-hidden>🌍</span>}
          <button className="league__name" onClick={() => onNavigate({ view: 'league', leagueCode: league.code })} title={t.matchList.tableAndTeams}>
            {league.name}
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
            showOdds={showOdds}
            onOpen={() => onNavigate({ view: 'match', leagueCode: match.leagueCode, matchId: match.id })}
          />
        ))}
      </ul>
    </section>
  )
}
