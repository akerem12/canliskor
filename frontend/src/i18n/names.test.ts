import { describe, expect, it } from 'vitest'
import {
  countryNameTr, leagueNameTr, localizeNames, searchQueryFor, sourceLeagueName, sourceTeamName,
  standingSummaryTr, tablePhraseTr, teamNameTr,
} from './names'

describe('Turkish names', () => {
  it('names competitions by their code, or by their English name', () => {
    expect(leagueNameTr('tur.1', 'Turkish Super Lig')).toBe('Süper Lig')
    expect(leagueNameTr(null, 'UEFA Champions League')).toBe('UEFA Şampiyonlar Ligi')
    expect(leagueNameTr('xyz.1', 'Some Cup')).toBe('Some Cup')
    expect(leagueNameTr('uefa.europa_qual', 'UEFA Europa League Qualifying')).toBe('UEFA Avrupa Ligi Elemeleri')
  })

  it('spells Turkish clubs with their own letters, full and short names alike', () => {
    expect(teamNameTr('Besiktas')).toBe('Beşiktaş')
    expect(teamNameTr('Istanbul BB')).toBe('Başakşehir')
    expect(teamNameTr('Galatasaray')).toBe('Galatasaray')
    expect(teamNameTr('Bayern Munich')).toBe('Bayern Münih')
  })

  it('names countries, as national teams and as nationalities', () => {
    expect(teamNameTr('Portugal')).toBe('Portekiz')
    expect(teamNameTr('Norway U21')).toBe('Norveç U21')
    expect(countryNameTr('Germany')).toBe('Almanya')
    expect(countryNameTr('Türkiye')).toBe('Türkiye')
    expect(countryNameTr('Ivory Coast')).toBe('Fildişi Sahili')
    expect(countryNameTr('England')).toBe('İngiltere')
    expect(countryNameTr('USA')).toBe('ABD')
    expect(countryNameTr('South Korea')).toBe('Güney Kore')
    expect(countryNameTr('Bosnia-Herzegovina')).toBe('Bosna-Hersek')
  })

  it('translates what surrounds a table', () => {
    expect(tablePhraseTr('Group A1')).toBe('A1 Grubu')
    expect(tablePhraseTr('Relegation')).toBe('Küme düşme')
    expect(tablePhraseTr('Something new')).toBe('Something new')
    expect(standingSummaryTr('2nd in Turkish Super Lig')).toBe('Süper Lig · 2. sıra')
    expect(standingSummaryTr('1st in Group A1')).toBe('A1 Grubu · 1. sıra')
  })
})

describe('localizeNames', () => {
  const match = {
    id: '1',
    leagueCode: 'uefa.nations',
    homeTeam: { id: '482', name: 'Portugal', shortName: 'Portugal', logoUrl: null },
    awayTeam: { id: '432', name: 'Besiktas', shortName: 'Besiktas', logoUrl: 'x.png' },
  }

  it('leaves English data untouched', () => {
    expect(localizeNames(match, 'en')).toBe(match)
  })

  it('translates teams wherever they are, without changing the original', () => {
    const day = { date: '2026-10-04', leagues: [{ code: 'uefa.nations', name: 'UEFA Nations League', matches: [match] }] }
    const localized = localizeNames(day, 'tr')

    expect(localized.leagues[0].name).toBe('UEFA Uluslar Ligi')
    expect(localized.leagues[0].matches[0].homeTeam.name).toBe('Portekiz')
    expect(localized.leagues[0].matches[0].awayTeam.shortName).toBe('Beşiktaş')
    expect(localized.leagues[0].matches[0].id).toBe('1')
    expect(match.homeTeam.name).toBe('Portugal')
  })

  it('leaves players alone, but translates their country', () => {
    const squad = { teamId: '1', teamName: 'Norway', players: [{ id: '9', name: 'Jordan Henderson', shortName: 'Jordan', nationality: 'England', flagUrl: null }] }
    const localized = localizeNames(squad, 'tr')

    expect(localized.teamName).toBe('Norveç')
    expect(localized.players[0].name).toBe('Jordan Henderson')
    expect(localized.players[0].shortName).toBe('Jordan')
    expect(localized.players[0].nationality).toBe('İngiltere')
  })

  it('translates a team page and a table', () => {
    const profile = { leagueCode: 'tur.1', team: match.awayTeam, standingSummary: '3rd in Turkish Super Lig', competitions: [{ code: 'club.friendly', name: 'Club Friendly' }] }
    expect(localizeNames(profile, 'tr')).toMatchObject({ standingSummary: 'Süper Lig · 3. sıra', competitions: [{ name: 'Hazırlık Maçı' }], team: { name: 'Beşiktaş' } })

    const standings = { leagueCode: 'eng.1', leagueName: 'English Premier League', groups: [{ name: 'Group B', rows: [{ rank: 18, team: match.homeTeam, note: 'Relegation' }] }] }
    expect(localizeNames(standings, 'tr')).toMatchObject({ leagueName: 'Premier Lig', groups: [{ name: 'B Grubu', rows: [{ note: 'Küme düşme', team: { name: 'Portekiz' } }] }] })
  })

  it("translates a player's competitions", () => {
    const stats = { name: '2026-27 Turkish Super Lig', leagueCode: 'tur.1', teamName: 'Fenerbahce', starts: 5 }
    expect(localizeNames(stats, 'tr')).toMatchObject({ name: '2026-27 Süper Lig', teamName: 'Fenerbahçe' })
  })

  it("translates a match's stadium country and its earlier meetings", () => {
    const detail = {
      info: { venue: 'Stade de France', city: 'Saint-Denis', country: 'France' },
      previousMeetings: [{ id: '7', competition: '2024-25 UEFA Nations League', homeTeam: match.homeTeam, awayTeam: match.awayTeam }],
    }
    const localized = localizeNames(detail, 'tr')

    expect(localized.info).toEqual({ venue: 'Stade de France', city: 'Saint-Denis', country: 'Fransa' })
    expect(localized.previousMeetings[0].competition).toBe('2024-25 UEFA Uluslar Ligi')
    expect(localized.previousMeetings[0].homeTeam.name).toBe('Portekiz')
  })

  it('passes null and undefined through', () => {
    expect(localizeNames(null, 'tr')).toBeNull()
    expect(localizeNames(undefined, 'tr')).toBeUndefined()
  })
})

describe('going back to the data source\'s names', () => {
  it('finds the stored name of a favourite starred in Turkish', () => {
    expect(sourceTeamName('Beşiktaş')).toBe('Besiktas')
    expect(sourceTeamName('Başakşehir')).toBe('Istanbul Basaksehir')
    expect(sourceTeamName('Portekiz')).toBe('Portugal')
    expect(sourceTeamName('Galatasaray')).toBe('Galatasaray')
    expect(sourceLeagueName('tur.1', 'Süper Lig')).toBe('Turkish Super Lig')
    expect(sourceLeagueName('xyz.1', 'Some Cup')).toBe('Some Cup')
  })

  it('searches for a country typed in Turkish by its English name', () => {
    expect(searchQueryFor('Almanya', 'tr')).toBe('Germany')
    expect(searchQueryFor('ingiltere', 'tr')).toBe('England')
    expect(searchQueryFor('Beşiktaş', 'tr')).toBe('Beşiktaş')
    expect(searchQueryFor('Almanya', 'en')).toBe('Almanya')
  })
})
