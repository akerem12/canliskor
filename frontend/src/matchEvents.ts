import type { MatchAssist, MatchEvent, Score } from './api/types'

export const isGoal = (event: MatchEvent) =>
  event.type === 'Goal' || event.type === 'PenaltyGoal' || event.type === 'OwnGoal'

/** Who set up a goal. Null for anything else, and for goals nobody assisted: penalties, own goals, solo efforts. */
export function assistOf(event: MatchEvent): MatchAssist | null {
  return event.type === 'Goal' && event.relatedPlayer
    ? { name: event.relatedPlayer, playerId: event.relatedPlayerId }
    : null
}

/** Pairs each event with the score right after it, so a goal can show e.g. "1 - 1". Events are in match order. */
export function withRunningScore(events: MatchEvent[]): { event: MatchEvent; score: Score }[] {
  let home = 0
  let away = 0
  return events.map(event => {
    if (isGoal(event)) {
      if (event.side === 'Home') home++
      else away++
    }
    return { event, score: { home, away } }
  })
}
