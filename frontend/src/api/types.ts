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
  /** Null until both line-ups are announced. */
  lineups: MatchLineups | null
  lastUpdatedUtc: string
}

export interface MatchLineups {
  home: TeamLineup
  away: TeamLineup
}

export interface TeamLineup {
  /** e.g. "4-2-3-1". */
  formation: string
  /** "#rrggbb", or null if unknown. */
  shirtColor: string | null
  /** Starting eleven: the goalkeeper's row first, then defence to attack; each row from the team's own left to right. */
  rows: LineupPlayer[][]
  /** Substitutes, whether they came on or not. */
  bench: LineupPlayer[]
}

export type PlayerPosition = 'Goalkeeper' | 'Defender' | 'Midfielder' | 'Forward'

export interface LineupPlayer {
  id: string
  name: string
  /** e.g. "G. Orban". */
  shortName: string
  jersey: string | null
  /** A substitute who came on has the position of the player they replaced; null if they stayed on the bench. */
  position: PlayerPosition | null
  /** Match minute, e.g. "76'"; null for starters and unused substitutes. */
  cameOnAt: string | null
  wentOffAt: string | null
  sentOffAt: string | null
  minutesPlayed: number | null
  /** Our own estimate from the match statistics, 3.0 to 10.0; null if played too briefly to be rated. */
  rating: number | null
  stats: PlayerStats
}

export interface PlayerStats {
  goals: number
  assists: number
  shots: number
  shotsOnTarget: number
  foulsCommitted: number
  foulsSuffered: number
  offsides: number
  yellowCards: number
  redCards: number
  ownGoals: number
  saves: number
  /** Goals the team conceded while the player was on the pitch. */
  goalsConceded: number
}

export interface Squad {
  teamId: string
  teamName: string
  /** Goalkeepers first, then defenders, midfielders and forwards; by shirt number within each. */
  players: SquadPlayer[]
  lastUpdatedUtc: string
}

export interface SquadPlayer {
  id: string
  name: string
  jersey: string | null
  position: PlayerPosition | null
  age: number | null
  /** Country name, e.g. "Türkiye". */
  nationality: string | null
}

/** SignalR "MatchUpdated" payload. Both flags false means only the clock moved. */
export interface MatchUpdatedMessage {
  match: Match
  scoreChanged: boolean
  statusChanged: boolean
}

export const isInPlay = (status: MatchStatus) => status === 'Live' || status === 'HalfTime'
