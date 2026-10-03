import { useEffect, useRef } from 'react'
import type { Match, MatchEvent, MatchStat, MatchStatType, Score, Team } from '../api/types'
import { isInPlay } from '../api/types'
import { useMatchDetail } from '../live/useMatchDetail'
import { isGoal, withRunningScore } from '../matchEvents'
import { formatTime } from '../time'

interface Props {
  leagueCode: string
  matchId: string
  leagueName: string | undefined
  /** The match as kept current over SignalR (live clock, score), if it is on the day being shown. */
  pushed: Match | undefined
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

/** Goals, cards and substitutions in order, plus team statistics. A modal dialog: Esc or a click outside closes it. */
export function MatchDetailDialog({ leagueCode, matchId, leagueName, pushed, onClose }: Props) {
  const dialogRef = useRef<HTMLDialogElement>(null)
  const { detail, error } = useMatchDetail(leagueCode, matchId, pushed)

  useEffect(() => {
    const dialog = dialogRef.current
    if (dialog && !dialog.open) dialog.showModal()
  }, [])

  // The pushed match has the live clock; the detail's copy may be up to one refresh behind.
  const match = pushed ?? detail?.match

  return (
    <dialog
      ref={dialogRef}
      className="detail"
      aria-label={match ? `${match.homeTeam.name} vs ${match.awayTeam.name}` : 'Match details'}
      onClose={onClose}
      // Clicks on the backdrop target the dialog element itself; clicks on the content target its children.
      onClick={e => e.target === e.currentTarget && dialogRef.current?.close()}
    >
      <div className="detail__body">
        <header className="detail__top">
          <span className="detail__league">{leagueName ?? leagueCode}</span>
          <button className="detail__close" onClick={() => dialogRef.current?.close()} aria-label="Close">×</button>
        </header>

        {match ? <Scoreline match={match} /> : !error && <p className="notice">Loading match…</p>}
        {error && !detail && <p className="notice notice--error">Can't load match details ({error}).</p>}

        {detail && match && (
          <>
            <Events events={detail.events} match={match} />
            {detail.stats.length > 0 && <Stats stats={detail.stats} />}
            <p className="detail__updated">updated {formatTime(detail.lastUpdatedUtc)}</p>
          </>
        )}
      </div>
    </dialog>
  )
}

function Scoreline({ match }: { match: Match }) {
  const live = isInPlay(match.status)
  const status = match.status === 'Live' ? (match.clock ?? 'Live') : statusText[match.status]

  return (
    <div className={live ? 'scoreline scoreline--live' : 'scoreline'}>
      <TeamBadge team={match.homeTeam} />
      <div className="scoreline__center">
        <span className="scoreline__score">
          {match.score ? `${match.score.home} - ${match.score.away}` : formatTime(match.kickoff)}
        </span>
        <span className="scoreline__status">
          {match.status === 'Live' && <span className="pulse" aria-hidden />}
          {status}
        </span>
      </div>
      <TeamBadge team={match.awayTeam} />
    </div>
  )
}

function TeamBadge({ team }: { team: Team }) {
  return (
    <div className="scoreline__team">
      {team.logoUrl
        ? <img src={team.logoUrl} alt="" width={48} height={48} />
        : <span className="scoreline__logo-placeholder" aria-hidden />}
      <span>{team.name}</span>
    </div>
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
      <h3 className="detail__heading">Events</h3>
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
      <h3 className="detail__heading">Statistics</h3>
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
