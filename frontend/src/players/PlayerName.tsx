import { useI18n } from '../i18n/useI18n'
import { usePlayerProfile } from './usePlayerProfile'

/**
 * A player's name that opens their profile. Without an id there is no profile to open, so it is plain text.
 * A span rather than a button: a button is one unbreakable box, and names have to wrap like text in narrow columns.
 */
export function PlayerName({ leagueCode, playerId, name }: { leagueCode: string; playerId: string | null; name: string }) {
  const openPlayer = usePlayerProfile()
  const { t } = useI18n()
  if (!playerId) return <>{name}</>

  const open = () => openPlayer({ leagueCode, playerId, name })
  return (
    <span
      className="player-link"
      role="button"
      tabIndex={0}
      title={t.player.open(name)}
      onClick={open}
      onKeyDown={e => {
        if (e.key === 'Enter' || e.key === ' ') {
          e.preventDefault()
          open()
        }
      }}
    >
      {name}
    </span>
  )
}
