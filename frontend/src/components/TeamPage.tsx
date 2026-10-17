import { Fragment, useMemo, useState } from 'react'
import type { ReactNode } from 'react'
import { getCompetitions, getMatchDetail, getStandings, getTeam } from '../api/http'
import type { Match, TeamProfile } from '../api/types'
import { isInPlay } from '../api/types'
import { useFetch } from '../api/useFetch'
import { FavoriteButton } from '../favorites/FavoriteButton'
import type { Dictionary } from '../i18n/en'
import { leagueCountryName } from '../i18n/names'
import { useI18n } from '../i18n/useI18n'
import { countryFlagUrl, domesticLeague, leagueCountryCode } from '../leagueCountry'
import type { Route } from '../route'
import { formGuide, isFriendly, resultFor } from '../teamForm'
import { seasonTimeline, timelinePoints } from '../teamTimeline'
import { formatMatchDate, formatNumericDate, formatTime } from '../time'
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

const tabs = ['fixtures', 'standings', 'squad'] as const
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
 * One team: who they are, their form and the matches either side of now up top, then the whole season's matches,
 * the table around them and the squad, each on a tab of its own.
 */
export function TeamPage({ leagueCode, teamId, leagueName, onNavigate, onBack }: Props) {
  const { t } = useI18n()
  const team = useFetch(`team/${leagueCode}/${teamId}`, () => getTeam(leagueCode, teamId))
  const standings = useFetch(`standings/${leagueCode}`, () => getStandings(leagueCode))
  const followed = useFetch('competitions', getCompetitions)
  const league = standings.data?.leagueName ?? leagueName ?? leagueCode

  const [tab, setTab] = useState<Tab>(() => tabFromUrl(tabs) ?? 'fixtures')
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
          <Hero profile={team.data} league={league} competitions={competitions} onNavigate={onNavigate} />

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

/**
 * Stays above the tabs. On the left who the team is: crest, name, country, its last official results, where it
 * plays and its league. On the right the match before and the match after, each a way into it.
 */
function Hero({ profile, league, competitions, onNavigate }: {
  profile: TeamProfile
  league: string
  competitions: Competitions
  onNavigate: (route: Route) => void
}) {
  const { t, language } = useI18n()
  const home = domesticLeague([profile.leagueCode, ...profile.competitions.map(c => c.code)])
  const homeCountry = home ? leagueCountryCode(home) : null
  const country = profile.isNationalTeam ? t.team.nationalTeam : homeCountry && leagueCountryName(homeCountry, language)
  const homeFlag = home ? countryFlagUrl(home) : null
  // The summary names the team's own league, with its place in it.
  const leagueLine = profile.standingSummary ?? (home ? competitionName(competitions, home) : league)
  const guide = formGuide(profile.recentMatches, profile.team.id, match => isFriendlyIn(competitions, match))
  const previous = profile.recentMatches.find(match => match.status === 'Finished')
  const next = profile.upcomingMatches.at(0)

  return (
    <div className="hero">
      <div className="hero__identity">
        <span className="hero__crest"><TeamLogo team={profile.team} size={80} /></span>
        <div className="hero__text">
          <h2 className="hero__name">{profile.team.name}</h2>
          {country && (
            <span className="hero__country">
              {homeFlag && !profile.isNationalTeam && <Flag url={homeFlag} className="hero__flag" />}
              {country}
            </span>
          )}
          {guide.length > 0 && (
            <ol className="form hero__form" aria-label={`${t.team.formOfficial} · ${t.team.formLabel}`}>
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
          )}
        </div>
      </div>

      <div className="hero__meta">
        {profile.stadium && (
          <span title={[profile.stadium, profile.stadiumCity].filter(Boolean).join(', ')} aria-label={t.team.stadium}>
            <span aria-hidden>🏟️</span> {profile.stadium}
          </span>
        )}
        <span>
          {homeFlag ? <Flag url={homeFlag} className="hero__league-icon" /> : <span aria-hidden>🏆</span>} {leagueLine}
        </span>
      </div>

      {(previous || next) && (
        <div className="hero__matches">
          {previous && (
            <MatchCard label={t.team.previousMatch} match={previous} competitions={competitions} onNavigate={onNavigate} />
          )}
          {next && (
            <MatchCard
              label={isInPlay(next.status) ? t.team.liveNow : t.team.nextMatch}
              match={next}
              competitions={competitions}
              onNavigate={onNavigate}
            />
          )}
        </div>
      )}

      <span className="hero__favorite">
        <FavoriteButton
          team={{ leagueCode: profile.leagueCode, teamId: profile.team.id, name: profile.team.name, logoUrl: profile.team.logoUrl }}
          size="large"
        />
      </span>
    </div>
  )
}

/** A country's flag as a small round icon; nothing at all if the image doesn't load. */
function Flag({ url, className }: { url: string; className: string }) {
  const [broken, setBroken] = useState(false)
  return broken ? null : <img className={className} src={url} alt="" loading="lazy" onError={() => setBroken(true)} />
}

/**
 * One match in little room, for the header: competition, day and time (or the minute), both teams and the score.
 * The loser of a finished match is dimmed. A match still to start shows its odds once they are posted.
 */
function MatchCard({ label, match, competitions, onNavigate }: {
  label: string
  match: Match
  competitions: Competitions
  onNavigate: (route: Route) => void
}) {
  const { t } = useI18n()
  const canOpen = competitions.followed?.has(match.leagueCode) ?? true
  const live = isInPlay(match.status)
  const finished = match.status === 'Finished'
  const flag = countryFlagUrl(match.leagueCode)
  const mayHaveOdds = match.status === 'Scheduled' && competitions.followed?.has(match.leagueCode) === true
  const { home, away } = match.score ?? { home: null, away: null }
  const lost = (side: 'home' | 'away') =>
    finished && home !== null && away !== null && (side === 'home' ? home < away : away < home)
  const when = live ? (match.status === 'HalfTime' ? t.status.halfTimeShort : match.clock)
    : finished ? t.status.fullTimeShort
    : match.status === 'Postponed' ? t.status.postponedShort
    : match.status === 'Cancelled' ? t.status.cancelledShort
    : formatTime(match.kickoff)

  const team = (side: 'home' | 'away') => {
    const which = side === 'home' ? match.homeTeam : match.awayTeam
    const goals = side === 'home' ? home : away
    return (
      <span className={lost(side) ? 'match-card__team match-card__team--lost' : 'match-card__team'}>
        <TeamLogo team={which} size={16} />
        <span className="match-card__name">{which.shortName}</span>
        {goals !== null && <span className="match-card__goals">{goals}</span>}
      </span>
    )
  }

  const content = (
    <>
      <span className="match-card__league">
        {flag && <Flag url={flag} className="match-card__flag" />}
        <span className="match-card__league-name"><CompetitionLabel match={match} competitions={competitions} /></span>
      </span>
      <span className="match-card__body">
        <span className={live ? 'match-card__when match-card__when--live' : 'match-card__when'}>
          {!live && <span>{formatNumericDate(match.kickoff, t)}</span>}
          <span>{live ? `● ${when}` : when}</span>
        </span>
        <span className="match-card__teams">
          {team('home')}
          {team('away')}
        </span>
      </span>
    </>
  )

  return (
    <div className="match-card">
      <span className="match-card__label">{label}</span>
      {canOpen ? (
        <button
          className="match-card__box"
          onClick={() => onNavigate({ view: 'match', leagueCode: match.leagueCode, matchId: match.id })}
        >
          {content}
        </button>
      ) : (
        <div className="match-card__box match-card__box--static" title={t.team.notFollowed}>{content}</div>
      )}
      {match.odds
        ? <OddsStrip odds={match.odds} className="match-card__odds" />
        : mayHaveOdds && <NextMatchOdds match={match} />}
    </div>
  )
}

/** A team's schedule has no price for the draw, so the odds come from the match itself. Nothing shows unless they arrive. */
function NextMatchOdds({ match }: { match: Match }) {
  const detail = useFetch(`match/${match.leagueCode}/${match.id}`, () => getMatchDetail(match.leagueCode, match.id))
  const odds = detail.data?.match.odds
  return odds ? <OddsStrip odds={odds} className="match-card__odds" /> : null
}

/**
 * The whole season in one list, oldest at the top: results (friendlies included), the match in play, then every
 * match still to come; all competitions together or one at a time. It starts at the match in play or the latest
 * result, with what comes next below it; earlier results are a tap away, a few at a time, above it.
 */
function Fixtures({ profile, competitions, onNavigate }: {
  profile: TeamProfile
  competitions: Competitions
  onNavigate: (route: Route) => void
}) {
  const { t } = useI18n()
  const [filter, setFilter] = useState<string | null>(null)
  const [earlierShown, setEarlierShown] = useState(0)
  const season = useMemo(() => seasonTimeline(profile.recentMatches, profile.upcomingMatches), [profile])
  const codes = [...new Set(season.map(match => match.leagueCode))]
  const shown = filter ? season.filter(match => match.leagueCode === filter) : season
  const { anchor, upcomingFrom } = timelinePoints(shown)
  const from = Math.max(0, (anchor ?? 0) - earlierShown)
  const played = shown.filter(match => match.status === 'Finished').length
  const anyFriendly = shown.some(match => isFriendlyIn(competitions, match))
  const pick = (code: string | null) => {
    setFilter(code)
    setEarlierShown(0)
  }

  return (
    <section className="panel" aria-label={t.team.fixtures}>
      <h3 className="panel__title">{t.team.fixturesTitle(played, shown.length - played)}</h3>
      {season.length === 0 ? (
        <EmptyState icon="📅" title={t.team.noFixtures} hint={t.team.noFixturesHint} />
      ) : (
        <>
          {codes.length > 1 && (
            <div className="quickbar team__filter" role="group" aria-label={t.team.filterByCompetition}>
              <button className={filter === null ? 'chip chip--active' : 'chip'} aria-pressed={filter === null} onClick={() => pick(null)}>
                {t.team.allCompetitions}
              </button>
              {codes.map(code => (
                <button key={code} className={filter === code ? 'chip chip--active' : 'chip'} aria-pressed={filter === code} onClick={() => pick(code)}>
                  {competitionName(competitions, code)}
                </button>
              ))}
            </div>
          )}
          {from > 0 && (
            <button className="fixtures__earlier" onClick={() => setEarlierShown(count => count + EarlierPerTap)}>
              ↑ {t.team.earlierMatches(from)}
            </button>
          )}
          <ul className="fixtures">
            {shown.slice(from).map((match, offset) => (
              <Fragment key={match.id}>
                {from + offset === upcomingFrom && <li className="fixtures__divider">{t.team.upcomingDivider}</li>}
                <FixtureRow match={match} teamId={profile.team.id} competitions={competitions} onNavigate={onNavigate} />
              </Fragment>
            ))}
          </ul>
          {anyFriendly && <p className="panel__note">{t.team.friendlyNote}</p>}
        </>
      )}
    </section>
  )
}

/** How many earlier results each tap on "Earlier matches" brings in. */
const EarlierPerTap = 10

/** The competition's name, or a "Friendly" tag in its place for a match that doesn't count. */
function CompetitionLabel({ match, competitions }: { match: Match; competitions: Competitions }) {
  const { t } = useI18n()
  const name = competitionName(competitions, match.leagueCode)

  return isFriendlyIn(competitions, match)
    ? <span className="friendly-tag" title={name}>{t.team.friendly}</span>
    : <>{name}</>
}

/** One result or fixture of the team's. Leads to the match if its competition is one the site follows. */
function FixtureRow({ match, teamId, competitions, onNavigate }: {
  match: Match
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
        <div className="hero__identity">
          <span className="skeleton skeleton--circle hero__crest" style={{ width: 72, height: 72 }} />
          <div className="hero__text"><Skeleton rows={3} /></div>
        </div>
      </div>
      <section className="panel"><Skeleton rows={6} /></section>
    </div>
  )
}
