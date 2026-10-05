import { useState } from 'react'
import type { Team } from '../api/types'
import { useI18n } from '../i18n/useI18n'

/** A team's crest. A missing or broken image gives a plain grey shield of the same size, so rows stay aligned. */
export function TeamLogo({ team, size }: { team: Team; size: number }) {
  const [broken, setBroken] = useState(false)

  return team.logoUrl && !broken
    ? <img className="logo" src={team.logoUrl} alt="" width={size} height={size} loading="lazy" onError={() => setBroken(true)} />
    : (
      <svg className="logo logo--none" width={size} height={size} viewBox="0 0 24 24" aria-hidden>
        <path d="M12 2.5 4.5 5v6.2c0 4.6 3 8.4 7.5 10.3 4.5-1.9 7.5-5.7 7.5-10.3V5z" />
      </svg>
    )
}

/** The site's mark: a goal frame with a ball in the net. Frame and net take their colours from the theme. */
export function LogoMark() {
  return (
    <svg className="logo-mark" viewBox="0 0 120 120" aria-hidden>
      <path className="logo-mark__net" d="M40 30 V92 M60 30 V92 M80 30 V92 M18 52 H102 M18 72 H102" strokeWidth="2" />
      <path d="M18 94 V28 H102 V94" fill="none" stroke="currentColor" strokeWidth="7" strokeLinecap="round" strokeLinejoin="round" />
      <circle cx="66" cy="76" r="13" fill="#2BB673" />
    </svg>
  )
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
