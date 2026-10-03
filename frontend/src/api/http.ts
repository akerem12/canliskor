import type { LeagueMatches, MatchDay, MatchDetail, Squad } from './types'

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
/** A team's squad for the current season. */
export const getSquad = (leagueCode: string, teamId: string) =>
  getJson<Squad>(`/api/leagues/${encodeURIComponent(leagueCode)}/teams/${encodeURIComponent(teamId)}/squad`)
