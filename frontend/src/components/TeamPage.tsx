import { Fragment, useLayoutEffect, useMemo, useRef, useState } from 'react'
import type { ReactNode, Ref } from 'react'
import { getCompetitions, getMatchDetail, getStandings, getTeam } from '../api/http'
import type { Match, TeamProfile } from '../api/types'
import { isInPlay } from '../api/types'
import { useFetch } from '../api/useFetch'
import { FavoriteButton } from '../favorites/FavoriteButton'
import type { Dictionary } from '../i18n/en'
import { useI18n } from '../i18n/useI18n'
import type { Route } from '../route'
import { formGuide, isFriendly, resultFor } from '../teamForm'
import { seasonTimeline, timelinePoints } from '../teamTimeline'
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
 * One team: who they are and their form up top, then the next match, the whole season's matches, the table
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
          {panel('fixtures', <Fixtures profile={team.data} competitions={competitions} active={tab === 'fixtures'} onNavigate={onNavigate} />)}
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

/** The next match, large; the season's other matches are on the Fixtures tab. */
function Overview({ profile, competitions, onNavigate }: { profile: TeamProfile; competitions: Competitions; onNavigate: (route: Route) => void }) {
  const { t } = useI18n()
  const next = profile.upcomingMatches.at(0)
  // With nothing left to play (the season is over), the last match takes its place.
  const last = next ? undefined : profile.recentMatches.at(0)
  const shown = next ?? last
  const title = next ? t.team.nextMatch : t.team.lastMatch

  return shown ? (
    <section className="panel" aria-label={title}>
      <h3 className="panel__title">{title}</h3>
      <NextMatch match={shown} competitions={competitions} onNavigate={onNavigate} />
    </section>
  ) : (
    <section className="panel">
      <EmptyState icon="📅" title={t.team.noFixtures} hint={t.team.noFixturesHint} />
    </section>
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

/**
 * The whole season in one list, oldest at the top: results (friendlies included), the match in play, then every
 * match still to come; all competitions together or one at a time. Showing the tab puts the match in play or the
 * latest result at the top of the screen with what comes next below it; earlier results are a scroll up.
 * @param active The tab is the one shown: the list is only measured and scrolled to then.
 */
function Fixtures({ profile, competitions, active, onNavigate }: {
  profile: TeamProfile
  competitions: Competitions
  active: boolean
  onNavigate: (route: Route) => void
}) {
  const { t } = useI18n()
  const [filter, setFilter] = useState<string | null>(null)
  const season = useMemo(() => seasonTimeline(profile.recentMatches, profile.upcomingMatches), [profile])
  const codes = [...new Set(season.map(match => match.leagueCode))]
  const shown = filter ? season.filter(match => match.leagueCode === filter) : season
  const { anchor, upcomingFrom } = timelinePoints(shown)
  const anchorId = anchor === null ? null : shown[anchor].id
  const played = shown.filter(match => match.status === 'Finished').length
  const anyFriendly = shown.some(match => isFriendlyIn(competitions, match))

  const anchorRef = useRef<HTMLLIElement>(null)
  const roomRef = useRef<HTMLDivElement>(null)
  useLayoutEffect(() => {
    const row = anchorRef.current
    const room = roomRef.current
    if (!active || !room) return
    room.style.height = '0px'
    if (!row) return

    // Below the header that stays at the top of the screen.
    const header = document.querySelector('.appbar')?.getBoundingClientRect().height ?? 0
    const top = row.getBoundingClientRect().top + window.scrollY - header - 8
    // With few matches to come the page may end too soon for the row to reach the top: make room below.
    const short = top + window.innerHeight - document.documentElement.scrollHeight
    if (short > 0) room.style.height = `${short}px`
    window.scrollTo({ top, behavior: 'instant' })
  }, [active, anchorId, filter])

  return (
    <>
      <section className="panel" aria-label={t.team.fixtures}>
        <h3 className="panel__title">{t.team.fixturesTitle(played, shown.length - played)}</h3>
        {season.length === 0 ? (
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
            <ul className="fixtures">
              {shown.map((match, i) => (
                <Fragment key={match.id}>
                  {i === upcomingFrom && <li className="fixtures__divider">{t.team.upcomingDivider}</li>}
                  <FixtureRow
                    match={match}
                    teamId={profile.team.id}
                    competitions={competitions}
                    rowRef={i === anchor ? anchorRef : undefined}
                    onNavigate={onNavigate}
                  />
                </Fragment>
              ))}
            </ul>
            {anyFriendly && <p className="panel__note">{t.team.friendlyNote}</p>}
          </>
        )}
      </section>
      <div ref={roomRef} aria-hidden />
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
function FixtureRow({ match, teamId, competitions, rowRef, onNavigate }: {
  match: Match
  teamId: string
  competitions: Competitions
  rowRef?: Ref<HTMLLIElement>
  onNavigate: (route: Route) => void
}) {
  const { t } = useI18n()
  const result = match.status === 'Finished' ? resultFor(match, teamId) : null
  const friendly = isFriendlyIn(competitions, match)
  const canOpen = competitions.followed?.has(match.leagueCode) ?? true

  const content = (
    <>
      <span className="fixture__when">
        {isInPlay(match.status)
          ? <span className="fixture__date fixture__date--live">● {match.status === 'HalfTime' ? t.status.halfTimeShort : match.clock}</span>
          : <span className="fixture__date">{formatMatchDate(match.kickoff, t)}</span>}
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
      {match.venue && !result && <span className="fixture__venue">📍 {match.venue}</span>}
    </>
  )
  const className = isInPlay(match.status) ? 'fixture fixture--live' : 'fixture'

  return (
    <li ref={rowRef}>
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
