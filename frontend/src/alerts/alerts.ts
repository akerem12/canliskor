// Match alerts: which updates deserve a notification, what it says, and what the visitor has switched on.
// Pure and free of the browser's Notification API and storage, so it can be unit-tested; useAlertsController adds both.

import type { Match, MatchUpdatedMessage } from '../api/types'

/** What a notification shows. */
export interface Alert {
  title: string
  body: string
  /** Notifications with the same tag replace each other, so one event never shows twice. */
  tag: string
}

/** A single match the visitor asked to be told about, whoever plays in it. */
export interface WatchedMatch {
  id: string
  leagueCode: string
  /** Kept so finished matches can be forgotten. */
  kickoff: string
}

export interface AlertSettings {
  /** Alerts for every match of a favourite team. Favourite leagues never alert: that would be every goal of a league. */
  teamAlerts: boolean
  matches: WatchedMatch[]
}

export const noAlerts: AlertSettings = { teamAlerts: false, matches: [] }

export const AlertsStorageKey = 'canliskor.alerts.v1'

/** A watched match is forgotten this long after kickoff: by then it is over, extra time included. */
const ForgetAfterMs = 24 * 60 * 60 * 1000

const isString = (value: unknown): value is string => typeof value === 'string' && value.length > 0

/** Reads what was stored; anything missing or damaged is dropped, and matches long over are forgotten. */
export function parseAlertSettings(stored: string | null, now: number): AlertSettings {
  if (!stored) return noAlerts

  let raw: unknown
  try {
    raw = JSON.parse(stored)
  } catch {
    return noAlerts
  }
  if (typeof raw !== 'object' || raw === null) return noAlerts

  const { teamAlerts, matches } = raw as { teamAlerts?: unknown; matches?: unknown }
  return {
    teamAlerts: teamAlerts === true,
    matches: (Array.isArray(matches) ? matches : [])
      .filter((m): m is WatchedMatch => isString(m?.id) && isString(m?.leagueCode) && isString(m?.kickoff))
      .map(m => ({ id: m.id, leagueCode: m.leagueCode, kickoff: m.kickoff }))
      .filter(m => now - Date.parse(m.kickoff) < ForgetAfterMs),
  }
}

export const isMatchWatched = (settings: AlertSettings, matchId: string) => settings.matches.some(m => m.id === matchId)

/** Starts watching the match, or stops if it is already watched. */
export function toggleMatch(settings: AlertSettings, match: Match): AlertSettings {
  return {
    ...settings,
    matches: isMatchWatched(settings, match.id)
      ? settings.matches.filter(m => m.id !== match.id)
      : [...settings.matches, { id: match.id, leagueCode: match.leagueCode, kickoff: match.kickoff }],
  }
}

/** True if an update of this match should notify: it is watched, or team alerts are on and a favourite team plays. */
export function wantsAlert(match: Match, settings: AlertSettings, favoriteTeamIds: ReadonlySet<string>): boolean {
  return isMatchWatched(settings, match.id)
    || (settings.teamAlerts && (favoriteTeamIds.has(match.homeTeam.id) || favoriteTeamIds.has(match.awayTeam.id)))
}

const minuteOf = (clock: string | null) => {
  const minute = parseInt(clock ?? '', 10)
  return Number.isNaN(minute) ? null : minute
}

/**
 * Turns a pushed update into the alert to show: a goal, kick-off, half time, the second half, full time, or a
 * match called off. Null for updates that are no news (the clock moving).
 * @param previous The match as it was before the update, if known. Without it a "now live" update can't be told
 *   apart from the second half starting, so it only counts as kick-off in the first minutes.
 */
export function describeUpdate(previous: Match | undefined, message: MatchUpdatedMessage): Alert | null {
  const { match } = message
  const teams = `${match.homeTeam.shortName} v ${match.awayTeam.shortName}`
  const score = match.score ? `${match.homeTeam.shortName} ${match.score.home} - ${match.score.away} ${match.awayTeam.shortName}` : teams
  const minute = match.clock ? `${match.clock} · ` : ''

  if (message.scoreChanged && match.score) {
    const before = previous?.score
    const goalsBefore = before ? before.home + before.away : null
    const goalsNow = match.score.home + match.score.away
    // A goal taken back (VAR) lowers the total.
    if (goalsBefore !== null && goalsNow < goalsBefore) {
      return { title: 'Goal ruled out', body: score, tag: `${match.id}:score:${goalsNow}:out` }
    }

    const scorer = before && match.score.home > before.home ? match.homeTeam.shortName
      : before && match.score.away > before.away ? match.awayTeam.shortName
      : null
    return { title: scorer ? `⚽ Goal for ${scorer}!` : '⚽ Goal!', body: `${minute}${score}`, tag: `${match.id}:score:${goalsNow}` }
  }

  if (!message.statusChanged) return null

  switch (match.status) {
    case 'Live': {
      const secondHalf = previous ? previous.status === 'HalfTime' : (minuteOf(match.clock) ?? 0) >= 45
      if (secondHalf) return { title: 'Second half under way', body: score, tag: `${match.id}:second-half` }
      // Without the previous state, a match well into the first half is no longer "just kicked off".
      if (!previous && (minuteOf(match.clock) ?? 0) > 5) return null
      return { title: 'Kick-off', body: teams, tag: `${match.id}:kickoff` }
    }
    case 'HalfTime':
      return { title: 'Half time', body: score, tag: `${match.id}:half-time` }
    case 'Finished':
      return { title: 'Full time', body: score, tag: `${match.id}:full-time` }
    case 'Postponed':
      return { title: 'Match postponed', body: teams, tag: `${match.id}:postponed` }
    case 'Cancelled':
      return { title: 'Match cancelled', body: teams, tag: `${match.id}:cancelled` }
    default:
      return null
  }
}
