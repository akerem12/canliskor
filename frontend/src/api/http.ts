import type { LeagueMatches, MatchDay } from './types'

async function getJson<T>(url: string): Promise<T> {
  const response = await fetch(url)
  if (!response.ok) {
    throw new Error(`GET ${url} failed with ${response.status}`)
  }
  return response.json() as Promise<T>
}

export const getLeagues = () => getJson<string[]>('/api/leagues')
export const getToday = () => getJson<MatchDay>('/api/matches')
/** Includes yesterday's games still running past midnight, which /api/matches doesn't. */
export const getLive = () => getJson<LeagueMatches[]>('/api/matches/live')
