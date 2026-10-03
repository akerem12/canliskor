import { useI18n } from '../i18n/useI18n'
import type { FavoriteLeague, FavoriteTeam } from './favorites'
import { isFavoriteLeague, isFavoriteTeam } from './favorites'
import { useFavorites } from './useFavorites'

type Props = ({ team: FavoriteTeam } | { league: FavoriteLeague }) & {
  /** "large" on a page's header, "small" inside rows and lists. */
  size?: 'small' | 'large'
}

/** A star that adds a team or a league to the favourites, or takes it out again. */
export function FavoriteButton(props: Props) {
  const { t } = useI18n()
  const { favorites, toggleTeam, toggleLeague } = useFavorites()
  const isTeam = 'team' in props
  const name = isTeam ? props.team.name : props.league.name
  const active = isTeam ? isFavoriteTeam(favorites, props.team.teamId) : isFavoriteLeague(favorites, props.league.code)
  const label = active ? t.favourites.remove(name) : t.favourites.add(name)

  return (
    <button
      className={['star', `star--${props.size ?? 'small'}`, active && 'star--on'].filter(Boolean).join(' ')}
      aria-pressed={active}
      aria-label={label}
      title={label}
      onClick={event => {
        // Stars sit inside rows that are links themselves.
        event.stopPropagation()
        if (isTeam) toggleTeam(props.team)
        else toggleLeague(props.league)
      }}
    >
      {active ? '★' : '☆'}
    </button>
  )
}
