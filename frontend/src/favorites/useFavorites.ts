import { createContext, useContext } from 'react'
import type { FavoriteLeague, FavoriteTeam, Favorites } from './favorites'

export interface FavoritesContextValue {
  favorites: Favorites
  toggleTeam: (team: FavoriteTeam) => void
  toggleLeague: (league: FavoriteLeague) => void
}

export const FavoritesContext = createContext<FavoritesContextValue | null>(null)

/** The visitor's favourite teams and leagues, and the two actions on them. See FavoritesProvider. */
export function useFavorites(): FavoritesContextValue {
  const value = useContext(FavoritesContext)
  if (!value) throw new Error('useFavorites must be used inside <FavoritesProvider>')
  return value
}
