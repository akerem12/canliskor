import { useEffect, useMemo, useState } from 'react'
import { getMatchDetail } from '../api/http'
import type { Match, MatchDetail } from '../api/types'
import { localizeNames } from '../i18n/names'
import { useI18n } from '../i18n/useI18n'
import type { WatchMatch } from './useLiveScores'

/** The newer of two details of the same match: a REST response and a push can arrive in either order. */
export function newerDetail(current: MatchDetail | null, incoming: MatchDetail): MatchDetail {
  return current && Date.parse(current.lastUpdatedUtc) > Date.parse(incoming.lastUpdatedUtc) ? current : incoming
}

/**
 * One match's line-ups, events and statistics. Loaded over REST, then kept current by the server: while the match
 * is in play and this page is open, every poll pushes a fresh detail (cards and substitutions included).
 * Also reloads at once when the pushed match changes score or status, which covers kickoff and full time.
 * @param pushed The match as kept current over SignalR, if it is on the day being shown.
 */
export function useMatchDetail(leagueCode: string, matchId: string, pushed: Match | undefined, watchMatch: WatchMatch) {
  const { language } = useI18n()
  const [detail, setDetail] = useState<MatchDetail | null>(null)
  const [error, setError] = useState<string | null>(null)

  const changeKey = pushed ? `${pushed.status}|${pushed.score?.home}-${pushed.score?.away}` : ''

  useEffect(() => {
    let cancelled = false
    getMatchDetail(leagueCode, matchId).then(
      d => {
        if (cancelled) return
        setDetail(current => newerDetail(current, d))
        setError(null)
      },
      (e: unknown) => {
        if (!cancelled) setError(e instanceof Error ? e.message : String(e))
      },
    )
    return () => {
      cancelled = true
    }
  }, [leagueCode, matchId, changeKey])

  useEffect(
    () => watchMatch(leagueCode, matchId, d => setDetail(current => newerDetail(current, d))),
    [leagueCode, matchId, watchMatch],
  )

  const localized = useMemo(() => localizeNames(detail, language), [detail, language])

  return { detail: localized, error }
}
