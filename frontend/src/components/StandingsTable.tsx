import type { Standings, StandingsGroup } from '../api/types'
import { FavoriteButton } from '../favorites/FavoriteButton'
import { useI18n } from '../i18n/useI18n'
import type { Route } from '../route'
import { TeamLogo } from './common'

interface Props {
  standings: Standings
  /** Highlights this team's row; with several groups, only the group it is in is shown. */
  teamId?: string
  /** The same for several teams at once: the two sides of a match. */
  teamIds?: readonly string[]
  onNavigate: (route: Route) => void
}

/** A league table (or one per group). Rows lead to the team's page; coloured edges mark what a position means. */
export function StandingsTable({ standings, teamId, teamIds, onNavigate }: Props) {
  const highlighted = teamIds ?? (teamId ? [teamId] : [])
  const ownGroups = standings.groups.filter(g => g.rows.some(r => highlighted.includes(r.team.id)))
  const groups = ownGroups.length > 0 ? ownGroups : standings.groups
  // A plain league's single group is named after the season, which the page already says.
  const showNames = standings.groups.length > 1

  return (
    <>
      {groups.map(group => (
        <GroupTable
          key={group.name}
          group={group}
          title={showNames ? group.name : undefined}
          leagueCode={standings.leagueCode}
          teamIds={highlighted}
          onNavigate={onNavigate}
        />
      ))}
      <Legend groups={groups} />
    </>
  )
}

function GroupTable({ group, title, leagueCode, teamIds, onNavigate }: {
  group: StandingsGroup
  title: string | undefined
  leagueCode: string
  teamIds: readonly string[]
  onNavigate: (route: Route) => void
}) {
  const { t } = useI18n()

  return (
    <div className="table-wrap">
      <table className="table">
        {title && <caption>{title}</caption>}
        <thead>
          <tr>
            <th className="table__rank" scope="col">#</th>
            <th className="table__team" scope="col">{t.table.team}</th>
            <th scope="col" title={t.table.playedTitle}>{t.table.played}</th>
            <th scope="col" title={t.table.wonTitle}>{t.table.won}</th>
            <th scope="col" title={t.table.drawnTitle}>{t.table.drawn}</th>
            <th scope="col" title={t.table.lostTitle}>{t.table.lost}</th>
            <th className="table__wide" scope="col" title={t.table.goalsTitle}>{t.table.goals}</th>
            <th scope="col" title={t.table.differenceTitle}>{t.table.difference}</th>
            <th scope="col" title={t.table.pointsTitle}>{t.table.points}</th>
            <th scope="col"><span className="visually-hidden">{t.table.favourite}</span></th>
          </tr>
        </thead>
        <tbody>
          {group.rows.map(row => (
            <tr key={row.team.id} className={teamIds.includes(row.team.id) ? 'table__row table__row--own' : 'table__row'}>
              <td className="table__rank" style={row.noteColor ? { boxShadow: `inset 3px 0 0 ${row.noteColor}` } : undefined} title={row.note ?? undefined}>
                {row.rank}
              </td>
              <td className="table__team">
                <button className="table__link" onClick={() => onNavigate({ view: 'team', leagueCode, teamId: row.team.id })}>
                  <TeamLogo team={row.team} size={20} />
                  <span className="team__name team__name--full">{row.team.name}</span>
                  <span className="team__name team__name--short">{row.team.shortName}</span>
                </button>
              </td>
              <td>{row.played}</td>
              <td>{row.wins}</td>
              <td>{row.draws}</td>
              <td>{row.losses}</td>
              <td className="table__wide">{row.goalsFor}:{row.goalsAgainst}</td>
              <td>{row.goalDifference > 0 ? `+${row.goalDifference}` : row.goalDifference}</td>
              <td className="table__points">{row.points}</td>
              <td className="table__star">
                <FavoriteButton team={{ leagueCode, teamId: row.team.id, name: row.team.name, logoUrl: row.team.logoUrl }} />
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}

/** What the coloured edges mean, each meaning once. */
function Legend({ groups }: { groups: StandingsGroup[] }) {
  const notes = new Map<string, string>()
  for (const row of groups.flatMap(g => g.rows)) {
    if (row.note && row.noteColor && !notes.has(row.note)) notes.set(row.note, row.noteColor)
  }
  if (notes.size === 0) return null

  return (
    <ul className="legend">
      {[...notes].map(([note, color]) => (
        <li key={note}><span className="legend__dot" style={{ background: color }} aria-hidden />{note}</li>
      ))}
    </ul>
  )
}
