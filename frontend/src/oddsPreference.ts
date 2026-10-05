import { useCallback, useEffect, useState } from 'react'

export const OddsStorageKey = 'canliskor.odds.v1'

/** Odds show in the match list unless they were switched off earlier. */
export const initialShowOdds = (stored: string | null): boolean => stored !== 'off'

function readStored(): boolean {
  try {
    return initialShowOdds(window.localStorage.getItem(OddsStorageKey))
  } catch {
    return true
  }
}

/** Whether the match list shows odds, remembered in the browser's localStorage. */
export function useShowOdds() {
  const [showOdds, setShowOdds] = useState(readStored)

  useEffect(() => {
    try {
      window.localStorage.setItem(OddsStorageKey, showOdds ? 'on' : 'off')
    } catch {
      // The choice still holds until the page is closed.
    }
  }, [showOdds])

  const toggle = useCallback(() => setShowOdds(shown => !shown), [])

  return { showOdds, toggle }
}
