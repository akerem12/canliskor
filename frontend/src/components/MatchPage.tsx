import { useState } from 'react'
import type { Match, MatchEvent, MatchStat, Score, Team } from '../api/types'
import { isInPlay } from '../api/types'
import { MatchAlertButton } from '../alerts/AlertControls'
import { getStandings } from '../api/http'
import { useFetch } from '../api/useFetch'
import type { Dictionary } from '../i18n/en'
import { useI18n } from '../i18n/useI18n'
import type { WatchMatch } from '../live/useLiveScores'
import { useMatchDetail } from '../live/useMatchDetail'
import { assistOf, isGoal, withRunningScore } from '../matchEvents'
import { PlayerName } from '../players/PlayerName'
import { formatTime } from '../time'
import type { Route } from '../route'
import { tabFromUrl, writeTabToUrl } from '../urlTab'
import { EmptyState, OwnGoalMark, Skeleton, TeamLogo } from './common'
import { Lineups } from './Lineups'
import { MatchOverview } from './MatchOverview'
import { Squads } from './Squads'
import { StandingsTable } from './StandingsTable'

interface Props {
  leagueCode: string
  matchId: string
  leagueName: string | undefined
  /** The match as kept current over SignalR (live clock, score), if it is on the day being shown. */
  pushed: Match | undefined
  watchMatch: WatchMatch
  onNavigate: (route: Route) => void
  onClose: () => void
}

/** "Half time", "Full time" and the like; nothing before kickoff. */
function statusText(status: Match['status'], t: Dictionary): string {
  switch (status) {
    case 'Live': return t.status.live
    case 'HalfTime': return t.status.halfTime
    case 'Finished': return t.status.fullTime
    case 'Postponed': return t.status.postponed
    case 'Cancelled': return t.status.cancelled
    default: return ''
  }
}

const tabs = ['details', 'lineups', 'stats', 'events', 'standings', 'squads'] as const
type Tab = typeof tabs[number]

const tabLabel = (tab: Tab, t: Dictionary) => ({
  details: t.match.overview,
  lineups: t.match.lineups,
  stats: t.match.statistics,
  events: t.match.events,
  standings: t.match.standings,
  squads: t.match.squads,
})[tab]

/** One match on a page of its own: the scoreline, then its details, line-ups, statistics, events, table and squads as tabs. */
export function MatchPage({ leagueCode, matchId, leagueName, pushed, watchMatch, onNavigate, onClose }: Props) {
  const { t } = useI18n()
  const { detail, error } = useMatchDetail(leagueCode, matchId, pushed, watchMatch)
  const [pickedTab, setPickedTab] = useState(() => tabFromUrl(tabs))
  const pickTab = (picked: Tab) => {
    setPickedTab(picked)
    writeTabToUrl(picked)
  }

  // The pushed match has the live clock; the detail's copy may be up to one refresh behind.
  const match = pushed ?? detail?.match

  const tab = pickedTab ?? 'details'

  return (
    <main className="detail" aria-label={match ? t.match.versus(match.homeTeam.name, match.awayTeam.name) : t.match.details}>
      <header className="detail__top">
        <button className="page__back" onClick={onClose}>{t.common.back}</button>
        <span className="detail__actions">
          {match && <MatchAlertButton match={match} />}
          <button className="page__crumb" onClick={() => onNavigate({ view: 'league', leagueCode })}>{leagueName ?? leagueCode}</button>
        </span>
      </header>

      {match
        ? <Scoreline match={match} onOpenTeam={teamId => onNavigate({ view: 'team', leagueCode, teamId })} />
        : !error && <Skeleton rows={4} height={22} />}
      {match && !detail && !error && <Skeleton rows={6} />}
      {error && !detail && <EmptyState icon="⚠️" title={t.match.cantLoad} hint={t.match.cantLoadHint} />}

      {detail && match && (
        <>
          <nav className="tabs detail__tabs" aria-label={t.match.sections}>
            {tabs.map(known => (
              <button key={known} className={known === tab ? 'tab tab--active' : 'tab'} onClick={() => pickTab(known)}>
                {tabLabel(known, t)}
              </button>
            ))}
          </nav>

          {tab === 'details' && <MatchOverview detail={detail} match={match} />}
          {tab === 'lineups' && <Lineups lineups={detail.lineups} match={match} />}
          {tab === 'events' && <Events events={detail.events} match={match} />}
          {tab === 'stats' && (detail.stats.length > 0
            ? <Stats stats={detail.stats} />
            : <p className="detail__empty">{t.match.statsLater}</p>)}
          {tab === 'standings' && <MatchStandings match={match} onNavigate={onNavigate} />}
          {tab === 'squads' && <Squads match={match} />}
          {tab !== 'squads' && tab !== 'standings' && <p className="detail__updated">{t.common.updated(formatTime(detail.lastUpdatedUtc))}</p>}
        </>
      )}
    </main>
  )
}

/** The competition's table, the two teams' rows marked; in a group stage, only their groups. */
function MatchStandings({ match, onNavigate }: { match: Match; onNavigate: (route: Route) => void }) {
  const { t } = useI18n()
  const standings = useFetch(`standings/${match.leagueCode}`, () => getStandings(match.leagueCode))

  return (
    <section aria-label={t.match.standings}>
      {standings.loading && <Skeleton rows={12} />}
      {standings.error && <EmptyState icon="📊" title={t.leagues.tableUnavailable} hint={t.leagues.sourceBusy} onRetry={standings.retry} />}
      {standings.data && (standings.data.groups.length === 0
        ? <EmptyState icon="📊" title={t.leagues.noTable} hint={t.leagues.noTableHintTeam} />
        : <StandingsTable standings={standings.data} teamIds={[match.homeTeam.id, match.awayTeam.id]} onNavigate={onNavigate} />)}
    </section>
  )
}

function Scoreline({ match, onOpenTeam }: { match: Match; onOpenTeam: (teamId: string) => void }) {
  const { t } = useI18n()
  const live = isInPlay(match.status)
  const status = match.status === 'Live' ? (match.clock ?? t.status.live) : statusText(match.status, t)

  return (
    <div className={live ? 'scoreline scoreline--live' : 'scoreline'}>
      <TeamBadge team={match.homeTeam} onOpen={onOpenTeam} />
      <div className="scoreline__center">
        <span className="scoreline__score">
          {match.score ? `${match.score.home} - ${match.score.away}` : formatTime(match.kickoff)}
        </span>
        <span className="scoreline__status">
          {match.status === 'Live' && <span className="pulse" aria-hidden />}
          {status}
        </span>
      </div>
      <TeamBadge team={match.awayTeam} onOpen={onOpenTeam} />
    </div>
  )
}

function TeamBadge({ team, onOpen }: { team: Team; onOpen: (teamId: string) => void }) {
  const { t } = useI18n()

  return (
    <button className="scoreline__team" onClick={() => onOpen(team.id)} title={t.match.teamTitle(team.name)}>
      <TeamLogo team={team} size={48} />
      <span>{team.name}</span>
    </button>
  )
}

function Events({ events, match }: { events: MatchEvent[]; match: Match }) {
  const { t } = useI18n()

  if (events.length === 0) {
    return (
      <p className="detail__empty">
        {match.status === 'Scheduled' ? t.match.eventsLater(formatTime(match.kickoff)) : t.match.noEvents}
      </p>
    )
  }

  return (
    <section aria-label={t.match.eventsLabel}>
      <ol className="events">
        {withRunningScore(events).map(({ event, score }, i) => (
          <EventRow key={i} event={event} score={score} leagueCode={match.leagueCode} />
        ))}
      </ol>
    </section>
  )
}

function EventRow({ event, score, leagueCode }: { event: MatchEvent; score: Score; leagueCode: string }) {
  const side = event.side === 'Home' ? 'home' : 'away'
  return (
    <li className={`event event--${side}${isGoal(event) ? ' event--goal' : ''}`}>
      <span className="event__clock">{event.clock}</span>
      <span className="event__content">
        <EventIcon type={event.type} />
        <span className="event__text">
          <EventText event={event} leagueCode={leagueCode} />
        </span>
        {isGoal(event) && <span className="event__score">{score.home} - {score.away}</span>}
      </span>
    </li>
  )
}

function EventIcon({ type }: { type: MatchEvent['type'] }) {
  const { t } = useI18n()

  switch (type) {
    case 'YellowCard':
      return <span className="card card--yellow" role="img" aria-label={t.match.yellowCard} />
    case 'RedCard':
      return <span className="card card--red" role="img" aria-label={t.match.redCard} />
    case 'Substitution':
      return <span className="event__icon" role="img" aria-label={t.match.substitution}>⇄</span>
    case 'OwnGoal':
      return <OwnGoalMark />
    default:
      return <span className="event__icon" role="img" aria-label={t.match.goal}>⚽</span>
  }
}

function EventText({ event, leagueCode }: { event: MatchEvent; leagueCode: string }) {
  const { t } = useI18n()
  const player = <PlayerName leagueCode={leagueCode} playerId={event.playerId} name={event.player ?? t.match.unknownPlayer} />
  const assist = assistOf(event)

  switch (event.type) {
    case 'Substitution':
      return (
        <>
          <span className="event__in">▲ {player}</span>
          {event.relatedPlayer && (
            <span className="event__out">
              ▼ <PlayerName leagueCode={leagueCode} playerId={event.relatedPlayerId} name={event.relatedPlayer} />
            </span>
          )}
        </>
      )
    case 'PenaltyGoal':
      return <span className="event__player">{player} <span className="event__note">{t.match.penalty}</span></span>
    case 'OwnGoal':
      return <span className="event__player">{player} <span className="event__note event__note--own-goal" title={t.lineups.ownGoal}>{t.match.ownGoal}</span></span>
    case 'Goal':
      return (
        <>
          <span className="event__player">{player}</span>
          {assist && (
            <span className="event__assist">
              <span className="assist-badge" role="img" aria-label={t.match.assist} title={t.match.assist}>A</span>
              <PlayerName leagueCode={leagueCode} playerId={assist.playerId} name={assist.name} />
            </span>
          )}
        </>
      )
    default:
      return <span className="event__player">{player}</span>
  }
}

function Stats({ stats }: { stats: MatchStat[] }) {
  const { t } = useI18n()

  return (
    <section aria-label={t.match.statistics}>
      <dl className="stats">
        {stats.map(stat => {
          const total = stat.home + stat.away
          const homeShare = total > 0 ? (stat.home / total) * 100 : 50
          const format = (value: number) => (stat.type === 'Possession' ? `${Math.round(value)}%` : String(value))
          return (
            <div key={stat.type} className="stat">
              <dt className="stat__label">{t.match.stats[stat.type]}</dt>
              <dd className="stat__values">
                <span>{format(stat.home)}</span>
                <span className="stat__bar" aria-hidden>
                  <span className="stat__bar-home" style={{ width: `${homeShare}%` }} />
                </span>
                <span>{format(stat.away)}</span>
              </dd>
            </div>
          )
        })}
      </dl>
    </section>
  )
}
