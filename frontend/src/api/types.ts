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
  /** The stadium, or null if unknown. */
  venue: string | null
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

export interface Competition {
  code: string
  name: string
}

export interface Standings {
  leagueCode: string
  leagueName: string
  /** One for a plain league, several for group stages, none if the competition has no table. */
  groups: StandingsGroup[]
  lastUpdatedUtc: string
}

export interface StandingsGroup {
  name: string
  rows: StandingsRow[]
}

export interface StandingsRow {
  rank: number
  team: Team
  played: number
  wins: number
  draws: number
  losses: number
  goalsFor: number
  goalsAgainst: number
  goalDifference: number
  points: number
  /** What the position means, e.g. "Champions League". */
  note: string | null
  /** "#rrggbb" of that zone. */
  noteColor: string | null
}

export interface TeamProfile {
  leagueCode: string
  team: Team
  isNationalTeam: boolean
  /** e.g. "3rd in Turkish Super Lig"; null if the competition has no table. */
  standingSummary: string | null
  /** Null if unknown, and for national teams. */
  stadium: string | null
  stadiumCity: string | null
  /** This season's played matches in all competitions, newest first. */
  recentMatches: Match[]
  /** Every scheduled match in all competitions, soonest first. */
  upcomingMatches: Match[]
  /** Names of the competitions those matches belong to; not all of them are followed. */
  competitions: Competition[]
  lastUpdatedUtc: string
}

/** A team found by name, with the league it was found in (its page opens under that one). */
export interface TeamSearchResult {
  league: Competition
  team: Team
}

export interface LeagueFixtures {
  leagueCode: string
  leagueName: string
  /** Matches still to be played this month and next, soonest first. */
  matches: Match[]
  lastUpdatedUtc: string
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
