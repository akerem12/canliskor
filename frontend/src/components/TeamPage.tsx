import { getStandings, getTeam } from '../api/http'
import type { Match, TeamProfile } from '../api/types'
import { isInPlay } from '../api/types'
import { useFetch } from '../api/useFetch'
import type { Route } from '../route'
import type { Result } from '../teamForm'
import { formGuide, resultFor } from '../teamForm'
import { formatMatchDate, formatTime } from '../time'
import { EmptyState, Skeleton, TeamLogo } from './common'
import { TeamSquad } from './Squads'
import { StandingsTable } from './StandingsTable'

interface Props {
  leagueCode: string
  teamId: string
  leagueName: string | undefined
  onNavigate: (route: Route) => void
  onBack: () => void
}

const UpcomingCount = 5
const resultNames: Record<Result, string> = { W: 'Won', D: 'Drawn', L: 'Lost' }

/** One team in one competition: who they are, their form, the table around them, what comes next, and the squad. */
export function TeamPage({ leagueCode, teamId, leagueName, onNavigate, onBack }: Props) {
  const team = useFetch(`team/${leagueCode}/${teamId}`, () => getTeam(leagueCode, teamId))
  const standings = useFetch(`standings/${leagueCode}`, () => getStandings(leagueCode))
  const league = standings.data?.leagueName ?? leagueName ?? leagueCode

  return (
    <main className="page" aria-label={team.data?.team.name ?? 'Team'}>
      <header className="page__top">
        <button className="page__back" onClick={onBack}>← Back</button>
        <button className="page__crumb" onClick={() => onNavigate({ view: 'league', leagueCode })}>{league}</button>
      </header>

      {team.loading && <TeamSkeleton />}
      {team.error && (
        <EmptyState icon="🛡️" title="This team isn't available right now." hint="The data source may be busy, or the team isn't part of this competition." onRetry={team.retry} />
      )}

      {team.data && (
        <>
          <Hero profile={team.data} league={league} />

          <section className="panel" aria-label="Form">
            <h3 className="panel__title">Last 5 matches</h3>
            <Form profile={team.data} onNavigate={onNavigate} />
          </section>

          <section className="panel" aria-label="Fixtures">
            <h3 className="panel__title">Coming up</h3>
            {team.data.upcomingMatches.length === 0 ? (
              <EmptyState icon="📅" title="No fixtures scheduled." hint="Nothing is planned in this competition yet." />
            ) : (
              <ul className="fixtures">
                {team.data.upcomingMatches.slice(0, UpcomingCount).map(match => (
                  <FixtureRow key={match.id} match={match} teamId={teamId} onNavigate={onNavigate} />
                ))}
              </ul>
            )}
          </section>
        </>
      )}

      {!team.error && (
        <section className="panel" aria-label="Standings">
          <h3 className="panel__title">Standings</h3>
          {standings.loading && <Skeleton rows={8} />}
          {standings.error && <EmptyState icon="📊" title="The table isn't available right now." onRetry={standings.retry} />}
          {standings.data && (standings.data.groups.length === 0
            ? <EmptyState icon="📊" title="This competition has no table." hint="Friendlies and knockout rounds aren't ranked." />
            : <StandingsTable standings={standings.data} teamId={teamId} onNavigate={onNavigate} />)}
        </section>
      )}

      {team.data && (
        <section className="panel" aria-label="Squad">
          <h3 className="panel__title">Squad</h3>
          <TeamSquad leagueCode={leagueCode} team={team.data.team} showName={false} />
        </section>
      )}
    </main>
  )
}

function Hero({ profile, league }: { profile: TeamProfile; league: string }) {
  const stadium = [profile.stadium, profile.stadiumCity].filter(Boolean).join(', ')

  return (
    <div className="hero">
      <TeamLogo team={profile.team} size={72} />
      <div className="hero__text">
        <h2>{profile.team.name}</h2>
        <p className="hero__line">{profile.standingSummary ?? league}</p>
        <dl className="hero__facts">
          <div>
            <dt>Competition</dt>
            <dd>{league}</dd>
          </div>
          {stadium && (
            <div>
              <dt>Stadium</dt>
              <dd>{stadium}</dd>
            </div>
          )}
          {profile.isNationalTeam && (
            <div>
              <dt>Type</dt>
              <dd>National team</dd>
            </div>
          )}
        </dl>
      </div>
    </div>
  )
}

function Form({ profile, onNavigate }: { profile: TeamProfile; onNavigate: (route: Route) => void }) {
  const guide = formGuide(profile.recentMatches, profile.team.id)
  if (guide.length === 0) {
    return <EmptyState icon="⚽" title="No matches played yet." hint="Results in this competition will appear here." />
  }

  return (
    <>
      <ol className="form" aria-label="Form, oldest first">
        {guide.map(({ match, result }) => (
          <li key={match.id} className={`form__chip form__chip--${result}`} title={resultNames[result]}>{result}</li>
        ))}
      </ol>
      <ul className="fixtures">
        {/* Newest first here: the list is read top down, the chips left to right. */}
        {[...guide].reverse().map(({ match }) => (
          <FixtureRow key={match.id} match={match} teamId={profile.team.id} onNavigate={onNavigate} />
        ))}
      </ul>
    </>
  )
}

/** One result or fixture of the team's; leads to the match. */
function FixtureRow({ match, teamId, onNavigate }: { match: Match; teamId: string; onNavigate: (route: Route) => void }) {
  const result = match.status === 'Finished' ? resultFor(match, teamId) : null
  const middle = match.score
    ? `${match.score.home} - ${match.score.away}`
    : match.status === 'Postponed' ? 'PP' : match.status === 'Cancelled' ? 'CANC' : formatTime(match.kickoff)

  return (
    <li>
      <button
        className={isInPlay(match.status) ? 'fixture fixture--live' : 'fixture'}
        onClick={() => onNavigate({ view: 'match', leagueCode: match.leagueCode, matchId: match.id })}
      >
        <span className="fixture__date">{formatMatchDate(match.kickoff)}</span>
        <span className={match.homeTeam.id === teamId ? 'fixture__team fixture__team--home fixture__team--own' : 'fixture__team fixture__team--home'}>
          {match.homeTeam.shortName}
        </span>
        <span className="fixture__score">{middle}</span>
        <span className={match.awayTeam.id === teamId ? 'fixture__team fixture__team--own' : 'fixture__team'}>
          {match.awayTeam.shortName}
        </span>
        {result
          ? <span className={`form__chip form__chip--small form__chip--${result}`} title={resultNames[result]}>{result}</span>
          : <span className="fixture__spacer" aria-hidden />}
      </button>
    </li>
  )
}

function TeamSkeleton() {
  return (
    <div aria-busy aria-label="Loading team">
      <div className="hero">
        <span className="skeleton skeleton--circle" style={{ width: 72, height: 72 }} />
        <div className="hero__text"><Skeleton rows={3} /></div>
      </div>
      <section className="panel"><Skeleton rows={6} /></section>
      <section className="panel"><Skeleton rows={5} /></section>
    </div>
  )
}
