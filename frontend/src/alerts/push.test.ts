import { describe, expect, it } from 'vitest'
import { fromBase64Url, toBase64Url } from './push'

describe('base64url', () => {
  it('turns a key into bytes and back', () => {
    const key = 'BNcRdreALRFXTkOOUHK1EtK2wtaz5Ry4YfYCA_0QTpQtUbVlUls0VJXg7A8u-Ts1XbjhazAkj7I99e8QcYP7DkM'

    expect(fromBase64Url(key)).toHaveLength(65)
    expect(toBase64Url(fromBase64Url(key))).toBe(key)
    expect(toBase64Url(fromBase64Url(key).buffer)).toBe(key)
  })
})
