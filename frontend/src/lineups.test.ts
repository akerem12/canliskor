import { describe, expect, it } from 'vitest'
import type { LineupPlayer, TeamLineup } from './api/types'
import { pitchName, pitchRows, ratingBand, shirtColors } from './lineups'

const player = (shortName: string) => ({ shortName }) as LineupPlayer

const lineup = (rows: string[][]): TeamLineup =>
  ({ formation: '', shirtColor: null, rows: rows.map(row => row.map(player)), bench: [] })

const names = (rows: LineupPlayer[][]) => rows.map(row => row.map(p => p.shortName))

describe('ratingBand', () => {
  it('splits ratings at 6, 7 and 8', () => {
    expect([5.9, 6, 6.9, 7, 7.9, 8, 10].map(ratingBand))
      .toEqual(['poor', 'average', 'average', 'good', 'good', 'excellent', 'excellent'])
  })
})

describe('shirtColors', () => {
  it('keeps both shirts when they differ', () => {
    expect(shirtColors('#990000', '#ffffff')).toEqual({
      home: { fill: '#990000', text: '#ffffff' },
      away: { fill: '#ffffff', text: '#1b1b1b' },
    })
  })

  it('gives the away team white when both play in the same dark colour', () => {
    expect(shirtColors('#ff0000', '#ff0000').away.fill).toBe('#ffffff')
    expect(shirtColors('#000066', '#000000').away.fill).toBe('#ffffff')
  })

  it('gives the away team black when both play in light colours', () => {
    expect(shirtColors('#ffffff', '#f2f2f2').away.fill).toBe('#1b1b1b')
  })

  it('falls back when a colour is unknown', () => {
    expect(shirtColors(null, null)).toEqual({
      home: { fill: '#ffffff', text: '#1b1b1b' },
      away: { fill: '#1b1b1b', text: '#ffffff' },
    })
  })
})

describe('pitchRows', () => {
  const rows = [['GK'], ['LB', 'CB', 'RB'], ['F']]

  it('mirrors the home team, which plays down the screen', () => {
    expect(names(pitchRows(lineup(rows), 'home'))).toEqual([['GK'], ['RB', 'CB', 'LB'], ['F']])
  })

  it('puts the away forwards first, as it plays up the screen', () => {
    expect(names(pitchRows(lineup(rows), 'away'))).toEqual([['F'], ['LB', 'CB', 'RB'], ['GK']])
  })
})

describe('pitchName', () => {
  it('drops the initial', () => {
    expect(pitchName(player('G. Orban'))).toBe('Orban')
    expect(pitchName(player('M. Burak Boznan'))).toBe('Burak Boznan')
  })

  it('keeps names without an initial', () => {
    expect(pitchName(player('Marcos Felipe'))).toBe('Marcos Felipe')
    expect(pitchName(player('Oh Hyeon-Gyu'))).toBe('Oh Hyeon-Gyu')
  })
})
