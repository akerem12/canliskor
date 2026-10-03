import { getCompetitions, getStandings } from '../api/http'
import { useFetch } from '../api/useFetch'
import { FavoriteButton } from '../favorites/FavoriteButton'
import type { Route } from '../route'
import { EmptyState, Skeleton } from './common'
import { StandingsTable } from './StandingsTable'
import { TeamSearchBox } from './TeamSearchBox'

/** Every followed competition; each leads to its table, and from there to its teams. */
export function LeaguesPage({ onNavigate }: { onNavigate: (route: Route) => void }) {
  const competitions = useFetch('competitions', getCompetitions)

  return (
    <main className="page" aria-label="Leagues">
      <h2 className="page__title">Leagues</h2>
      <TeamSearchBox onNavigate={onNavigate} />
      <p className="page__intro">Or pick a competition to see its table and teams, also on days without matches.</p>

      {competitions.loading && <Skeleton rows={8} height={44} />}
      {competitions.error && <EmptyState icon="🏆" title="The leagues can't be loaded right now." onRetry={competitions.retry} />}
      {competitions.data && (
        <ul className="league-list">
          {competitions.data.map(league => (
            <li key={league.code}>
              <button className="league-list__item" onClick={() => onNavigate({ view: 'league', leagueCode: league.code })}>
                <span>{league.name}</span>
                <span aria-hidden>›</span>
              </button>
              <FavoriteButton league={league} size="large" />
            </li>
          ))}
        </ul>
      )}
    </main>
  )
}

interface LeagueProps {
  leagueCode: string
  leagueName: string | undefined
  onNavigate: (route: Route) => void
  onBack: () => void
}

/** One competition's table. Every team in it is listed whether or not it plays today. */
export function LeaguePage({ leagueCode, leagueName, onNavigate, onBack }: LeagueProps) {
  const standings = useFetch(`standings/${leagueCode}`, () => getStandings(leagueCode))
  const name = standings.data?.leagueName ?? leagueName ?? leagueCode

  return (
    <main className="page" aria-label={name}>
      <header className="page__top">
        <button className="page__back" onClick={onBack}>← Back</button>
        <button className="page__crumb" onClick={() => onNavigate({ view: 'leagues' })}>All leagues</button>
      </header>
      <h2 className="page__title">
        {name}
        <FavoriteButton league={{ code: leagueCode, name }} size="large" />
      </h2>

      <section className="panel" aria-label="Standings">
        {standings.loading && <Skeleton rows={12} />}
        {standings.error && <EmptyState icon="📊" title="The table isn't available right now." hint="The data source may be busy." onRetry={standings.retry} />}
        {standings.data && (standings.data.groups.length === 0
          ? <EmptyState icon="📊" title="This competition has no table." hint="Friendlies aren't ranked. Their matches are on the Matches page." />
          : <StandingsTable standings={standings.data} onNavigate={onNavigate} />)}
      </section>
    </main>
  )
}
