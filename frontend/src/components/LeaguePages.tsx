import { useEffect, useLayoutEffect, useMemo, useRef, useState } from 'react'
import { getCompetitions, getLeagueSeason, getStandings } from '../api/http'
import type { LeagueFixtures } from '../api/types'
import { isInPlay } from '../api/types'
import { useFetch } from '../api/useFetch'
import { FavoriteButton } from '../favorites/FavoriteButton'
import { localizeNames } from '../i18n/names'
import { useI18n } from '../i18n/useI18n'
import { matchDays, openingDay } from '../leagueTimeline'
import type { Route } from '../route'
import { formatDayLabel, formatLongDate, istanbulToday } from '../time'
import { tabFromUrl, writeTabToUrl } from '../urlTab'
import { EmptyState, Skeleton } from './common'
import { MatchRow } from './MatchRow'
import { StandingsTable } from './StandingsTable'
import { TeamSearchBox } from './TeamSearchBox'

/** Every followed competition; each leads to its table, and from there to its teams. */
export function LeaguesPage({ onNavigate }: { onNavigate: (route: Route) => void }) {
  const { t } = useI18n()
  const competitions = useFetch('competitions', getCompetitions)

  return (
    <main className="page" aria-label={t.leagues.title}>
      <h2 className="page__title">{t.leagues.title}</h2>
      <TeamSearchBox onNavigate={onNavigate} />
      <p className="page__intro">{t.leagues.intro}</p>

      {competitions.loading && <Skeleton rows={8} height={44} />}
      {competitions.error && <EmptyState icon="🏆" title={t.leagues.cantLoad} onRetry={competitions.retry} />}
      {competitions.data && (
        <ul className="league-list">
          {competitions.data.map(league => (
            <li key={league.code}>
              <button className="league-list__item" onClick={() => onNavigate({ view: 'league', leagueCode: league.code })}>
                <span>{league.name}</span>
                <span aria-hidden>›</span>
              </button>
              <FavoriteButton league={league} size="large" />
            </li>
          ))}
        </ul>
      )}
    </main>
  )
}

interface LeagueProps {
  leagueCode: string
  leagueName: string | undefined
  onNavigate: (route: Route) => void
  onBack: () => void
}

const tabs = ['standings', 'fixtures'] as const
type Tab = typeof tabs[number]

/** One competition: its table (every team, whether or not it plays today), and its season's matches. */
export function LeaguePage({ leagueCode, leagueName, onNavigate, onBack }: LeagueProps) {
  const { t } = useI18n()
  const standings = useFetch(`standings/${leagueCode}`, () => getStandings(leagueCode))
  const name = standings.data?.leagueName ?? leagueName ?? leagueCode

  const [tab, setTab] = useState<Tab>(() => tabFromUrl(tabs) ?? 'standings')
  // A tab is built when it is first opened and kept from then on, so coming back to it loads nothing again.
  const [opened, setOpened] = useState<ReadonlySet<Tab>>(() => new Set([tab]))
  const pickTab = (picked: Tab) => {
    setTab(picked)
    setOpened(previous => new Set(previous).add(picked))
    writeTabToUrl(picked)
  }

  return (
    <main className="page" aria-label={name}>
      <header className="page__top">
        <button className="page__back" onClick={onBack}>{t.common.back}</button>
        <button className="page__crumb" onClick={() => onNavigate({ view: 'leagues' })}>{t.leagues.all}</button>
      </header>
      <h2 className="page__title">
        {name}
        <FavoriteButton league={{ code: leagueCode, name }} size="large" />
      </h2>

      <nav className="tabs team__tabs" aria-label={t.leagues.sections}>
        {tabs.map(known => (
          <button
            key={known}
            className={known === tab ? 'tab tab--active' : 'tab'}
            aria-current={known === tab ? 'page' : undefined}
            onClick={() => pickTab(known)}
          >
            {t.leagues.tabs[known]}
          </button>
        ))}
      </nav>

      {opened.has('standings') && (
        <section className="panel" aria-label={t.leagues.standings} hidden={tab !== 'standings'}>
          {standings.loading && <Skeleton rows={12} />}
          {standings.error && <EmptyState icon="📊" title={t.leagues.tableUnavailable} hint={t.leagues.sourceBusy} onRetry={standings.retry} />}
          {standings.data && (standings.data.groups.length === 0
            ? <EmptyState icon="📊" title={t.leagues.noTable} hint={t.leagues.noTableHintLeague} />
            : <StandingsTable standings={standings.data} onNavigate={onNavigate} />)}
        </section>
      )}
      {opened.has('fixtures') && (
        <div hidden={tab !== 'fixtures'}>
          <SeasonMatches leagueCode={leagueCode} active={tab === 'fixtures'} onNavigate={onNavigate} />
        </div>
      )}
    </main>
  )
}

/** While a match is in play the list is asked for again this often (the server has it from the live scoreboard). */
const LiveRefreshMs = 30_000

/**
 * The season's matches day by day, oldest at the top. Showing the tab brings the round in progress (or the
 * latest one played) to the top of the screen, with the rounds to come below it; earlier ones are a scroll up.
 * @param active The tab is the one shown: the list is only scrolled to then, and only refreshed then.
 */
function SeasonMatches({ leagueCode, active, onNavigate }: { leagueCode: string; active: boolean; onNavigate: (route: Route) => void }) {
  const { t, language } = useI18n()
  const season = useFetch(`season/${leagueCode}`, () => getLeagueSeason(leagueCode))
  const [refreshed, setRefreshed] = useState<LeagueFixtures | null>(null)
  const data = refreshed ?? season.data
  const days = useMemo(() => matchDays(data?.matches ?? []), [data])
  const opening = openingDay(days)
  const today = istanbulToday()
  const anyInPlay = data?.matches.some(match => isInPlay(match.status)) ?? false

  useEffect(() => {
    if (!active || !anyInPlay) return
    const timer = setInterval(() => {
      getLeagueSeason(leagueCode).then(fresh => setRefreshed(localizeNames(fresh, language)), () => {})
    }, LiveRefreshMs)
    return () => clearInterval(timer)
  }, [active, anyInPlay, leagueCode, language])

  // Once each time the tab is shown: a refresh while it is open must not move the list under the reader.
  const placed = useRef(false)
  const openingRef = useRef<HTMLElement>(null)
  const roomRef = useRef<HTMLDivElement>(null)
  useLayoutEffect(() => {
    if (!active) {
      placed.current = false
      return
    }
    const day = openingRef.current
    const room = roomRef.current
    if (placed.current || days.length === 0 || !room) return
    placed.current = true
    room.style.height = '0px'
    if (!day) return

    // Below the header that stays at the top of the screen.
    const header = document.querySelector('.appbar')?.getBoundingClientRect().height ?? 0
    const top = day.getBoundingClientRect().top + window.scrollY - header - 8
    // Near the end of a season the page may end too soon for the day to reach the top: make room below.
    const short = top + window.innerHeight - document.documentElement.scrollHeight
    if (short > 0) room.style.height = `${short}px`
    window.scrollTo({ top, behavior: 'instant' })
  }, [active, days])

  if (season.loading) return <section className="panel"><Skeleton rows={10} /></section>
  if (season.error && !data) {
    return (
      <section className="panel">
        <EmptyState icon="📅" title={t.leagues.seasonUnavailable} hint={t.leagues.sourceBusy} onRetry={season.retry} />
      </section>
    )
  }
  if (days.length === 0) {
    return <section className="panel"><EmptyState icon="📅" title={t.leagues.noSeasonMatches} /></section>
  }

  return (
    <>
      {days.map(({ day, matches }, index) => {
        const offset = Math.round((Date.parse(`${day}T00:00:00Z`) - Date.parse(`${today}T00:00:00Z`)) / 86_400_000)
        const relative = Math.abs(offset) <= 1 ? `${formatDayLabel(day, offset, t)} · ` : ''
        return (
          <section key={day} className="league" ref={index === opening ? openingRef : undefined}>
            <header className="league__header">
              <h2>{relative}{formatLongDate(day, t)}</h2>
            </header>
            <ul className="league__matches">
              {matches.map(match => (
                <MatchRow
                  key={match.id}
                  match={match}
                  justScored={false}
                  showOdds={false}
                  onOpen={() => onNavigate({ view: 'match', leagueCode, matchId: match.id })}
                />
              ))}
            </ul>
          </section>
        )
      })}
      <div ref={roomRef} aria-hidden />
    </>
  )
}
