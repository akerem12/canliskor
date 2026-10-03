import { describe, expect, it } from 'vitest'
import { en } from './i18n/en'
import { tr } from './i18n/tr'
import { addDays, formatDayLabel, formatLongDate, formatMatchDate, istanbulToday } from './time'

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
    expect(formatDayLabel('2026-10-03', 0, en)).toBe('Today')
    expect(formatDayLabel('2026-10-02', -1, en)).toBe('Yesterday')
    expect(formatDayLabel('2026-10-10', 7, en)).toBe('Sat 10 Oct')
  })

  it('speaks Turkish', () => {
    expect(formatDayLabel('2026-10-03', 0, tr)).toBe('Bugün')
    expect(formatDayLabel('2026-10-04', 1, tr)).toBe('Yarın')
    expect(formatDayLabel('2026-10-10', 7, tr)).toBe('10 Eki Cmt')
  })
})

describe('formatLongDate and formatMatchDate', () => {
  it('follow the language', () => {
    expect(formatLongDate('2026-10-09', en)).toBe('Friday 9 October')
    expect(formatLongDate('2026-10-09', tr)).toBe('9 Ekim Cuma')
    // 21:30 UTC on the 9th is already the 10th in Istanbul.
    expect(formatMatchDate('2026-10-09T21:30:00Z', en)).toBe('Sat 10 Oct')
    expect(formatMatchDate('2026-10-09T21:30:00Z', tr)).toBe('10 Eki Cmt')
  })
})
