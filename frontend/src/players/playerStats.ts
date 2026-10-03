import type { PlayerCompetitionStats } from '../api/types'

export interface StatLine {
  label: string
  value: number
}

/**
 * The lines to show for one competition, in display order. Goalkeepers get their own numbers and lose the
 * attacking ones that are zero; substitute appearances only show where they are known.
 */
export function statLines(stats: PlayerCompetitionStats): StatLine[] {
  const isGoalkeeper = stats.saves !== null || stats.cleanSheets !== null || stats.goalsConceded !== null
  const lines: (StatLine | false)[] = [
    stats.substituteAppearances !== null && { label: 'Matches played', value: stats.starts + stats.substituteAppearances },
    { label: 'Starts', value: stats.starts },
    stats.substituteAppearances !== null && { label: 'Substitute appearances', value: stats.substituteAppearances },
    stats.cleanSheets !== null && { label: 'Clean sheets', value: stats.cleanSheets },
    stats.saves !== null && { label: 'Saves', value: stats.saves },
    stats.goalsConceded !== null && { label: 'Goals conceded', value: stats.goalsConceded },
    (!isGoalkeeper || stats.goals > 0) && { label: 'Goals', value: stats.goals },
    (!isGoalkeeper || stats.assists > 0) && { label: 'Assists', value: stats.assists },
    (!isGoalkeeper || stats.shots > 0) && { label: 'Shots', value: stats.shots },
    (!isGoalkeeper || stats.shotsOnTarget > 0) && { label: 'Shots on target', value: stats.shotsOnTarget },
    { label: 'Yellow cards', value: stats.yellowCards },
    { label: 'Red cards', value: stats.redCards },
    { label: 'Fouls committed', value: stats.foulsCommitted },
    { label: 'Fouls suffered', value: stats.foulsSuffered },
    (!isGoalkeeper || stats.offsides > 0) && { label: 'Offsides', value: stats.offsides },
  ]

  return lines.filter((line): line is StatLine => line !== false)
}

/** "2026-27 Turkish Super Lig" → "Turkish Super Lig": the season is the same for every tab. */
export const competitionLabel = (name: string) => name.replace(/^\d{4}(-\d{2})?\s+/, '')
