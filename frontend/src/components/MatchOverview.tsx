import type { ReactNode } from 'react'
import type { Match, MatchDetail, MatchInfo, PreviousMeeting, Team } from '../api/types'
import { tallyMeetings, winnerOf } from '../headToHead'
import { useI18n } from '../i18n/useI18n'
import { formatDate, formatDateWithWeekday, formatTime } from '../time'
import { TeamLogo } from './common'
import { OddsBoard } from './Odds'

/** The match at a glance: odds, the two teams' earlier meetings, and when and where it is played. */
export function MatchOverview({ detail, match }: { detail: MatchDetail; match: Match }) {
  return (
    <div className="overview">
      {match.odds && <OddsBoard odds={match.odds} match={match} />}
      <HeadToHead meetings={detail.previousMeetings ?? []} match={match} />
      <MatchInfoCard info={detail.info ?? null} match={match} />
    </div>
  )
}

/** A bar of wins, draws and wins, then the meetings themselves. Nothing at all if the teams never met. */
function HeadToHead({ meetings, match }: { meetings: PreviousMeeting[]; match: Match }) {
  const { t } = useI18n()
  if (meetings.length === 0) return null

  const tally = tallyMeetings(meetings, match.homeTeam.id)
  const segments = [
    { key: 'home', count: tally.home, name: match.homeTeam.shortName },
    { key: 'draws', count: tally.draws, name: t.h2h.draws },
    { key: 'away', count: tally.away, name: match.awayTeam.shortName },
  ] as const

  return (
    <section className="overview__card" aria-label={t.h2h.title}>
      <header className="overview__head">
        <h3>{t.h2h.title}</h3>
        <span>{t.h2h.lastMeetings(meetings.length)}</span>
      </header>

      <dl className="h2h__tally">
        {segments.map(({ key, count, name }) => (
          <div key={key} className={`h2h__count h2h__count--${key}`}>
            <dd>{count}</dd>
            <dt>{name}</dt>
          </div>
        ))}
      </dl>
      <div className="h2h__bar" aria-hidden>
        {segments.map(({ key, count }) => count > 0 && (
          <span key={key} className={`h2h__segment h2h__segment--${key}`} style={{ flexGrow: count }} />
        ))}
      </div>

      <ol className="h2h__list">
        {meetings.map(meeting => <MeetingRow key={meeting.id} meeting={meeting} />)}
      </ol>
    </section>
  )
}

function MeetingRow({ meeting }: { meeting: PreviousMeeting }) {
  const { t } = useI18n()
  const winnerId = winnerOf(meeting)

  const side = (team: Team, align: 'home' | 'away') => (
    <span className={`h2h__team h2h__team--${align}${team.id === winnerId ? ' h2h__team--winner' : ''}`} title={team.name}>
      <TeamLogo team={team} size={20} />
      <span className="team__name team__name--full">{team.name}</span>
      <span className="team__name team__name--short">{team.shortName}</span>
    </span>
  )

  return (
    <li className="h2h__row">
      <span className="h2h__when">
        {formatDate(meeting.kickoff, t)}
        {meeting.competition && <span className="h2h__competition"> · {meeting.competition}</span>}
      </span>
      {side(meeting.homeTeam, 'home')}
      <span className="h2h__score">{meeting.score.home} - {meeting.score.away}</span>
      {side(meeting.awayTeam, 'away')}
    </li>
  )
}

/** Kick-off, stadium, referee and crowd. A row shows only if its fact is known. */
function MatchInfoCard({ info, match }: { info: MatchInfo | null; match: Match }) {
  const { t } = useI18n()
  const venue = info?.venue ?? match.venue
  const place = [info?.city, info?.country].filter(Boolean).join(', ')

  const row = (icon: string, label: string, value: ReactNode, note?: string) => (
    <div className="info__row">
      <span className="info__icon" aria-hidden>{icon}</span>
      <dt>{label}</dt>
      <dd>
        {value}
        {note && <span className="info__note">{note}</span>}
      </dd>
    </div>
  )

  return (
    <section className="overview__card" aria-label={t.info.title}>
      <header className="overview__head">
        <h3>{t.info.title}</h3>
      </header>
      <dl className="info">
        {row('📅', t.info.kickoff, `${formatDateWithWeekday(match.kickoff, t)} · ${formatTime(match.kickoff)}`, t.info.istanbulTime)}
        {venue ? row('🏟️', t.info.venue, venue, place || undefined) : place && row('📍', t.info.venue, place)}
        {info?.referee && row('🧑‍⚖️', t.info.referee, info.referee)}
        {info?.attendance != null && row('👥', t.info.attendance, info.attendance.toLocaleString(t.locale))}
      </dl>
    </section>
  )
}
