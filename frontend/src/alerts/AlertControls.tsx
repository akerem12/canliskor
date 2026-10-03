import type { Match } from '../api/types'
import type { AlertPermission } from './useAlerts'
import { useAlerts } from './useAlerts'

/** Why alerts can't be switched on, in words; null if they can. */
function blockedReason(permission: AlertPermission): string | null {
  if (permission === 'unsupported') return "This browser doesn't support notifications."
  if (permission === 'denied') return 'Notifications are blocked for this site. Allow them in the browser\'s site settings (the icon left of the address) to use alerts.'
  return null
}

/** The switch for alerts on every match of a favourite team. Shown on the Favourites page. */
export function TeamAlertsSwitch({ teamCount }: { teamCount: number }) {
  const { permission, teamAlerts, setTeamAlerts } = useAlerts()
  const blocked = blockedReason(permission)

  return (
    <div className="alerts">
      <div className="alerts__text">
        <strong>🔔 Match alerts for your teams</strong>
        <span>
          {blocked
            ?? (teamCount === 0
              ? 'Star a team first: alerts are sent for favourite teams, not for whole leagues.'
              : 'Goals, kick-off, half time and full time of your favourite teams, while this site is open in a tab.')}
        </span>
      </div>
      <button
        className={teamAlerts ? 'switch switch--on' : 'switch'}
        role="switch"
        aria-checked={teamAlerts}
        aria-label="Match alerts for favourite teams"
        disabled={blocked !== null}
        onClick={() => void setTeamAlerts(!teamAlerts)}
      >
        <span className="switch__knob" />
      </button>
    </div>
  )
}

/** A bell that switches alerts on for one match only. Shown on the match's page until it is over. */
export function MatchAlertButton({ match }: { match: Match }) {
  const { permission, isWatched, toggleMatch } = useAlerts()
  if (match.status === 'Finished' || match.status === 'Cancelled' || match.status === 'Postponed') return null

  const blocked = blockedReason(permission)
  const watched = isWatched(match.id)

  return (
    <button
      className={watched ? 'bell bell--on' : 'bell'}
      aria-pressed={watched}
      disabled={blocked !== null}
      title={blocked ?? (watched ? 'Stop alerts for this match' : 'Get goals, kick-off and full time of this match as notifications')}
      onClick={() => void toggleMatch(match)}
    >
      {watched ? '🔔 Alerts on' : '🔕 Notify me'}
    </button>
  )
}
