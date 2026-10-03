import type { Competition, ExpectedLineups, LeagueFixtures, LeagueMatches, MatchDay, MatchDetail, PlayerProfile, Squad, Standings, TeamProfile, TeamSearchResult } from './types'

async function getJson<T>(url: string): Promise<T> {
  const response = await fetch(url)
  if (!response.ok) {
    throw new Error(`GET ${url} failed with ${response.status}`)
  }
  return response.json() as Promise<T>
}

export const getLeagues = () => getJson<string[]>('/api/leagues')
/** A date (YYYY-MM-DD, Istanbul) within a week of today; omitted means today. */
export const getDay = (date?: string) => getJson<MatchDay>(date ? `/api/matches?date=${date}` : '/api/matches')
/** Includes yesterday's games still running past midnight, which /api/matches doesn't. */
export const getLive = () => getJson<LeagueMatches[]>('/api/matches/live')
/** Goals, cards, substitutions and team statistics of one match. */
export const getMatchDetail = (leagueCode: string, matchId: string) =>
  getJson<MatchDetail>(`/api/leagues/${encodeURIComponent(leagueCode)}/matches/${encodeURIComponent(matchId)}`)
/** How the teams may line up, for a match that hasn't announced its line-ups. */
export const getExpectedLineups = (leagueCode: string, matchId: string) =>
  getJson<ExpectedLineups>(`/api/leagues/${encodeURIComponent(leagueCode)}/matches/${encodeURIComponent(matchId)}/expected-lineups`)
/** A team's squad for the current season. */
export const getSquad = (leagueCode: string, teamId: string) =>
  getJson<Squad>(`/api/leagues/${encodeURIComponent(leagueCode)}/teams/${encodeURIComponent(teamId)}/squad`)
/** Followed competitions with their names, in display order. */
export const getCompetitions = () => getJson<Competition[]>('/api/competitions')
/** A competition's table; no groups if it has none. */
export const getStandings = (leagueCode: string) =>
  getJson<Standings>(`/api/leagues/${encodeURIComponent(leagueCode)}/standings`)
/** A team's results and fixtures in one competition. */
export const getTeam = (leagueCode: string, teamId: string) =>
  getJson<TeamProfile>(`/api/leagues/${encodeURIComponent(leagueCode)}/teams/${encodeURIComponent(teamId)}`)
/** A competition's matches still to be played, this month and next. */
export const getLeagueFixtures = (leagueCode: string) =>
  getJson<LeagueFixtures>(`/api/leagues/${encodeURIComponent(leagueCode)}/fixtures`)
/** Teams of the followed leagues whose name contains the query (at least two characters). */
export const searchTeams = (query: string) => getJson<TeamSearchResult[]>(`/api/teams/search?q=${encodeURIComponent(query)}`)
/** A player: who they are and their season in every competition. The league is the one they were found in. */
export const getPlayer = (leagueCode: string, playerId: string) =>
  getJson<PlayerProfile>(`/api/leagues/${encodeURIComponent(leagueCode)}/players/${encodeURIComponent(playerId)}`)
