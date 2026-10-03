import { describe, expect, it } from 'vitest'
import type { LeagueMatches, Match } from '../api/types'
import type { Favorites } from './favorites'
import { noFavorites } from './favorites'
import { pinFavorites } from './pinned'

const match = (id: string, kickoff: string, homeId: string, awayId: string) =>
  ({ id, kickoff, homeTeam: { id: homeId }, awayTeam: { id: awayId } }) as Match

const league = (code: string, ...matches: Match[]): LeagueMatches => ({ code, name: code, lastUpdatedUtc: '', matches })

const day = [
  league('uefa.nations', match('1', '2026-10-05T21:45:00+03:00', 'ita', 'tur')),
  league('arg.1', match('2', '2026-10-05T23:00:00+03:00', 'boca', 'river')),
  league('tur.1', match('3', '2026-10-05T19:00:00+03:00', 'gs', 'kas'), match('4', '2026-10-05T16:00:00+03:00', 'bjk', 'ts')),
]

const favorites = (teamIds: string[], leagueCodes: string[]): Favorites => ({
  teams: teamIds.map(teamId => ({ leagueCode: 'x', teamId, name: teamId, logoUrl: null })),
  leagues: leagueCodes.map(code => ({ code, name: code })),
})

describe('pinFavorites', () => {
  it('changes nothing without favourites', () => {
    const pinned = pinFavorites(day, noFavorites)

    expect(pinned.teamMatches).toEqual([])
    expect(pinned.leagues.map(l => l.code)).toEqual(['uefa.nations', 'arg.1', 'tur.1'])
  })

  it('collects the matches of favourite teams, home or away, in kickoff order', () => {
    const pinned = pinFavorites(day, favorites(['tur', 'gs'], []))

    expect(pinned.teamMatches.map(m => m.id)).toEqual(['3', '1'])
  })

  it('moves favourite leagues to the front and keeps the rest in order', () => {
    const pinned = pinFavorites(day, favorites([], ['tur.1']))

    expect(pinned.leagues.map(l => l.code)).toEqual(['tur.1', 'uefa.nations', 'arg.1'])
  })

  it('keeps a favourite team\'s match in its league too', () => {
    const pinned = pinFavorites(day, favorites(['gs'], []))

    expect(pinned.leagues.find(l => l.code === 'tur.1')?.matches.map(m => m.id)).toEqual(['3', '4'])
    expect(day.map(l => l.code)).toEqual(['uefa.nations', 'arg.1', 'tur.1'])
  })
})
