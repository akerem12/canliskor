import { describe, expect, it } from 'vitest'
import type { PlayerCompetitionStats } from '../api/types'
import { competitionLabel, statLines, totalStats } from './playerStats'

const outfield: PlayerCompetitionStats = {
  name: '2026-27 Turkish Super Lig', leagueCode: 'tur.1', teamName: 'Galatasaray',
  starts: 3, substituteAppearances: 3, goals: 2, assists: 1, shots: 8, shotsOnTarget: 3,
  yellowCards: 1, redCards: 0, foulsCommitted: 2, foulsSuffered: 6, offsides: 0,
  cleanSheets: null, saves: null, goalsConceded: null,
}

const labels = (stats: PlayerCompetitionStats) => statLines(stats).map(line => line.key)
const valueOf = (stats: PlayerCompetitionStats, key: string) => statLines(stats).find(line => line.key === key)?.value

describe('statLines', () => {
  it('adds starts and substitute appearances up to matches played', () => {
    expect(valueOf(outfield, 'played')).toBe(6)
    expect(valueOf(outfield, 'starts')).toBe(3)
    expect(valueOf(outfield, 'substituteAppearances')).toBe(3)
  })

  it('leaves matches played out where substitute appearances are unknown', () => {
    const cup = { ...outfield, substituteAppearances: null }

    expect(labels(cup)).not.toContain('played')
    expect(labels(cup)[0]).toBe('starts')
  })

  it('shows an outfield player no goalkeeping numbers, and zeros of the rest', () => {
    expect(labels(outfield)).not.toContain('saves')
    expect(valueOf(outfield, 'offsides')).toBe(0)
  })

  it('shows a goalkeeper clean sheets, saves and goals conceded instead of empty attacking numbers', () => {
    const keeper = { ...outfield, goals: 0, assists: 0, shots: 0, shotsOnTarget: 0, cleanSheets: 2, saves: 8, goalsConceded: 8 }

    expect(valueOf(keeper, 'cleanSheets')).toBe(2)
    expect(valueOf(keeper, 'saves')).toBe(8)
    expect(labels(keeper)).not.toContain('goals')
    expect(labels(keeper)).not.toContain('shots')
  })

  it('keeps the goal of a goalkeeper who scored', () => {
    expect(valueOf({ ...outfield, saves: 4, goals: 1 }, 'goals')).toBe(1)
  })
})

describe('totalStats', () => {
  const cup: PlayerCompetitionStats = {
    ...outfield, name: '2026-27 Champions League', leagueCode: 'uefa.champions',
    starts: 1, substituteAppearances: null, goals: 1, assists: 0, shots: 2, shotsOnTarget: 1,
    yellowCards: 0, redCards: 1, foulsCommitted: 1, foulsSuffered: 0, offsides: 3,
  }

  it('adds up every competition', () => {
    const total = totalStats([outfield, cup])

    expect(total.starts).toBe(4)
    expect(total.goals).toBe(3)
    expect(total.assists).toBe(1)
    expect(total.shots).toBe(10)
    expect(total.shotsOnTarget).toBe(4)
    expect(total.yellowCards).toBe(1)
    expect(total.redCards).toBe(1)
    expect(total.foulsCommitted).toBe(3)
    expect(total.foulsSuffered).toBe(6)
    expect(total.offsides).toBe(3)
  })

  it('has no matches played, as substitute appearances are known for one competition only', () => {
    expect(totalStats([outfield, cup]).substituteAppearances).toBeNull()
    expect(labels(totalStats([outfield, cup]))[0]).toBe('starts')
  })

  it('keeps goalkeeping numbers out for an outfield player', () => {
    const total = totalStats([outfield, cup])

    expect(total.saves).toBeNull()
    expect(total.cleanSheets).toBeNull()
    expect(total.goalsConceded).toBeNull()
  })

  it('adds up a goalkeeper\'s clean sheets, saves and goals conceded', () => {
    const league = { ...outfield, cleanSheets: 2, saves: 8, goalsConceded: 8 }
    const europe = { ...cup, cleanSheets: 0, saves: 2, goalsConceded: 3 }

    expect(totalStats([league, europe])).toMatchObject({ cleanSheets: 2, saves: 10, goalsConceded: 11 })
  })
})

describe('competitionLabel', () => {
  it('drops the season', () => {
    expect(competitionLabel('2026-27 Turkish Super Lig')).toBe('Turkish Super Lig')
    expect(competitionLabel('2026 FIFA World Cup')).toBe('FIFA World Cup')
  })

  it('leaves a name without a season alone', () => {
    expect(competitionLabel('Champions League')).toBe('Champions League')
  })
})
