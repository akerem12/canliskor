// The Android app is this site in a native shell (Capacitor). Everything that only exists there goes through
// this folder, so the website never runs any of it.

import { App } from '@capacitor/app'
import { Capacitor } from '@capacitor/core'

/** True inside the Android app, false on the website. */
export const isNativeApp = Capacitor.isNativePlatform()

/**
 * Tells when the app goes to the background (false) and comes back (true). Never called on the website, where
 * a hidden tab keeps its live connection on purpose (alerts fire from it).
 * @returns A function that stops listening.
 */
export function onAppActiveChange(listener: (active: boolean) => void): () => void {
  if (!isNativeApp) return () => {}
  const handle = App.addListener('appStateChange', state => listener(state.isActive))
  return () => {
    void handle.then(h => h.remove())
  }
}
