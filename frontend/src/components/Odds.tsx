import type { Match, Odds } from '../api/types'
import { useI18n } from '../i18n/useI18n'

/** 1.5 → "1.50": bookmakers always quote two decimals. */
const formatOdds = (value: number) => value.toFixed(2)

const outcomes = [
  { key: 'home', label: '1', side: 'Home' },
  { key: 'draw', label: 'X', side: null },
  { key: 'away', label: '2', side: 'Away' },
] as const

/** The three prices in a row, the favourite's highlighted. Small enough for a match card. */
export function OddsStrip({ odds, className }: { odds: Odds; className?: string }) {
  const { t } = useI18n()

  return (
    <span
      className={className ? `odds-strip ${className}` : 'odds-strip'}
      aria-label={t.odds.label(formatOdds(odds.home), formatOdds(odds.draw), formatOdds(odds.away))}
    >
      {outcomes.map(({ key, label, side }) => {
        const favorite = side !== null && side === odds.favorite
        return (
          <span key={key} className={favorite ? 'odd odd--favorite' : 'odd'} title={favorite ? t.odds.favourite : undefined} aria-hidden>
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
  const { t } = useI18n()
  const names = { home: match.homeTeam.shortName, draw: t.odds.draw, away: match.awayTeam.shortName }
  const title = match.status === 'Scheduled' ? t.odds.title : t.odds.closing

  return (
    <section className="odds-board" aria-label={title}>
      <header className="odds-board__head">
        <h3>{title} · 1X2</h3>
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
              {favorite && <span className="odds-cell__badge">{t.odds.favourite}</span>}
            </div>
          )
        })}
      </div>
      <p className="odds-board__note">{t.odds.note}</p>
    </section>
  )
}
