import { createContext, useCallback, useContext, useEffect, useRef, useState } from 'react'
import type { Match, MatchUpdatedMessage } from '../api/types'
import type { AlertSettings } from './alerts'
import { AlertsStorageKey, describeUpdate, isMatchWatched, noAlerts, parseAlertSettings, toggleMatch, wantsAlert } from './alerts'

/** "unsupported": this browser has no notifications at all. Otherwise the browser's own permission state. */
export type AlertPermission = 'unsupported' | NotificationPermission

export interface AlertsContextValue {
  permission: AlertPermission
  /** Alerts for every match of a favourite team are switched on. */
  teamAlerts: boolean
  /** Switches team alerts; switching on asks the browser for permission first if needed. */
  setTeamAlerts: (on: boolean) => Promise<void>
  isWatched: (matchId: string) => boolean
  /** Starts or stops alerts for one match; starting asks for permission first if needed. */
  toggleMatch: (match: Match) => Promise<void>
}

export const AlertsContext = createContext<AlertsContextValue | null>(null)

/** What the visitor has switched alerts on for, and the actions to change it. See useAlertsController. */
export function useAlerts(): AlertsContextValue {
  const value = useContext(AlertsContext)
  if (!value) throw new Error('useAlerts must be used inside <AlertsContext.Provider>')
  return value
}

const supported = () => typeof window !== 'undefined' && 'Notification' in window

function readStored(): AlertSettings {
  try {
    return parseAlertSettings(window.localStorage.getItem(AlertsStorageKey), Date.now())
  } catch {
    return noAlerts
  }
}

function writeStored(settings: AlertSettings) {
  try {
    window.localStorage.setItem(AlertsStorageKey, JSON.stringify(settings))
  } catch {
    // Storage unavailable: the choice holds until the page is closed.
  }
}

/**
 * Match alerts as browser notifications: goals, kick-off, half time and full time, for every match of a favourite
 * team (if switched on) and for single matches the visitor picked. Settings live in localStorage.
 *
 * Alerts come from the live updates this page already receives, so they work while the site is open in a tab,
 * also in the background, but not once the tab or the browser is closed.
 *
 * @param favoriteTeamIds Ids of the favourite teams. Favourite leagues don't alert.
 * @param onOpenMatch Called when a notification is clicked.
 * @returns The context value for the pages, and `handleUpdate` to feed every live update into.
 */
export function useAlertsController(favoriteTeamIds: ReadonlySet<string>, onOpenMatch: (match: Match) => void) {
  const [settings, setSettings] = useState(readStored)
  const [permission, setPermission] = useState<AlertPermission>(() => (supported() ? Notification.permission : 'unsupported'))

  // handleUpdate is called from the long-lived connection, so it reads the current values through refs.
  const settingsRef = useRef(settings)
  const favoritesRef = useRef(favoriteTeamIds)
  const openRef = useRef(onOpenMatch)
  useEffect(() => {
    settingsRef.current = settings
    favoritesRef.current = favoriteTeamIds
    openRef.current = onOpenMatch
  })

  const update = useCallback((change: (current: AlertSettings) => AlertSettings) => {
    setSettings(current => {
      const next = change(current)
      writeStored(next)
      return next
    })
  }, [])

  /** True if notifications may be shown, asking the visitor if they haven't decided yet. */
  const ensurePermission = useCallback(async () => {
    if (!supported()) return false
    const result = Notification.permission === 'default' ? await Notification.requestPermission() : Notification.permission
    setPermission(result)
    return result === 'granted'
  }, [])

  const setTeamAlerts = useCallback(async (on: boolean) => {
    if (on && !(await ensurePermission())) return
    update(current => ({ ...current, teamAlerts: on }))
  }, [ensurePermission, update])

  const toggle = useCallback(async (match: Match) => {
    const starting = !isMatchWatched(settingsRef.current, match.id)
    if (starting && !(await ensurePermission())) return
    update(current => toggleMatch(current, match))
  }, [ensurePermission, update])

  const handleUpdate = useCallback((message: MatchUpdatedMessage, previous: Match | undefined) => {
    if (!supported() || Notification.permission !== 'granted') return
    if (!wantsAlert(message.match, settingsRef.current, favoritesRef.current)) return

    const alert = describeUpdate(previous, message)
    if (!alert) return

    const notification = new Notification(alert.title, { body: alert.body, tag: alert.tag, icon: '/favicon.svg' })
    notification.onclick = () => {
      window.focus()
      openRef.current(message.match)
      notification.close()
    }
  }, [])

  const value: AlertsContextValue = {
    permission,
    teamAlerts: settings.teamAlerts && permission === 'granted',
    setTeamAlerts,
    isWatched: matchId => permission === 'granted' && isMatchWatched(settings, matchId),
    toggleMatch: toggle,
  }

  return { value, handleUpdate }
}
