import type { MatchAssist, MatchEvent, Score } from './api/types'

export const isGoal = (event: MatchEvent) =>
  event.type === 'Goal' || event.type === 'PenaltyGoal' || event.type === 'OwnGoal'

/** Who set up a goal. Null for anything else, and for goals nobody assisted: penalties, own goals, solo efforts. */
export function assistOf(event: MatchEvent): MatchAssist | null {
  return event.type === 'Goal' && event.relatedPlayer
    ? { name: event.relatedPlayer, playerId: event.relatedPlayerId }
    : null
}

/** One player's goals for one side, in the order they were scored. */
export interface Scorer {
  name: string | null
  playerId: string | null
  goals: { clock: string; type: MatchEvent['type'] }[]
}

/**
 * Goals grouped by scorer for each side, ordered by each scorer's first goal: "Watkins 14', 52'" rather than two lines.
 * An own goal sits with the team it counts for, as the events have it.
 */
export function scorersBySide(events: MatchEvent[]): { home: Scorer[]; away: Scorer[] } {
  const home = new Map<string, Scorer>()
  const away = new Map<string, Scorer>()
  for (const event of events) {
    if (!isGoal(event)) continue
    const side = event.side === 'Home' ? home : away
    const key = event.playerId ?? `name:${event.player ?? ''}`
    const scorer = side.get(key) ?? { name: event.player, playerId: event.playerId, goals: [] }
    scorer.goals.push({ clock: event.clock, type: event.type })
    side.set(key, scorer)
  }
  return { home: [...home.values()], away: [...away.values()] }
}

/** "Ollie Watkins" → "O. Watkins", "Virgil van Dijk" → "V. van Dijk". One-word and already shortened names stay as they are. */
export function shortPlayerName(name: string): string {
  const [first, ...rest] = name.trim().split(/\s+/)
  if (rest.length === 0 || first.endsWith('.')) return name.trim()
  return `${[...first][0]}. ${rest.join(' ')}`
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
