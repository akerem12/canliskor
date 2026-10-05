import { describe, expect, it } from 'vitest'
import type { PreviousMeeting, Team } from './api/types'
import { tallyMeetings, winnerOf } from './headToHead'

const team = (id: string): Team => ({ id, name: id, shortName: id, logoUrl: null })

const meeting = (home: string, homeGoals: number, away: string, awayGoals: number): PreviousMeeting => ({
  id: `${home}-${away}-${homeGoals}-${awayGoals}`,
  kickoff: '2026-01-31T18:00:00+03:00',
  competition: null,
  homeTeam: team(home),
  awayTeam: team(away),
  score: { home: homeGoals, away: awayGoals },
})

describe('tallyMeetings', () => {
  it("counts each team's wins wherever the match was played", () => {
    const meetings = [
      meeting('ars', 5, 'lee', 0),
      meeting('lee', 0, 'ars', 4),
      meeting('lee', 2, 'ars', 1),
      meeting('ars', 1, 'lee', 1),
    ]

    expect(tallyMeetings(meetings, 'ars')).toEqual({ home: 2, draws: 1, away: 1 })
    expect(tallyMeetings(meetings, 'lee')).toEqual({ home: 1, draws: 1, away: 2 })
  })

  it('is all zeroes without meetings', () => {
    expect(tallyMeetings([], 'ars')).toEqual({ home: 0, draws: 0, away: 0 })
  })
})

describe('winnerOf', () => {
  it('names the winner, or nobody for a draw', () => {
    expect(winnerOf(meeting('ars', 2, 'lee', 1))).toBe('ars')
    expect(winnerOf(meeting('ars', 0, 'lee', 1))).toBe('lee')
    expect(winnerOf(meeting('ars', 0, 'lee', 0))).toBeNull()
  })
})
