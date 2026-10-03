import { useCallback, useEffect, useState } from 'react'
import type { Theme } from './theme'
import { initialTheme, otherTheme, themeColors, ThemeStorageKey } from './theme'

function readStored(): Theme {
  try {
    return initialTheme(window.localStorage.getItem(ThemeStorageKey))
  } catch {
    return 'dark'
  }
}

/** The site's theme, dark or light, remembered in the browser's localStorage and applied to the whole page. */
export function useTheme() {
  const [theme, setTheme] = useState(readStored)

  // The colours hang on this attribute (see index.css).
  useEffect(() => {
    document.documentElement.dataset.theme = theme
    document.querySelector('meta[name="theme-color"]')?.setAttribute('content', themeColors[theme])
    try {
      window.localStorage.setItem(ThemeStorageKey, theme)
    } catch {
      // The choice still holds until the page is closed.
    }
  }, [theme])

  const toggle = useCallback(() => setTheme(otherTheme), [])

  return { theme, toggle }
}
