import { useState } from 'react'
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

/**
 * @param showName False where the page around it already names the team.
 * @param showSeason Adds each player's appearances, goals and assists; needs the room of a full-width list.
 */
export function TeamSquad({ leagueCode, team, showName = true, showSeason = false }: {
  leagueCode: string
  team: Team
  showName?: boolean
  showSeason?: boolean
}) {
  const { t } = useI18n()
  const squad = useFetch(`squad/${leagueCode}/${team.id}`, () => getSquad(leagueCode, team.id))
  const allPlayers = squad.data?.players
  // Without numbers for anyone the columns would be three rows of dashes.
  const withSeason = showSeason && allPlayers?.some(p => p.season !== null) === true

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
            <div className="squad__head">
              <h4 className="detail__heading">{position ? t.squad[position] : t.squad.others}</h4>
              {withSeason && (
                <span className="squad__season squad__season--head">
                  <abbr title={t.squad.appearancesTitle}>{t.squad.appearances}</abbr>
                  <abbr title={t.squad.goalsTitle}>{t.squad.goals}</abbr>
                  <abbr title={t.squad.assistsTitle}>{t.squad.assists}</abbr>
                </span>
              )}
            </div>
            <ul className="squad__players">
              {players.map(player => <SquadRow key={player.id} player={player} leagueCode={leagueCode} showSeason={withSeason} />)}
            </ul>
          </div>
        )
      })}
      {withSeason && <p className="squad__note">{t.squad.seasonNote}</p>}
    </div>
  )
}

function SquadRow({ player, leagueCode, showSeason }: { player: SquadPlayer; leagueCode: string; showSeason: boolean }) {
  const { t } = useI18n()
  const season = player.season

  return (
    <li className="squad__player">
      <span className="squad__number">{player.jersey ?? '–'}</span>
      <span className="squad__name">
        <PlayerName leagueCode={leagueCode} playerId={player.id} name={player.name} />
        {player.nationality && (
          <span className="squad__nationality">
            {player.flagUrl && <Flag url={player.flagUrl} />}
            {player.nationality}
          </span>
        )}
      </span>
      {player.age !== null && <span className="squad__age">{t.squad.age(player.age)}</span>}
      {showSeason && (
        <span className="squad__season">
          <span title={t.squad.appearancesTitle}>{season?.appearances ?? '–'}</span>
          <span title={t.squad.goalsTitle}>{season?.goals ?? '–'}</span>
          <span title={t.squad.assistsTitle}>{season?.assists ?? '–'}</span>
        </span>
      )}
    </li>
  )
}

/** A country's flag; nothing at all if the image doesn't load. */
function Flag({ url }: { url: string }) {
  const [broken, setBroken] = useState(false)
  return broken ? null : <img className="squad__flag" src={url} alt="" loading="lazy" onError={() => setBroken(true)} />
}
