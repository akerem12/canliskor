import { describe, expect, it } from 'vitest'
import type { LeagueFixtures, Match, TeamProfile } from '../api/types'
import { buildFeed, filterFeed } from './feed'

const match = (id: string, kickoff: string, leagueCode: string, homeId: string, awayId: string) =>
  ({ id, kickoff, leagueCode, homeTeam: { id: homeId }, awayTeam: { id: awayId } }) as Match

const team = (upcomingMatches: Match[], competitions: { code: string; name: string }[] = []) =>
  ({ upcomingMatches, competitions }) as TeamProfile

const league = (leagueCode: string, leagueName: string, matches: Match[]): LeagueFixtures =>
  ({ leagueCode, leagueName, matches, lastUpdatedUtc: '' })

const derby = match('1', '2026-10-26T21:30:00+03:00', 'tur.1', 'gs', 'fb')
const europe = match('2', '2026-10-13T22:00:00+03:00', 'uefa.champions', 'gs', 'barca')
const other = match('3', '2026-10-17T19:00:00+03:00', 'tur.1', 'bjk', 'ts')

describe('buildFeed', () => {
  it('lists every match once, soonest first', () => {
    const feed = buildFeed(
      [team([europe, derby]), team([derby])],
      [league('tur.1', 'Turkish Super Lig', [other, derby])],
    )

    expect(feed.map(f => f.match.id)).toEqual(['2', '3', '1'])
  })

  it('names the competition of each match', () => {
    const feed = buildFeed(
      [team([europe, derby], [{ code: 'uefa.champions', name: 'UEFA Champions League' }, { code: 'tur.1', name: 'Super Lig (team)' }])],
      [league('tur.1', 'Turkish Super Lig', [])],
    )

    // A favourite league's own name wins over the one a team's schedule gives.
    expect(feed.map(f => f.leagueName)).toEqual(['UEFA Champions League', 'Turkish Super Lig'])
  })

  it('falls back to the code for a competition nobody named', () => {
    expect(buildFeed([team([europe])], [])[0].leagueName).toBe('uefa.champions')
  })

  it('is empty without favourites', () => {
    expect(buildFeed([], [])).toEqual([])
  })
})

describe('filterFeed', () => {
  const feed = buildFeed([team([europe, derby])], [league('tur.1', 'Turkish Super Lig', [other])])

  it('keeps everything without a filter', () => {
    expect(filterFeed(feed, null)).toBe(feed)
  })

  it('narrows to one team, home or away', () => {
    expect(filterFeed(feed, { kind: 'team', teamId: 'fb' }).map(f => f.match.id)).toEqual(['1'])
    expect(filterFeed(feed, { kind: 'team', teamId: 'gs' }).map(f => f.match.id)).toEqual(['2', '1'])
  })

  it('narrows to one league', () => {
    expect(filterFeed(feed, { kind: 'league', code: 'tur.1' }).map(f => f.match.id)).toEqual(['3', '1'])
  })
})
