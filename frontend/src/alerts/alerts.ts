// Match alerts: which updates deserve a notification, what it says, and what the visitor has switched on.
// Pure and free of the browser's Notification API and storage, so it can be unit-tested; useAlertsController adds both.

import type { Match, MatchUpdatedMessage } from '../api/types'
import type { Dictionary } from '../i18n/en'
import { en } from '../i18n/en'

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
  /** A reminder half an hour before kick-off, for the same matches. Sent by the server, so it arrives with the site closed. */
  kickoffReminder: boolean
  /** A notification when the starting elevens are announced. Sent by the server too. */
  lineupAlerts: boolean
}

export const noAlerts: AlertSettings = { teamAlerts: false, matches: [], kickoffReminder: true, lineupAlerts: true }

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

  const { teamAlerts, matches, kickoffReminder, lineupAlerts } = raw as Record<string, unknown>
  return {
    teamAlerts: teamAlerts === true,
    // On unless switched off: settings stored before these two existed have neither.
    kickoffReminder: kickoffReminder !== false,
    lineupAlerts: lineupAlerts !== false,
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

/** What the server is asked to send this browser; see push.ts. */
export interface PushWishes {
  language: string
  teamIds: string[]
  matchIds: string[]
  kickoffReminder: boolean
  lineupAlerts: boolean
}

/**
 * What to ask the server for: the reminder and the line-ups alert, for the favourite teams (if team alerts are on)
 * and the watched matches. Null if that comes to nothing, so the browser needn't be subscribed at all.
 */
export function pushWishes(settings: AlertSettings, favoriteTeamIds: ReadonlySet<string>, language: string): PushWishes | null {
  const teamIds = settings.teamAlerts ? [...favoriteTeamIds].sort() : []
  const matchIds = settings.matches.map(m => m.id)
  if (teamIds.length + matchIds.length === 0 || !(settings.kickoffReminder || settings.lineupAlerts)) return null

  return { language, teamIds, matchIds, kickoffReminder: settings.kickoffReminder, lineupAlerts: settings.lineupAlerts }
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
 * @param t The words of the language to say it in.
 */
export function describeUpdate(previous: Match | undefined, message: MatchUpdatedMessage, t: Dictionary = en): Alert | null {
  const { match } = message
  const teams = t.match.versus(match.homeTeam.shortName, match.awayTeam.shortName)
  const score = match.score ? `${match.homeTeam.shortName} ${match.score.home} - ${match.score.away} ${match.awayTeam.shortName}` : teams
  const minute = match.clock ? `${match.clock} · ` : ''

  if (message.scoreChanged && match.score) {
    const before = previous?.score
    const goalsBefore = before ? before.home + before.away : null
    const goalsNow = match.score.home + match.score.away
    // A goal taken back (VAR) lowers the total.
    if (goalsBefore !== null && goalsNow < goalsBefore) {
      return { title: t.alerts.goalRuledOut, body: score, tag: `${match.id}:score:${goalsNow}:out` }
    }

    const scorer = before && match.score.home > before.home ? match.homeTeam.shortName
      : before && match.score.away > before.away ? match.awayTeam.shortName
      : null
    return { title: scorer ? t.alerts.goalFor(scorer) : t.alerts.goal, body: `${minute}${score}`, tag: `${match.id}:score:${goalsNow}` }
  }

  if (!message.statusChanged) return null

  switch (match.status) {
    case 'Live': {
      const secondHalf = previous ? previous.status === 'HalfTime' : (minuteOf(match.clock) ?? 0) >= 45
      if (secondHalf) return { title: t.alerts.secondHalf, body: score, tag: `${match.id}:second-half` }
      // Without the previous state, a match well into the first half is no longer "just kicked off".
      if (!previous && (minuteOf(match.clock) ?? 0) > 5) return null
      return { title: t.alerts.kickOff, body: teams, tag: `${match.id}:kickoff` }
    }
    case 'HalfTime':
      return { title: t.alerts.halfTime, body: score, tag: `${match.id}:half-time` }
    case 'Finished':
      return { title: t.alerts.fullTime, body: score, tag: `${match.id}:full-time` }
    case 'Postponed':
      return { title: t.alerts.postponed, body: teams, tag: `${match.id}:postponed` }
    case 'Cancelled':
      return { title: t.alerts.cancelled, body: teams, tag: `${match.id}:cancelled` }
    default:
      return null
  }
}
