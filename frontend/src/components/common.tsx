import { useState } from 'react'
import type { Team } from '../api/types'
import { useI18n } from '../i18n/useI18n'

/** A team's crest. A missing or broken image leaves an empty slot of the same size, so rows stay aligned. */
export function TeamLogo({ team, size }: { team: Team; size: number }) {
  const [broken, setBroken] = useState(false)
  const style = { width: size, height: size }

  return team.logoUrl && !broken
    ? <img className="logo" src={team.logoUrl} alt="" style={style} loading="lazy" onError={() => setBroken(true)} />
    : <span className="logo logo--none" style={style} aria-hidden />
}

/**
 * An own goal: a greyed ball with an "OG" badge, so it can't be taken for a goal or a red card.
 * @param compact The badge alone, for the small marks next to a player on the pitch.
 */
export function OwnGoalMark({ compact = false, count = 1 }: { compact?: boolean; count?: number }) {
  const { t } = useI18n()

  return (
    <span className="own-goal" role="img" aria-label={t.lineups.ownGoal} title={t.lineups.ownGoal}>
      {!compact && <span className="own-goal__ball">⚽</span>}
      <span className="own-goal__badge">{t.match.ownGoalShort}{count > 1 ? count : ''}</span>
    </span>
  )
}

/** Grey bars standing in for content that is on its way. */
export function Skeleton({ rows, height = 18 }: { rows: number; height?: number }) {
  return (
    <div className="skeleton-group" aria-hidden>
      {Array.from({ length: rows }, (_, i) => (
        <span key={i} className="skeleton" style={{ height, width: `${92 - ((i * 17) % 30)}%` }} />
      ))}
    </div>
  )
}

/** Shown where there is nothing to show: no data yet, none at all, or a failed load (then with a retry). */
export function EmptyState({ icon, title, hint, onRetry }: { icon: string; title: string; hint?: string; onRetry?: () => void }) {
  const { t } = useI18n()

  return (
    <div className="empty">
      <span className="empty__icon" aria-hidden>{icon}</span>
      <p className="empty__title">{title}</p>
      {hint && <p className="empty__hint">{hint}</p>}
      {onRetry && <button className="empty__retry" onClick={onRetry}>{t.common.tryAgain}</button>}
    </div>
  )
}
