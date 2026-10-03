import { createContext, useContext } from 'react'

/** Enough to open a player's profile: where they were found, who they are, and a name to show while it loads. */
export interface PlayerRef {
  leagueCode: string
  playerId: string
  name: string
}

export type OpenPlayer = (player: PlayerRef) => void

export const PlayerProfileContext = createContext<OpenPlayer | null>(null)

/** Opens a player's profile on top of whatever page is showing. */
export function usePlayerProfile(): OpenPlayer {
  const open = useContext(PlayerProfileContext)
  if (!open) throw new Error('usePlayerProfile needs a PlayerProfileProvider above it')
  return open
}
