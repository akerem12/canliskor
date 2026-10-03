import { useEffect, useState } from 'react'
import { getMatchDetail } from '../api/http'
import type { Match, MatchDetail } from '../api/types'
import { isInPlay } from '../api/types'

/** Cards and substitutions aren't pushed, so a live match is also refreshed on a timer (the server's poll interval). */
const LiveRefreshMs = 30_000

/**
 * One match's events and statistics. Reloads at once when the pushed match changes score or status (the server
 * then fetches a fresh detail, so the new goal comes with its scorer), and every 30 s while the match is in play.
 * @param pushed The match as kept current over SignalR, if it is on the day being shown.
 */
export function useMatchDetail(leagueCode: string, matchId: string, pushed: Match | undefined) {
  const [detail, setDetail] = useState<MatchDetail | null>(null)
  const [error, setError] = useState<string | null>(null)

  const status = pushed?.status ?? detail?.match.status
  const inPlay = status !== undefined && isInPlay(status)
  const changeKey = pushed ? `${pushed.status}|${pushed.score?.home}-${pushed.score?.away}` : ''

  useEffect(() => {
    let cancelled = false
    const load = () =>
      getMatchDetail(leagueCode, matchId).then(
        d => {
          if (cancelled) return
          setDetail(d)
          setError(null)
        },
        (e: unknown) => {
          if (!cancelled) setError(e instanceof Error ? e.message : String(e))
        },
      )

    void load()
    const timer = inPlay ? setInterval(load, LiveRefreshMs) : undefined
    return () => {
      cancelled = true
      clearInterval(timer)
    }
  }, [leagueCode, matchId, changeKey, inPlay])

  return { detail, error }
}
