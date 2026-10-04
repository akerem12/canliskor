import type { ReactNode } from 'react'
import { useCallback, useEffect, useMemo, useState } from 'react'
import { localizeNames, sourceLeagueName, sourceTeamName } from '../i18n/names'
import { useI18n } from '../i18n/useI18n'
import type { Favorites } from './favorites'
import { noFavorites, parseFavorites, StorageKey, toggleLeague, toggleTeam } from './favorites'
import type { FavoritesContextValue } from './useFavorites'
import { FavoritesContext } from './useFavorites'

/** Private browsing or a full disk can make storage throw; favourites then simply last for this visit. */
function readStored(): Favorites {
  try {
    return parseFavorites(window.localStorage.getItem(StorageKey))
  } catch {
    return noFavorites
  }
}

function writeStored(favorites: Favorites) {
  try {
    window.localStorage.setItem(StorageKey, JSON.stringify(favorites))
  } catch {
    // Nothing to do: the choice still holds until the page is closed.
  }
}

/**
 * Keeps the visitor's favourite teams and leagues, in the browser's localStorage so they are still there after
 * a reload. There is no server side to wait for, so a click on a star shows at once everywhere on the page.
 */
export function FavoritesProvider({ children }: { children: ReactNode }) {
  const { language } = useI18n()
  const [favorites, setFavorites] = useState(readStored)

  // Starred in another tab: this one follows.
  useEffect(() => {
    const onStorage = (event: StorageEvent) => {
      if (event.key === StorageKey) setFavorites(parseFavorites(event.newValue))
    }
    window.addEventListener('storage', onStorage)
    return () => window.removeEventListener('storage', onStorage)
  }, [])

  const update = useCallback((change: (current: Favorites) => Favorites) => {
    setFavorites(current => {
      const next = change(current)
      writeStored(next)
      return next
    })
  }, [])

  // A star may be pressed while the site is in Turkish: the name is kept as the data source spells it, and
  // shown in whichever language the site is in.
  const value = useMemo<FavoritesContextValue>(() => ({
    favorites: localizeNames(favorites, language),
    toggleTeam: team => update(current => toggleTeam(current, { ...team, name: sourceTeamName(team.name) })),
    toggleLeague: league => update(current => toggleLeague(current, { ...league, name: sourceLeagueName(league.code, league.name) })),
  }), [favorites, language, update])

  return <FavoritesContext.Provider value={value}>{children}</FavoritesContext.Provider>
}
