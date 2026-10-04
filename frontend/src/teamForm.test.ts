import { describe, expect, it } from 'vitest'
import type { Match } from './api/types'
import { formGuide, isFriendly, resultFor } from './teamForm'

const match = (id: string, homeId: string, awayId: string, score: [number, number] | null, leagueCode = 'tur.1') =>
  ({ id, leagueCode, homeTeam: { id: homeId }, awayTeam: { id: awayId }, score: score && { home: score[0], away: score[1] } }) as Match

describe('resultFor', () => {
  it('reads the score from the team\'s side', () => {
    const game = match('1', 'bjk', 'fb', [1, 2])
    expect(resultFor(game, 'bjk')).toBe('L')
    expect(resultFor(game, 'fb')).toBe('W')
  })

  it('knows a draw', () => {
    expect(resultFor(match('1', 'bjk', 'fb', [0, 0]), 'fb')).toBe('D')
  })

  it('has no result without a score or for a team that did not play', () => {
    expect(resultFor(match('1', 'bjk', 'fb', null), 'bjk')).toBeNull()
    expect(resultFor(match('1', 'bjk', 'fb', [1, 0]), 'gs')).toBeNull()
  })
})

describe('isFriendly', () => {
  it('knows friendlies by the competition code', () => {
    expect(isFriendly('club.friendly')).toBe(true)
    expect(isFriendly('fifa.friendly')).toBe(true)
  })

  it('knows them by the competition name too', () => {
    expect(isFriendly('some.cup', 'Pre-Season Cup')).toBe(true)
    expect(isFriendly('some.cup', 'Summer Exhibition')).toBe(true)
  })

  it('counts leagues, cups and continental competitions as official', () => {
    expect(isFriendly('tur.1', 'Turkish Super Lig')).toBe(false)
    expect(isFriendly('uefa.champions', 'UEFA Champions League')).toBe(false)
    expect(isFriendly('conmebol.libertadores')).toBe(false)
  })
})

describe('formGuide', () => {
  // Newest first, as the API sends them.
  const recent = [
    match('6', 'x', 'bjk', [3, 2]),
    match('5', 'bjk', 'x', [3, 0]),
    match('4', 'x', 'bjk', [2, 1]),
    match('3', 'bjk', 'x', [6, 2]),
    match('2', 'x', 'bjk', [0, 1]),
    match('1', 'bjk', 'x', [0, 1]),
  ]

  it('takes the last five, oldest first', () => {
    const guide = formGuide(recent, 'bjk')
    expect(guide.map(g => g.match.id)).toEqual(['2', '3', '4', '5', '6'])
    expect(guide.map(g => g.result).join('')).toBe('WWLWL')
  })

  it('passes over friendlies, reaching further back for five official matches', () => {
    const withFriendlies = [
      match('8', 'bjk', 'x', [5, 0], 'club.friendly'),
      match('7', 'x', 'bjk', [0, 0], 'club.friendly'),
      ...recent,
    ]
    const guide = formGuide(withFriendlies, 'bjk')
    expect(guide.map(g => g.match.id)).toEqual(['2', '3', '4', '5', '6'])
  })

  it('takes what counts as a friendly from the caller', () => {
    const guide = formGuide(recent, 'bjk', m => m.id === '6')
    expect(guide.map(g => g.match.id)).toEqual(['1', '2', '3', '4', '5'])
  })

  it('skips matches without a score', () => {
    expect(formGuide([match('2', 'bjk', 'x', null), match('1', 'bjk', 'x', [1, 0])], 'bjk').map(g => g.match.id)).toEqual(['1'])
  })
})
