import { describe, expect, it } from 'vitest'
import { addDays, formatDayLabel, istanbulToday } from './time'

describe('addDays', () => {
  it('crosses month and year boundaries', () => {
    expect(addDays('2026-10-31', 1)).toBe('2026-11-01')
    expect(addDays('2027-01-01', -1)).toBe('2026-12-31')
  })

  it('is not shifted by DST changes', () => {
    // Europe switches clocks on 25 Oct 2026; Istanbul doesn't, but the viewer's browser might.
    expect(addDays('2026-10-24', 2)).toBe('2026-10-26')
  })
})

describe('istanbulToday', () => {
  it('uses the Istanbul calendar, not UTC', () => {
    // 22:30 UTC on 3 Oct is already 01:30 on 4 Oct in Istanbul.
    expect(istanbulToday(new Date('2026-10-03T22:30:00Z'))).toBe('2026-10-04')
  })
})

describe('formatDayLabel', () => {
  it('names nearby days and shows a date otherwise', () => {
    expect(formatDayLabel('2026-10-03', 0)).toBe('Today')
    expect(formatDayLabel('2026-10-02', -1)).toBe('Yesterday')
    expect(formatDayLabel('2026-10-10', 7)).toBe('Sat 10 Oct')
  })
})
