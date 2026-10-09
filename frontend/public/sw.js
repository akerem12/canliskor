// The site's service worker. Its only job is notifications: the server pushes one (a kick-off reminder, line-ups
// announced), the browser wakes this file even when the site is closed, and it shows the notification and opens the
// match when it is tapped. It caches nothing, so it can never serve an old version of the site.

self.addEventListener('install', () => self.skipWaiting())
self.addEventListener('activate', event => event.waitUntil(self.clients.claim()))

// The payload is written by the server (backend WebPushSender): { title, body, url, tag }.
self.addEventListener('push', event => {
  let data = {}
  try {
    data = event.data ? event.data.json() : {}
  } catch {
    // Not ours or damaged: a plain notification is still better than the browser's "updated in the background".
  }

  event.waitUntil(
    self.registration.showNotification(data.title || 'CanlıSkor', {
      body: data.body || '',
      // The same tag replaces the earlier notification instead of adding a second one.
      tag: data.tag,
      icon: '/icon-192.png',
      data: { url: data.url || '/' },
    }),
  )
})

self.addEventListener('notificationclick', event => {
  event.notification.close()
  const url = new URL(event.notification.data?.url || '/', self.location.origin).href

  event.waitUntil(
    (async () => {
      // A tab of the site that is already open is brought to the front and sent to the match; else a new one opens.
      const tabs = await self.clients.matchAll({ type: 'window', includeUncontrolled: true })
      for (const tab of tabs) {
        try {
          await tab.navigate(url)
          await tab.focus()
          return
        } catch {
          // A tab this worker doesn't control can't be navigated: try the next one.
        }
      }
      await self.clients.openWindow(url)
    })(),
  )
})
