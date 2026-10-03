import { useEffect, useRef, useState } from 'react'
import type { LineupPlayer, Match, MatchLineups, PlayerStats, Team, TeamLineup } from '../api/types'
import type { Shirt } from '../lineups'
import { pitchName, pitchRows, shirtColors } from '../lineups'
import { usePlayerProfile } from '../players/usePlayerProfile'
import { formatTime } from '../time'

interface Props {
  lineups: MatchLineups | null
  match: Match
}

interface Selected {
  player: LineupPlayer
  team: Team
  shirt: Shirt
}

/** Both starting elevens on a pitch (home at the top), the benches below. Clicking a player shows their match. */
export function Lineups({ lineups, match }: Props) {
  const [selected, setSelected] = useState<Selected | null>(null)

  if (!lineups) {
    return (
      <p className="detail__empty">
        {match.status === 'Scheduled'
          ? `Line-ups are announced about an hour before kick-off (${formatTime(match.kickoff)}).`
          : 'No line-ups available for this match.'}
      </p>
    )
  }

  const shirts = shirtColors(lineups.home.shirtColor, lineups.away.shirtColor)
  const sides = [
    { side: 'home', lineup: lineups.home, team: match.homeTeam, shirt: shirts.home },
    { side: 'away', lineup: lineups.away, team: match.awayTeam, shirt: shirts.away },
  ] as const

  return (
    <section aria-label="Line-ups">
      <div className="pitch">
        {sides.map(({ side, lineup, team, shirt }) => (
          <div key={side} className={`pitch__half pitch__half--${side}`} aria-label={`${team.name} starting eleven`}>
            <span className="pitch__team">{team.shortName} · {lineup.formation}</span>
            {pitchRows(lineup, side).map((row, i) => (
              <div key={i} className="pitch__row">
                {row.map(player => (
                  <PitchPlayer key={player.id} player={player} shirt={shirt} onSelect={() => setSelected({ player, team, shirt })} />
                ))}
              </div>
            ))}
          </div>
        ))}
      </div>

      <h3 className="detail__heading">Substitutes</h3>
      <div className="benches">
        {sides.map(({ side, lineup, team, shirt }) => (
          <Bench key={side} lineup={lineup} team={team} shirt={shirt} onSelect={player => setSelected({ player, team, shirt })} />
        ))}
      </div>

      {selected && <PlayerSheet {...selected} leagueCode={match.leagueCode} onClose={() => setSelected(null)} />}
    </section>
  )
}

function PitchPlayer({ player, shirt, onSelect }: { player: LineupPlayer; shirt: Shirt; onSelect: () => void }) {
  return (
    <button className="player" onClick={onSelect} title={player.name}>
      <span className="player__shirt" style={{ background: shirt.fill, color: shirt.text }}>
        {player.jersey ?? ''}
        <PlayerMarks player={player} className="player__marks" />
      </span>
      <span className="player__name">{pitchName(player)}</span>
      {player.wentOffAt && <span className="player__off">▼ {player.wentOffAt}</span>}
    </button>
  )
}

function Bench({ lineup, team, shirt, onSelect }: {
  lineup: TeamLineup
  team: Team
  shirt: Shirt
  onSelect: (player: LineupPlayer) => void
}) {
  return (
    <div className="bench">
      <h4 className="bench__team">{team.name}</h4>
      <ul aria-label={`${team.name} substitutes`}>
      {lineup.bench.map(player => (
        <li key={player.id}>
          <button className={player.cameOnAt ? 'bench__player' : 'bench__player bench__player--unused'} onClick={() => onSelect(player)}>
            <span className="bench__shirt" style={{ background: shirt.fill, color: shirt.text }}>{player.jersey ?? ''}</span>
            <span className="bench__name">
              {player.shortName}
              <PlayerMarks player={player} />
            </span>
            {player.cameOnAt && <span className="bench__on">▲ {player.cameOnAt}</span>}
          </button>
        </li>
      ))}
      </ul>
    </div>
  )
}

/** Goals, assists and cards next to a player. */
function PlayerMarks({ player, className }: { player: LineupPlayer; className?: string }) {
  const { goals, assists, ownGoals, yellowCards, redCards } = player.stats
  if (goals + assists + ownGoals + yellowCards + redCards === 0) return null

  return (
    <span className={className ? `marks ${className}` : 'marks'}>
      {goals > 0 && <span role="img" aria-label={`${goals} goal${goals > 1 ? 's' : ''}`}>⚽{goals > 1 ? goals : ''}</span>}
      {assists > 0 && (
        <span className="assist-badge" role="img" aria-label={`${assists} assist${assists > 1 ? 's' : ''}`} title="Assist">
          A{assists > 1 ? assists : ''}
        </span>
      )}
      {ownGoals > 0 && <span className="marks__own-goal" role="img" aria-label="Own goal">⚽</span>}
      {yellowCards > 0 && redCards === 0 && <span className="card card--yellow" role="img" aria-label="Yellow card" />}
      {redCards > 0 && <span className="card card--red" role="img" aria-label="Red card" />}
    </span>
  )
}

const statLabels: [keyof PlayerStats, string][] = [
  ['goals', 'Goals'],
  ['assists', 'Assists'],
  ['shots', 'Shots'],
  ['shotsOnTarget', 'Shots on target'],
  ['saves', 'Saves'],
  ['goalsConceded', 'Goals conceded while on the pitch'],
  ['foulsCommitted', 'Fouls committed'],
  ['foulsSuffered', 'Fouls suffered'],
  ['offsides', 'Offsides'],
  ['yellowCards', 'Yellow cards'],
  ['redCards', 'Red cards'],
  ['ownGoals', 'Own goals'],
]

/** One player's match: minutes and statistics. A modal dialog: Esc or a click outside closes it. */
function PlayerSheet({ player, team, shirt, leagueCode, onClose }: Selected & { leagueCode: string; onClose: () => void }) {
  const dialogRef = useRef<HTMLDialogElement>(null)
  const openPlayer = usePlayerProfile()

  useEffect(() => {
    const dialog = dialogRef.current
    if (dialog && !dialog.open) dialog.showModal()
  }, [])

  const played = player.minutesPlayed !== null
  // Goalkeepers' numbers mean nothing for outfield players and the other way round; zeros of the rest stay out too.
  const stats = statLabels.filter(([key]) =>
    player.stats[key] > 0 || (key === 'saves' && player.position === 'Goalkeeper'))

  return (
    <dialog
      ref={dialogRef}
      className="sheet"
      aria-label={player.name}
      onClose={onClose}
      // Clicks on the backdrop target the dialog element itself; clicks on the content target its children.
      onClick={e => e.target === e.currentTarget && dialogRef.current?.close()}
    >
      <div className="sheet__body">
        <header className="sheet__top">
          <span className="bench__shirt" style={{ background: shirt.fill, color: shirt.text }}>{player.jersey ?? ''}</span>
          <div className="sheet__who">
            <strong>{player.name}</strong>
            <span>{[team.name, player.position].filter(Boolean).join(' · ')}</span>
          </div>
          <button className="sheet__close" onClick={() => dialogRef.current?.close()} aria-label="Close">×</button>
        </header>

        {played ? (
          <>
            <div className="sheet__summary">
              <span className="sheet__value">{player.minutesPlayed}'</span>
              <span className="sheet__label">
                {[
                  'minutes played',
                  player.cameOnAt && `on ${player.cameOnAt}`,
                  player.wentOffAt && `off ${player.wentOffAt}`,
                  player.sentOffAt && `sent off ${player.sentOffAt}`,
                ].filter(Boolean).join(' · ')}
              </span>
            </div>

            {stats.length > 0 ? (
              <dl className="sheet__stats">
                {stats.map(([key, label]) => (
                  <div key={key}>
                    <dt>{label}</dt>
                    <dd>{player.stats[key]}</dd>
                  </div>
                ))}
              </dl>
            ) : (
              <p className="sheet__empty">Nothing recorded: no shots, fouls, cards or goals conceded.</p>
            )}

          </>
        ) : (
          <p className="sheet__empty">{player.position ? 'The match has not started.' : 'Has not played in this match.'}</p>
        )}

        <button
          className="more"
          onClick={() => {
            // One dialog at a time: this one makes way for the profile.
            dialogRef.current?.close()
            openPlayer({ leagueCode, playerId: player.id, name: player.name })
          }}
        >
          Profile &amp; season statistics ›
        </button>
      </div>
    </dialog>
  )
}
