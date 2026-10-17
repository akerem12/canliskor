// Domestic competitions carry their country in their code: "tur.1" is Türkiye's first tier, "bra.2" Brazil's second.
// Competitions across countries ("uefa.champions", "fifa.friendly") have none.

const Domestic = /^([a-z]{3})\.\d+$/

/** "tur.1" → "tur"; null for a competition across countries. */
export const leagueCountryCode = (leagueCode: string): string | null => Domestic.exec(leagueCode)?.[1] ?? null

/** "tur.1" → Türkiye's flag; null for a competition across countries. */
export function countryFlagUrl(leagueCode: string): string | null {
  const country = leagueCountryCode(leagueCode)
  return country ? `https://a.espncdn.com/i/teamlogos/countries/500/${country}.png` : null
}

/** The first domestic competition among these, in order: a club's own league. */
export const domesticLeague = (leagueCodes: Iterable<string>): string | null =>
  [...leagueCodes].find(code => leagueCountryCode(code) !== null) ?? null
