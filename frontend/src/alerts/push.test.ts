import { afterEach, describe, expect, it, vi } from 'vitest'
import { deviceId, fromBase64Url, toBase64Url } from './push'

describe('deviceId', () => {
  afterEach(() => vi.unstubAllGlobals())

  it('is made once and then kept', () => {
    const stored = new Map<string, string>()
    vi.stubGlobal('window', {
      localStorage: {
        getItem: (key: string) => stored.get(key) ?? null,
        setItem: (key: string, value: string) => stored.set(key, value),
        removeItem: (key: string) => stored.delete(key),
      },
    })

    const first = deviceId()

    expect(first).toMatch(/^[0-9a-f-]{32,36}$/)
    expect(deviceId()).toBe(first)
    expect(stored.get('canliskor.deviceId.v1')).toBe(first)
  })

  it('still gives an id where nothing can be stored', () => {
    vi.stubGlobal('window', {
      localStorage: {
        getItem: () => { throw new Error('private mode') },
        setItem: () => { throw new Error('private mode') },
      },
    })

    expect(deviceId()).toMatch(/^[0-9a-f-]{32,36}$/)
  })
})

describe('base64url', () => {
  it('turns a key into bytes and back', () => {
    const key = 'BNcRdreALRFXTkOOUHK1EtK2wtaz5Ry4YfYCA_0QTpQtUbVlUls0VJXg7A8u-Ts1XbjhazAkj7I99e8QcYP7DkM'

    expect(fromBase64Url(key)).toHaveLength(65)
    expect(toBase64Url(fromBase64Url(key))).toBe(key)
    expect(toBase64Url(fromBase64Url(key).buffer)).toBe(key)
  })
})
