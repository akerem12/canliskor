import { describe, expect, it } from 'vitest'
import type { FavoriteTeam } from './favorites'
import { isFavoriteLeague, isFavoriteTeam, noFavorites, parseFavorites, toggleLeague, toggleTeam } from './favorites'

const besiktas: FavoriteTeam = { leagueCode: 'tur.1', teamId: '1895', name: 'Besiktas', logoUrl: 'https://logo/1895.png' }
const superLig = { code: 'tur.1', name: 'Turkish Super Lig' }

describe('toggleTeam', () => {
  it('adds a team and removes it again', () => {
    const added = toggleTeam(noFavorites, besiktas)
    expect(added.teams).toEqual([besiktas])
    expect(isFavoriteTeam(added, '1895')).toBe(true)

    const removed = toggleTeam(added, besiktas)
    expect(removed.teams).toEqual([])
    expect(isFavoriteTeam(removed, '1895')).toBe(false)
  })

  it('treats a team as the same favourite in any league', () => {
    const added = toggleTeam(noFavorites, besiktas)
    expect(toggleTeam(added, { ...besiktas, leagueCode: 'uefa.europa' }).teams).toEqual([])
  })

  it('leaves the leagues and the original alone', () => {
    const start = toggleLeague(noFavorites, superLig)
    const next = toggleTeam(start, besiktas)
    expect(next.leagues).toEqual([superLig])
    expect(start.teams).toEqual([])
  })
})

describe('toggleLeague', () => {
  it('adds a league and removes it again', () => {
    const added = toggleLeague(noFavorites, superLig)
    expect(isFavoriteLeague(added, 'tur.1')).toBe(true)
    expect(toggleLeague(added, superLig).leagues).toEqual([])
  })
})

describe('parseFavorites', () => {
  it('reads back what was stored', () => {
    const favorites = toggleLeague(toggleTeam(noFavorites, besiktas), superLig)
    expect(parseFavorites(JSON.stringify(favorites))).toEqual(favorites)
  })

  it('starts empty when nothing is stored or the value is damaged', () => {
    expect(parseFavorites(null)).toEqual(noFavorites)
    expect(parseFavorites('')).toEqual(noFavorites)
    expect(parseFavorites('{not json')).toEqual(noFavorites)
    expect(parseFavorites('"text"')).toEqual(noFavorites)
    expect(parseFavorites('null')).toEqual(noFavorites)
  })

  it('drops bad entries and keeps the good ones', () => {
    const stored = JSON.stringify({
      teams: [besiktas, { teamId: '1' }, null, 'x', { leagueCode: 'tur.1', teamId: '432', name: 'Galatasaray' }],
      leagues: [superLig, { code: 'eng.1' }, 7],
    })

    expect(parseFavorites(stored)).toEqual({
      teams: [besiktas, { leagueCode: 'tur.1', teamId: '432', name: 'Galatasaray', logoUrl: null }],
      leagues: [superLig],
    })
  })

  it('copes with lists that are not lists', () => {
    expect(parseFavorites('{"teams":"x","leagues":{}}')).toEqual(noFavorites)
  })
})
