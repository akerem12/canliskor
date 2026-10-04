import { useEffect, useState } from 'react'
import { searchTeams } from '../api/http'
import type { TeamSearchResult } from '../api/types'
import { FavoriteButton } from '../favorites/FavoriteButton'
import { localizeNames, searchQueryFor } from '../i18n/names'
import { useI18n } from '../i18n/useI18n'
import type { Route } from '../route'
import { EmptyState, Skeleton, TeamLogo } from './common'

/** The server ignores shorter queries. */
const MinQueryLength = 2
/** Wait this long after the last keystroke before asking the server. */
const DebounceMs = 300

interface Found {
  query: string
  results: TeamSearchResult[] | null
  error: boolean
}

/** Finds any team of the followed leagues by name; a result opens the team's page. */
export function TeamSearchBox({ onNavigate }: { onNavigate: (route: Route) => void }) {
  const { t, language } = useI18n()
  const [text, setText] = useState('')
  const [found, setFound] = useState<Found | null>(null)
  const query = text.trim()
  const searching = query.length >= MinQueryLength

  useEffect(() => {
    if (!searching) return
    let cancelled = false
    const timer = setTimeout(() => {
      searchTeams(searchQueryFor(query, language)).then(
        results => {
          if (!cancelled) setFound({ query, results, error: false })
        },
        () => {
          if (!cancelled) setFound({ query, results: null, error: true })
        },
      )
    }, DebounceMs)
    return () => {
      cancelled = true
      clearTimeout(timer)
    }
  }, [query, searching, language])

  // Results of an earlier query aren't this query's results.
  const current = localizeNames(searching && found?.query === query ? found : null, language)

  return (
    <div className="search">
      <input
        className="search__input"
        type="search"
        value={text}
        onChange={e => setText(e.target.value)}
        placeholder={t.search.placeholder}
        aria-label={t.search.label}
        autoComplete="off"
      />

      {searching && (
        <div className="search__results" aria-live="polite" aria-busy={current === null}>
          {current === null && <Skeleton rows={3} height={30} />}
          {current?.error && <EmptyState icon="⚠️" title={t.search.unavailable} hint={t.search.unavailableHint} />}
          {current?.results?.length === 0 && (
            <EmptyState icon="🔍" title={t.search.none(query)} hint={t.search.noneHint} />
          )}
          {current?.results && current.results.length > 0 && (
            <ul className="search__list">
              {current.results.map(({ league, team }) => (
                <li key={team.id}>
                  <button className="search__item" onClick={() => onNavigate({ view: 'team', leagueCode: league.code, teamId: team.id })}>
                    <TeamLogo team={team} size={26} />
                    <span className="search__name">{team.name}</span>
                    <span className="search__league">{league.name}</span>
                  </button>
                  <FavoriteButton team={{ leagueCode: league.code, teamId: team.id, name: team.name, logoUrl: team.logoUrl }} />
                </li>
              ))}
            </ul>
          )}
        </div>
      )}
    </div>
  )
}
