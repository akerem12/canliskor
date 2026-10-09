import { afterEach, describe, expect, it, vi } from 'vitest'
import { notificationId, openAppUrl } from './notifications'

describe('notificationId', () => {
  it('gives the same tag the same number, so a repeat replaces the earlier notification', () => {
    expect(notificationId('401888379:reminder')).toBe(notificationId('401888379:reminder'))
    expect(notificationId('401888379:reminder')).not.toBe(notificationId('401888379:lineups'))
  })

  it('is a number Android accepts: a non-negative 32-bit integer', () => {
    for (const tag of ['', 'a', '401888379:score:3', 'x'.repeat(500)]) {
      const id = notificationId(tag)
      expect(Number.isInteger(id)).toBe(true)
      expect(id).toBeGreaterThanOrEqual(0)
      expect(id).toBeLessThanOrEqual(2 ** 31)
    }
  })
})

describe('openAppUrl', () => {
  afterEach(() => vi.unstubAllGlobals())

  function stubWindow() {
    const pushState = vi.fn()
    const dispatchEvent = vi.fn()
    vi.stubGlobal('window', { history: { pushState }, dispatchEvent })
    vi.stubGlobal('PopStateEvent', class {})
    return { pushState, dispatchEvent }
  }

  it('opens a page of the app and tells the router', () => {
    const { pushState, dispatchEvent } = stubWindow()

    openAppUrl('/?league=tur.1&match=401888379&tab=lineups')

    expect(pushState).toHaveBeenCalledWith(null, '', '/?league=tur.1&match=401888379&tab=lineups')
    expect(dispatchEvent).toHaveBeenCalledOnce()
  })

  it('ignores anything that is not a page of this app', () => {
    const { pushState } = stubWindow()

    for (const url of ['https://example.org/', '//example.org/', 'javascript:alert(1)', '', undefined, 42]) openAppUrl(url)

    expect(pushState).not.toHaveBeenCalled()
  })
})
