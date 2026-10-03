import type { Match, Odds } from '../api/types'

/** 1.5 → "1.50": bookmakers always quote two decimals. */
const formatOdds = (value: number) => value.toFixed(2)

const outcomes = [
  { key: 'home', label: '1', side: 'Home' },
  { key: 'draw', label: 'X', side: null },
  { key: 'away', label: '2', side: 'Away' },
] as const

/** The three prices in a row, the favourite's highlighted. Small enough for a match card. */
export function OddsStrip({ odds, className }: { odds: Odds; className?: string }) {
  return (
    <span
      className={className ? `odds-strip ${className}` : 'odds-strip'}
      aria-label={`Odds: home win ${formatOdds(odds.home)}, draw ${formatOdds(odds.draw)}, away win ${formatOdds(odds.away)}`}
    >
      {outcomes.map(({ key, label, side }) => {
        const favorite = side !== null && side === odds.favorite
        return (
          <span key={key} className={favorite ? 'odd odd--favorite' : 'odd'} title={favorite ? 'Favourite' : undefined} aria-hidden>
            <span className="odd__label">{label}</span>
            {formatOdds(odds[key])}
          </span>
        )
      })}
    </span>
  )
}

/** The prices with the teams' names and a "Favourite" badge, for the match page. */
export function OddsBoard({ odds, match }: { odds: Odds; match: Match }) {
  const names = { home: match.homeTeam.shortName, draw: 'Draw', away: match.awayTeam.shortName }

  return (
    <section className="odds-board" aria-label="Match odds">
      <header className="odds-board__head">
        <h3>{match.status === 'Scheduled' ? 'Match odds' : 'Closing odds'} · 1X2</h3>
        {odds.provider && <span>{odds.provider}</span>}
      </header>
      <div className="odds-board__cells">
        {outcomes.map(({ key, label, side }) => {
          const favorite = side !== null && side === odds.favorite
          return (
            <div key={key} className={favorite ? 'odds-cell odds-cell--favorite' : 'odds-cell'}>
              <span className="odds-cell__label">
                <span className="odds-cell__key">{label}</span>
                <span className="odds-cell__name">{names[key]}</span>
              </span>
              <strong className="odds-cell__price">{formatOdds(odds[key])}</strong>
              {favorite && <span className="odds-cell__badge">Favourite</span>}
            </div>
          )
        })}
      </div>
      <p className="odds-board__note">Decimal odds, for information only.</p>
    </section>
  )
}
