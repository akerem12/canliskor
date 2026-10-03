const TimeZone = 'Europe/Istanbul'

const timeFormat = new Intl.DateTimeFormat('en-GB', { timeZone: TimeZone, hour: '2-digit', minute: '2-digit' })
// en-CA formats dates as YYYY-MM-DD, the same shape the API uses.
const dateFormat = new Intl.DateTimeFormat('en-CA', { timeZone: TimeZone, year: 'numeric', month: '2-digit', day: '2-digit' })
const longDateFormat = new Intl.DateTimeFormat('en-GB', { timeZone: TimeZone, weekday: 'long', day: 'numeric', month: 'long' })

/** "20:00" in Istanbul, whatever the viewer's own time zone is. */
export const formatTime = (iso: string) => timeFormat.format(new Date(iso))

/** Today's date in Istanbul as YYYY-MM-DD — the backend's definition of "today". */
export const istanbulToday = (now = new Date()) => dateFormat.format(now)

export const formatLongDate = (yyyyMmDd: string) => longDateFormat.format(new Date(`${yyyyMmDd}T12:00:00+03:00`))

/** Calendar arithmetic on YYYY-MM-DD strings (done in UTC, so no time zone or DST can shift the day). */
export function addDays(yyyyMmDd: string, days: number): string {
  const date = new Date(`${yyyyMmDd}T00:00:00Z`)
  date.setUTCDate(date.getUTCDate() + days)
  return date.toISOString().slice(0, 10)
}

const relativeDays: Record<number, string> = { [-1]: 'Yesterday', 0: 'Today', 1: 'Tomorrow' }
const shortDateFormat = new Intl.DateTimeFormat('en-GB', { timeZone: TimeZone, weekday: 'short', day: 'numeric', month: 'short' })

/** "Today", "Tomorrow", or e.g. "Sat 10 Oct". */
export const formatDayLabel = (yyyyMmDd: string, offset: number) =>
  relativeDays[offset] ?? shortDateFormat.format(new Date(`${yyyyMmDd}T12:00:00+03:00`))

/** "Sat 10 Oct" in Istanbul, for a match's kickoff. */
export const formatMatchDate = (iso: string) => shortDateFormat.format(new Date(iso))
