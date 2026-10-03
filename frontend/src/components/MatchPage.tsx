import { useState } from 'react'
import type { Match, MatchEvent, MatchStat, MatchStatType, Score, Team } from '../api/types'
import { isInPlay } from '../api/types'
import { MatchAlertButton } from '../alerts/AlertControls'
import type { WatchMatch } from '../live/useLiveScores'
import { useMatchDetail } from '../live/useMatchDetail'
import { isGoal, withRunningScore } from '../matchEvents'
import { formatTime } from '../time'
import type { Route } from '../route'
import { EmptyState, Skeleton, TeamLogo } from './common'
import { Lineups } from './Lineups'
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

const statusText: Record<Match['status'], string> = {
  Scheduled: '',
  Live: 'Live',
  HalfTime: 'Half time',
  Finished: 'Full time',
  Postponed: 'Postponed',
  Cancelled: 'Cancelled',
}

const statLabels: Record<MatchStatType, string> = {
  Possession: 'Possession',
  Shots: 'Shots',
  ShotsOnTarget: 'Shots on target',
  Corners: 'Corners',
  Fouls: 'Fouls',
  Offsides: 'Offsides',
  YellowCards: 'Yellow cards',
  RedCards: 'Red cards',
  Saves: 'Saves',
}

type Tab = 'lineups' | 'events' | 'stats' | 'squads'

const tabLabels: Record<Tab, string> = { lineups: 'Line-ups', events: 'Events', stats: 'Statistics', squads: 'Squads' }

/** ?tab=squads → the tab to open, so a link leads to the same view. */
function tabFromUrl(): Tab | null {
  const tab = new URLSearchParams(window.location.search).get('tab')
  return tab !== null && tab in tabLabels ? (tab as Tab) : null
}

function writeTabToUrl(tab: Tab) {
  const url = new URL(window.location.href)
  url.searchParams.set('tab', tab)
  window.history.replaceState(window.history.state, '', url)
}

/** One match on a page of its own: the scoreline, then line-ups, events, statistics and squads as tabs. */
export function MatchPage({ leagueCode, matchId, leagueName, pushed, watchMatch, onNavigate, onClose }: Props) {
  const { detail, error } = useMatchDetail(leagueCode, matchId, pushed, watchMatch)
  // Until a tab is picked: line-ups once they are announced, otherwise events.
  const [pickedTab, setPickedTab] = useState(tabFromUrl)
  const pickTab = (picked: Tab) => {
    setPickedTab(picked)
    writeTabToUrl(picked)
  }
  const tab = pickedTab ?? (detail?.lineups ? 'lineups' : 'events')

  // The pushed match has the live clock; the detail's copy may be up to one refresh behind.
  const match = pushed ?? detail?.match

  return (
    <main className="detail" aria-label={match ? `${match.homeTeam.name} vs ${match.awayTeam.name}` : 'Match details'}>
      <header className="detail__top">
        <button className="page__back" onClick={onClose}>← Back</button>
        <span className="detail__actions">
          {match && <MatchAlertButton match={match} />}
          <button className="page__crumb" onClick={() => onNavigate({ view: 'league', leagueCode })}>{leagueName ?? leagueCode}</button>
        </span>
      </header>

      {match
        ? <Scoreline match={match} onOpenTeam={teamId => onNavigate({ view: 'team', leagueCode, teamId })} />
        : !error && <Skeleton rows={4} height={22} />}
      {match && !detail && !error && <Skeleton rows={6} />}
      {error && !detail && (
        <EmptyState icon="⚠️" title="This match can't be loaded right now." hint="The data source may be busy, or the link is wrong." />
      )}

      {detail && match && (
        <>
          <nav className="tabs detail__tabs" aria-label="Match sections">
            {(Object.keys(tabLabels) as Tab[]).map(t => (
              <button key={t} className={t === tab ? 'tab tab--active' : 'tab'} onClick={() => pickTab(t)}>
                {tabLabels[t]}
              </button>
            ))}
          </nav>

          {tab === 'lineups' && <Lineups lineups={detail.lineups} match={match} />}
          {tab === 'events' && <Events events={detail.events} match={match} />}
          {tab === 'stats' && (detail.stats.length > 0
            ? <Stats stats={detail.stats} />
            : <p className="detail__empty">Statistics appear once the match has kicked off.</p>)}
          {tab === 'squads' && <Squads match={match} />}
          {tab !== 'squads' && <p className="detail__updated">updated {formatTime(detail.lastUpdatedUtc)}</p>}
        </>
      )}
    </main>
  )
}

function Scoreline({ match, onOpenTeam }: { match: Match; onOpenTeam: (teamId: string) => void }) {
  const live = isInPlay(match.status)
  const status = match.status === 'Live' ? (match.clock ?? 'Live') : statusText[match.status]

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
  return (
    <button className="scoreline__team" onClick={() => onOpen(team.id)} title={`${team.name}: form, table and fixtures`}>
      <TeamLogo team={team} size={48} />
      <span>{team.name}</span>
    </button>
  )
}

function Events({ events, match }: { events: MatchEvent[]; match: Match }) {
  if (events.length === 0) {
    return (
      <p className="detail__empty">
        {match.status === 'Scheduled'
          ? `Kick-off at ${formatTime(match.kickoff)}. Goals, cards and substitutions will appear here.`
          : 'No goals, cards or substitutions yet.'}
      </p>
    )
  }

  return (
    <section aria-label="Match events">
      <ol className="events">
        {withRunningScore(events).map(({ event, score }, i) => (
          <EventRow key={i} event={event} score={score} />
        ))}
      </ol>
    </section>
  )
}

function EventRow({ event, score }: { event: MatchEvent; score: Score }) {
  const side = event.side === 'Home' ? 'home' : 'away'
  return (
    <li className={`event event--${side}${isGoal(event) ? ' event--goal' : ''}`}>
      <span className="event__clock">{event.clock}</span>
      <span className="event__content">
        <EventIcon type={event.type} />
        <span className="event__text">
          <EventText event={event} />
        </span>
        {isGoal(event) && <span className="event__score">{score.home} - {score.away}</span>}
      </span>
    </li>
  )
}

function EventIcon({ type }: { type: MatchEvent['type'] }) {
  switch (type) {
    case 'YellowCard':
      return <span className="card card--yellow" role="img" aria-label="Yellow card" />
    case 'RedCard':
      return <span className="card card--red" role="img" aria-label="Red card" />
    case 'Substitution':
      return <span className="event__icon" role="img" aria-label="Substitution">⇄</span>
    default:
      return <span className="event__icon" role="img" aria-label="Goal">⚽</span>
  }
}

function EventText({ event }: { event: MatchEvent }) {
  const player = event.player ?? 'Unknown player'
  switch (event.type) {
    case 'Substitution':
      return (
        <>
          <span className="event__in">▲ {player}</span>
          {event.relatedPlayer && <span className="event__out">▼ {event.relatedPlayer}</span>}
        </>
      )
    case 'PenaltyGoal':
      return <span className="event__player">{player} <span className="event__note">(pen.)</span></span>
    case 'OwnGoal':
      return <span className="event__player">{player} <span className="event__note">(o.g.)</span></span>
    case 'Goal':
      return (
        <>
          <span className="event__player">{player}</span>
          {event.relatedPlayer && <span className="event__note">assist: {event.relatedPlayer}</span>}
        </>
      )
    default:
      return <span className="event__player">{player}</span>
  }
}

function Stats({ stats }: { stats: MatchStat[] }) {
  return (
    <section aria-label="Statistics">
      <dl className="stats">
        {stats.map(stat => {
          const total = stat.home + stat.away
          const homeShare = total > 0 ? (stat.home / total) * 100 : 50
          const format = (value: number) => (stat.type === 'Possession' ? `${Math.round(value)}%` : String(value))
          return (
            <div key={stat.type} className="stat">
              <dt className="stat__label">{statLabels[stat.type]}</dt>
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
