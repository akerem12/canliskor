import { getSquad } from '../api/http'
import type { Match, PlayerPosition, SquadPlayer, Team } from '../api/types'
import { useFetch } from '../api/useFetch'
import { useI18n } from '../i18n/useI18n'
import { PlayerName } from '../players/PlayerName'
import { EmptyState, Skeleton } from './common'

/** The order the groups are listed in; null collects players without a known position. */
const positions: (PlayerPosition | null)[] = ['Goalkeeper', 'Defender', 'Midfielder', 'Forward', null]

/** Both teams' full squads for the season, side by side, grouped by position. */
export function Squads({ match }: { match: Match }) {
  const { t } = useI18n()

  return (
    <section className="squads" aria-label={t.squad.title}>
      <TeamSquad leagueCode={match.leagueCode} team={match.homeTeam} />
      <TeamSquad leagueCode={match.leagueCode} team={match.awayTeam} />
    </section>
  )
}

/** @param showName False where the page around it already names the team. */
export function TeamSquad({ leagueCode, team, showName = true }: { leagueCode: string; team: Team; showName?: boolean }) {
  const { t } = useI18n()
  const squad = useFetch(`squad/${leagueCode}/${team.id}`, () => getSquad(leagueCode, team.id))
  const allPlayers = squad.data?.players

  return (
    <div className="squad">
      {showName && <h3 className="squad__team">{team.name}</h3>}
      {squad.loading && <Skeleton rows={8} />}
      {squad.error && <EmptyState icon="👥" title={t.squad.unavailable} onRetry={squad.retry} />}
      {allPlayers?.length === 0 && <EmptyState icon="👥" title={t.squad.empty} />}
      {allPlayers && positions.map(position => {
        const players = allPlayers.filter(p => p.position === position)
        return players.length > 0 && (
          <div key={position ?? 'others'}>
            <h4 className="detail__heading">{position ? t.squad[position] : t.squad.others}</h4>
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
  const { t } = useI18n()

  return (
    <li className="squad__player">
      <span className="squad__number">{player.jersey ?? '–'}</span>
      <span className="squad__name">
        <PlayerName leagueCode={leagueCode} playerId={player.id} name={player.name} />
        {player.nationality && <span className="squad__nationality">{player.nationality}</span>}
      </span>
      {player.age !== null && <span className="squad__age">{t.squad.age(player.age)}</span>}
    </li>
  )
}
