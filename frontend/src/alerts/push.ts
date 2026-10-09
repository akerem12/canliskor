// Push notifications: the ones the server sends, which arrive while the site is closed (the kick-off reminder and
// "line-ups are out"). This file keeps the server in step with what the visitor wants; public/sw.js shows them.

import type { PushWishes } from './alerts'

export const pushSupported = () =>
  typeof navigator !== 'undefined' && 'serviceWorker' in navigator && 'PushManager' in window && 'Notification' in window

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
  if (!pushSupported()) return

  if (!wishes) {
    // Nothing wanted (any more): if this browser was subscribed, the server forgets it.
    const registration = await navigator.serviceWorker.getRegistration()
    const subscription = await registration?.pushManager.getSubscription()
    if (subscription) {
      await fetch(`/api/push/subscription?endpoint=${encodeURIComponent(subscription.endpoint)}`, { method: 'DELETE' })
    }
    return
  }

  const registration = await navigator.serviceWorker.register('/sw.js')
  await navigator.serviceWorker.ready

  const keyResponse = await fetch('/api/push/key')
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

  const response = await fetch('/api/push/subscription', {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ ...subscription.toJSON(), ...wishes }),
  })
  if (!response.ok) throw new Error(`PUT /api/push/subscription failed with ${response.status}`)
}

let queue: Promise<void> = Promise.resolve()

/**
 * Tells the server what this browser wants to be notified about, subscribing it first if needed; null means
 * nothing. The server keeps this in memory only, so the call is repeated on every visit. Calls run one after
 * another, and a failed one (offline, server restarting) is simply made up for by the next.
 */
export function syncPush(wishes: PushWishes | null): Promise<void> {
  queue = queue.then(() => sync(wishes)).catch(() => {})
  return queue
}
