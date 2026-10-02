import { HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr'
import { useEffect, useReducer, useRef, useState } from 'react'
import { getDay, getLeagues, getLive } from '../api/http'
import type { MatchUpdatedMessage } from '../api/types'
import { addDays, istanbulToday } from '../time'
import { initialState, mergeLeagues, scoresReducer } from './matchState'

export type ConnectionStatus = 'connecting' | 'live' | 'reconnecting' | 'offline'

const HubUrl = '/hubs/live-scores'
const RestartDelayMs = 5_000
const GoalHighlightMs = 10_000
const DayCheckIntervalMs = 60_000

const errorMessage = (e: unknown) => (e instanceof Error ? e.message : String(e))

/**
 * One day's matches, kept current over SignalR.
 * @param dayOffset Days from today (Istanbul): 0 = today, -1 = yesterday. Relative on purpose, so "today" moves on at midnight.
 *
 * Flow per (re)connect: subscribe to every league first, then load over REST — so no update can be missed
 * in between (updates that arrive during a load are buffered by the reducer). Changing the day only reloads.
 */
export function useLiveScores(dayOffset: number) {
  const [state, dispatch] = useReducer(scoresReducer, initialState)
  const [status, setStatus] = useState<ConnectionStatus>('connecting')
  const [date, setDate] = useState(() => addDays(istanbulToday(), dayOffset))
  const [recentGoals, setRecentGoals] = useState<ReadonlySet<string>>(new Set())
  const [error, setError] = useState<string | null>(null)

  // The connection lives for the whole page, so its callbacks read the current day through refs.
  const dayOffsetRef = useRef(dayOffset)
  const loadRef = useRef<(() => Promise<void>) | null>(null)

  useEffect(() => {
    let disposed = false
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

    connection.on('MatchUpdated', (message: MatchUpdatedMessage) => {
      dispatch({ type: 'update', message })
      if (message.scoreChanged) {
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
    }

    async function load() {
      // Clicking through days quickly: only the latest request may update the screen.
      const seq = ++loadSeq
      const today = istanbulToday()
      const offset = dayOffsetRef.current
      const day = addDays(today, offset)
      setDate(day)
      dispatch({ type: 'loading' })

      // Today also includes yesterday's games still running past midnight.
      const result = offset === 0
        ? await Promise.all([getDay(), getLive()]).then(([d, live]) => mergeLeagues(d.leagues, live, leagues))
        : (await getDay(day)).leagues

      if (disposed || seq !== loadSeq) return
      // The day was changed while connecting (no reload was triggered then): load the one now selected.
      if (offset !== dayOffsetRef.current) return load()
      loadedToday = today
      dispatch({ type: 'loaded', leagues: result })
      setError(null)
    }

    loadRef.current = load

    async function start() {
      setStatus('connecting')
      try {
        await connection.start()
        await subscribe()
        await load()
        if (!disposed) setStatus('live')
      } catch (e) {
        if (disposed) return
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
      // Automatic reconnect gave up: start over.
      if (disposed) return
      setStatus('offline')
      restartTimer = setTimeout(start, RestartDelayMs)
    })

    // At Istanbul midnight every relative day moves on (today becomes yesterday): reload.
    const dayTimer = setInterval(() => {
      if (connection.state === HubConnectionState.Connected && loadedToday !== undefined && istanbulToday() !== loadedToday) {
        load().catch(e => setError(errorMessage(e)))
      }
    }, DayCheckIntervalMs)

    // Deferred: React StrictMode (dev) mounts, unmounts and remounts at once. Starting synchronously would open
    // a connection only to stop it mid-negotiation, which SignalR reports as an error in the console.
    restartTimer = setTimeout(start, 0)

    return () => {
      disposed = true
      loadRef.current = null
      clearTimeout(restartTimer)
      clearInterval(dayTimer)
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

  return { leagues: state.leagues, loaded: state.loaded, status, date, recentGoals, error }
}
