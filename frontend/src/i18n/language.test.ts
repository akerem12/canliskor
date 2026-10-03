import { describe, expect, it } from 'vitest'
import { en } from './en'
import { dictionaries, initialLanguage, otherLanguage } from './language'
import { tr } from './tr'

describe('initialLanguage', () => {
  it('keeps the language picked earlier, whatever the browser says', () => {
    expect(initialLanguage('en', ['tr-TR'])).toBe('en')
    expect(initialLanguage('tr', ['en-GB'])).toBe('tr')
  })

  it('starts in Turkish for a browser set to Turkish', () => {
    expect(initialLanguage(null, ['tr-TR', 'en'])).toBe('tr')
    expect(initialLanguage(null, ['tr'])).toBe('tr')
  })

  it('starts in English otherwise', () => {
    expect(initialLanguage(null, ['de-DE', 'tr'])).toBe('en')
    expect(initialLanguage(null, [])).toBe('en')
    expect(initialLanguage('klingon', ['en-GB'])).toBe('en')
  })
})

describe('otherLanguage', () => {
  it('switches between the two', () => {
    expect(otherLanguage('en')).toBe('tr')
    expect(otherLanguage('tr')).toBe('en')
  })
})

describe('dictionaries', () => {
  /** Every key path, e.g. "match.stats.Shots". */
  const paths = (value: object, prefix = ''): string[] =>
    Object.entries(value).flatMap(([key, child]) =>
      typeof child === 'object' && child !== null ? paths(child, `${prefix}${key}.`) : [`${prefix}${key}`])

  it('say the same things in both languages', () => {
    expect(paths(tr)).toEqual(paths(en))
  })

  it('leave nothing empty', () => {
    const texts = (value: object): unknown[] =>
      Object.values(value).flatMap(child => (typeof child === 'object' && child !== null ? texts(child) : [child]))

    for (const dictionary of Object.values(dictionaries)) {
      expect(texts(dictionary).filter(text => text === '')).toEqual([])
    }
  })

  it('offer the other language in its own words', () => {
    expect(en.settings.language).toBe('Türkçe')
    expect(tr.settings.language).toBe('English')
  })
})
