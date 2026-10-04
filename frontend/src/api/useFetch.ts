import { useEffect, useMemo, useRef, useState } from 'react'
import { localizeNames } from '../i18n/names'
import { useI18n } from '../i18n/useI18n'

interface FetchState<T> {
  key: string
  data: T | null
  error: string | null
}

/**
 * Loads something once per key and tells apart "still loading", "failed" and "here it is".
 * Team, competition and country names in the result are in the site's language.
 * @param key Identifies what is being loaded; a new key starts a new load and forgets the old result.
 */
export function useFetch<T>(key: string, load: () => Promise<T>) {
  const { language } = useI18n()
  const [state, setState] = useState<FetchState<T>>({ key, data: null, error: null })
  const [attempt, setAttempt] = useState(0)

  // The loader is recreated on every render; only a new key (or a retry) should trigger a load.
  const loadRef = useRef(load)
  useEffect(() => {
    loadRef.current = load
  })

  useEffect(() => {
    let cancelled = false
    loadRef.current().then(
      data => {
        if (!cancelled) setState({ key, data, error: null })
      },
      (e: unknown) => {
        if (!cancelled) setState({ key, data: null, error: e instanceof Error ? e.message : String(e) })
      },
    )
    return () => {
      cancelled = true
    }
  }, [key, attempt])

  // A result that belongs to a previous key is not this key's result.
  const current = state.key === key ? state : { data: null, error: null }
  const data = useMemo(() => localizeNames(current.data, language), [current.data, language])
  return {
    data,
    error: current.error,
    loading: current.data === null && current.error === null,
    retry: () => {
      setState({ key, data: null, error: null })
      setAttempt(n => n + 1)
    },
  }
}
