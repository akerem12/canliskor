import type { Match } from '../api/types'
import type { Dictionary } from '../i18n/en'
import { useI18n } from '../i18n/useI18n'
import { isNativeApp } from '../native/app'
import type { AlertPermission } from './useAlerts'
import { useAlerts } from './useAlerts'

/** Why alerts can't be switched on, in words; null if they can. */
function blockedReason(permission: AlertPermission, t: Dictionary): string | null {
  if (permission === 'unsupported') return t.alerts.unsupported
  if (permission === 'denied') return isNativeApp ? t.alerts.deniedApp : t.alerts.denied
  return null
}

/** The switch for alerts on every match of a favourite team. Shown on the Favourites page. */
export function TeamAlertsSwitch({ teamCount }: { teamCount: number }) {
  const { t } = useI18n()
  const { permission, teamAlerts, setTeamAlerts, pushSupported, kickoffReminder, setKickoffReminder, lineupAlerts, setLineupAlerts } = useAlerts()
  const blocked = blockedReason(permission, t)

  return (
    <div className="alerts">
      <div className="alerts__row">
        <div className="alerts__text">
          <strong>{t.alerts.heading}</strong>
          <span>{blocked ?? (teamCount === 0 ? t.alerts.starFirst : isNativeApp ? t.alerts.descriptionApp : t.alerts.description)}</span>
        </div>
        <button
          className={teamAlerts ? 'switch switch--on' : 'switch'}
          role="switch"
          aria-checked={teamAlerts}
          aria-label={t.alerts.switchLabel}
          disabled={blocked !== null}
          onClick={() => void setTeamAlerts(!teamAlerts)}
        >
          <span className="switch__knob" />
        </button>
      </div>
      {/* The two the server sends. They also cover matches picked with the bell, so they show whatever the switch says. */}
      {blocked === null && pushSupported && (
        <fieldset className="alerts__options">
          <legend>{isNativeApp ? t.alerts.pushLegendApp : t.alerts.pushLegend}</legend>
          <label>
            <input type="checkbox" checked={kickoffReminder} onChange={event => setKickoffReminder(event.target.checked)} />
            {t.alerts.kickoffReminder}
          </label>
          <label>
            <input type="checkbox" checked={lineupAlerts} onChange={event => setLineupAlerts(event.target.checked)} />
            {t.alerts.lineupAlerts}
          </label>
        </fieldset>
      )}
    </div>
  )
}

/** A bell that switches alerts on for one match only: filled while they are on, an outline while off. Shown on the
 * match's page until it is over; what it does is in its tooltip and label. */
export function MatchAlertButton({ match }: { match: Match }) {
  const { t } = useI18n()
  const { permission, isWatched, toggleMatch } = useAlerts()
  if (match.status === 'Finished' || match.status === 'Cancelled' || match.status === 'Postponed') return null

  const blocked = blockedReason(permission, t)
  const watched = isWatched(match.id)
  const label = blocked ?? (watched ? t.alerts.stop : t.alerts.start)

  return (
    <button
      className={watched ? 'bell bell--on' : 'bell'}
      aria-pressed={watched}
      aria-label={label}
      disabled={blocked !== null}
      title={label}
      onClick={() => void toggleMatch(match)}
    >
      <svg viewBox="0 0 24 24" width="22" height="22" aria-hidden>
        <path
          d="M12 3a6 6 0 0 0-6 6v3.6L4.3 15.4A1 1 0 0 0 5.2 17h13.6a1 1 0 0 0 .9-1.6L18 12.6V9a6 6 0 0 0-6-6Zm-2.2 16a2.3 2.3 0 0 0 4.4 0Z"
          fill={watched ? 'currentColor' : 'none'}
          stroke="currentColor"
          strokeWidth="1.8"
          strokeLinejoin="round"
        />
      </svg>
    </button>
  )
}
