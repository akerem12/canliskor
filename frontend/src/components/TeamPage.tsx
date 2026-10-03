import { useState } from 'react'
import { getCompetitions, getStandings, getTeam } from '../api/http'
import type { Match, TeamProfile } from '../api/types'
import { isInPlay } from '../api/types'
import { useFetch } from '../api/useFetch'
import { FavoriteButton } from '../favorites/FavoriteButton'
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

/** How many fixtures and results show before "Show all". */
const CollapsedCount = 5
const resultNames: Record<Result, string> = { W: 'Won', D: 'Drawn', L: 'Lost' }

/** What a fixture row needs to know about the competitions around it. */
interface Competitions {
  /** Competition code → its name, for the label on each row. */
  names: ReadonlyMap<string, string>
  /** Codes whose matches the site can open. Undefined until known: then every row is a link. */
  followed: ReadonlySet<string> | undefined
}

/**
 * One team: who they are, their form, every match still to come in all competitions, the table around them
 * and the squad.
 */
export function TeamPage({ leagueCode, teamId, leagueName, onNavigate, onBack }: Props) {
  const team = useFetch(`team/${leagueCode}/${teamId}`, () => getTeam(leagueCode, teamId))
  const standings = useFetch(`standings/${leagueCode}`, () => getStandings(leagueCode))
  const followed = useFetch('competitions', getCompetitions)
  const league = standings.data?.leagueName ?? leagueName ?? leagueCode

  const competitions: Competitions = {
    names: new Map(team.data?.competitions.map(c => [c.code, c.name])),
    followed: followed.data ? new Set(followed.data.map(c => c.code)) : undefined,
  }

  return (
    <main className="page" aria-label={team.data?.team.name ?? 'Team'}>
      <header className="page__top">
        <button className="page__back" onClick={onBack}>← Back</button>
        <button className="page__crumb" onClick={() => onNavigate({ view: 'league', leagueCode })}>{league}</button>
      </header>

      {team.loading && <TeamSkeleton />}
      {team.error && (
        <EmptyState icon="🛡️" title="This team isn't available right now." hint="The data source may be busy, or the link is wrong." onRetry={team.retry} />
      )}

      {team.data && (
        <>
          <Hero profile={team.data} league={league} />

          <section className="panel" aria-label="Form">
            <h3 className="panel__title">Last 5 matches</h3>
            <Form profile={team.data} competitions={competitions} onNavigate={onNavigate} />
          </section>

          <section className="panel" aria-label="Fixtures">
            <h3 className="panel__title">Fixtures · {team.data.upcomingMatches.length} to play</h3>
            {team.data.upcomingMatches.length === 0 ? (
              <EmptyState icon="📅" title="No fixtures scheduled." hint="Nothing is planned for this team yet." />
            ) : (
              <MatchList matches={team.data.upcomingMatches} what="fixtures" teamId={teamId} competitions={competitions} onNavigate={onNavigate} />
            )}
          </section>
        </>
      )}

      {!team.error && (
        <section className="panel" aria-label="Standings">
          <h3 className="panel__title">Standings · {league}</h3>
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
  const others = profile.competitions.map(c => c.name).filter(name => name !== league)

  return (
    <div className="hero">
      <TeamLogo team={profile.team} size={72} />
      <div className="hero__text">
        <h2>
          {profile.team.name}
          <FavoriteButton
            team={{ leagueCode: profile.leagueCode, teamId: profile.team.id, name: profile.team.name, logoUrl: profile.team.logoUrl }}
            size="large"
          />
        </h2>
        <p className="hero__line">{profile.standingSummary ?? league}</p>
        <dl className="hero__facts">
          <div>
            <dt>Competition</dt>
            <dd>{league}</dd>
          </div>
          {others.length > 0 && (
            <div>
              <dt>Also in</dt>
              <dd>{others.join(', ')}</dd>
            </div>
          )}
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

function Form({ profile, competitions, onNavigate }: { profile: TeamProfile; competitions: Competitions; onNavigate: (route: Route) => void }) {
  const guide = formGuide(profile.recentMatches, profile.team.id)
  if (guide.length === 0) {
    return <EmptyState icon="⚽" title="No matches played yet." hint="Results will appear here." />
  }

  return (
    <>
      <ol className="form" aria-label="Form, oldest first">
        {guide.map(({ match, result }) => (
          <li key={match.id} className={`form__chip form__chip--${result}`} title={resultNames[result]}>{result}</li>
        ))}
      </ol>
      {/* Newest first here: the list is read top down, the chips left to right. */}
      <MatchList matches={profile.recentMatches} what="results" teamId={profile.team.id} competitions={competitions} onNavigate={onNavigate} />
    </>
  )
}

/** The first few matches, and all of them on request. */
function MatchList({ matches, what, teamId, competitions, onNavigate }: {
  matches: Match[]
  what: 'fixtures' | 'results'
  teamId: string
  competitions: Competitions
  onNavigate: (route: Route) => void
}) {
  const [showAll, setShowAll] = useState(false)
  const shown = showAll ? matches : matches.slice(0, CollapsedCount)

  return (
    <>
      <ul className="fixtures">
        {shown.map(match => (
          <FixtureRow key={match.id} match={match} teamId={teamId} competitions={competitions} onNavigate={onNavigate} />
        ))}
      </ul>
      {matches.length > CollapsedCount && (
        <button className="more" onClick={() => setShowAll(!showAll)} aria-expanded={showAll}>
          {showAll ? 'Show fewer' : `Show all ${matches.length} ${what}`}
        </button>
      )}
    </>
  )
}

/** One result or fixture of the team's. Leads to the match if its competition is one the site follows. */
function FixtureRow({ match, teamId, competitions, onNavigate }: {
  match: Match
  teamId: string
  competitions: Competitions
  onNavigate: (route: Route) => void
}) {
  const result = match.status === 'Finished' ? resultFor(match, teamId) : null
  const middle = match.score
    ? `${match.score.home} - ${match.score.away}`
    : match.status === 'Postponed' ? 'PP' : match.status === 'Cancelled' ? 'CANC' : formatTime(match.kickoff)
  const canOpen = competitions.followed?.has(match.leagueCode) ?? true

  const content = (
    <>
      <span className="fixture__when">
        <span className="fixture__date">{formatMatchDate(match.kickoff)}</span>
        <span className="fixture__league">{competitions.names.get(match.leagueCode) ?? match.leagueCode}</span>
      </span>
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
    </>
  )
  const className = isInPlay(match.status) ? 'fixture fixture--live' : 'fixture'

  return (
    <li>
      {canOpen ? (
        <button className={className} onClick={() => onNavigate({ view: 'match', leagueCode: match.leagueCode, matchId: match.id })}>
          {content}
        </button>
      ) : (
        <div className={`${className} fixture--static`} title="This competition isn't followed, so the match can't be opened.">
          {content}
        </div>
      )}
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
