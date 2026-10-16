import { useContext } from 'react'
import { AlertsContext } from '../alerts/useAlerts'
import { useI18n } from '../i18n/useI18n'
import type { FavoriteLeague, FavoriteTeam } from './favorites'
import { isFavoriteLeague, isFavoriteTeam } from './favorites'
import { useFavorites } from './useFavorites'

type Props = ({ team: FavoriteTeam } | { league: FavoriteLeague }) & {
  /** "large" on a page's header, "small" inside rows and lists. */
  size?: 'small' | 'large'
}

/**
 * A star that adds a team or a league to the favourites, or takes it out again. A favourite team's matches are
 * notified, so starring one also asks for permission to notify if that hasn't been decided yet.
 */
export function FavoriteButton(props: Props) {
  const { t } = useI18n()
  const { favorites, toggleTeam, toggleLeague } = useFavorites()
  // Absent where a star is drawn without alerts around it (tests, previews).
  const alerts = useContext(AlertsContext)
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
        if (isTeam) {
          toggleTeam(props.team)
          if (!active) void alerts?.askPermission()
        } else {
          toggleLeague(props.league)
        }
      }}
    >
      {active ? '★' : '☆'}
    </button>
  )
}
