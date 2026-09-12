import { describe, expect, it } from 'vitest'
import { hashRequest } from '../src/http/hash.ts'

describe('hashRequest', () => {
  it('is independent of top-level key order', () => {
    expect(hashRequest({ a: 1, b: 2 })).toBe(hashRequest({ b: 2, a: 1 }))
  })

  it('is independent of nested-object key order', () => {
    expect(hashRequest({ outer: { a: 1, b: 2 }, x: 'y' }))
      .toBe(hashRequest({ x: 'y', outer: { b: 2, a: 1 } }))
  })

  it('still treats array order as significant', () => {
    expect(hashRequest({ items: [1, 2, 3] }))
      .not.toBe(hashRequest({ items: [3, 2, 1] }))
  })

  it('treats a structurally different body as a different hash', () => {
    expect(hashRequest({ a: 1 })).not.toBe(hashRequest({ a: 2 }))
  })
})
