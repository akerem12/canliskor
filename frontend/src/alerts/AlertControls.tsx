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

/**
 * Favourite teams always alert, so there is nothing to switch. This only speaks up, on the Favourites page, when
 * notifications aren't allowed: why not, or a button to allow them (for teams starred before permission was asked).
 */
export function TeamAlertsNotice({ teamCount }: { teamCount: number }) {
  const { t } = useI18n()
  const { permission, askPermission } = useAlerts()
  if (teamCount === 0 || permission === 'granted') return null

  const blocked = blockedReason(permission, t)
  return (
    <div className="alerts" role="status">
      <div className="alerts__row">
        <span className="alerts__text">{blocked ?? t.alerts.enableHint}</span>
        {!blocked && <button className="cta cta--small" onClick={() => void askPermission()}>{t.alerts.enable}</button>}
      </div>
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
