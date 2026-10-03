import type { LeagueMatches, Match } from '../api/types'
import type { Favorites } from './favorites'

/**
 * A day's matches arranged around the favourites: the matches of favourite teams collected on top, and the
 * favourite leagues ahead of the others. Nothing is removed: a favourite team's match is also still listed in
 * its league.
 */
export function pinFavorites(leagues: LeagueMatches[], favorites: Favorites): { teamMatches: Match[]; leagues: LeagueMatches[] } {
  const teamIds = new Set(favorites.teams.map(t => t.teamId))
  const leagueCodes = new Set(favorites.leagues.map(l => l.code))

  const teamMatches = leagues
    .flatMap(l => l.matches)
    .filter(m => teamIds.has(m.homeTeam.id) || teamIds.has(m.awayTeam.id))
    .sort((a, b) => Date.parse(a.kickoff) - Date.parse(b.kickoff))

  // Array.prototype.sort is stable: within each half the leagues keep their order.
  const ordered = [...leagues].sort((a, b) => Number(leagueCodes.has(b.code)) - Number(leagueCodes.has(a.code)))

  return { teamMatches, leagues: ordered }
}
