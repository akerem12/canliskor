import { describe, expect, it } from 'vitest'
import { initialShowOdds } from './oddsPreference'

describe('initialShowOdds', () => {
  it('shows odds until they are switched off', () => {
    expect(initialShowOdds(null)).toBe(true)
    expect(initialShowOdds('on')).toBe(true)
    expect(initialShowOdds('nonsense')).toBe(true)
  })

  it('keeps them hidden once switched off', () => {
    expect(initialShowOdds('off')).toBe(false)
  })
})
