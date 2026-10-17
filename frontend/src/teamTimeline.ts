// A team's season as one list, and where in it "now" is. Pure, so it can be unit-tested; the Fixtures tab of the
// team page draws it.

import type { Match } from './api/types'
import { isInPlay } from './api/types'

/** At the end of a season, when nothing is left to come, this many of the last results are in view. */
export const ResultsInViewAtSeasonEnd = 5

/**
 * Results and fixtures together, oldest first. A match in both lists (the one in play is among the fixtures, and
 * may already be among the results) appears once, as the fixtures have it.
 */
export function seasonTimeline(results: Match[], fixtures: Match[]): Match[] {
  const byId = new Map<string, Match>()
  for (const match of [...results, ...fixtures]) byId.set(match.id, match)
  return [...byId.values()].sort((a, b) => Date.parse(a.kickoff) - Date.parse(b.kickoff))
}

export interface TimelinePoints {
  /**
   * The row to bring to the top of the screen: the match in play, else the latest result, with what is to come
   * below it. At the end of a season the last few results instead; null before the first match (nothing to scroll).
   */
  anchor: number | null
  /** The first match after the one in play or the latest result; null if none follows or nothing was played. */
  upcomingFrom: number | null
}

export function timelinePoints(matches: Match[]): TimelinePoints {
  const live = matches.findIndex(match => isInPlay(match.status))
  const lastPlayed = matches.findLastIndex(match => match.status === 'Finished')
  const now = live >= 0 ? live : lastPlayed
  if (now < 0) return { anchor: null, upcomingFrom: null }

  const upcomingFrom = now + 1 < matches.length ? now + 1 : null
  const seasonOver = upcomingFrom === null && live < 0
  return { anchor: seasonOver ? Math.max(0, lastPlayed - (ResultsInViewAtSeasonEnd - 1)) : now, upcomingFrom }
}
