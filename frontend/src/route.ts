// Which page is open, kept in the URL's query string so every page can be linked to and Back works.

export type Route =
  | { view: 'matches' }
  | { view: 'leagues' }
  | { view: 'favorites' }
  | { view: 'league'; leagueCode: string }
  | { view: 'team'; leagueCode: string; teamId: string }
  | { view: 'match'; leagueCode: string; matchId: string }

/**
 * "?league=tur.1&match=401888379" → that match; "?league=tur.1&team=1895" → that team; "?league=tur.1" → the
 * league's table; "?view=leagues" → the list of leagues; "?view=favorites" → the favourites; anything else → the matches of the day.
 */
export function routeFromSearch(search: string): Route {
  const params = new URLSearchParams(search)
  const leagueCode = params.get('league')
  const matchId = params.get('match')
  const teamId = params.get('team')

  if (leagueCode && matchId) return { view: 'match', leagueCode, matchId }
  if (leagueCode && teamId) return { view: 'team', leagueCode, teamId }
  if (leagueCode) return { view: 'league', leagueCode }
  if (params.get('view') === 'leagues') return { view: 'leagues' }
  if (params.get('view') === 'favorites') return { view: 'favorites' }
  return { view: 'matches' }
}

/**
 * The query string for a route. Only the selected day (?date=) survives from the current one, so returning to the
 * matches shows the same day; a match's open tab and the like belong to the page being left.
 */
export function routeToSearch(route: Route, currentSearch: string): string {
  const params = new URLSearchParams()
  const date = new URLSearchParams(currentSearch).get('date')
  if (date) params.set('date', date)

  switch (route.view) {
    case 'leagues':
      params.set('view', 'leagues')
      break
    case 'favorites':
      params.set('view', 'favorites')
      break
    case 'league':
      params.set('league', route.leagueCode)
      break
    case 'team':
      params.set('league', route.leagueCode)
      params.set('team', route.teamId)
      break
    case 'match':
      params.set('league', route.leagueCode)
      params.set('match', route.matchId)
      break
  }

  const search = params.toString()
  return search ? `?${search}` : ''
}

export const sameRoute = (a: Route, b: Route) => JSON.stringify(a) === JSON.stringify(b)
