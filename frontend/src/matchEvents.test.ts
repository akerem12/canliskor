import { describe, expect, it } from 'vitest'
import type { MatchEvent } from './api/types'
import { withRunningScore } from './matchEvents'

const event = (type: MatchEvent['type'], side: MatchEvent['side']): MatchEvent =>
  ({ type, side, clock: "10'", player: 'Player', relatedPlayer: null })

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
