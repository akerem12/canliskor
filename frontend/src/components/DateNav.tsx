import { useI18n } from '../i18n/useI18n'
import { addDays, formatDayLabel, istanbulToday } from '../time'

/** Mirrors MatchQueryService.MaxDaysAway on the backend. */
export const MaxDaysAway = 7

interface Props {
  offset: number
  onChange: (offset: number) => void
}

/** ‹ Yesterday · Today · Tomorrow › — one tap per day, up to a week either way. */
export function DateNav({ offset, onChange }: Props) {
  const { t } = useI18n()
  const today = istanbulToday()
  const visible = [offset - 1, offset, offset + 1].filter(o => Math.abs(o) <= MaxDaysAway)

  return (
    <nav className="date-nav" aria-label={t.days.choose}>
      <button className="date-nav__arrow" onClick={() => onChange(offset - 1)} disabled={offset <= -MaxDaysAway} aria-label={t.days.previous}>
        ‹
      </button>
      {visible.map(o => (
        <button
          key={o}
          className={o === offset ? 'date-nav__day date-nav__day--active' : 'date-nav__day'}
          aria-current={o === offset ? 'date' : undefined}
          onClick={() => onChange(o)}
        >
          {formatDayLabel(addDays(today, o), o, t)}
        </button>
      ))}
      <button className="date-nav__arrow" onClick={() => onChange(offset + 1)} disabled={offset >= MaxDaysAway} aria-label={t.days.next}>
        ›
      </button>
      {offset !== 0 && (
        <button className="date-nav__today" onClick={() => onChange(0)}>
          {t.days.backToToday}
        </button>
      )}
    </nav>
  )
}
