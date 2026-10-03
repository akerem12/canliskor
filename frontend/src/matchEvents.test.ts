import { describe, expect, it } from 'vitest'
import type { MatchEvent } from './api/types'
import { assistOf, withRunningScore } from './matchEvents'

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
