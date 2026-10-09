// Push notifications: the ones the server sends, which arrive while the site is closed (the kick-off reminder and
// "line-ups are out"). This file keeps the server in step with what the visitor wants; public/sw.js shows them.

import { apiUrl } from '../api/base'
import { isNativeApp } from '../native/app'
import { nativePushToken } from '../native/notifications'
import type { PushWishes } from './alerts'

/** True in the Android app (Firebase delivers there) and in browsers that have Web Push. */
export const pushSupported = () =>
  isNativeApp
  || (typeof navigator !== 'undefined' && 'serviceWorker' in navigator && 'PushManager' in window && 'Notification' in window)

/** The token the server last heard of from this phone, so it can be taken back when alerts are switched off. */
const TokenStorageKey = 'canliskor.pushToken.v1'

const unsubscribe = (endpoint: string) =>
  fetch(apiUrl(`/api/push/subscription?endpoint=${encodeURIComponent(endpoint)}`), { method: 'DELETE' })

async function register(subscriber: object, wishes: PushWishes) {
  const response = await fetch(apiUrl('/api/push/subscription'), {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ ...subscriber, ...wishes }),
  })
  if (!response.ok) throw new Error(`PUT /api/push/subscription failed with ${response.status}`)
}

/** The Android app: the phone is known to the server by its Firebase token. Permission is granted by now. */
async function syncApp(wishes: PushWishes | null) {
  const known = window.localStorage.getItem(TokenStorageKey)
  if (!wishes) {
    if (known) {
      await unsubscribe(known)
      window.localStorage.removeItem(TokenStorageKey)
    }
    return
  }

  const token = await nativePushToken()
  // Firebase gave the phone a new token: the old one would only collect notifications nobody sees.
  if (known && known !== token) await unsubscribe(known).catch(() => {})
  await register({ token }, wishes)
  window.localStorage.setItem(TokenStorageKey, token)
}

/** "SGVsbG8_" ↔ bytes: keys travel as base64url. */
export function toBase64Url(bytes: ArrayBuffer | Uint8Array): string {
  const view = bytes instanceof Uint8Array ? bytes : new Uint8Array(bytes)
  return btoa(String.fromCharCode(...view)).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '')
}

export function fromBase64Url(text: string): Uint8Array<ArrayBuffer> {
  const binary = atob(text.replace(/-/g, '+').replace(/_/g, '/'))
  return Uint8Array.from(binary, char => char.charCodeAt(0))
}

async function sync(wishes: PushWishes | null) {
  if (isNativeApp) return syncApp(wishes)
  if (!pushSupported()) return

  if (!wishes) {
    // Nothing wanted (any more): if this browser was subscribed, the server forgets it.
    const registration = await navigator.serviceWorker.getRegistration()
    const subscription = await registration?.pushManager.getSubscription()
    if (subscription) await unsubscribe(subscription.endpoint)
    return
  }

  const registration = await navigator.serviceWorker.register('/sw.js')
  await navigator.serviceWorker.ready

  const keyResponse = await fetch(apiUrl('/api/push/key'))
  if (!keyResponse.ok) throw new Error(`GET /api/push/key failed with ${keyResponse.status}`)
  const { publicKey } = (await keyResponse.json()) as { publicKey: string }

  // A subscription only works with the server key it was made for; after the key changed, a new one is needed.
  let subscription = await registration.pushManager.getSubscription()
  const subscribedKey = subscription?.options.applicationServerKey
  if (subscription && (!subscribedKey || toBase64Url(subscribedKey) !== publicKey)) {
    await subscription.unsubscribe()
    subscription = null
  }
  subscription ??= await registration.pushManager.subscribe({ userVisibleOnly: true, applicationServerKey: fromBase64Url(publicKey) })

  await register(subscription.toJSON(), wishes)
}

let queue: Promise<void> = Promise.resolve()

/**
 * Tells the server what this browser (or phone) wants to be notified about, subscribing it first if needed; null means
 * nothing. The call is repeated on every visit, which also makes up for anything the server lost. Calls run one
 * after another, and a failed one (offline, server restarting) is simply made up for by the next.
 */
export function syncPush(wishes: PushWishes | null): Promise<void> {
  queue = queue.then(() => sync(wishes)).catch(() => {})
  return queue
}
