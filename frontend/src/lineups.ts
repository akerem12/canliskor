import type { LineupPlayer, TeamLineup } from './api/types'

export interface Shirt {
  /** "#rrggbb" */
  fill: string
  /** Colour of the number on it. */
  text: string
}

const White = '#ffffff'
const Black = '#1b1b1b'

/** Below this distance (of 441 at most, black to white) two shirts are too alike to tell the teams apart. */
const ClashDistance = 110

function rgb(hex: string): [number, number, number] {
  const n = parseInt(hex.slice(1), 16)
  return [(n >> 16) & 255, (n >> 8) & 255, n & 255]
}

function distance(a: string, b: string): number {
  const [r1, g1, b1] = rgb(a)
  const [r2, g2, b2] = rgb(b)
  return Math.hypot(r1 - r2, g1 - g2, b1 - b2)
}

function shirt(fill: string): Shirt {
  const [r, g, b] = rgb(fill)
  // Perceived brightness: a dark number on a light shirt and the other way round.
  const brightness = (r * 299 + g * 587 + b * 114) / 1000
  return { fill, text: brightness > 150 ? Black : White }
}

/**
 * Shirt colours for the pitch. The home team keeps its own; if the away team's is unknown or too close to it
 * (both in red, say), the away team gets white or black, whichever is further from the home shirt.
 */
export function shirtColors(home: string | null, away: string | null): { home: Shirt; away: Shirt } {
  const homeFill = home ?? White
  const awayFill = away && distance(homeFill, away) >= ClashDistance
    ? away
    : distance(homeFill, White) > distance(homeFill, Black) ? White : Black
  return { home: shirt(homeFill), away: shirt(awayFill) }
}

/**
 * Rows as drawn on a vertical pitch, top to bottom. The home team is at the top playing downwards, so its rows
 * keep their order (goalkeeper first) but each is mirrored: its own left is the viewer's right. The away team
 * plays upwards: forwards first, each row as it comes.
 */
export function pitchRows(lineup: TeamLineup, side: 'home' | 'away'): LineupPlayer[][] {
  return side === 'home' ? lineup.rows.map(row => [...row].reverse()) : [...lineup.rows].reverse()
}

/** The name under a shirt: "G. Orban" → "Orban", so it fits; single names stay as they are. */
export function pitchName(player: LineupPlayer): string {
  const afterInitial = player.shortName.replace(/^\S+\.\s+/, '')
  return afterInitial || player.shortName
}
