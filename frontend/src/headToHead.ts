import type { PreviousMeeting } from './api/types'

export interface MeetingTally {
  home: number
  draws: number
  away: number
}

/**
 * Wins of today's home team, draws, and wins of today's away team over their earlier meetings.
 * The teams are told apart by id: who was at home back then doesn't matter.
 */
export function tallyMeetings(meetings: readonly PreviousMeeting[], homeTeamId: string): MeetingTally {
  const tally: MeetingTally = { home: 0, draws: 0, away: 0 }
  for (const meeting of meetings) {
    const winnerId = winnerOf(meeting)
    if (winnerId === null) tally.draws++
    else if (winnerId === homeTeamId) tally.home++
    else tally.away++
  }
  return tally
}

/** The id of the team that won, or null for a draw. */
export function winnerOf(meeting: PreviousMeeting): string | null {
  const { home, away } = meeting.score
  if (home === away) return null
  return home > away ? meeting.homeTeam.id : meeting.awayTeam.id
}
