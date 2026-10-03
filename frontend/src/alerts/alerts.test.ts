import { describe, expect, it } from 'vitest'
import type { Match, MatchStatus, MatchUpdatedMessage } from '../api/types'
import { describeUpdate, isMatchWatched, noAlerts, parseAlertSettings, toggleMatch, wantsAlert } from './alerts'

const match = (status: MatchStatus, score: [number, number] | null, clock: string | null = null): Match => ({
  id: '77',
  leagueCode: 'tur.1',
  kickoff: '2026-10-09T20:00:00+03:00',
  status,
  clock,
  homeTeam: { id: 'gs', name: 'Galatasaray', shortName: 'Galatasaray', logoUrl: null },
  awayTeam: { id: 'fb', name: 'Fenerbahce', shortName: 'Fenerbahce', logoUrl: null },
  score: score && { home: score[0], away: score[1] },
  venue: null,
})

const update = (m: Match, changed: 'score' | 'status' | 'clock'): MatchUpdatedMessage =>
  ({ match: m, scoreChanged: changed === 'score', statusChanged: changed === 'status' })

describe('describeUpdate', () => {
  it('announces a goal with the scorer\'s team, the minute and the score', () => {
    const alert = describeUpdate(match('Live', [0, 0], "66'"), update(match('Live', [1, 0], "67'"), 'score'))

    expect(alert).toEqual({ title: '⚽ Goal for Galatasaray!', body: "67' · Galatasaray 1 - 0 Fenerbahce", tag: '77:score:1' })
  })

  it('names the away team when it scores', () => {
    expect(describeUpdate(match('Live', [1, 0]), update(match('Live', [1, 1], "80'"), 'score'))?.title).toBe('⚽ Goal for Fenerbahce!')
  })

  it('announces a goal without naming a team when the earlier score is unknown', () => {
    expect(describeUpdate(undefined, update(match('Live', [2, 1], "80'"), 'score'))?.title).toBe('⚽ Goal!')
  })

  it('says so when a goal is taken back', () => {
    const alert = describeUpdate(match('Live', [1, 0]), update(match('Live', [0, 0], "70'"), 'score'))

    expect(alert?.title).toBe('Goal ruled out')
    expect(alert?.body).toBe('Galatasaray 0 - 0 Fenerbahce')
  })

  it('announces kick-off, half time, the second half and full time', () => {
    const titles = [
      describeUpdate(match('Scheduled', null), update(match('Live', [0, 0], "1'"), 'status')),
      describeUpdate(match('Live', [1, 0], "45'"), update(match('HalfTime', [1, 0]), 'status')),
      describeUpdate(match('HalfTime', [1, 0]), update(match('Live', [1, 0], "46'"), 'status')),
      describeUpdate(match('Live', [2, 1], "90'"), update(match('Finished', [2, 1]), 'status')),
    ].map(a => a?.title)

    expect(titles).toEqual(['Kick-off', 'Half time', 'Second half under way', 'Full time'])
  })

  it('puts the final score in the full-time alert', () => {
    expect(describeUpdate(undefined, update(match('Finished', [2, 1]), 'status'))?.body).toBe('Galatasaray 2 - 1 Fenerbahce')
  })

  it('guesses from the clock when the earlier state is unknown', () => {
    expect(describeUpdate(undefined, update(match('Live', [0, 0], "1'"), 'status'))?.title).toBe('Kick-off')
    expect(describeUpdate(undefined, update(match('Live', [0, 0], "46'"), 'status'))?.title).toBe('Second half under way')
    // Noticed late: no longer a kick-off.
    expect(describeUpdate(undefined, update(match('Live', [0, 0], "23'"), 'status'))).toBeNull()
  })

  it('announces a match called off', () => {
    expect(describeUpdate(match('Scheduled', null), update(match('Postponed', null), 'status'))?.title).toBe('Match postponed')
    expect(describeUpdate(match('Scheduled', null), update(match('Cancelled', null), 'status'))?.title).toBe('Match cancelled')
  })

  it('stays quiet when only the clock moved', () => {
    expect(describeUpdate(match('Live', [1, 0], "66'"), update(match('Live', [1, 0], "67'"), 'clock'))).toBeNull()
  })

  it('gives each kind of event its own tag', () => {
    const tags = [
      describeUpdate(match('Live', [0, 0]), update(match('Live', [1, 0]), 'score')),
      describeUpdate(match('Live', [1, 0]), update(match('Live', [2, 0]), 'score')),
      describeUpdate(match('Live', [2, 0]), update(match('Finished', [2, 0]), 'status')),
    ].map(a => a?.tag)

    expect(new Set(tags).size).toBe(3)
  })
})

describe('wantsAlert', () => {
  const game = match('Live', [0, 0])

  it('alerts for a favourite team only while team alerts are on', () => {
    expect(wantsAlert(game, { teamAlerts: true, matches: [] }, new Set(['fb']))).toBe(true)
    expect(wantsAlert(game, { teamAlerts: false, matches: [] }, new Set(['fb']))).toBe(false)
    expect(wantsAlert(game, { teamAlerts: true, matches: [] }, new Set(['bjk']))).toBe(false)
  })

  it('alerts for a watched match whoever plays and whatever the team switch says', () => {
    expect(wantsAlert(game, toggleMatch(noAlerts, game), new Set())).toBe(true)
  })
})

describe('toggleMatch', () => {
  it('starts and stops watching a match', () => {
    const game = match('Scheduled', null)
    const watching = toggleMatch(noAlerts, game)

    expect(watching.matches).toEqual([{ id: '77', leagueCode: 'tur.1', kickoff: '2026-10-09T20:00:00+03:00' }])
    expect(isMatchWatched(watching, '77')).toBe(true)
    expect(toggleMatch(watching, game).matches).toEqual([])
  })
})

describe('parseAlertSettings', () => {
  const kickoff = '2026-10-09T20:00:00+03:00'
  const at = (iso: string) => Date.parse(iso)

  it('reads back what was stored', () => {
    const settings = { teamAlerts: true, matches: [{ id: '77', leagueCode: 'tur.1', kickoff }] }

    expect(parseAlertSettings(JSON.stringify(settings), at('2026-10-09T18:00:00Z'))).toEqual(settings)
  })

  it('forgets matches a day after kickoff', () => {
    const stored = JSON.stringify({ teamAlerts: false, matches: [{ id: '77', leagueCode: 'tur.1', kickoff }] })

    expect(parseAlertSettings(stored, at('2026-10-10T16:59:00Z')).matches).toHaveLength(1)
    expect(parseAlertSettings(stored, at('2026-10-10T17:00:00Z')).matches).toEqual([])
  })

  it('starts with everything off when nothing usable is stored', () => {
    for (const stored of [null, '', '{broken', '"text"', 'null', '{"teamAlerts":"yes","matches":[{"id":1}, null]}']) {
      expect(parseAlertSettings(stored, 0)).toEqual(noAlerts)
    }
  })
})
