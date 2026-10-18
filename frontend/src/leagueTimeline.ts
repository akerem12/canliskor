// A league's season as days of matches, and the day the list opens at. Pure, so it can be unit-tested; the
// Fixtures tab of the league page draws it.

import type { Match } from './api/types'
import { timelinePoints } from './teamTimeline'
import { istanbulToday } from './time'

export interface MatchDay {
  /** YYYY-MM-DD in Istanbul. */
  day: string
  matches: Match[]
}

/** Days of one round are at most this far apart: a weekend round runs Friday to Monday. */
const RoundGapDays = 2

/** Going back to the start of a round stops after this many days, so a busy stretch can't take the list far back. */
const MaxRoundDays = 4

/** Matches in kickoff order → one entry per day (in Istanbul) that has any, in order. */
export function matchDays(matches: Match[]): MatchDay[] {
  const days: MatchDay[] = []
  for (const match of matches) {
    const day = istanbulToday(new Date(match.kickoff))
    const last = days.at(-1)
    if (last?.day === day) last.matches.push(match)
    else days.push({ day, matches: [match] })
  }
  return days
}

const daysBetween = (from: string, to: string) => (Date.parse(`${to}T00:00:00Z`) - Date.parse(`${from}T00:00:00Z`)) / 86_400_000

/**
 * The day the list opens at: the first day of the round with the match in play or the latest result, so the round
 * in progress is at the top with what comes next below it. At the end of a season its last round; null before the
 * first match is played (the list starts at the top).
 */
export function openingDay(days: MatchDay[]): number | null {
  const matches = days.flatMap(day => day.matches)
  const { anchor } = timelinePoints(matches)
  if (anchor === null) return null

  const id = matches[anchor].id
  const found = days.findIndex(day => day.matches.some(match => match.id === id))
  let index = found
  while (index > 0 && found - index < MaxRoundDays - 1 && daysBetween(days[index - 1].day, days[index].day) <= RoundGapDays) index--
  return index
}
