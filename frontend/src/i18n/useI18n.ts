import { createContext, useContext } from 'react'
import type { Dictionary } from './en'
import type { Language } from './language'

export interface I18n {
  language: Language
  /** The site's words in the current language. */
  t: Dictionary
  setLanguage: (language: Language) => void
}

export const I18nContext = createContext<I18n | null>(null)

/** The current language and its words. Components re-render when the language is switched. */
export function useI18n(): I18n {
  const value = useContext(I18nContext)
  if (!value) throw new Error('useI18n needs an I18nProvider above it')
  return value
}
