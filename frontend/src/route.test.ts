import { describe, expect, it } from 'vitest'
import type { Route } from './route'
import { routeFromSearch, routeToSearch, sameRoute } from './route'

describe('routeFromSearch', () => {
  it('opens the matches of the day by default', () => {
    expect(routeFromSearch('')).toEqual({ view: 'matches' })
    expect(routeFromSearch('?date=2026-10-10')).toEqual({ view: 'matches' })
    expect(routeFromSearch('?match=1')).toEqual({ view: 'matches' })
  })

  it('reads a match, a team, a league and the league list', () => {
    expect(routeFromSearch('?league=tur.1&match=401')).toEqual({ view: 'match', leagueCode: 'tur.1', matchId: '401' })
    expect(routeFromSearch('?league=tur.1&team=1895')).toEqual({ view: 'team', leagueCode: 'tur.1', teamId: '1895' })
    expect(routeFromSearch('?league=tur.1')).toEqual({ view: 'league', leagueCode: 'tur.1' })
    expect(routeFromSearch('?view=leagues')).toEqual({ view: 'leagues' })
    expect(routeFromSearch('?view=favorites')).toEqual({ view: 'favorites' })
  })

  it('still opens links to a match tab', () => {
    expect(routeFromSearch('?league=tur.1&match=401&tab=squads')).toEqual({ view: 'match', leagueCode: 'tur.1', matchId: '401' })
  })
})

describe('routeToSearch', () => {
  const routes: Route[] = [
    { view: 'matches' },
    { view: 'leagues' },
    { view: 'favorites' },
    { view: 'league', leagueCode: 'bra.1' },
    { view: 'team', leagueCode: 'bra.1', teamId: '2026' },
    { view: 'match', leagueCode: 'bra.1', matchId: '401' },
  ]

  it('round-trips every route', () => {
    for (const route of routes) {
      expect(routeFromSearch(routeToSearch(route, ''))).toEqual(route)
    }
  })

  it('keeps the selected day and drops everything else', () => {
    expect(routeToSearch({ view: 'matches' }, '?date=2026-10-10&league=tur.1&match=1&tab=squads')).toBe('?date=2026-10-10')
    expect(routeToSearch({ view: 'team', leagueCode: 'tur.1', teamId: '1895' }, '?date=2026-10-10'))
      .toBe('?date=2026-10-10&league=tur.1&team=1895')
  })

  it('is empty for the plain matches page', () => {
    expect(routeToSearch({ view: 'matches' }, '?view=leagues')).toBe('')
  })
})

describe('sameRoute', () => {
  it('compares by value', () => {
    expect(sameRoute({ view: 'league', leagueCode: 'tur.1' }, { view: 'league', leagueCode: 'tur.1' })).toBe(true)
    expect(sameRoute({ view: 'league', leagueCode: 'tur.1' }, { view: 'league', leagueCode: 'eng.1' })).toBe(false)
  })
})
