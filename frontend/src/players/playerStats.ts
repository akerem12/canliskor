import type { PlayerCompetitionStats } from '../api/types'
import type { Dictionary } from '../i18n/en'

export interface StatLine {
  /** Which number this is; the words for it are in the dictionary. */
  key: keyof Dictionary['player']['stats']
  value: number
}

/**
 * The lines to show for one competition, in display order. Goalkeepers get their own numbers and lose the
 * attacking ones that are zero; substitute appearances only show where they are known.
 */
export function statLines(stats: PlayerCompetitionStats): StatLine[] {
  const isGoalkeeper = stats.saves !== null || stats.cleanSheets !== null || stats.goalsConceded !== null
  const lines: (StatLine | false)[] = [
    stats.substituteAppearances !== null && { key: 'played', value: stats.starts + stats.substituteAppearances },
    { key: 'starts', value: stats.starts },
    stats.substituteAppearances !== null && { key: 'substituteAppearances', value: stats.substituteAppearances },
    stats.cleanSheets !== null && { key: 'cleanSheets', value: stats.cleanSheets },
    stats.saves !== null && { key: 'saves', value: stats.saves },
    stats.goalsConceded !== null && { key: 'goalsConceded', value: stats.goalsConceded },
    (!isGoalkeeper || stats.goals > 0) && { key: 'goals', value: stats.goals },
    (!isGoalkeeper || stats.assists > 0) && { key: 'assists', value: stats.assists },
    (!isGoalkeeper || stats.shots > 0) && { key: 'shots', value: stats.shots },
    (!isGoalkeeper || stats.shotsOnTarget > 0) && { key: 'shotsOnTarget', value: stats.shotsOnTarget },
    { key: 'yellowCards', value: stats.yellowCards },
    { key: 'redCards', value: stats.redCards },
    { key: 'foulsCommitted', value: stats.foulsCommitted },
    { key: 'foulsSuffered', value: stats.foulsSuffered },
    (!isGoalkeeper || stats.offsides > 0) && { key: 'offsides', value: stats.offsides },
  ]

  return lines.filter((line): line is StatLine => line !== false)
}

/**
 * Everything added up: club and country, league and cups. Substitute appearances are only known for one
 * competition, so the total has none (and with it no "matches played"); the goalkeeping numbers are added up
 * where they exist and stay out for outfield players.
 */
export function totalStats(competitions: PlayerCompetitionStats[]): PlayerCompetitionStats {
  const sum = (pick: (stats: PlayerCompetitionStats) => number) => competitions.reduce((total, c) => total + pick(c), 0)
  const sumKnown = (pick: (stats: PlayerCompetitionStats) => number | null) =>
    competitions.some(c => pick(c) !== null) ? sum(c => pick(c) ?? 0) : null

  return {
    name: '',
    leagueCode: null,
    teamName: null,
    starts: sum(c => c.starts),
    substituteAppearances: null,
    goals: sum(c => c.goals),
    assists: sum(c => c.assists),
    shots: sum(c => c.shots),
    shotsOnTarget: sum(c => c.shotsOnTarget),
    yellowCards: sum(c => c.yellowCards),
    redCards: sum(c => c.redCards),
    foulsCommitted: sum(c => c.foulsCommitted),
    foulsSuffered: sum(c => c.foulsSuffered),
    offsides: sum(c => c.offsides),
    cleanSheets: sumKnown(c => c.cleanSheets),
    saves: sumKnown(c => c.saves),
    goalsConceded: sumKnown(c => c.goalsConceded),
  }
}

/** "2026-27 Turkish Super Lig" → "Turkish Super Lig": the season is the same for every tab. */
export const competitionLabel = (name: string) => name.replace(/^\d{4}(-\d{2})?\s+/, '')
