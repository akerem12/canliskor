import { useI18n } from '../i18n/useI18n'
import { usePlayerProfile } from './usePlayerProfile'

/**
 * A player's name that opens their profile. Without an id there is no profile to open, so it is plain text.
 * A span rather than a button: a button is one unbreakable box, and names have to wrap like text in narrow columns.
 * `label` shows a shortened name (e.g. "O. Watkins") while the profile and tooltip keep the full one.
 */
export function PlayerName({ leagueCode, playerId, name, label = name }: { leagueCode: string; playerId: string | null; name: string; label?: string }) {
  const openPlayer = usePlayerProfile()
  const { t } = useI18n()
  if (!playerId) return label === name ? <>{name}</> : <span title={name}>{label}</span>

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
      {label}
    </span>
  )
}
