import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState } from 'react'
import type { Match, MatchUpdatedMessage } from '../api/types'
import type { Dictionary } from '../i18n/en'
import type { AlertSettings } from './alerts'
import { AlertsStorageKey, describeUpdate, isMatchWatched, noAlerts, parseAlertSettings, pushWishes, toggleMatch, wantsAlert } from './alerts'
import { isNativeApp, onAppActiveChange } from '../native/app'
import { nativePermission, requestNativePermission, startNativeNotifications } from '../native/notifications'
import { pushSupported, syncPush } from './push'

/** "unsupported": this browser has no notifications at all. Otherwise the browser's own permission state. */
export type AlertPermission = 'unsupported' | NotificationPermission

export interface AlertsContextValue {
  permission: AlertPermission
  /**
   * Asks the browser (or the phone) for permission to notify, if the visitor hasn't decided yet. Called when a team
   * is starred, since that switches its alerts on; it must run straight from the click, or Safari won't ask.
   */
  askPermission: () => Promise<void>
  isWatched: (matchId: string) => boolean
  /** Starts or stops alerts for one match; starting asks for permission first if needed. */
  toggleMatch: (match: Match) => Promise<void>
}

/** How often an open page repeats what it wants to the server, in case the server lost it. */
const PushResyncMs = 10 * 60 * 1000

export const AlertsContext = createContext<AlertsContextValue | null>(null)

/** What the visitor has switched alerts on for, and the actions to change it. See useAlertsController. */
export function useAlerts(): AlertsContextValue {
  const value = useContext(AlertsContext)
  if (!value) throw new Error('useAlerts must be used inside <AlertsContext.Provider>')
  return value
}

/** Notifications exist here: always in the Android app, in a browser if it has the Notification API. */
const supported = () => isNativeApp || (typeof window !== 'undefined' && 'Notification' in window)

/** What the browser says right now. In the app the answer has to be asked for, so it starts as "not asked yet". */
const knownPermission = (): AlertPermission => (isNativeApp ? 'default' : supported() ? Notification.permission : 'unsupported')

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
 * Match alerts, for every match of a favourite team and for single matches the visitor picked with the bell (kept
 * in localStorage). Favouriting a team is all it takes; there is nothing else to switch on.
 *
 * The server sends them all as push notifications, which arrive also while the site or the app is closed: the
 * reminder half an hour before kick-off, the line-ups, the kick-off, goals, missed penalties and red cards. For
 * that it is told what this browser or phone follows (see push.ts). A browser without push (one that can only
 * show notifications from an open page) gets goals, kick-off, half time and full time from the live updates this
 * page receives instead, while it is open.
 *
 * @param favoriteTeamIds Ids of the favourite teams. Favourite leagues don't alert.
 * @param onOpenMatch Called when a notification is clicked.
 * @param dictionary The words of the current language, for the notifications' text.
 * @param language The current language's code; the server writes the push notifications in it.
 * @returns The context value for the pages, and `handleUpdate` to feed every live update into.
 */
export function useAlertsController(favoriteTeamIds: ReadonlySet<string>, onOpenMatch: (match: Match) => void, dictionary: Dictionary, language: string) {
  const [settings, setSettings] = useState(readStored)
  const [permission, setPermission] = useState<AlertPermission>(knownPermission)

  // handleUpdate is called from the long-lived connection, so it reads the current values through refs.
  const settingsRef = useRef(settings)
  const favoritesRef = useRef(favoriteTeamIds)
  const openRef = useRef(onOpenMatch)
  const dictionaryRef = useRef(dictionary)
  const permissionRef = useRef(permission)
  useEffect(() => {
    permissionRef.current = permission
    settingsRef.current = settings
    favoritesRef.current = favoriteTeamIds
    openRef.current = onOpenMatch
    dictionaryRef.current = dictionary
  })

  // The app: listen for taps on notifications from the start, and ask Android what is allowed, again whenever
  // the app comes back to the front (the answer may have been changed in the phone's settings meanwhile).
  const channelName = dictionary.alerts.channelName
  useEffect(() => {
    if (!isNativeApp) return
    startNativeNotifications(channelName)
    const ask = () => void nativePermission().then(setPermission, () => {})
    ask()
    return onAppActiveChange(active => {
      if (active) ask()
    })
  }, [channelName])

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
    let result: NotificationPermission
    if (isNativeApp) {
      const current = await nativePermission()
      result = current === 'default' ? await requestNativePermission() : current
    } else {
      result = Notification.permission === 'default' ? await Notification.requestPermission() : Notification.permission
    }
    setPermission(result)
    return result === 'granted'
  }, [])

  const askPermission = useCallback(async () => {
    await ensurePermission()
  }, [ensurePermission])

  const toggle = useCallback(async (match: Match) => {
    const starting = !isMatchWatched(settingsRef.current, match.id)
    if (starting && !(await ensurePermission())) return
    update(current => toggleMatch(current, match))
  }, [ensurePermission, update])

  // What the server should push, as text: the effect below only runs when the wishes themselves change.
  const wishes = useMemo(
    () => JSON.stringify(permission === 'granted' ? pushWishes(settings, favoriteTeamIds, language) : null),
    [permission, settings, favoriteTeamIds, language],
  )
  useEffect(() => {
    const sync = () => void syncPush(JSON.parse(wishes))
    const whenShown = () => {
      if (document.visibilityState === 'visible') sync()
    }

    sync()
    const timer = window.setInterval(sync, PushResyncMs)
    document.addEventListener('visibilitychange', whenShown)
    return () => {
      window.clearInterval(timer)
      document.removeEventListener('visibilitychange', whenShown)
    }
  }, [wishes])

  const handleUpdate = useCallback((message: MatchUpdatedMessage, previous: Match | undefined) => {
    // Where push works the server sends all of it, also while the page is open; this would say it twice.
    if (permissionRef.current !== 'granted' || pushSupported()) return
    if (!wantsAlert(message.match, settingsRef.current, favoritesRef.current)) return

    const alert = describeUpdate(previous, message, dictionaryRef.current)
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
    askPermission,
    isWatched: matchId => permission === 'granted' && isMatchWatched(settings, matchId),
    toggleMatch: toggle,
  }

  return { value, handleUpdate }
}
