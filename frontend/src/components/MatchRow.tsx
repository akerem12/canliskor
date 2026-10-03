import type { Match, Team } from '../api/types'
import { isInPlay } from '../api/types'
import type { Dictionary } from '../i18n/en'
import { useI18n } from '../i18n/useI18n'
import { formatTime } from '../time'
import { OddsStrip } from './Odds'

/** "HT", "FT" and the like; nothing before kickoff, and a live match shows its minute instead. */
function statusLabel(status: Match['status'], t: Dictionary): string {
  switch (status) {
    case 'HalfTime': return t.status.halfTimeShort
    case 'Finished': return t.status.fullTimeShort
    case 'Postponed': return t.status.postponedShort
    case 'Cancelled': return t.status.cancelledShort
    default: return ''
  }
}

function TeamName({ team, align }: { team: Team; align: 'home' | 'away' }) {
  return (
    <span className={`team team--${align}`} title={team.name}>
      {/* Same space with or without a logo, so team names line up. */}
      {team.logoUrl
        ? <img className="team__logo" src={team.logoUrl} alt="" width={20} height={20} loading="lazy" />
        : <span className="team__logo" aria-hidden />}
      <span className="team__name team__name--full">{team.name}</span>
      <span className="team__name team__name--short">{team.shortName}</span>
    </span>
  )
}

interface Props {
  match: Match
  justScored: boolean
  onOpen: () => void
}

export function MatchRow({ match, justScored, onOpen }: Props) {
  const { t } = useI18n()
  const live = isInPlay(match.status)
  const minute = match.status === 'Live' ? (match.clock ?? t.status.liveShort) : statusLabel(match.status, t)

  return (
    <li>
      <button
        className={['match', live && 'match--live', match.status !== 'Scheduled' && 'match--started', justScored && 'match--goal'].filter(Boolean).join(' ')}
        onClick={onOpen}
      >
        <span className="match__time">{formatTime(match.kickoff)}</span>
        <span className="match__minute">
          {match.status === 'Live' && <span className="pulse" aria-hidden />}
          {minute}
        </span>
        <TeamName team={match.homeTeam} align="home" />
        <span className="match__score" aria-label={match.score ? t.matchList.score(match.score.home, match.score.away) : t.matchList.notStarted}>
          {match.score ? `${match.score.home} - ${match.score.away}` : '-'}
        </span>
        <TeamName team={match.awayTeam} align="away" />
        {/* Prices are for what is still to come: once the match has kicked off they are history. */}
        {match.status === 'Scheduled' && match.odds && <OddsStrip odds={match.odds} className="match__odds" />}
        {justScored && <span className="match__goal-badge">{t.matchList.goal}</span>}
      </button>
    </li>
  )
}
