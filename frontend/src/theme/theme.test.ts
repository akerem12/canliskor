import { describe, expect, it } from 'vitest'
import { initialTheme, otherTheme } from './theme'

describe('initialTheme', () => {
  it('keeps the theme picked earlier', () => {
    expect(initialTheme('light')).toBe('light')
    expect(initialTheme('dark')).toBe('dark')
  })

  it('is dark when nothing usable is stored', () => {
    expect(initialTheme(null)).toBe('dark')
    expect(initialTheme('sepia')).toBe('dark')
  })
})

describe('otherTheme', () => {
  it('switches between the two', () => {
    expect(otherTheme('dark')).toBe('light')
    expect(otherTheme('light')).toBe('dark')
  })
})
