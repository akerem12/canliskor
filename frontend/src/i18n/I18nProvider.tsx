import type { ReactNode } from 'react'
import { useCallback, useEffect, useMemo, useState } from 'react'
import type { Language } from './language'
import { dictionaries, initialLanguage, LanguageStorageKey } from './language'
import type { I18n } from './useI18n'
import { I18nContext } from './useI18n'

function readInitial(): Language {
  let stored: string | null = null
  try {
    stored = window.localStorage.getItem(LanguageStorageKey)
  } catch {
    // Storage unavailable: the browser's language decides.
  }
  return initialLanguage(stored, navigator.languages ?? [navigator.language])
}

/** Keeps the site's language: English or Turkish, remembered in the browser's localStorage. */
export function I18nProvider({ children }: { children: ReactNode }) {
  const [language, setLanguageState] = useState(readInitial)

  // Screen readers and the browser's own translation offer go by the page's language.
  useEffect(() => {
    document.documentElement.lang = language
  }, [language])

  const setLanguage = useCallback((next: Language) => {
    setLanguageState(next)
    try {
      window.localStorage.setItem(LanguageStorageKey, next)
    } catch {
      // The choice still holds until the page is closed.
    }
  }, [])

  const value = useMemo<I18n>(() => ({ language, t: dictionaries[language], setLanguage }), [language, setLanguage])

  return <I18nContext.Provider value={value}>{children}</I18nContext.Provider>
}
