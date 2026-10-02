import type { Match, Team } from '../api/types'
import { isInPlay } from '../api/types'
import { formatTime } from '../time'

const statusLabel: Record<Match['status'], string> = {
  Scheduled: '',
  Live: '',
  HalfTime: 'HT',
  Finished: 'FT',
  Postponed: 'PP',
  Cancelled: 'CANC',
}

function TeamName({ team, align }: { team: Team; align: 'home' | 'away' }) {
  return (
    <span className={`team team--${align}`} title={team.name}>
      {team.logoUrl && <img className="team__logo" src={team.logoUrl} alt="" width={20} height={20} loading="lazy" />}
      <span className="team__name">{team.name}</span>
    </span>
  )
}

export function MatchRow({ match, justScored }: { match: Match; justScored: boolean }) {
  const live = isInPlay(match.status)
  const minute = match.status === 'Live' ? (match.clock ?? 'LIVE') : statusLabel[match.status]

  return (
    <li className={['match', live && 'match--live', match.status !== 'Scheduled' && 'match--started', justScored && 'match--goal'].filter(Boolean).join(' ')}>
      <span className="match__time">{formatTime(match.kickoff)}</span>
      <span className="match__minute">
        {match.status === 'Live' && <span className="pulse" aria-hidden />}
        {minute}
      </span>
      <TeamName team={match.homeTeam} align="home" />
      <span className="match__score" aria-label={match.score ? `${match.score.home} to ${match.score.away}` : 'not started'}>
        {match.score ? `${match.score.home} - ${match.score.away}` : '-'}
      </span>
      <TeamName team={match.awayTeam} align="away" />
      {justScored && <span className="match__goal-badge">GOAL</span>}
    </li>
  )
}
