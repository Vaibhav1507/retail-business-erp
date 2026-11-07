import { beforeEach, describe, expect, it, vi } from 'vitest'
import { api, ApiError, resetCsrf } from './api'

type Call = { url: string; init?: RequestInit }
let calls: Call[]

function mockFetch(handler: (url: string, init?: RequestInit) => { status?: number; body?: unknown }) {
  calls = []
  vi.stubGlobal('fetch', vi.fn(async (url: string, init?: RequestInit) => {
    calls.push({ url, init })
    const r = handler(url, init)
    const text = r.body === undefined ? '' : JSON.stringify(r.body)
    return new Response(text, { status: r.status ?? 200 })
  }))
}

beforeEach(() => { resetCsrf(); vi.unstubAllGlobals() })

describe('api client', () => {
  it('does not send a CSRF token on GET', async () => {
    mockFetch(() => ({ body: [] }))
    await api('GET', '/products')
    expect(calls).toHaveLength(1)
    expect((calls[0].init?.headers as Record<string, string>)['X-CSRF-TOKEN']).toBeUndefined()
  })

  it('fetches a CSRF token once and sends it with idempotency key on POST', async () => {
    mockFetch(url => (url === '/api/csrf' ? { body: { token: 'tok' } } : { body: { ok: true } }))
    await api('POST', '/opening-stock', { a: 1 }, 'key-12345678')
    await api('POST', '/products', { a: 1 })
    expect(calls.filter(c => c.url === '/api/csrf')).toHaveLength(1)
    const h = calls[1].init?.headers as Record<string, string>
    expect(h['X-CSRF-TOKEN']).toBe('tok')
    expect(h['Idempotency-Key']).toBe('key-12345678')
    expect(h['Content-Type']).toBe('application/json')
    expect(calls[1].init?.credentials).toBe('same-origin')
  })

  it('throws ApiError carrying server message and status', async () => {
    mockFetch(url => (url === '/api/csrf' ? { body: { token: 't' } } : { status: 409, body: { error: 'Opening stock is already posted for this location.' } }))
    const err = (await api('POST', '/opening-stock', {}, 'key-12345678').catch((e: unknown) => e)) as ApiError
    expect(err).toBeInstanceOf(ApiError)
    expect(err.status).toBe(409)
    expect(err.message).toBe('Opening stock is already posted for this location.')
  })

  it('refreshes the CSRF token after a 400', async () => {
    let n = 0
    mockFetch(url => (url === '/api/csrf' ? { body: { token: `t${++n}` } } : { status: 400, body: { error: 'Invalid CSRF token.' } }))
    await api('POST', '/x', {}).catch(() => undefined)
    await api('POST', '/x', {}).catch(() => undefined)
    expect(calls.filter(c => c.url === '/api/csrf')).toHaveLength(2)
  })
})

