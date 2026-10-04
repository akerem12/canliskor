import type { LeagueMatches, Match, MatchUpdatedMessage } from '../api/types'

// Pure state logic for the live scoreboard, kept free of React and SignalR so it can be unit-tested.

export interface ScoresState {
  leagues: LeagueMatches[]
  /** False while a (re)load is in flight. */
  loaded: boolean
  /**
   * Updates received during a load. The REST response may have been read from the store just before
   * an update was stored, so these are replayed, in arrival order, on top of the loaded data.
   */
  pending: MatchUpdatedMessage[]
}

export type ScoresAction =
  | { type: 'loading' }
  | { type: 'loaded'; leagues: LeagueMatches[] }
  | { type: 'update'; message: MatchUpdatedMessage }

export const initialState: ScoresState = { leagues: [], loaded: false, pending: [] }

export function scoresReducer(state: ScoresState, action: ScoresAction): ScoresState {
  switch (action.type) {
    case 'loading':
      // Keep showing the old data while reloading.
      return { ...state, loaded: false, pending: [] }
    case 'loaded':
      return { leagues: state.pending.reduce(applyUpdate, action.leagues), loaded: true, pending: [] }
    case 'update':
      return state.loaded
        ? { ...state, leagues: applyUpdate(state.leagues, action.message) }
        : { ...state, pending: [...state.pending, action.message] }
  }
}

/** Replaces the match with the same id. Unknown matches are ignored (same rule as the backend's change detector). */
/** Goals scored so far by both teams; none before kick-off or for a match not seen before. */
export const goalsIn = (match: Match | undefined) => (match?.score ? match.score.home + match.score.away : 0)

export function applyUpdate(leagues: LeagueMatches[], { match }: MatchUpdatedMessage): LeagueMatches[] {
  return leagues.map(league =>
    league.code !== match.leagueCode || !league.matches.some(m => m.id === match.id)
      ? league
      : { ...league, matches: league.matches.map(m => (m.id === match.id ? match : m)) },
  )
}

/**
 * Today's matches plus matches from /live that aren't in today's list — i.e. yesterday's games still running
 * past midnight. Leagues follow the configured order; matches are sorted by kickoff.
 */
export function mergeLeagues(today: LeagueMatches[], live: LeagueMatches[], order: string[]): LeagueMatches[] {
  const byCode = new Map(today.map(l => [l.code, l]))

  for (const league of live) {
    const existing = byCode.get(league.code)
    if (!existing) {
      byCode.set(league.code, league)
      continue
    }
    const known = new Set(existing.matches.map(m => m.id))
    const extra = league.matches.filter(m => !known.has(m.id))
    if (extra.length > 0) {
      byCode.set(league.code, { ...existing, matches: [...existing.matches, ...extra] })
    }
  }

  const rank = (code: string) => (order.includes(code) ? order.indexOf(code) : order.length)
  return [...byCode.values()]
    .sort((a, b) => rank(a.code) - rank(b.code))
    .map(l => ({ ...l, matches: [...l.matches].sort((a, b) => Date.parse(a.kickoff) - Date.parse(b.kickoff)) }))
}
