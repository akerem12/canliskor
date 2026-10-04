import { useState } from 'react'
import type { ReactNode } from 'react'
import { getCompetitions, getMatchDetail, getStandings, getTeam } from '../api/http'
import type { Match, TeamProfile } from '../api/types'
import { isInPlay } from '../api/types'
import { useFetch } from '../api/useFetch'
import { FavoriteButton } from '../favorites/FavoriteButton'
import type { Dictionary } from '../i18n/en'
import { useI18n } from '../i18n/useI18n'
import type { Route } from '../route'
import { formGuide, isFriendly, resultFor } from '../teamForm'
import { formatMatchDate, formatTime } from '../time'
import { tabFromUrl, writeTabToUrl } from '../urlTab'
import { EmptyState, Skeleton, TeamLogo } from './common'
import { OddsStrip } from './Odds'
import { TeamSquad } from './Squads'
import { StandingsTable } from './StandingsTable'

interface Props {
  leagueCode: string
  teamId: string
  leagueName: string | undefined
  onNavigate: (route: Route) => void
  onBack: () => void
}

/** How many results show before "Show all". */
const CollapsedCount = 5

const tabs = ['overview', 'fixtures', 'standings', 'squad'] as const
type Tab = typeof tabs[number]

/** What a fixture row needs to know about the competitions around it. */
interface Competitions {
  /** Competition code → its name, for the label on each row. */
  names: ReadonlyMap<string, string>
  /** Codes whose matches the site can open. Undefined until known: then every row is a link. */
  followed: ReadonlySet<string> | undefined
}

const competitionName = (competitions: Competitions, code: string) => competitions.names.get(code) ?? code

const isFriendlyIn = (competitions: Competitions, match: Match) =>
  isFriendly(match.leagueCode, competitions.names.get(match.leagueCode))

/** What stands between the two teams of a row: the score, the kick-off time, or why there is neither. */
function middleText(match: Match, t: Dictionary): string {
  return match.score
    ? `${match.score.home} - ${match.score.away}`
    : match.status === 'Postponed' ? t.status.postponedShort
    : match.status === 'Cancelled' ? t.status.cancelledShort
    : formatTime(match.kickoff)
}

/**
 * One team: who they are and their form up top, then recent matches, every match still to come, the table
 * around them and the squad, each on a tab of its own.
 */
export function TeamPage({ leagueCode, teamId, leagueName, onNavigate, onBack }: Props) {
  const { t } = useI18n()
  const team = useFetch(`team/${leagueCode}/${teamId}`, () => getTeam(leagueCode, teamId))
  const standings = useFetch(`standings/${leagueCode}`, () => getStandings(leagueCode))
  const followed = useFetch('competitions', getCompetitions)
  const league = standings.data?.leagueName ?? leagueName ?? leagueCode

  const [tab, setTab] = useState<Tab>(() => tabFromUrl(tabs) ?? 'overview')
  // A tab is built when it is first opened and kept from then on, so coming back to it loads nothing again.
  const [opened, setOpened] = useState<ReadonlySet<Tab>>(() => new Set([tab]))
  const pickTab = (picked: Tab) => {
    setTab(picked)
    setOpened(previous => new Set(previous).add(picked))
    writeTabToUrl(picked)
  }
  const panel = (name: Tab, content: ReactNode) =>
    opened.has(name) && <div key={name} hidden={tab !== name}>{content}</div>

  const competitions: Competitions = {
    names: new Map(team.data?.competitions.map(c => [c.code, c.name])),
    followed: followed.data ? new Set(followed.data.map(c => c.code)) : undefined,
  }

  return (
    <main className="page" aria-label={team.data?.team.name ?? t.team.team}>
      <header className="page__top">
        <button className="page__back" onClick={onBack}>{t.common.back}</button>
        <button className="page__crumb" onClick={() => onNavigate({ view: 'league', leagueCode })}>{league}</button>
      </header>

      {team.loading && <TeamSkeleton />}
      {team.error && (
        <EmptyState icon="🛡️" title={t.team.unavailable} hint={t.team.unavailableHint} onRetry={team.retry} />
      )}

      {team.data && (
        <>
          <Hero profile={team.data} league={league} competitions={competitions} />

          <nav className="tabs team__tabs" aria-label={t.team.sections}>
            {tabs.map(known => (
              <button
                key={known}
                className={known === tab ? 'tab tab--active' : 'tab'}
                aria-current={known === tab ? 'page' : undefined}
                onClick={() => pickTab(known)}
              >
                {t.team.tabs[known]}
              </button>
            ))}
          </nav>

          {panel('overview', <Overview profile={team.data} competitions={competitions} onNavigate={onNavigate} />)}
          {panel('fixtures', <Fixtures profile={team.data} competitions={competitions} onNavigate={onNavigate} />)}
          {panel('standings', (
            <section className="panel" aria-label={t.leagues.standings}>
              <h3 className="panel__title">{t.team.standingsOf(league)}</h3>
              {standings.loading && <Skeleton rows={8} />}
              {standings.error && <EmptyState icon="📊" title={t.leagues.tableUnavailable} onRetry={standings.retry} />}
              {standings.data && (standings.data.groups.length === 0
                ? <EmptyState icon="📊" title={t.leagues.noTable} hint={t.leagues.noTableHintTeam} />
                : <StandingsTable standings={standings.data} teamId={teamId} onNavigate={onNavigate} />)}
            </section>
          ))}
          {panel('squad', (
            <section className="panel" aria-label={t.team.squad}>
              <h3 className="panel__title">{t.team.squad}</h3>
              <TeamSquad leagueCode={leagueCode} team={team.data.team} showName={false} showSeason />
            </section>
          ))}
        </>
      )}
    </main>
  )
}

/** Stays above the tabs: crest, name, where the team plays, and how its last official matches went. */
function Hero({ profile, league, competitions }: { profile: TeamProfile; league: string; competitions: Competitions }) {
  const { t } = useI18n()
  const stadium = [profile.stadium, profile.stadiumCity].filter(Boolean).join(', ')
  const others = profile.competitions.map(c => c.name).filter(name => name !== league)
  const guide = formGuide(profile.recentMatches, profile.team.id, match => isFriendlyIn(competitions, match))

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
            <dt>{t.team.competition}</dt>
            <dd>{league}</dd>
          </div>
          {others.length > 0 && (
            <div>
              <dt>{t.team.alsoIn}</dt>
              <dd>{others.join(', ')}</dd>
            </div>
          )}
          {stadium && (
            <div>
              <dt>{t.team.stadium}</dt>
              <dd>{stadium}</dd>
            </div>
          )}
          {profile.isNationalTeam && (
            <div>
              <dt>{t.team.type}</dt>
              <dd>{t.team.nationalTeam}</dd>
            </div>
          )}
        </dl>
        {guide.length > 0 && (
          <div className="hero__form" aria-label={t.team.form}>
            <span className="hero__form-label">{t.team.formOfficial}</span>
            <ol className="form" aria-label={t.team.formLabel}>
              {guide.map(({ match, result }) => (
                <li
                  key={match.id}
                  className={`form__chip form__chip--${result}`}
                  title={`${t.team.resultName[result]}: ${match.homeTeam.shortName} ${middleText(match, t)} ${match.awayTeam.shortName}`}
                >
                  {t.team.resultLetter[result]}
                </li>
              ))}
            </ol>
          </div>
        )}
      </div>
    </div>
  )
}

/** The next match, then what has been played, friendlies included. */
function Overview({ profile, competitions, onNavigate }: { profile: TeamProfile; competitions: Competitions; onNavigate: (route: Route) => void }) {
  const { t } = useI18n()
  const next = profile.upcomingMatches.at(0)
  const anyFriendly = profile.recentMatches.some(match => isFriendlyIn(competitions, match))

  return (
    <>
      {next && (
        <section className="panel" aria-label={t.team.nextMatch}>
          <h3 className="panel__title">{t.team.nextMatch}</h3>
          <NextMatch match={next} competitions={competitions} onNavigate={onNavigate} />
        </section>
      )}

      <section className="panel" aria-label={t.team.recentMatches}>
        <h3 className="panel__title">{t.team.recentMatches}</h3>
        {profile.recentMatches.length === 0 ? (
          <EmptyState icon="⚽" title={t.team.nothingPlayed} hint={t.team.nothingPlayedHint} />
        ) : (
          <>
            {/* Newest first here: the list is read top down, the form chips left to right. */}
            <MatchList
              matches={profile.recentMatches}
              collapsedCount={CollapsedCount}
              teamId={profile.team.id}
              competitions={competitions}
              onNavigate={onNavigate}
            />
            {anyFriendly && <p className="panel__note">{t.team.friendlyNote}</p>}
          </>
        )}
      </section>
    </>
  )
}

/** The team's next match, large: who, when, where, and the odds once they are posted. */
function NextMatch({ match, competitions, onNavigate }: { match: Match; competitions: Competitions; onNavigate: (route: Route) => void }) {
  const { t } = useI18n()
  const canOpen = competitions.followed?.has(match.leagueCode) ?? true
  // Odds are for matches still to start, and only matches of followed competitions can be asked about.
  const mayHaveOdds = match.status === 'Scheduled' && competitions.followed?.has(match.leagueCode) === true

  const content = (
    <>
      <span className="next__meta">
        <CompetitionLabel match={match} competitions={competitions} /> · {formatMatchDate(match.kickoff, t)}
      </span>
      <span className="next__teams">
        <span className="next__team">
          <TeamLogo team={match.homeTeam} size={40} />
          <span>{match.homeTeam.name}</span>
        </span>
        <span className="fixture__score">{middleText(match, t)}</span>
        <span className="next__team">
          <TeamLogo team={match.awayTeam} size={40} />
          <span>{match.awayTeam.name}</span>
        </span>
      </span>
      {match.venue && <span className="next__venue">📍 {match.venue}</span>}
    </>
  )
  const className = isInPlay(match.status) ? 'next__match fixture--live' : 'next__match'

  return (
    <div className="next">
      {canOpen ? (
        <button className={className} onClick={() => onNavigate({ view: 'match', leagueCode: match.leagueCode, matchId: match.id })}>
          {content}
        </button>
      ) : (
        <div className={`${className} next__match--static`} title={t.team.notFollowed}>{content}</div>
      )}
      {match.odds
        ? <OddsStrip odds={match.odds} />
        : mayHaveOdds && <NextMatchOdds match={match} />}
    </div>
  )
}

/** A team's schedule has no price for the draw, so the odds come from the match itself. Nothing shows unless they arrive. */
function NextMatchOdds({ match }: { match: Match }) {
  const detail = useFetch(`match/${match.leagueCode}/${match.id}`, () => getMatchDetail(match.leagueCode, match.id))
  const odds = detail.data?.match.odds
  return odds ? <OddsStrip odds={odds} /> : null
}

/** Every match still to come, all competitions together or one at a time. */
function Fixtures({ profile, competitions, onNavigate }: { profile: TeamProfile; competitions: Competitions; onNavigate: (route: Route) => void }) {
  const { t } = useI18n()
  const [filter, setFilter] = useState<string | null>(null)
  const matches = profile.upcomingMatches
  const codes = [...new Set(matches.map(match => match.leagueCode))]
  const shown = filter ? matches.filter(match => match.leagueCode === filter) : matches

  return (
    <section className="panel" aria-label={t.team.fixtures}>
      <h3 className="panel__title">{t.team.fixturesTitle(shown.length)}</h3>
      {matches.length === 0 ? (
        <EmptyState icon="📅" title={t.team.noFixtures} hint={t.team.noFixturesHint} />
      ) : (
        <>
          {codes.length > 1 && (
            <div className="quickbar team__filter" role="group" aria-label={t.team.filterByCompetition}>
              <button className={filter === null ? 'chip chip--active' : 'chip'} aria-pressed={filter === null} onClick={() => setFilter(null)}>
                {t.team.allCompetitions}
              </button>
              {codes.map(code => (
                <button key={code} className={filter === code ? 'chip chip--active' : 'chip'} aria-pressed={filter === code} onClick={() => setFilter(code)}>
                  {competitionName(competitions, code)}
                </button>
              ))}
            </div>
          )}
          <MatchList matches={shown} showVenue teamId={profile.team.id} competitions={competitions} onNavigate={onNavigate} />
        </>
      )}
    </section>
  )
}

/** @param collapsedCount Shows only this many until "Show all" is pressed; all of them if left out. */
function MatchList({ matches, collapsedCount, showVenue = false, teamId, competitions, onNavigate }: {
  matches: Match[]
  collapsedCount?: number
  showVenue?: boolean
  teamId: string
  competitions: Competitions
  onNavigate: (route: Route) => void
}) {
  const { t } = useI18n()
  const [showAll, setShowAll] = useState(false)
  const collapsible = collapsedCount !== undefined && matches.length > collapsedCount
  const shown = collapsible && !showAll ? matches.slice(0, collapsedCount) : matches

  return (
    <>
      <ul className="fixtures">
        {shown.map(match => (
          <FixtureRow key={match.id} match={match} showVenue={showVenue} teamId={teamId} competitions={competitions} onNavigate={onNavigate} />
        ))}
      </ul>
      {collapsible && (
        <button className="more" onClick={() => setShowAll(!showAll)} aria-expanded={showAll}>
          {showAll ? t.team.showFewer : t.team.showAllResults(matches.length)}
        </button>
      )}
    </>
  )
}

/** The competition's name, or a "Friendly" tag in its place for a match that doesn't count. */
function CompetitionLabel({ match, competitions }: { match: Match; competitions: Competitions }) {
  const { t } = useI18n()
  const name = competitionName(competitions, match.leagueCode)

  return isFriendlyIn(competitions, match)
    ? <span className="friendly-tag" title={name}>{t.team.friendly}</span>
    : <>{name}</>
}

/** One result or fixture of the team's. Leads to the match if its competition is one the site follows. */
function FixtureRow({ match, showVenue, teamId, competitions, onNavigate }: {
  match: Match
  showVenue: boolean
  teamId: string
  competitions: Competitions
  onNavigate: (route: Route) => void
}) {
  const { t } = useI18n()
  const result = match.status === 'Finished' ? resultFor(match, teamId) : null
  const friendly = isFriendlyIn(competitions, match)
  const canOpen = competitions.followed?.has(match.leagueCode) ?? true

  const content = (
    <>
      <span className="fixture__when">
        <span className="fixture__date">{formatMatchDate(match.kickoff, t)}</span>
        <span className="fixture__league"><CompetitionLabel match={match} competitions={competitions} /></span>
      </span>
      <span className={match.homeTeam.id === teamId ? 'fixture__team fixture__team--home fixture__team--own' : 'fixture__team fixture__team--home'}>
        <span className="fixture__name">{match.homeTeam.shortName}</span>
        <TeamLogo team={match.homeTeam} size={18} />
      </span>
      <span className="fixture__score">{middleText(match, t)}</span>
      <span className={match.awayTeam.id === teamId ? 'fixture__team fixture__team--own' : 'fixture__team'}>
        <TeamLogo team={match.awayTeam} size={18} />
        <span className="fixture__name">{match.awayTeam.shortName}</span>
      </span>
      {result ? (
        <span
          className={`form__chip form__chip--small form__chip--${result}${friendly ? ' form__chip--friendly' : ''}`}
          title={friendly ? t.team.friendlyResult(t.team.resultName[result]) : t.team.resultName[result]}
        >
          {t.team.resultLetter[result]}
        </span>
      ) : <span className="fixture__spacer" aria-hidden />}
      {showVenue && match.venue && <span className="fixture__venue">📍 {match.venue}</span>}
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
        <div className={`${className} fixture--static`} title={t.team.notFollowed}>
          {content}
        </div>
      )}
    </li>
  )
}

function TeamSkeleton() {
  const { t } = useI18n()

  return (
    <div aria-busy aria-label={t.team.loading}>
      <div className="hero">
        <span className="skeleton skeleton--circle" style={{ width: 72, height: 72 }} />
        <div className="hero__text"><Skeleton rows={3} /></div>
      </div>
      <section className="panel"><Skeleton rows={3} /></section>
      <section className="panel"><Skeleton rows={6} /></section>
    </div>
  )
}
