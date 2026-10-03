import type { Dictionary } from './i18n/en'

const TimeZone = 'Europe/Istanbul'

const timeFormat = new Intl.DateTimeFormat('en-GB', { timeZone: TimeZone, hour: '2-digit', minute: '2-digit' })
// en-CA formats dates as YYYY-MM-DD, the same shape the API uses.
const dateFormat = new Intl.DateTimeFormat('en-CA', { timeZone: TimeZone, year: 'numeric', month: '2-digit', day: '2-digit' })

/** Formatters are costly to build, so each locale's pair is built once. */
const formats = new Map<string, { long: Intl.DateTimeFormat; short: Intl.DateTimeFormat }>()

function formatsFor(locale: string) {
  let entry = formats.get(locale)
  if (!entry) {
    entry = {
      long: new Intl.DateTimeFormat(locale, { timeZone: TimeZone, weekday: 'long', day: 'numeric', month: 'long' }),
      short: new Intl.DateTimeFormat(locale, { timeZone: TimeZone, weekday: 'short', day: 'numeric', month: 'short' }),
    }
    formats.set(locale, entry)
  }
  return entry
}

/** "20:00" in Istanbul, whatever the viewer's own time zone is. The same in every language. */
export const formatTime = (iso: string) => timeFormat.format(new Date(iso))

/** Today's date in Istanbul as YYYY-MM-DD — the backend's definition of "today". */
export const istanbulToday = (now = new Date()) => dateFormat.format(now)

/** "Friday 9 October", or "9 Ekim Cuma" in Turkish. */
export const formatLongDate = (yyyyMmDd: string, t: Dictionary) =>
  formatsFor(t.locale).long.format(new Date(`${yyyyMmDd}T12:00:00+03:00`))

/** Calendar arithmetic on YYYY-MM-DD strings (done in UTC, so no time zone or DST can shift the day). */
export function addDays(yyyyMmDd: string, days: number): string {
  const date = new Date(`${yyyyMmDd}T00:00:00Z`)
  date.setUTCDate(date.getUTCDate() + days)
  return date.toISOString().slice(0, 10)
}

/** "Today", "Tomorrow", or e.g. "Sat 10 Oct". */
export function formatDayLabel(yyyyMmDd: string, offset: number, t: Dictionary): string {
  const relative: Record<number, string> = { [-1]: t.days.yesterday, 0: t.days.today, 1: t.days.tomorrow }
  return relative[offset] ?? formatsFor(t.locale).short.format(new Date(`${yyyyMmDd}T12:00:00+03:00`))
}

/** "Sat 10 Oct" in Istanbul, for a match's kickoff. */
export const formatMatchDate = (iso: string, t: Dictionary) => formatsFor(t.locale).short.format(new Date(iso))
