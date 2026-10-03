import type { Dictionary } from './en'
import { en } from './en'
import { tr } from './tr'

export type Language = 'en' | 'tr'

export const dictionaries: Record<Language, Dictionary> = { en, tr }

export const LanguageStorageKey = 'canliskor.language.v1'

const isLanguage = (value: unknown): value is Language => value === 'en' || value === 'tr'

/**
 * The language to start in: the one picked earlier, else Turkish for a browser set to Turkish, else English.
 * @param preferred The browser's languages, most wanted first, e.g. ["tr-TR", "en"].
 */
export function initialLanguage(stored: string | null, preferred: readonly string[]): Language {
  if (isLanguage(stored)) return stored
  return preferred[0]?.toLowerCase().startsWith('tr') ? 'tr' : 'en'
}

export const otherLanguage = (language: Language): Language => (language === 'en' ? 'tr' : 'en')
