import { describe, expect, it } from 'vitest'
import type { Match } from './api/types'
import { formGuide, resultFor } from './teamForm'

const match = (id: string, homeId: string, awayId: string, score: [number, number] | null) =>
  ({ id, homeTeam: { id: homeId }, awayTeam: { id: awayId }, score: score && { home: score[0], away: score[1] } }) as Match

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

  it('skips matches without a score', () => {
    expect(formGuide([match('2', 'bjk', 'x', null), match('1', 'bjk', 'x', [1, 0])], 'bjk').map(g => g.match.id)).toEqual(['1'])
  })
})
