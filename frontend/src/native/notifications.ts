// Notifications in the Android app. The website uses the browser's Notification API and a service worker; the
// app's web view has neither, so here the same jobs go through Capacitor's plugins: Firebase delivers what the
// server sends (also while the app is closed), and the phone's own notifications show what the open app notices.

import { LocalNotifications } from '@capacitor/local-notifications'
import { PushNotifications } from '@capacitor/push-notifications'
import { isNativeApp } from './app'

/** The channel all match notifications use; the backend names the same one (FcmPushSender.AndroidChannelId). */
const ChannelId = 'matches'

/** The white-on-transparent icon in android/app/src/main/res/drawable. */
const SmallIcon = 'ic_stat_notify'

/** What the app's notifications say and where a tap leads: a page of this app, e.g. "/?league=tur.1&match=1". */
export interface NativeNotification {
  title: string
  body: string
  /** The same tag replaces the earlier notification instead of adding a second one. */
  tag: string
  url: string
}

/** Android wants a number where the web has a tag; the same tag always gives the same number. */
export function notificationId(tag: string): number {
  let hash = 0
  for (let i = 0; i < tag.length; i++) hash = (Math.imul(hash, 31) + tag.charCodeAt(i)) | 0
  return Math.abs(hash)
}

/**
 * Opens a page of the app the way a link would: the address changes and the app's router is told, so the open
 * tab ("&tab=lineups") comes along. Anything that isn't a page of this app is ignored.
 */
export function openAppUrl(url: unknown) {
  if (typeof url !== 'string' || !url.startsWith('/') || url.startsWith('//')) return
  window.history.pushState(null, '', url)
  window.dispatchEvent(new PopStateEvent('popstate'))
}

/** "default": not asked yet. */
export async function nativePermission(): Promise<NotificationPermission> {
  const { receive } = await PushNotifications.checkPermissions()
  return receive === 'granted' || receive === 'denied' ? receive : 'default'
}

/** Shows Android's permission question (Android 13 and later; earlier versions simply allow). */
export async function requestNativePermission(): Promise<NotificationPermission> {
  const { receive } = await PushNotifications.requestPermissions()
  return receive === 'granted' ? 'granted' : 'denied'
}

/** Shows a notification from the app itself. */
export function showNative(notification: NativeNotification) {
  void LocalNotifications.schedule({
    notifications: [{
      id: notificationId(notification.tag),
      title: notification.title,
      body: notification.body,
      channelId: ChannelId,
      smallIcon: SmallIcon,
      // Shown at once, so no alarm is involved. Left at its default (exact), the plugin sends the user to
      // Android's "Alarms & reminders" settings screen first.
      isExactNotification: false,
      extra: { url: notification.url },
    }],
  })
}

let waiting: { resolve: (token: string) => void; reject: (error: Error) => void }[] = []
let listening: Promise<unknown> | null = null
let channelName = 'Matches'

/**
 * Everything the app listens for, set up once: the token Firebase gives this phone, taps on notifications, and
 * notifications arriving while the app is open (Android only shows those by itself when the app isn't).
 */
function listen() {
  listening ??= Promise.all([
    // Importance 4: pops up and makes a sound. The user can change that per channel in the phone's settings.
    PushNotifications.createChannel({ id: ChannelId, name: channelName, importance: 4, visibility: 1 }),
    PushNotifications.addListener('registration', token => {
      for (const { resolve } of waiting) resolve(token.value)
      waiting = []
    }),
    PushNotifications.addListener('registrationError', error => {
      for (const { reject } of waiting) reject(new Error(error.error))
      waiting = []
    }),
    PushNotifications.addListener('pushNotificationActionPerformed', action => openAppUrl(action.notification.data?.url)),
    LocalNotifications.addListener('localNotificationActionPerformed', action => openAppUrl(action.notification.extra?.url)),
    PushNotifications.addListener('pushNotificationReceived', notification => {
      const { title, body, tag, url } = notification.data ?? {}
      if (typeof title === 'string' && typeof tag === 'string') {
        showNative({ title, body: typeof body === 'string' ? body : '', tag, url: typeof url === 'string' ? url : '/' })
      }
    }),
  ])
  return listening
}

/**
 * Starts listening for taps on notifications. Called when the app starts, so a tap that opened the app is
 * handled even if alerts were never touched in this run.
 * @param name The channel's name in the phone's notification settings, in the app's language.
 */
export function startNativeNotifications(name: string) {
  if (!isNativeApp) return
  channelName = name
  void listen().catch(() => {})
}

/**
 * Registers this phone with Firebase. Permission must have been granted.
 * @returns The token the server sends this phone's notifications to. It can change (rarely), so ask every time.
 */
export async function nativePushToken(): Promise<string> {
  await listen()
  return new Promise<string>((resolve, reject) => {
    waiting.push({ resolve, reject })
    PushNotifications.register().catch(reject)
  })
}
