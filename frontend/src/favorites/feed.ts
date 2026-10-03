import type { LeagueFixtures, Match, TeamProfile } from '../api/types'

/** One upcoming match in the favourites feed, with its competition's name. */
export interface FeedItem {
  match: Match
  leagueName: string
}

/** Narrows the feed to one favourite: a team (by id) or a league (by code). */
export type FeedFilter = { kind: 'team'; teamId: string } | { kind: 'league'; code: string } | null

/**
 * The upcoming matches of the favourite teams and leagues as one list, soonest first. A match that comes in
 * twice (two favourite teams playing each other, or a favourite team in a favourite league) is listed once.
 */
export function buildFeed(teams: TeamProfile[], leagues: LeagueFixtures[]): FeedItem[] {
  const names = new Map<string, string>()
  for (const league of leagues) names.set(league.leagueCode, league.leagueName)
  for (const competition of teams.flatMap(t => t.competitions)) {
    if (!names.has(competition.code)) names.set(competition.code, competition.name)
  }

  const byId = new Map<string, Match>()
  for (const match of [...teams.flatMap(t => t.upcomingMatches), ...leagues.flatMap(l => l.matches)]) {
    if (!byId.has(match.id)) byId.set(match.id, match)
  }

  return [...byId.values()]
    .sort((a, b) => Date.parse(a.kickoff) - Date.parse(b.kickoff) || a.id.localeCompare(b.id))
    .map(match => ({ match, leagueName: names.get(match.leagueCode) ?? match.leagueCode }))
}

export function filterFeed(feed: FeedItem[], filter: FeedFilter): FeedItem[] {
  if (!filter) return feed
  return filter.kind === 'team'
    ? feed.filter(({ match }) => match.homeTeam.id === filter.teamId || match.awayTeam.id === filter.teamId)
    : feed.filter(({ match }) => match.leagueCode === filter.code)
}
