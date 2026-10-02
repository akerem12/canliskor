import { HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr'
import { useEffect, useReducer, useState } from 'react'
import { getLeagues, getLive, getToday } from '../api/http'
import type { MatchUpdatedMessage } from '../api/types'
import { istanbulToday } from '../time'
import { initialState, mergeLeagues, scoresReducer } from './matchState'

export type ConnectionStatus = 'connecting' | 'live' | 'reconnecting' | 'offline'

const HubUrl = '/hubs/live-scores'
const RestartDelayMs = 5_000
const GoalHighlightMs = 10_000
const DayCheckIntervalMs = 60_000

/**
 * Today's matches, kept current over SignalR.
 * Flow per (re)connect: subscribe to every league first, then load over REST — so no update can be missed
 * in between (updates that arrive during the load are buffered by the reducer).
 */
export function useLiveScores() {
  const [state, dispatch] = useReducer(scoresReducer, initialState)
  const [status, setStatus] = useState<ConnectionStatus>('connecting')
  const [date, setDate] = useState(istanbulToday)
  const [recentGoals, setRecentGoals] = useState<ReadonlySet<string>>(new Set())
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let disposed = false
    let restartTimer: ReturnType<typeof setTimeout> | undefined
    let loadedDate: string | undefined
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
    async function sync() {
      dispatch({ type: 'loading' })
      const leagues = await getLeagues()
      await Promise.all(leagues.map(code => connection.invoke('SubscribeToLeague', code)))
      const [today, live] = await Promise.all([getToday(), getLive()])
      if (disposed) return
      dispatch({ type: 'loaded', leagues: mergeLeagues(today.leagues, live, leagues) })
      loadedDate = today.date
      setDate(today.date)
      setError(null)
    }

    async function start() {
      setStatus('connecting')
      try {
        await connection.start()
        await sync()
        if (!disposed) setStatus('live')
      } catch (e) {
        if (disposed) return
        // withAutomaticReconnect only covers drops after a successful start; retry the first start ourselves.
        setStatus('offline')
        setError(e instanceof Error ? e.message : String(e))
        await connection.stop()
        restartTimer = setTimeout(start, RestartDelayMs)
      }
    }

    connection.onreconnecting(() => setStatus('reconnecting'))
    connection.onreconnected(async () => {
      try {
        await sync()
        setStatus('live')
      } catch (e) {
        // Without a successful sync we'd show stale data and buffer updates forever: start over instead.
        setError(e instanceof Error ? e.message : String(e))
        await connection.stop()
      }
    })
    connection.onclose(() => {
      // Automatic reconnect gave up: start over.
      if (disposed) return
      setStatus('offline')
      restartTimer = setTimeout(start, RestartDelayMs)
    })

    // At Istanbul midnight "today" changes; reload so the new day's fixtures show.
    const dayTimer = setInterval(() => {
      if (connection.state === HubConnectionState.Connected && loadedDate !== undefined && istanbulToday() !== loadedDate) {
        sync().catch(e => setError(e instanceof Error ? e.message : String(e)))
      }
    }, DayCheckIntervalMs)

    void start()

    return () => {
      disposed = true
      clearTimeout(restartTimer)
      clearInterval(dayTimer)
      goalTimers.forEach(clearTimeout)
      void connection.stop()
    }
  }, [])

  return { leagues: state.leagues, loaded: state.loaded, status, date, recentGoals, error }
}
