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
