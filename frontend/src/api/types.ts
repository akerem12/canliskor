// Mirrors backend/src/CanliSkor.Api/Contracts/MatchResponses.cs.

export type MatchStatus = 'Scheduled' | 'Live' | 'HalfTime' | 'Finished' | 'Postponed' | 'Cancelled'

export interface Team {
  id: string
  name: string
  shortName: string
  logoUrl: string | null
}

export interface Score {
  home: number
  away: number
}

export interface Match {
  id: string
  leagueCode: string
  /** Istanbul time with offset, e.g. "2026-10-09T20:00:00+03:00". */
  kickoff: string
  status: MatchStatus
  /** e.g. "67'" or "90'+4'"; null before kickoff. */
  clock: string | null
  homeTeam: Team
  awayTeam: Team
  score: Score | null
}

export interface LeagueMatches {
  code: string
  name: string
  lastUpdatedUtc: string
  matches: Match[]
}

export interface MatchDay {
  date: string
  leagues: LeagueMatches[]
}

export type MatchEventType = 'Goal' | 'PenaltyGoal' | 'OwnGoal' | 'YellowCard' | 'RedCard' | 'Substitution'

export interface MatchEvent {
  type: MatchEventType
  /** e.g. "57'" or "45'+2'". */
  clock: string
  /** The team the event counts for; an own goal counts for the team that benefits. */
  side: 'Home' | 'Away'
  /** Scorer, booked player, or the player coming on. */
  player: string | null
  /** Assist provider, or the player going off. */
  relatedPlayer: string | null
}

export type MatchStatType =
  | 'Possession' | 'Shots' | 'ShotsOnTarget' | 'Corners' | 'Fouls' | 'Offsides' | 'YellowCards' | 'RedCards' | 'Saves'

export interface MatchStat {
  type: MatchStatType
  /** Possession is a percentage; everything else a count. */
  home: number
  away: number
}

export interface MatchDetail {
  match: Match
  events: MatchEvent[]
  /** Empty before kickoff. */
  stats: MatchStat[]
  lastUpdatedUtc: string
}

/** SignalR "MatchUpdated" payload. Both flags false means only the clock moved. */
export interface MatchUpdatedMessage {
  match: Match
  scoreChanged: boolean
  statusChanged: boolean
}

export const isInPlay = (status: MatchStatus) => status === 'Live' || status === 'HalfTime'
