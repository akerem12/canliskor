import { useState } from 'react'
import type { Match, MatchEvent, MatchStat, Score, Team } from '../api/types'
import { isInPlay } from '../api/types'
import { MatchAlertButton } from '../alerts/AlertControls'
import type { Dictionary } from '../i18n/en'
import { useI18n } from '../i18n/useI18n'
import type { WatchMatch } from '../live/useLiveScores'
import { useMatchDetail } from '../live/useMatchDetail'
import { assistOf, isGoal, withRunningScore } from '../matchEvents'
import { PlayerName } from '../players/PlayerName'
import { formatTime } from '../time'
import type { Route } from '../route'
import { EmptyState, Skeleton, TeamLogo } from './common'
import { Lineups } from './Lineups'
import { OddsBoard } from './Odds'
import { Squads } from './Squads'

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

const tabs = ['lineups', 'events', 'stats', 'squads'] as const
type Tab = typeof tabs[number]

const tabLabel = (tab: Tab, t: Dictionary) =>
  ({ lineups: t.match.lineups, events: t.match.events, stats: t.match.statistics, squads: t.match.squads })[tab]

/** ?tab=squads → the tab to open, so a link leads to the same view. */
function tabFromUrl(): Tab | null {
  const tab = new URLSearchParams(window.location.search).get('tab')
  return tabs.find(known => known === tab) ?? null
}

function writeTabToUrl(tab: Tab) {
  const url = new URL(window.location.href)
  url.searchParams.set('tab', tab)
  window.history.replaceState(window.history.state, '', url)
}

/** One match on a page of its own: the scoreline, then line-ups, events, statistics and squads as tabs. */
export function MatchPage({ leagueCode, matchId, leagueName, pushed, watchMatch, onNavigate, onClose }: Props) {
  const { t } = useI18n()
  const { detail, error } = useMatchDetail(leagueCode, matchId, pushed, watchMatch)
  const [pickedTab, setPickedTab] = useState(tabFromUrl)
  const pickTab = (picked: Tab) => {
    setPickedTab(picked)
    writeTabToUrl(picked)
  }

  // The pushed match has the live clock; the detail's copy may be up to one refresh behind.
  const match = pushed ?? detail?.match

  // Until a tab is picked: line-ups once they are announced, or the possible ones before kick-off; otherwise events.
  const tab = pickedTab ?? (detail?.lineups || match?.status === 'Scheduled' ? 'lineups' : 'events')

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
      {match?.odds && <OddsBoard odds={match.odds} match={match} />}
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

          {tab === 'lineups' && <Lineups lineups={detail.lineups} match={match} />}
          {tab === 'events' && <Events events={detail.events} match={match} />}
          {tab === 'stats' && (detail.stats.length > 0
            ? <Stats stats={detail.stats} />
            : <p className="detail__empty">{t.match.statsLater}</p>)}
          {tab === 'squads' && <Squads match={match} />}
          {tab !== 'squads' && <p className="detail__updated">{t.common.updated(formatTime(detail.lastUpdatedUtc))}</p>}
        </>
      )}
    </main>
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
      return <span className="event__player">{player} <span className="event__note">{t.match.ownGoal}</span></span>
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
