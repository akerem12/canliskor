import { getSquad } from '../api/http'
import type { Match, PlayerPosition, SquadPlayer, Team } from '../api/types'
import { useFetch } from '../api/useFetch'
import { PlayerName } from '../players/PlayerName'
import { EmptyState, Skeleton } from './common'

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

/** @param showName False where the page around it already names the team. */
export function TeamSquad({ leagueCode, team, showName = true }: { leagueCode: string; team: Team; showName?: boolean }) {
  const squad = useFetch(`squad/${leagueCode}/${team.id}`, () => getSquad(leagueCode, team.id))
  const allPlayers = squad.data?.players

  return (
    <div className="squad">
      {showName && <h3 className="squad__team">{team.name}</h3>}
      {squad.loading && <Skeleton rows={8} />}
      {squad.error && <EmptyState icon="👥" title="No squad available for this team." onRetry={squad.retry} />}
      {allPlayers?.length === 0 && <EmptyState icon="👥" title="No players listed." />}
      {allPlayers && groups.map(({ position, label }) => {
        const players = allPlayers.filter(p => p.position === position)
        return players.length > 0 && (
          <div key={label}>
            <h4 className="detail__heading">{label}</h4>
            <ul className="squad__players">
              {players.map(player => <SquadRow key={player.id} player={player} leagueCode={leagueCode} />)}
            </ul>
          </div>
        )
      })}
    </div>
  )
}

function SquadRow({ player, leagueCode }: { player: SquadPlayer; leagueCode: string }) {
  return (
    <li className="squad__player">
      <span className="squad__number">{player.jersey ?? '–'}</span>
      <span className="squad__name">
        <PlayerName leagueCode={leagueCode} playerId={player.id} name={player.name} />
        {player.nationality && <span className="squad__nationality">{player.nationality}</span>}
      </span>
      {player.age !== null && <span className="squad__age">{player.age} yrs</span>}
    </li>
  )
}
