import { useEffect, useRef, useState } from 'react'
import { getExpectedLineups } from '../api/http'
import type { ExpectedLineup, LineupPlayer, Match, MatchLineups, PlayerStats, Team, TeamLineup } from '../api/types'
import { useFetch } from '../api/useFetch'
import { useI18n } from '../i18n/useI18n'
import type { Shirt } from '../lineups'
import { pitchName, pitchRows, shirtColors } from '../lineups'
import { usePlayerProfile } from '../players/usePlayerProfile'
import { formatMatchDate, formatTime } from '../time'
import { Skeleton } from './common'

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
  const { t } = useI18n()
  const [selected, setSelected] = useState<Selected | null>(null)

  if (!lineups) {
    // Before the announcement there is at least a guess to show.
    return match.status === 'Scheduled'
      ? <PossibleLineups match={match} />
      : <p className="detail__empty">{t.lineups.none}</p>
  }

  const shirts = shirtColors(lineups.home.shirtColor, lineups.away.shirtColor)
  const sides = [
    { side: 'home', lineup: lineups.home, team: match.homeTeam, shirt: shirts.home },
    { side: 'away', lineup: lineups.away, team: match.awayTeam, shirt: shirts.away },
  ] as const

  return (
    <section aria-label={t.lineups.title}>
      <div className="pitch">
        {sides.map(({ side, lineup, team, shirt }) => (
          <div key={side} className={`pitch__half pitch__half--${side}`} aria-label={t.lineups.startingEleven(team.name)}>
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

      <h3 className="detail__heading">{t.lineups.substitutes}</h3>
      <div className="benches">
        {sides.map(({ side, lineup, team, shirt }) => (
          <Bench key={side} lineup={lineup} team={team} shirt={shirt} onSelect={player => setSelected({ player, team, shirt })} />
        ))}
      </div>

      {selected && <PlayerSheet {...selected} leagueCode={match.leagueCode} onClose={() => setSelected(null)} />}
    </section>
  )
}

/**
 * How the teams may line up, for a match still waiting for its line-ups: each team as it started its last match.
 * Titled "Possible line-ups" and drawn with a dashed outline; hovering a team's name says which match it is from.
 * A player leads straight to their profile, as there is no match of theirs to show yet.
 */
function PossibleLineups({ match }: { match: Match }) {
  const { t } = useI18n()
  const openPlayer = usePlayerProfile()
  const expected = useFetch(`expected-lineups/${match.leagueCode}/${match.id}`, () => getExpectedLineups(match.leagueCode, match.id))
  const announcedLater = <p className="detail__empty">{t.lineups.announcedLater(formatTime(match.kickoff))}</p>

  if (expected.loading) return <Skeleton rows={8} height={26} />
  const { home, away } = expected.data ?? { home: null, away: null }
  if (!home && !away) return announcedLater

  const shirts = shirtColors(home?.lineup.shirtColor ?? null, away?.lineup.shirtColor ?? null)
  const sides = [
    { side: 'home', expected: home, team: match.homeTeam, shirt: shirts.home },
    { side: 'away', expected: away, team: match.awayTeam, shirt: shirts.away },
  ] as const
  const source = ({ basedOn }: ExpectedLineup) => t.lineups.basedOn(
    `${basedOn.homeTeam.shortName} ${basedOn.score ? `${basedOn.score.home}-${basedOn.score.away}` : '-'} ${basedOn.awayTeam.shortName}`,
    formatMatchDate(basedOn.kickoff, t))

  return (
    <section aria-label={t.lineups.possible}>
      <h3 className="detail__heading possible">{t.lineups.possible}</h3>

      <div className={home && away ? 'pitch pitch--possible' : 'pitch pitch--possible pitch--single'}>
        {sides.map(({ side, expected: known, team, shirt }) => known && (
          <div key={side} className={`pitch__half pitch__half--${side}`} aria-label={t.lineups.startingEleven(team.name)}>
            <span className="pitch__team" title={source(known)}>{team.shortName} · {known.lineup.formation}</span>
            {pitchRows(known.lineup, side).map((row, i) => (
              <div key={i} className="pitch__row">
                {row.map(player => (
                  <PitchPlayer
                    key={player.id}
                    player={player}
                    shirt={shirt}
                    onSelect={() => openPlayer({ leagueCode: match.leagueCode, playerId: player.id, name: player.name })}
                  />
                ))}
              </div>
            ))}
          </div>
        ))}
      </div>
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
  const { t } = useI18n()

  return (
    <div className="bench">
      <h4 className="bench__team">{team.name}</h4>
      <ul aria-label={t.lineups.substitutesOf(team.name)}>
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
  const { t } = useI18n()
  const { goals, assists, ownGoals, yellowCards, redCards } = player.stats
  if (goals + assists + ownGoals + yellowCards + redCards === 0) return null

  return (
    <span className={className ? `marks ${className}` : 'marks'}>
      {goals > 0 && <span role="img" aria-label={t.lineups.goals(goals)}>⚽{goals > 1 ? goals : ''}</span>}
      {assists > 0 && (
        <span className="assist-badge" role="img" aria-label={t.lineups.assists(assists)} title={t.match.assist}>
          A{assists > 1 ? assists : ''}
        </span>
      )}
      {ownGoals > 0 && <span className="marks__own-goal" role="img" aria-label={t.lineups.ownGoal}>⚽</span>}
      {yellowCards > 0 && redCards === 0 && <span className="card card--yellow" role="img" aria-label={t.match.yellowCard} />}
      {redCards > 0 && <span className="card card--red" role="img" aria-label={t.match.redCard} />}
    </span>
  )
}

/** The order the numbers are listed in. */
const statKeys: (keyof PlayerStats)[] = [
  'goals', 'assists', 'shots', 'shotsOnTarget', 'saves', 'goalsConceded',
  'foulsCommitted', 'foulsSuffered', 'offsides', 'yellowCards', 'redCards', 'ownGoals',
]

/** One player's match: minutes and statistics. A modal dialog: Esc or a click outside closes it. */
function PlayerSheet({ player, team, shirt, leagueCode, onClose }: Selected & { leagueCode: string; onClose: () => void }) {
  const { t } = useI18n()
  const dialogRef = useRef<HTMLDialogElement>(null)
  const openPlayer = usePlayerProfile()

  useEffect(() => {
    const dialog = dialogRef.current
    if (dialog && !dialog.open) dialog.showModal()
  }, [])

  const played = player.minutesPlayed !== null
  // Goalkeepers' numbers mean nothing for outfield players and the other way round; zeros of the rest stay out too.
  const stats = statKeys.filter(key => player.stats[key] > 0 || (key === 'saves' && player.position === 'Goalkeeper'))

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
            <span>{[team.name, player.position && t.positions[player.position]].filter(Boolean).join(' · ')}</span>
          </div>
          <button className="sheet__close" onClick={() => dialogRef.current?.close()} aria-label={t.common.close}>×</button>
        </header>

        {played ? (
          <>
            <div className="sheet__summary">
              <span className="sheet__value">{player.minutesPlayed}'</span>
              <span className="sheet__label">
                {[
                  t.lineups.minutesPlayed,
                  player.cameOnAt && t.lineups.on(player.cameOnAt),
                  player.wentOffAt && t.lineups.off(player.wentOffAt),
                  player.sentOffAt && t.lineups.sentOff(player.sentOffAt),
                ].filter(Boolean).join(' · ')}
              </span>
            </div>

            {stats.length > 0 ? (
              <dl className="sheet__stats">
                {stats.map(key => (
                  <div key={key}>
                    <dt>{t.lineups.stats[key]}</dt>
                    <dd>{player.stats[key]}</dd>
                  </div>
                ))}
              </dl>
            ) : (
              <p className="sheet__empty">{t.lineups.nothingRecorded}</p>
            )}

          </>
        ) : (
          <p className="sheet__empty">{player.position ? t.lineups.notStarted : t.lineups.notPlayed}</p>
        )}

        <button
          className="more"
          onClick={() => {
            // One dialog at a time: this one makes way for the profile.
            dialogRef.current?.close()
            openPlayer({ leagueCode, playerId: player.id, name: player.name })
          }}
        >
          {t.lineups.openProfile}
        </button>
      </div>
    </dialog>
  )
}
