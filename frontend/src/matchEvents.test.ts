import { describe, expect, it } from 'vitest'
import type { MatchEvent } from './api/types'
import { assistOf, scorersBySide, shortPlayerName, withRunningScore } from './matchEvents'

const event = (type: MatchEvent['type'], side: MatchEvent['side']): MatchEvent =>
  ({ type, side, clock: "10'", player: 'Player', relatedPlayer: null, playerId: '1', relatedPlayerId: null })

describe('assistOf', () => {
  it('names the player who set up a goal', () => {
    const goal = { ...event('Goal', 'Home'), relatedPlayer: 'De Bruyne', relatedPlayerId: '7' }

    expect(assistOf(goal)).toEqual({ name: 'De Bruyne', playerId: '7' })
  })

  it('is null for a goal nobody assisted', () => {
    expect(assistOf(event('Goal', 'Home'))).toBeNull()
    expect(assistOf(event('PenaltyGoal', 'Home'))).toBeNull()
    expect(assistOf(event('OwnGoal', 'Home'))).toBeNull()
  })

  it('does not mistake the player going off for an assist', () => {
    const substitution = { ...event('Substitution', 'Home'), relatedPlayer: 'Going Off', relatedPlayerId: '8' }

    expect(assistOf(substitution)).toBeNull()
  })
})

describe('withRunningScore', () => {
  it('counts every kind of goal for the side it is credited to', () => {
    const result = withRunningScore([
      event('Goal', 'Away'),
      event('YellowCard', 'Home'),
      event('PenaltyGoal', 'Home'),
      event('OwnGoal', 'Home'), // credited to the team that benefits
    ])

    expect(result.map(r => r.score)).toEqual([
      { home: 0, away: 1 },
      { home: 0, away: 1 },
      { home: 1, away: 1 },
      { home: 2, away: 1 },
    ])
  })
})

describe('scorersBySide', () => {
  const goal = (type: MatchEvent['type'], side: MatchEvent['side'], clock: string, player: string, playerId: string | null): MatchEvent =>
    ({ type, side, clock, player, relatedPlayer: null, playerId, relatedPlayerId: null })

  it('puts a player who scores twice on one line, in the order the goals came', () => {
    const { home, away } = scorersBySide([
      goal('Goal', 'Home', "14'", 'Ollie Watkins', '9'),
      goal('Goal', 'Away', "20'", 'Bukayo Saka', '7'),
      goal('PenaltyGoal', 'Home', "38'", 'Jhon Durán', '11'),
      { ...event('YellowCard', 'Home'), playerId: '9' },
      goal('PenaltyGoal', 'Home', "52'", 'Ollie Watkins', '9'),
    ])

    expect(home).toEqual([
      { name: 'Ollie Watkins', playerId: '9', goals: [{ clock: "14'", type: 'Goal' }, { clock: "52'", type: 'PenaltyGoal' }] },
      { name: 'Jhon Durán', playerId: '11', goals: [{ clock: "38'", type: 'PenaltyGoal' }] },
    ])
    expect(away).toEqual([{ name: 'Bukayo Saka', playerId: '7', goals: [{ clock: "20'", type: 'Goal' }] }])
  })

  it('lists an own goal under the team it counts for', () => {
    const { home, away } = scorersBySide([goal('OwnGoal', 'Away', "67'", 'Becão', '4')])

    expect(home).toEqual([])
    expect(away).toEqual([{ name: 'Becão', playerId: '4', goals: [{ clock: "67'", type: 'OwnGoal' }] }])
  })

  it('groups by name when the provider gives no id', () => {
    const { home } = scorersBySide([goal('Goal', 'Home', "5'", 'Icardi', null), goal('Goal', 'Home', "80'", 'Icardi', null)])

    expect(home).toHaveLength(1)
    expect(home[0].goals).toHaveLength(2)
  })

  it('is empty without goals', () => {
    expect(scorersBySide([event('YellowCard', 'Home'), event('Substitution', 'Away')])).toEqual({ home: [], away: [] })
  })
})

describe('shortPlayerName', () => {
  it('cuts the first name to an initial and keeps the rest', () => {
    expect(shortPlayerName('Ollie Watkins')).toBe('O. Watkins')
    expect(shortPlayerName('Virgil van Dijk')).toBe('V. van Dijk')
    expect(shortPlayerName('Ömer Faruk Beyaz')).toBe('Ö. Faruk Beyaz')
  })

  it('leaves one-word and already shortened names alone', () => {
    expect(shortPlayerName('Rodrygo')).toBe('Rodrygo')
    expect(shortPlayerName('S. Becão')).toBe('S. Becão')
  })
})
