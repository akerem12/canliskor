// Favourite teams and leagues: the data and the pure operations on it. Kept free of React and of the browser's
// storage so it can be unit-tested; useFavorites.tsx adds both.

/** Enough to show the team without asking the server: its page link, name and crest. */
export interface FavoriteTeam {
  /** The league the team was starred in; its page is opened under this league. */
  leagueCode: string
  teamId: string
  name: string
  logoUrl: string | null
}

export interface FavoriteLeague {
  code: string
  name: string
}

export interface Favorites {
  teams: FavoriteTeam[]
  leagues: FavoriteLeague[]
}

export const noFavorites: Favorites = { teams: [], leagues: [] }

export const StorageKey = 'canliskor.favorites.v1'

const isString = (value: unknown): value is string => typeof value === 'string' && value.length > 0

/**
 * Reads what was stored. Anything missing, damaged or written by something else is dropped entry by entry,
 * so one bad entry (or an edited value) never costs the visitor the rest of their favourites.
 */
export function parseFavorites(stored: string | null): Favorites {
  if (!stored) return noFavorites

  let raw: unknown
  try {
    raw = JSON.parse(stored)
  } catch {
    return noFavorites
  }
  if (typeof raw !== 'object' || raw === null) return noFavorites

  const { teams, leagues } = raw as { teams?: unknown; leagues?: unknown }
  return {
    teams: (Array.isArray(teams) ? teams : [])
      .filter((t): t is FavoriteTeam => isString(t?.leagueCode) && isString(t?.teamId) && isString(t?.name))
      .map(t => ({ leagueCode: t.leagueCode, teamId: t.teamId, name: t.name, logoUrl: isString(t.logoUrl) ? t.logoUrl : null })),
    leagues: (Array.isArray(leagues) ? leagues : [])
      .filter((l): l is FavoriteLeague => isString(l?.code) && isString(l?.name))
      .map(l => ({ code: l.code, name: l.name })),
  }
}

export const isFavoriteTeam = (favorites: Favorites, teamId: string) => favorites.teams.some(t => t.teamId === teamId)

export const isFavoriteLeague = (favorites: Favorites, code: string) => favorites.leagues.some(l => l.code === code)

/** Adds the team, or removes it if it is already there. A team is one favourite whichever league it was starred in. */
export function toggleTeam(favorites: Favorites, team: FavoriteTeam): Favorites {
  return {
    ...favorites,
    teams: isFavoriteTeam(favorites, team.teamId)
      ? favorites.teams.filter(t => t.teamId !== team.teamId)
      : [...favorites.teams, team],
  }
}

/** Adds the league, or removes it if it is already there. */
export function toggleLeague(favorites: Favorites, league: FavoriteLeague): Favorites {
  return {
    ...favorites,
    leagues: isFavoriteLeague(favorites, league.code)
      ? favorites.leagues.filter(l => l.code !== league.code)
      : [...favorites.leagues, league],
  }
}
