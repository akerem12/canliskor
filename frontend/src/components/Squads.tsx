import { useEffect, useState } from 'react'
import { getSquad } from '../api/http'
import type { Match, PlayerPosition, Squad, SquadPlayer, Team } from '../api/types'

const groups: { position: PlayerPosition | null; label: string }[] = [
  { position: 'Goalkeeper', label: 'Goalkeepers' },
  { position: 'Defender', label: 'Defenders' },
  { position: 'Midfielder', label: 'Midfielders' },
  { position: 'Forward', label: 'Forwards' },
  { position: null, label: 'Others' },
]

/** Both teams' full squads for the season, side by side, grouped by position. */
export function Squads({ match }: { match: Match }) {
  return (
    <section className="squads" aria-label="Squads">
      <TeamSquad leagueCode={match.leagueCode} team={match.homeTeam} />
      <TeamSquad leagueCode={match.leagueCode} team={match.awayTeam} />
    </section>
  )
}

function TeamSquad({ leagueCode, team }: { leagueCode: string; team: Team }) {
  const [squad, setSquad] = useState<Squad | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let cancelled = false
    getSquad(leagueCode, team.id).then(
      s => {
        if (!cancelled) setSquad(s)
      },
      (e: unknown) => {
        if (!cancelled) setError(e instanceof Error ? e.message : String(e))
      },
    )
    return () => {
      cancelled = true
    }
  }, [leagueCode, team.id])

  return (
    <div className="squad">
      <h3 className="squad__team">{team.name}</h3>
      {!squad && !error && <p className="detail__empty">Loading squad…</p>}
      {error && <p className="detail__empty">No squad available for this team.</p>}
      {squad && squad.players.length === 0 && <p className="detail__empty">No players listed.</p>}
      {squad && groups.map(({ position, label }) => {
        const players = squad.players.filter(p => p.position === position)
        return players.length > 0 && (
          <div key={label}>
            <h4 className="detail__heading">{label}</h4>
            <ul className="squad__players">
              {players.map(player => <SquadRow key={player.id} player={player} />)}
            </ul>
          </div>
        )
      })}
    </div>
  )
}

function SquadRow({ player }: { player: SquadPlayer }) {
  return (
    <li className="squad__player">
      <span className="squad__number">{player.jersey ?? '–'}</span>
      <span className="squad__name">
        {player.name}
        {player.nationality && <span className="squad__nationality">{player.nationality}</span>}
      </span>
      {player.age !== null && <span className="squad__age">{player.age} yrs</span>}
    </li>
  )
}
