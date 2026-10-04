import { describe, expect, it } from 'vitest'
import type { LeagueMatches, Match, MatchUpdatedMessage } from '../api/types'
import { applyUpdate, goalsIn, initialState, mergeLeagues, scoresReducer } from './matchState'

const match = (id: string, overrides: Partial<Match> = {}): Match => ({
  id,
  leagueCode: 'tur.1',
  kickoff: '2026-10-09T20:00:00+03:00',
  status: 'Live',
  clock: "20'",
  homeTeam: { id: 'h', name: 'Galatasaray', shortName: 'GS', logoUrl: null },
  awayTeam: { id: 'a', name: 'Kasimpasa', shortName: 'KAS', logoUrl: null },
  score: { home: 0, away: 0 },
  venue: null,
  odds: null,
  ...overrides,
})

const league = (code: string, ...matches: Match[]): LeagueMatches => ({
  code,
  name: code.toUpperCase(),
  lastUpdatedUtc: '2026-10-09T17:20:00Z',
  matches,
})

const goal = (id: string, home: number): MatchUpdatedMessage => ({
  match: match(id, { score: { home, away: 0 } }),
  scoreChanged: true,
  statusChanged: false,
})

describe('applyUpdate', () => {
  it('replaces the matching match', () => {
    const [result] = applyUpdate([league('tur.1', match('1'), match('2'))], goal('1', 1))

    expect(result.matches[0].score).toEqual({ home: 1, away: 0 })
    expect(result.matches[1].score).toEqual({ home: 0, away: 0 })
  })

  it('ignores unknown matches and leaves untouched leagues as the same object', () => {
    const leagues = [league('tur.1', match('1'))]

    const result = applyUpdate(leagues, goal('99', 1))

    expect(result[0]).toBe(leagues[0])
  })
})

describe('scoresReducer', () => {
  it('replays updates received while loading on top of the loaded data, in order', () => {
    let state = scoresReducer(initialState, { type: 'loading' })
    state = scoresReducer(state, { type: 'update', message: goal('1', 1) })
    state = scoresReducer(state, { type: 'update', message: goal('1', 2) })

    // The REST response was read before either goal was stored.
    state = scoresReducer(state, { type: 'loaded', leagues: [league('tur.1', match('1'))] })

    expect(state.leagues[0].matches[0].score).toEqual({ home: 2, away: 0 })
    expect(state.pending).toEqual([])
  })

  it('applies updates directly once loaded', () => {
    let state = scoresReducer(initialState, { type: 'loaded', leagues: [league('tur.1', match('1'))] })
    state = scoresReducer(state, { type: 'update', message: goal('1', 1) })

    expect(state.leagues[0].matches[0].score).toEqual({ home: 1, away: 0 })
  })

  it('keeps showing old data while reloading', () => {
    const loaded = scoresReducer(initialState, { type: 'loaded', leagues: [league('tur.1', match('1'))] })

    const reloading = scoresReducer(loaded, { type: 'loading' })

    expect(reloading.leagues).toBe(loaded.leagues)
    expect(reloading.loaded).toBe(false)
  })
})

describe('mergeLeagues', () => {
  it("adds yesterday's still-live games from /live without duplicating today's", () => {
    const today = [league('uefa.champions', match('today', { leagueCode: 'uefa.champions' }))]
    const live = [league('uefa.champions',
      match('today', { leagueCode: 'uefa.champions' }),
      match('yesterday', { leagueCode: 'uefa.champions', kickoff: '2026-10-08T22:00:00+03:00' }))]

    const [result] = mergeLeagues(today, live, ['uefa.champions'])

    expect(result.matches.map(m => m.id)).toEqual(['yesterday', 'today'])
  })

  it('orders leagues as configured', () => {
    const result = mergeLeagues([league('eng.1'), league('tur.1')], [], ['tur.1', 'eng.1'])

    expect(result.map(l => l.code)).toEqual(['tur.1', 'eng.1'])
  })
})

describe('goalsIn', () => {
  it('counts the goals of both teams', () => {
    expect(goalsIn(match('1', { score: { home: 2, away: 1 } }))).toBe(3)
  })

  it('is the same before kick-off and at 0-0, so kick-off is no goal', () => {
    expect(goalsIn(match('1', { status: 'Scheduled', score: null }))).toBe(0)
    expect(goalsIn(match('1'))).toBe(0)
    expect(goalsIn(undefined)).toBe(0)
  })
})
