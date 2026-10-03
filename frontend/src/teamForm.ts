import type { Match } from './api/types'

export type Result = 'W' | 'D' | 'L'

/** How a finished match went for one of its two teams. Null if it has no score or the team didn't play in it. */
export function resultFor(match: Match, teamId: string): Result | null {
  if (!match.score) return null
  const isHome = match.homeTeam.id === teamId
  if (!isHome && match.awayTeam.id !== teamId) return null

  const scored = isHome ? match.score.home : match.score.away
  const conceded = isHome ? match.score.away : match.score.home
  return scored > conceded ? 'W' : scored < conceded ? 'L' : 'D'
}

/** The team's last few results, oldest first, the way a form guide is read. Matches come newest first. */
export function formGuide(recentMatches: Match[], teamId: string, count = 5): { match: Match; result: Result }[] {
  return recentMatches
    .map(match => ({ match, result: resultFor(match, teamId) }))
    .filter((entry): entry is { match: Match; result: Result } => entry.result !== null)
    .slice(0, count)
    .reverse()
}
