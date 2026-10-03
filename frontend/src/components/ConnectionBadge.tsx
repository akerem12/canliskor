import { useI18n } from '../i18n/useI18n'
import type { ConnectionStatus } from '../live/useLiveScores'

export function ConnectionBadge({ status }: { status: ConnectionStatus }) {
  const { t } = useI18n()

  return (
    <span className={`connection connection--${status}`} role="status" title={t.connection[status]}>
      <span className="connection__dot" aria-hidden />
      {/* On phones only the dot shows; the words stay for screen readers. */}
      <span className="connection__label">{t.connection[status]}</span>
    </span>
  )
}
