import { useEffect, useState } from 'react'
import { TeamAlertsSwitch } from '../alerts/AlertControls'
import { getCompetitions, getLeagueFixtures, getTeam } from '../api/http'
import type { LeagueFixtures, Team, TeamProfile } from '../api/types'
import { isInPlay } from '../api/types'
import { useFetch } from '../api/useFetch'
import { OddsStrip } from '../components/Odds'
import { EmptyState, Skeleton, TeamLogo } from '../components/common'
import { useI18n } from '../i18n/useI18n'
import type { Route } from '../route'
import { formatMatchDate, formatTime } from '../time'
import type { Favorites, FavoriteTeam } from './favorites'
import type { FeedFilter, FeedItem } from './feed'
import { buildFeed, filterFeed } from './feed'
import { useFavorites } from './useFavorites'

/** How many matches show at first, and how many more each "Show more" adds. */
const PageSize = 20

interface Loaded {
  /** What was asked for, so a result is never shown for a different set of favourites. */
  key: string
  teams: TeamProfile[]
  leagues: LeagueFixtures[]
  /** Names of the favourites whose matches could not be loaded. */
  failed: string[]
}

/**
 * Loads the upcoming matches of every favourite. One favourite failing doesn't hide the others: what loaded is
 * shown, and the page says which ones are missing.
 */
function useFavoriteMatches(favorites: Favorites) {
  const key = JSON.stringify([favorites.teams.map(t => `${t.leagueCode}/${t.teamId}`), favorites.leagues.map(l => l.code)])
  const [loaded, setLoaded] = useState<Loaded | null>(null)
  const [attempt, setAttempt] = useState(0)

  useEffect(() => {
    let cancelled = false
    const { teams, leagues } = favorites
    Promise.allSettled([
      ...teams.map(t => getTeam(t.leagueCode, t.teamId)),
      ...leagues.map(l => getLeagueFixtures(l.code)),
    ]).then(results => {
      if (cancelled) return
      const names = [...teams.map(t => t.name), ...leagues.map(l => l.name)]
      const fulfilled = <T,>(slice: PromiseSettledResult<unknown>[]) =>
        slice.filter(r => r.status === 'fulfilled').map(r => (r as PromiseFulfilledResult<T>).value)

      setLoaded({
        key,
        teams: fulfilled<TeamProfile>(results.slice(0, teams.length)),
        leagues: fulfilled<LeagueFixtures>(results.slice(teams.length)),
        failed: names.filter((_, i) => results[i].status === 'rejected'),
      })
    })
    return () => {
      cancelled = true
    }
    // `key` stands for the favourites: the same ids mean the same requests.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [key, attempt])

  const current = loaded?.key === key ? loaded : null
  return {
    loading: current === null,
    feed: current ? buildFeed(current.teams, current.leagues) : [],
    failed: current?.failed ?? [],
    retry: () => {
      setLoaded(null)
      setAttempt(n => n + 1)
    },
  }
}

/**
 * The visitor's favourites: a bar of their teams and leagues for quick access and filtering, and below it the
 * matches those teams and leagues are about to play, soonest first.
 */
export function FavoritesPage({ onNavigate }: { onNavigate: (route: Route) => void }) {
  const { t } = useI18n()
  const { favorites } = useFavorites()
  const [filter, setFilter] = useState<FeedFilter>(null)
  const [shownCount, setShownCount] = useState(PageSize)
  const { loading, feed, failed, retry } = useFavoriteMatches(favorites)
  const followed = useFetch('competitions', getCompetitions)

  if (favorites.teams.length === 0 && favorites.leagues.length === 0) {
    return (
      <main className="page" aria-label={t.favourites.title}>
        <h2 className="page__title">{t.favourites.title}</h2>
        <div className="panel">
          <EmptyState
            icon="⭐"
            title={t.favourites.emptyTitle}
            hint={t.favourites.emptyHint}
          />
          <button className="cta" onClick={() => onNavigate({ view: 'leagues' })}>{t.favourites.browse}</button>
        </div>
      </main>
    )
  }

  // A favourite that was just removed can't stay selected.
  const activeFilter = filter && (filter.kind === 'team'
    ? favorites.teams.some(t => t.teamId === filter.teamId)
    : favorites.leagues.some(l => l.code === filter.code)) ? filter : null
  const pick = (next: FeedFilter) => {
    setFilter(next)
    setShownCount(PageSize)
  }

  const matches = filterFeed(feed, activeFilter)
  const followedCodes = followed.data ? new Set(followed.data.map(c => c.code)) : undefined
  const favoriteLeagueOf = new Map(favorites.teams.map(t => [t.teamId, t.leagueCode]))

  return (
    <main className="page" aria-label={t.favourites.title}>
      <h2 className="page__title">{t.favourites.title}</h2>

      <TeamAlertsSwitch teamCount={favorites.teams.length} />

      <QuickBar favorites={favorites} filter={activeFilter} onPick={pick} onNavigate={onNavigate} />

      <section className="panel" aria-label={t.favourites.upcoming} aria-busy={loading}>
        <h3 className="panel__title">{t.favourites.upcoming}</h3>

        {failed.length > 0 && (
          <p className="notice notice--error" role="alert">
            {t.favourites.failed(failed.join(', '))}{' '}
            <button className="notice__retry" onClick={retry}>{t.common.tryAgain}</button>
          </p>
        )}

        {loading ? (
          <Skeleton rows={6} height={54} />
        ) : matches.length === 0 ? (
          failed.length === 0 && (
            <EmptyState
              icon="📅"
              title={t.favourites.nothingPlanned}
              hint={activeFilter ? t.favourites.nothingForThis : t.favourites.nothingForAny}
            />
          )
        ) : (
          <>
            <ul className="feed">
              {matches.slice(0, shownCount).map(item => (
                <FeedCard
                  key={item.match.id}
                  item={item}
                  // A team's page needs a league the site follows: the one it was starred in, else the match's own.
                  teamLeague={team => favoriteLeagueOf.get(team.id) ?? (followedCodes?.has(item.match.leagueCode) ? item.match.leagueCode : undefined)}
                  canOpenMatch={followedCodes?.has(item.match.leagueCode) ?? true}
                  onNavigate={onNavigate}
                />
              ))}
            </ul>
            {matches.length > shownCount && (
              <button className="more" onClick={() => setShownCount(shownCount + PageSize)}>
                {t.favourites.showMore(matches.length - shownCount)}
              </button>
            )}
          </>
        )}
      </section>
    </main>
  )
}

/** The favourites as a row of chips that scrolls sideways. A chip narrows the list to that team or league. */
function QuickBar({ favorites, filter, onPick, onNavigate }: {
  favorites: Favorites
  filter: FeedFilter
  onPick: (filter: FeedFilter) => void
  onNavigate: (route: Route) => void
}) {
  const { t } = useI18n()
  const selectedTeam = filter?.kind === 'team' ? favorites.teams.find(t => t.teamId === filter.teamId) : undefined
  const selectedLeague = filter?.kind === 'league' ? favorites.leagues.find(l => l.code === filter.code) : undefined

  return (
    <>
      <div className="quickbar" role="toolbar" aria-label={t.favourites.filterBy}>
        <button className={filter === null ? 'chip chip--active' : 'chip'} aria-pressed={filter === null} onClick={() => onPick(null)}>
          {t.favourites.all}
        </button>
        {favorites.teams.map(team => {
          const active = filter?.kind === 'team' && filter.teamId === team.teamId
          return (
            <button
              key={team.teamId}
              className={active ? 'chip chip--active' : 'chip'}
              aria-pressed={active}
              // A second click on the selected chip shows everything again.
              onClick={() => onPick(active ? null : { kind: 'team', teamId: team.teamId })}
            >
              <TeamLogo team={asTeam(team)} size={22} />
              {team.name}
            </button>
          )
        })}
        {favorites.leagues.map(league => {
          const active = filter?.kind === 'league' && filter.code === league.code
          return (
            <button
              key={league.code}
              className={active ? 'chip chip--active' : 'chip'}
              aria-pressed={active}
              onClick={() => onPick(active ? null : { kind: 'league', code: league.code })}
            >
              <span aria-hidden>🏆</span>
              {league.name}
            </button>
          )
        })}
      </div>

      {selectedTeam && (
        <button className="quickbar__open" onClick={() => onNavigate({ view: 'team', leagueCode: selectedTeam.leagueCode, teamId: selectedTeam.teamId })}>
          {t.favourites.openTeam(selectedTeam.name)}
        </button>
      )}
      {selectedLeague && (
        <button className="quickbar__open" onClick={() => onNavigate({ view: 'league', leagueCode: selectedLeague.code })}>
          {t.favourites.openLeague(selectedLeague.name)}
        </button>
      )}
    </>
  )
}

const asTeam = (favorite: FavoriteTeam): Team =>
  ({ id: favorite.teamId, name: favorite.name, shortName: favorite.name, logoUrl: favorite.logoUrl })

/** One upcoming match: when, which competition, who, where; with links to the match and to both teams. */
function FeedCard({ item, teamLeague, canOpenMatch, onNavigate }: {
  item: FeedItem
  /** The league under which a team's page can be opened; undefined if there is none. */
  teamLeague: (team: Team) => string | undefined
  canOpenMatch: boolean
  onNavigate: (route: Route) => void
}) {
  const { t } = useI18n()
  const { match, leagueName } = item
  const live = isInPlay(match.status)

  const side = (team: Team, align: 'home' | 'away') => {
    const leagueCode = teamLeague(team)
    const content = (
      <>
        <TeamLogo team={team} size={28} />
        <span className="feed__name">{team.name}</span>
      </>
    )
    return leagueCode ? (
      <button className={`feed__team feed__team--${align}`} onClick={() => onNavigate({ view: 'team', leagueCode, teamId: team.id })} title={t.favourites.teamPage(team.name)}>
        {content}
      </button>
    ) : (
      <span className={`feed__team feed__team--${align}`}>{content}</span>
    )
  }

  return (
    <li className={live ? 'feed__card feed__card--live' : 'feed__card'}>
      <div className="feed__meta">
        <span className="feed__league">{leagueName}</span>
        <span>{formatMatchDate(match.kickoff, t)}</span>
      </div>
      <div className="feed__teams">
        {side(match.homeTeam, 'home')}
        <span className="feed__time">
          {match.score ? `${match.score.home} - ${match.score.away}` : formatTime(match.kickoff)}
          {live && <span className="feed__live">{match.clock ?? t.status.live}</span>}
        </span>
        {side(match.awayTeam, 'away')}
      </div>
      {match.status === 'Scheduled' && match.odds && <OddsStrip odds={match.odds} className="feed__odds" />}
      <div className="feed__foot">
        <span className="feed__venue">{match.venue ? `📍 ${match.venue}` : ''}</span>
        {canOpenMatch && (
          <button className="feed__open" onClick={() => onNavigate({ view: 'match', leagueCode: match.leagueCode, matchId: match.id })}>
            {t.favourites.matchPage}
          </button>
        )}
      </div>
    </li>
  )
}
