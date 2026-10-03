import type { Standings, StandingsGroup } from '../api/types'
import type { Route } from '../route'
import { TeamLogo } from './common'

interface Props {
  standings: Standings
  /** Highlights this team's row; with several groups, only the group it is in is shown. */
  teamId?: string
  onNavigate: (route: Route) => void
}

/** A league table (or one per group). Rows lead to the team's page; coloured edges mark what a position means. */
export function StandingsTable({ standings, teamId, onNavigate }: Props) {
  const ownGroup = teamId ? standings.groups.find(g => g.rows.some(r => r.team.id === teamId)) : undefined
  const groups = ownGroup ? [ownGroup] : standings.groups
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
          teamId={teamId}
          onNavigate={onNavigate}
        />
      ))}
      <Legend groups={groups} />
    </>
  )
}

function GroupTable({ group, title, leagueCode, teamId, onNavigate }: {
  group: StandingsGroup
  title: string | undefined
  leagueCode: string
  teamId: string | undefined
  onNavigate: (route: Route) => void
}) {
  return (
    <div className="table-wrap">
      <table className="table">
        {title && <caption>{title}</caption>}
        <thead>
          <tr>
            <th className="table__rank" scope="col">#</th>
            <th className="table__team" scope="col">Team</th>
            <th scope="col" title="Played">P</th>
            <th scope="col" title="Won">W</th>
            <th scope="col" title="Drawn">D</th>
            <th scope="col" title="Lost">L</th>
            <th className="table__wide" scope="col" title="Goals for and against">Goals</th>
            <th scope="col" title="Goal difference">GD</th>
            <th scope="col" title="Points">Pts</th>
          </tr>
        </thead>
        <tbody>
          {group.rows.map(row => (
            <tr key={row.team.id} className={row.team.id === teamId ? 'table__row table__row--own' : 'table__row'}>
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
