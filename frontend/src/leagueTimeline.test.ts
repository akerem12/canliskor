import { describe, expect, it } from 'vitest'
import type { Match, MatchStatus } from './api/types'
import { matchDays, openingDay } from './leagueTimeline'

const match = (id: string, kickoff: string, status: MatchStatus) => ({ id, kickoff, status }) as Match

describe('matchDays', () => {
  it('groups matches by their day in Istanbul', () => {
    const days = matchDays([
      match('a', '2026-10-16T17:00:00Z', 'Finished'),
      // 00:30 on the 17th in Istanbul, though still the 16th in UTC.
      match('b', '2026-10-16T21:30:00Z', 'Finished'),
      match('c', '2026-10-17T16:00:00Z', 'Scheduled'),
    ])

    expect(days.map(d => [d.day, d.matches.map(m => m.id)])).toEqual([
      ['2026-10-16', ['a']],
      ['2026-10-17', ['b', 'c']],
    ])
  })
})

describe('openingDay', () => {
  // A weekend round (Friday to Monday), then the next one.
  const season = matchDays([
    match('prev', '2026-10-05T17:00:00Z', 'Finished'),
    match('fri', '2026-10-09T17:00:00Z', 'Finished'),
    match('sat', '2026-10-10T17:00:00Z', 'Finished'),
    match('sun', '2026-10-11T17:00:00Z', 'Finished'),
    match('mon', '2026-10-12T17:00:00Z', 'Scheduled'),
    match('next', '2026-10-16T17:00:00Z', 'Scheduled'),
  ])

  it('opens at the first day of the round with the latest result', () => {
    expect(season[openingDay(season)!].day).toBe('2026-10-09')
  })

  it('opens at the round of the match in play', () => {
    const live = matchDays([match('done', '2026-10-01T17:00:00Z', 'Finished'), match('now', '2026-10-09T17:00:00Z', 'Live')])

    expect(openingDay(live)).toBe(1)
  })

  it('stays at the top before the first match', () => {
    expect(openingDay(matchDays([match('f', '2026-08-14T17:00:00Z', 'Scheduled')]))).toBeNull()
  })
})
