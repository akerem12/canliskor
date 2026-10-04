// Turkish names for what the data source only names in English: competitions, countries (national teams and
// players' nationalities), Turkish clubs spelt with their own letters, a few clubs with a Turkish name, and the
// short phrases around a league table. Whatever isn't known here is shown as it arrives.

import type { Language } from './language'

/** Competition code, the data source's English name, the Turkish name. */
const leagues: readonly (readonly [code: string, english: string, turkish: string])[] = [
  ['tur.1', 'Turkish Super Lig', 'Süper Lig'],
  ['eng.1', 'English Premier League', 'Premier Lig'],
  ['esp.1', 'Spanish LALIGA', 'La Liga'],
  ['ger.1', 'German Bundesliga', 'Bundesliga'],
  ['ita.1', 'Italian Serie A', 'Serie A'],
  ['fra.1', 'French Ligue 1', 'Ligue 1'],
  ['uefa.champions', 'UEFA Champions League', 'UEFA Şampiyonlar Ligi'],
  ['uefa.europa', 'UEFA Europa League', 'UEFA Avrupa Ligi'],
  ['uefa.europa.conf', 'UEFA Conference League', 'UEFA Konferans Ligi'],
  ['uefa.nations', 'UEFA Nations League', 'UEFA Uluslar Ligi'],
  ['uefa.super_cup', 'UEFA Super Cup', 'UEFA Süper Kupa'],
  ['uefa.euro', 'UEFA European Championship', 'Avrupa Şampiyonası'],
  ['fifa.world', 'FIFA World Cup', 'FIFA Dünya Kupası'],
  ['conmebol.libertadores', 'CONMEBOL Libertadores', 'Libertadores Kupası'],
  ['conmebol.sudamericana', 'CONMEBOL Sudamericana', 'Sudamericana Kupası'],
  ['arg.1', 'Argentine Liga Profesional de Fútbol', 'Arjantin Ligi'],
  ['bra.1', 'Brazilian Serie A', 'Brezilya Serie A'],
  ['col.1', 'Colombian Primera A', 'Kolombiya Ligi'],
  ['chi.1', 'Chilean Primera División', 'Şili Ligi'],
  ['fifa.friendly', 'International Friendly', 'Millî Takım Hazırlık Maçı'],
  ['club.friendly', 'Club Friendly', 'Hazırlık Maçı'],
]

/** Clubs, by the data source's spelling (full and short names alike). The first entry for a Turkish name is its English one. */
const clubs: readonly (readonly [english: string, turkish: string])[] = [
  // Turkish clubs: the data source drops the Turkish letters.
  ['Besiktas', 'Beşiktaş'],
  ['Fenerbahce', 'Fenerbahçe'],
  ['Kasimpasa', 'Kasımpaşa'],
  ['Caykur Rizespor', 'Çaykur Rizespor'],
  ['Istanbul Basaksehir', 'Başakşehir'],
  ['Istanbul BB', 'Başakşehir'],
  ['Genclerbirligi', 'Gençlerbirliği'],
  ['Genclerbirli', 'Gençlerbirliği'],
  ['Goztepe', 'Göztepe'],
  ['Eyupspor', 'Eyüpspor'],
  ['Erzurum BB', 'Erzurumspor FK'],
  ['Erzurum', 'Erzurumspor'],
  ['Ankaragucu', 'Ankaragücü'],
  ['Fatih Karagumruk', 'Fatih Karagümrük'],
  ['Istanbulspor', 'İstanbulspor'],
  ['Umraniyespor', 'Ümraniyespor'],
  ['Bandirmaspor', 'Bandırmaspor'],
  ['Sanliurfaspor', 'Şanlıurfaspor'],
  ['Igdir FK', 'Iğdır FK'],
  // Clubs abroad that Turkish calls by another name.
  ['Bayern Munich', 'Bayern Münih'],
  ['Sporting CP', 'Sporting Lizbon'],
  ['Red Star Belgrade', 'Kızılyıldız'],
  ['Olympiacos', 'Olympiakos'],
  ['Shakhtar Donetsk', 'Şahtar Donetsk'],
  ['Dynamo Kyiv', 'Dinamo Kiev'],
  ['FC Copenhagen', 'Kopenhag'],
  ['Slavia Prague', 'Slavia Prag'],
  ['Sparta Prague', 'Sparta Prag'],
  ['Marseille', 'Marsilya'],
  ['Qarabag', 'Karabağ'],
  ['Ferencvaros', 'Ferençvaroş'],
  ['Athletic Club', 'Athletic Bilbao'],
  ['Internazionale', 'Inter'],
  ['AC Milan', 'Milan'],
  ['AS Roma', 'Roma'],
  ['FC Porto', 'Porto'],
  ['AS Monaco', 'Monaco'],
]

/** What a position in the table means, and the names of table sections. Keys in lower case. */
const tablePhrases: Readonly<Record<string, string>> = {
  'champions league': 'Şampiyonlar Ligi',
  'champions league qualifying': 'Şampiyonlar Ligi elemeleri',
  'europa league': 'Avrupa Ligi',
  'europa league qualifying': 'Avrupa Ligi elemeleri',
  'conference league': 'Konferans Ligi',
  'conference league qualifying': 'Konferans Ligi elemeleri',
  'relegation': 'Küme düşme',
  'relegated': 'Küme düşme',
  'relegation playoffs': 'Küme düşme play-off\'u',
  'promotion': 'Yükselme',
  'promotion playoffs': 'Yükselme play-off\'u',
  'eliminated': 'Elendi',
  'knockout phase playoffs - seeded': 'Play-off turu (seri başı)',
  'knockout phase playoffs - unseeded': 'Play-off turu (seri başı değil)',
  'qualifies for round of 16': 'Son 16 turu',
  'qualifies for ko playoffs': 'Play-off turu',
  'qualifies for sudamericana ko playoffs': 'Sudamericana play-off turu',
  'a, b: relegation playoffs': 'A, B: Küme düşme play-off\'u',
  'a, b: relegation; c: relegation or playoffs': 'A, B: Küme düşme; C: Küme düşme veya play-off',
  'a: qualifies for qfs; b-d: promotion': 'A: Çeyrek final; B-D: Yükselme',
  'a: qualifies for qfs; b-d: promotion playoffs': 'A: Çeyrek final; B-D: Yükselme play-off\'u',
  'league phase': 'Lig Aşaması',
}

/** The parts of the United Kingdom play as countries of their own, but aren't in the browser's list of countries. */
const homeNations: readonly (readonly [english: string, turkish: string])[] = [
  ['England', 'İngiltere'],
  ['Scotland', 'İskoçya'],
  ['Wales', 'Galler'],
  ['Northern Ireland', 'Kuzey İrlanda'],
]

/**
 * Country names the data source spells differently from the browser's English list, and ones browsers don't agree
 * on among themselves ("South Korea" is plain "Korea" in some): folded name → country code.
 */
const countryAliases: Readonly<Record<string, string>> = {
  'south korea': 'KR', 'north korea': 'KP', 'turkiye': 'TR', 'czechia': 'CZ', 'russia': 'RU', 'iran': 'IR', 'syria': 'SY',
  'vietnam': 'VN', 'moldova': 'MD', 'tanzania': 'TZ', 'bolivia': 'BO', 'venezuela': 'VE', 'cape verde': 'CV',
  'north macedonia': 'MK', 'united states': 'US', 'curacao': 'CW', 'kosovo': 'XK', 'taiwan': 'TW', 'laos': 'LA',
  'turkey': 'TR', 'usa': 'US', 'ivory coast': 'CI', 'czech republic': 'CZ', 'bosnia-herzegovina': 'BA',
  'bosnia and herzegovina': 'BA', 'republic of ireland': 'IE', 'dr congo': 'CD', 'congo dr': 'CD', 'congo': 'CG',
  'cape verde islands': 'CV', 'china pr': 'CN', 'hong kong': 'HK', 'macau': 'MO', 'trinidad and tobago': 'TT',
  'antigua and barbuda': 'AG', 'st. kitts and nevis': 'KN', 'st. vincent and the grenadines': 'VC', 'saint lucia': 'LC',
  'sao tome and principe': 'ST', 'myanmar': 'MM', 'palestine': 'PS', 'korea republic': 'KR', 'korea dpr': 'KP',
  'ir iran': 'IR', 'the gambia': 'GM', 'swaziland': 'SZ', 'east timor': 'TL', 'chinese taipei': 'TW',
  'kyrgyz republic': 'KG', 'brunei darussalam': 'BN', 'turks and caicos islands': 'TC',
}

/** Where everyday Turkish differs from the browser's formal name. */
const turkishCountryOverrides: Readonly<Record<string, string>> = {
  US: 'ABD', CI: 'Fildişi Sahili', CD: 'Demokratik Kongo', CG: 'Kongo', HK: 'Hong Kong', MO: 'Makao',
  CV: 'Yeşil Burun Adaları', MM: 'Myanmar', PS: 'Filistin',
}

/** Lower case without accents, and Turkish's two i's as one, so names compare however they are typed. */
const fold = (text: string) =>
  text.trim().toLowerCase().replace(/ı/g, 'i').normalize('NFD').replace(/[̀-ͯ]/g, '')

const leagueByCode = new Map(leagues.map(league => [league[0], league]))
const leagueByEnglish = new Map(leagues.map(league => [league[1].toLowerCase(), league]))
const clubByEnglish = new Map(clubs)
const clubByTurkish = new Map<string, string>()
for (const [english, turkish] of clubs) {
  if (!clubByTurkish.has(turkish)) clubByTurkish.set(turkish, english)
}

interface Countries {
  /** Folded English name → Turkish name. */
  turkish: Map<string, string>
  /** Folded Turkish name → English name. */
  english: Map<string, string>
}

let countries: Countries | undefined

/** Built on first use from the browser's own country names in both languages; empty where it has none. */
function countryNames(): Countries {
  if (countries) return countries
  const turkish = new Map<string, string>()
  const english = new Map<string, string>()
  const add = (englishName: string, turkishName: string, alias = englishName) => {
    turkish.set(fold(alias), turkishName)
    if (!english.has(fold(turkishName))) english.set(fold(turkishName), englishName)
  }

  try {
    const inEnglish = new Intl.DisplayNames('en', { type: 'region' })
    const inTurkish = new Intl.DisplayNames('tr', { type: 'region' })
    const turkishOf = (code: string) => turkishCountryOverrides[code] ?? inTurkish.of(code)

    for (let first = 65; first <= 90; first++) {
      for (let second = 65; second <= 90; second++) {
        const code = String.fromCharCode(first, second)
        const englishName = inEnglish.of(code)
        const turkishName = turkishOf(code)
        // An unassigned code comes back as itself.
        if (englishName && turkishName && englishName !== code && code !== 'ZZ') add(englishName, turkishName)
      }
    }
    for (const [alias, code] of Object.entries(countryAliases)) {
      const englishName = inEnglish.of(code)
      const turkishName = turkishOf(code)
      if (englishName && turkishName) add(englishName, turkishName, alias)
    }
  } catch {
    // A browser without country names: only the home nations below are translated.
  }
  for (const [englishName, turkishName] of homeNations) add(englishName, turkishName)

  countries = { turkish, english }
  return countries
}

/** "Portugal" → "Portekiz"; a name that isn't a country's comes back unchanged. */
export function countryNameTr(name: string): string {
  return countryNames().turkish.get(fold(name)) ?? name
}

/** A club's Turkish name or spelling, or a national team's country in Turkish ("Norway U21" → "Norveç U21"). */
export function teamNameTr(name: string): string {
  const club = clubByEnglish.get(name)
  if (club) return club

  const youth = /^(.+) (U\d{2})$/.exec(name)
  return youth ? `${countryNameTr(youth[1])} ${youth[2]}` : countryNameTr(name)
}

/** By the competition's code, or failing that by its English name. */
export function leagueNameTr(code: string | null | undefined, name: string): string {
  const known = (code ? leagueByCode.get(code) : undefined) ?? leagueByEnglish.get(name.toLowerCase())
  if (known) return known[2]

  // "UEFA Europa League Qualifying" is a competition of its own, named after the one it leads to.
  const qualifying = /^(.+) Qualifying$/i.exec(name)
  const main = qualifying ? leagueByEnglish.get(qualifying[1].toLowerCase()) : undefined
  return main ? `${main[2]} Elemeleri` : name
}

/** "2026-27 Turkish Super Lig" → "2026-27 Süper Lig". */
function seasonLeagueNameTr(code: string | null | undefined, name: string): string {
  const season = /^(\d{4}(?:-\d{2,4})?) (.+)$/.exec(name)
  return season ? `${season[1]} ${leagueNameTr(code, season[2])}` : leagueNameTr(code, name)
}

/** A table section ("Group A1" → "A1 Grubu") or what a position means ("Relegation" → "Küme düşme"). */
export function tablePhraseTr(text: string): string {
  const group = /^Group (.+)$/.exec(text)
  if (group) return `${group[1]} Grubu`
  return tablePhrases[text.toLowerCase()] ?? seasonLeagueNameTr(null, text)
}

/** "2nd in Turkish Super Lig" → "Süper Lig · 2. sıra". */
export function standingSummaryTr(summary: string): string {
  const place = /^(\d+)(?:st|nd|rd|th) in (.+)$/.exec(summary)
  return place ? `${tablePhraseTr(place[2])} · ${place[1]}. sıra` : summary
}

type Json = Record<string, unknown>

const isText = (value: unknown): value is string => typeof value === 'string'

/** The Turkish version of one object of the API, told by its fields; its children are already done. */
function localizeFields(source: Json, result: Json) {
  if ('logoUrl' in source && isText(source.name)) {
    // A team (players have no crest).
    result.name = teamNameTr(source.name)
    if (isText(source.shortName)) result.shortName = teamNameTr(source.shortName)
  } else if (isText(source.code) && isText(source.name)) {
    // A competition, with or without its matches.
    result.name = leagueNameTr(source.code, source.name)
  } else if ('starts' in source && isText(source.name)) {
    // A player's season in one competition.
    result.name = seasonLeagueNameTr(isText(source.leagueCode) ? source.leagueCode : null, source.name)
  } else if (Array.isArray(source.rows) && isText(source.name)) {
    // A section of a league table.
    result.name = tablePhraseTr(source.name)
  }

  if (isText(source.leagueCode) && isText(source.leagueName)) result.leagueName = leagueNameTr(source.leagueCode, source.leagueName)
  if (isText(source.teamName)) result.teamName = teamNameTr(source.teamName)
  if (isText(source.nationality)) result.nationality = countryNameTr(source.nationality)
  if (isText(source.standingSummary)) result.standingSummary = standingSummaryTr(source.standingSummary)
  if ('rank' in source && isText(source.note)) result.note = tablePhraseTr(source.note)
}

function localizeValue(value: unknown): unknown {
  if (Array.isArray(value)) return value.map(localizeValue)
  if (typeof value !== 'object' || value === null) return value

  const source = value as Json
  const result: Json = {}
  for (const key of Object.keys(source)) result[key] = localizeValue(source[key])
  localizeFields(source, result)
  return result
}

/**
 * Anything loaded from the API, with its team, competition and country names in the given language.
 * English is how the data arrives, so it comes back untouched (the very same object).
 */
export function localizeNames<T>(data: T, language: Language): T {
  return language === 'tr' ? localizeValue(data) as T : data
}

/** The data source's own name for a team shown in Turkish, so what is stored reads right in either language. */
export function sourceTeamName(name: string): string {
  const club = clubByTurkish.get(name)
  if (club) return club

  const youth = /^(.+) (U\d{2})$/.exec(name)
  const country = countryNames().english.get(fold(youth ? youth[1] : name))
  return country ? (youth ? `${country} ${youth[2]}` : country) : name
}

/** The data source's own name for a competition, by its code. */
export function sourceLeagueName(code: string, name: string): string {
  return leagueByCode.get(code)?.[1] ?? name
}

/**
 * What to ask the server for: it only knows English names, so a country typed in Turkish ("almanya") is
 * looked up by its English name. Anything else is searched as typed; accents never matter to the server.
 */
export function searchQueryFor(query: string, language: Language): string {
  return language === 'tr' ? countryNames().english.get(fold(query)) ?? query : query
}
