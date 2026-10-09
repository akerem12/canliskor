import { HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr'
import type { HubConnection } from '@microsoft/signalr'
import { useCallback, useEffect, useMemo, useReducer, useRef, useState } from 'react'
import { apiUrl } from '../api/base'
import { getDay, getLeagues, getLive } from '../api/http'
import type { Match, MatchDetail, MatchUpdatedMessage } from '../api/types'
import { localizeNames } from '../i18n/names'
import { useI18n } from '../i18n/useI18n'
import { onAppActiveChange } from '../native/app'
import { addDays, istanbulToday } from '../time'
import { goalsIn, initialState, mergeLeagues, scoresReducer } from './matchState'

export type ConnectionStatus = 'connecting' | 'live' | 'reconnecting' | 'offline'

const HubUrl = apiUrl('/hubs/live-scores')
const RestartDelayMs = 5_000
const GoalHighlightMs = 10_000
const DayCheckIntervalMs = 60_000
/** While the live connection is down, the scores are fetched this often instead. */
const FallbackPollMs = 15_000

const errorMessage = (e: unknown) => (e instanceof Error ? e.message : String(e))

/**
 * Tells the server a match page is open, so it pushes that match's detail while it is in play.
 * @returns A function that stops watching.
 */
export type WatchMatch = (leagueCode: string, matchId: string, onDetail: (detail: MatchDetail) => void) => () => void

/** Called for every pushed update, with the match as it was known before it (if it was). */
export type UpdateListener = (message: MatchUpdatedMessage, previous: Match | undefined) => void

interface WatchedMatch {
  leagueCode: string
  matchId: string
  onDetail: (detail: MatchDetail) => void
}

/**
 * One day's matches, kept current over SignalR.
 * @param dayOffset Days from today (Istanbul): 0 = today, -1 = yesterday. Relative on purpose, so "today" moves on at midnight.
 *
 * Flow per (re)connect: subscribe to every league first, then load over REST — so no update can be missed
 * in between (updates that arrive during a load are buffered by the reducer). Changing the day only reloads.
 * The same connection also carries the detail of the one match whose page is open (see {@link WatchMatch}).
 * @param onUpdate Told about every pushed update, whichever day is shown (match alerts hang off this).
 */
export function useLiveScores(dayOffset: number, onUpdate?: UpdateListener) {
  const [state, dispatch] = useReducer(scoresReducer, initialState)
  const [status, setStatus] = useState<ConnectionStatus>('connecting')
  const [date, setDate] = useState(() => addDays(istanbulToday(), dayOffset))
  const [recentGoals, setRecentGoals] = useState<ReadonlySet<string>>(new Set())
  const [error, setError] = useState<string | null>(null)

  // The connection lives for the whole page, so its callbacks read the current day through refs.
  const dayOffsetRef = useRef(dayOffset)
  const loadRef = useRef<(() => Promise<void>) | null>(null)
  const retryRef = useRef<(() => void) | null>(null)
  const connectionRef = useRef<HubConnection | null>(null)
  const watchedRef = useRef<WatchedMatch | null>(null)

  // Every match seen so far, so a listener can compare an update with what was known before it.
  const knownRef = useRef(new Map<string, Match>())
  const onUpdateRef = useRef(onUpdate)
  const { language } = useI18n()
  const languageRef = useRef(language)
  useEffect(() => {
    languageRef.current = language
    onUpdateRef.current = onUpdate
    for (const match of state.leagues.flatMap(l => l.matches)) {
      knownRef.current.set(match.id, match)
    }
  })

  useEffect(() => {
    let disposed = false
    // The Android app in the background: no connection and no requests until it is opened again.
    let paused = false
    let stopping: Promise<void> = Promise.resolve()
    // Counts attempts to connect, so one that was abandoned (pause, retry) can tell and leaves things alone.
    let attempt = 0
    let restartTimer: ReturnType<typeof setTimeout> | undefined
    let leagues: string[] = []
    let loadedToday: string | undefined
    let loadSeq = 0
    const goalTimers = new Set<ReturnType<typeof setTimeout>>()

    const connection = new HubConnectionBuilder()
      .withUrl(HubUrl)
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build()
    connectionRef.current = connection

    connection.on('MatchDetailUpdated', (detail: MatchDetail) => {
      const watched = watchedRef.current
      if (watched && watched.leagueCode === detail.match.leagueCode && watched.matchId === detail.match.id) {
        watched.onDetail(detail)
      }
    })

    connection.on('MatchUpdated', (message: MatchUpdatedMessage) => {
      const previous = knownRef.current.get(message.match.id)
      knownRef.current.set(message.match.id, message.match)
      // Alerts name the teams, so they get the update in the site's language.
      onUpdateRef.current?.(localizeNames(message, languageRef.current), localizeNames(previous, languageRef.current))

      dispatch({ type: 'update', message })
      if (message.scoreChanged && goalsIn(message.match) > goalsIn(previous)) {
        highlightGoal(message.match.id)
      }
    })

    function highlightGoal(matchId: string) {
      setRecentGoals(previous => new Set(previous).add(matchId))
      const timer = setTimeout(() => {
        goalTimers.delete(timer)
        setRecentGoals(previous => {
          const next = new Set(previous)
          next.delete(matchId)
          return next
        })
      }, GoalHighlightMs)
      goalTimers.add(timer)
    }

    // Groups belong to a connection, so this runs again after every reconnect.
    async function subscribe() {
      leagues = await getLeagues()
      await Promise.all(leagues.map(code => connection.invoke('SubscribeToLeague', code)))

      // A match page opened before we were connected, or still open after a reconnect. A link to a match the
      // server rejects must not take the scores down with it, so failures are ignored.
      const watched = watchedRef.current
      if (watched) {
        await connection.invoke('SubscribeToMatch', watched.leagueCode, watched.matchId).catch(() => {})
      }
    }

    /** @param quiet A refresh behind the scenes: what is on screen stays as it is until the new data is in. */
    async function load(quiet = false) {
      // Clicking through days quickly: only the latest request may update the screen.
      const seq = ++loadSeq
      const today = istanbulToday()
      const offset = dayOffsetRef.current
      const day = addDays(today, offset)
      setDate(day)
      if (!quiet) dispatch({ type: 'loading' })

      // Today also includes yesterday's games still running past midnight.
      const result = offset === 0
        ? await Promise.all([getDay(), getLive()]).then(([d, live]) => mergeLeagues(d.leagues, live, leagues))
        : (await getDay(day)).leagues

      if (disposed || seq !== loadSeq) return
      // The day was changed while connecting (no reload was triggered then): load the one now selected.
      if (offset !== dayOffsetRef.current) return load(quiet)
      loadedToday = today
      dispatch({ type: 'loaded', leagues: result })
      setError(null)
    }

    loadRef.current = load

    async function start() {
      const mine = ++attempt
      setStatus('connecting')
      try {
        // A stop still under way (pause, retry) must finish first, or start() is refused.
        await stopping
        await connection.start()
        await subscribe()
        await load()
        if (!disposed && mine === attempt) setStatus('live')
      } catch (e) {
        if (disposed || paused || mine !== attempt) return
        // withAutomaticReconnect only covers drops after a successful start; retry the first start ourselves.
        setStatus('offline')
        setError(errorMessage(e))
        await connection.stop()
        restartTimer = setTimeout(start, RestartDelayMs)
      }
    }

    connection.onreconnecting(() => setStatus('reconnecting'))
    connection.onreconnected(async () => {
      try {
        await subscribe()
        await load()
        setStatus('live')
      } catch (e) {
        // Without a successful sync we'd show stale data and buffer updates forever: start over instead.
        setError(errorMessage(e))
        await connection.stop()
      }
    })
    connection.onclose(() => {
      // Automatic reconnect gave up: start over. Stopped on purpose (pause, retry): whoever stopped it restarts it.
      if (disposed || paused || restarting) return
      setStatus('offline')
      restartTimer = setTimeout(start, RestartDelayMs)
    })

    // At Istanbul midnight every relative day moves on (today becomes yesterday): reload.
    const dayTimer = setInterval(() => {
      if (connection.state === HubConnectionState.Connected && loadedToday !== undefined && istanbulToday() !== loadedToday) {
        load().catch(e => setError(errorMessage(e)))
      }
    }, DayCheckIntervalMs)

    // No push without a connection: until it is back, ask for the scores every few seconds instead.
    const fallbackTimer = setInterval(async () => {
      if (disposed || paused || connection.state === HubConnectionState.Connected) return
      try {
        if (leagues.length === 0) leagues = await getLeagues()
        await load(true)
      } catch {
        // The server can't be reached at all; the next round tries again.
      }
    }, FallbackPollMs)

    /** Drops whatever the connection is doing and connects anew, now. */
    let restarting = false
    function restart() {
      clearTimeout(restartTimer)
      attempt++
      restarting = true
      stopping = connection.stop().finally(() => {
        restarting = false
      })
      restartTimer = setTimeout(start, 0)
    }
    retryRef.current = restart

    const stopListening = onAppActiveChange(active => {
      if (disposed || active !== paused) return
      paused = !active
      if (active) {
        restart()
      } else {
        clearTimeout(restartTimer)
        attempt++
        stopping = connection.stop()
      }
    })

    // Deferred: React StrictMode (dev) mounts, unmounts and remounts at once. Starting synchronously would open
    // a connection only to stop it mid-negotiation, which SignalR reports as an error in the console.
    restartTimer = setTimeout(start, 0)

    return () => {
      disposed = true
      loadRef.current = null
      retryRef.current = null
      stopListening()
      connectionRef.current = null
      clearTimeout(restartTimer)
      clearInterval(dayTimer)
      clearInterval(fallbackTimer)
      goalTimers.forEach(clearTimeout)
      void connection.stop()
    }
  }, [])

  useEffect(() => {
    if (dayOffsetRef.current === dayOffset) return
    dayOffsetRef.current = dayOffset
    // Not connected yet: start() loads the current day once it is.
    if (status === 'live') {
      loadRef.current?.().catch(e => setError(errorMessage(e)))
    }
  }, [dayOffset, status])

  const watchMatch = useCallback<WatchMatch>((leagueCode, matchId, onDetail) => {
    const watched = { leagueCode, matchId, onDetail }
    watchedRef.current = watched

    // Not connected yet: subscribe() does it once we are.
    const invoke = (method: string) => {
      const connection = connectionRef.current
      if (connection?.state === HubConnectionState.Connected) {
        connection.invoke(method, leagueCode, matchId).catch(() => {})
      }
    }
    invoke('SubscribeToMatch')

    return () => {
      if (watchedRef.current === watched) watchedRef.current = null
      invoke('UnsubscribeFromMatch')
    }
  }, [])

  /** Stops waiting for the current attempt to connect and tries again at once. */
  const retry = useCallback(() => retryRef.current?.(), [])

  const leagues = useMemo(() => localizeNames(state.leagues, language), [state.leagues, language])

  return { leagues, loaded: state.loaded, status, date, recentGoals, error, watchMatch, retry }
}
