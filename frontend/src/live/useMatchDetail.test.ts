import { describe, expect, it } from 'vitest'
import type { MatchDetail } from '../api/types'
import { newerDetail } from './useMatchDetail'

const detail = (lastUpdatedUtc: string) => ({ lastUpdatedUtc }) as MatchDetail

describe('newerDetail', () => {
  it('takes the first detail', () => {
    const incoming = detail('2026-10-03T12:00:00Z')
    expect(newerDetail(null, incoming)).toBe(incoming)
  })

  it('takes a later detail', () => {
    const incoming = detail('2026-10-03T12:00:30.5Z')
    expect(newerDetail(detail('2026-10-03T12:00:00.1234567+00:00'), incoming)).toBe(incoming)
  })

  it('keeps the current detail when an older response arrives late', () => {
    const current = detail('2026-10-03T12:00:30Z')
    expect(newerDetail(current, detail('2026-10-03T12:00:00Z'))).toBe(current)
  })

  it('takes a detail fetched at the same moment', () => {
    const incoming = detail('2026-10-03T12:00:00Z')
    expect(newerDetail(detail('2026-10-03T12:00:00Z'), incoming)).toBe(incoming)
  })
})
