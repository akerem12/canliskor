import { describe, expect, it } from 'vitest'
import type { Match, MatchStatus } from './api/types'
import { seasonTimeline, timelinePoints } from './teamTimeline'

const match = (id: string, day: number, status: MatchStatus) =>
  ({ id, kickoff: `2026-10-${String(day).padStart(2, '0')}T19:00:00+03:00`, status }) as Match

const ids = (matches: Match[]) => matches.map(m => m.id)

describe('seasonTimeline', () => {
  it('puts results and fixtures in one list, oldest first', () => {
    // Results come newest first, fixtures soonest first.
    const results = [match('r3', 14, 'Finished'), match('r2', 10, 'Finished'), match('r1', 3, 'Finished')]
    const fixtures = [match('f1', 20, 'Scheduled'), match('f2', 26, 'Scheduled')]

    expect(ids(seasonTimeline(results, fixtures))).toEqual(['r1', 'r2', 'r3', 'f1', 'f2'])
  })

  it('lists a match found in both once, as the fixtures have it', () => {
    const timeline = seasonTimeline([match('x', 17, 'Finished')], [match('x', 17, 'Live')])

    expect(timeline).toHaveLength(1)
    expect(timeline[0].status).toBe('Live')
  })
})

describe('timelinePoints', () => {
  it('starts at the latest result, with what is to come below it', () => {
    const matches = [match('r1', 3, 'Finished'), match('r2', 10, 'Finished'), match('f1', 20, 'Scheduled')]

    expect(timelinePoints(matches)).toEqual({ anchor: 1, upcomingFrom: 2 })
  })

  it('starts at the match in play', () => {
    const matches = [match('r1', 3, 'Finished'), match('now', 17, 'HalfTime'), match('f1', 20, 'Scheduled')]

    expect(timelinePoints(matches)).toEqual({ anchor: 1, upcomingFrom: 2 })
  })

  it('shows the last five results when the season is over', () => {
    const matches = Array.from({ length: 8 }, (_, i) => match(`r${i}`, i + 1, 'Finished'))

    expect(timelinePoints(matches)).toEqual({ anchor: 3, upcomingFrom: null })
  })

  it('stays at the top before the first match', () => {
    expect(timelinePoints([match('f1', 20, 'Scheduled'), match('f2', 26, 'Scheduled')])).toEqual({ anchor: null, upcomingFrom: null })
  })

  it('counts a postponed match after the latest result as still to come', () => {
    const matches = [match('r1', 3, 'Finished'), match('p', 5, 'Postponed'), match('f1', 20, 'Scheduled')]

    expect(timelinePoints(matches)).toEqual({ anchor: 0, upcomingFrom: 1 })
  })
})
