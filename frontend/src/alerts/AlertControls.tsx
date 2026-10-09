import type { Match } from '../api/types'
import type { Dictionary } from '../i18n/en'
import { useI18n } from '../i18n/useI18n'
import type { AlertPermission } from './useAlerts'
import { useAlerts } from './useAlerts'

/** Why alerts can't be switched on, in words; null if they can. */
function blockedReason(permission: AlertPermission, t: Dictionary): string | null {
  if (permission === 'unsupported') return t.alerts.unsupported
  if (permission === 'denied') return t.alerts.denied
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
          <span>{blocked ?? (teamCount === 0 ? t.alerts.starFirst : t.alerts.description)}</span>
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
          <legend>{t.alerts.pushLegend}</legend>
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

/** A bell that switches alerts on for one match only. Shown on the match's page until it is over. */
export function MatchAlertButton({ match }: { match: Match }) {
  const { t } = useI18n()
  const { permission, isWatched, toggleMatch } = useAlerts()
  if (match.status === 'Finished' || match.status === 'Cancelled' || match.status === 'Postponed') return null

  const blocked = blockedReason(permission, t)
  const watched = isWatched(match.id)

  return (
    <button
      className={watched ? 'bell bell--on' : 'bell'}
      aria-pressed={watched}
      disabled={blocked !== null}
      title={blocked ?? (watched ? t.alerts.stop : t.alerts.start)}
      onClick={() => void toggleMatch(match)}
    >
      {watched ? t.alerts.on : t.alerts.off}
    </button>
  )
}
