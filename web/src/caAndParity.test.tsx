import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { CaWorkspace } from './CaWorkspace'
import { resetCsrf } from './api'
import { catalogueKeys, setLanguage, t } from './i18n'
import type { MessageKey } from './i18n'
import * as csvModule from './exportCsv'

type Route = () => { status?: number; body?: unknown }

function mock(routes: Record<string, Route>) {
  const calls: string[] = []
  vi.stubGlobal('fetch', vi.fn(async (url: string) => {
    calls.push(url)
    const r = routes[url.split('?')[0]]?.() ?? { status: 404, body: { error: 'not found' } }
    return new Response(r.body === undefined ? '' : JSON.stringify(r.body), { status: r.status ?? 200 })
  }))
  return calls
}

beforeEach(() => resetCsrf())
afterEach(() => { setLanguage('en'); vi.unstubAllGlobals(); vi.restoreAllMocks() })

describe('CaWorkspace (G6)', () => {
  const csrf: Record<string, Route> = { '/api/csrf': () => ({ body: { token: 't' } }) }

  it('offers one download button per statement', () => {
    mock(csrf)
    render(<CaWorkspace />)
    expect(screen.getAllByRole('button', { name: 'Download CSV' }).length).toBe(7)
  })

  it('downloads the trial balance for the chosen period and reports success', async () => {
    const spy = vi.spyOn(csvModule, 'downloadCsv').mockImplementation(() => {})
    const calls = mock({ ...csrf, '/api/reports/trial-balance': () => ({ body: { lines: [{ code: '1000', name: 'Cash', debit: 5, credit: 0 }] } }) })
    render(<CaWorkspace />)
    await userEvent.click(screen.getAllByRole('button', { name: 'Download CSV' })[0])
    expect(await screen.findByRole('status')).toHaveTextContent('Trial balance downloaded.')
    expect(spy).toHaveBeenCalledTimes(1)
    expect(calls.some(c => c.startsWith('/api/reports/trial-balance?asOf='))).toBe(true)
  })

  it('says there is nothing to export when the report is empty', async () => {
    const spy = vi.spyOn(csvModule, 'downloadCsv').mockImplementation(() => {})
    mock({ ...csrf, '/api/reports/trial-balance': () => ({ body: { lines: [] } }) })
    render(<CaWorkspace />)
    await userEvent.click(screen.getAllByRole('button', { name: 'Download CSV' })[0])
    expect(await screen.findByRole('status')).toHaveTextContent('nothing to export')
    expect(spy).not.toHaveBeenCalled()
  })

  it('shows an alert when the report cannot be loaded', async () => {
    mock({ ...csrf, '/api/reports/trial-balance': () => ({ status: 500, body: { error: 'Server broke' } }) })
    render(<CaWorkspace />)
    await userEvent.click(screen.getAllByRole('button', { name: 'Download CSV' })[0])
    expect(await screen.findByRole('alert')).toBeInTheDocument()
  })
})

describe('i18n key parity (G4)', () => {
  it('has exactly the same keys in English and Hindi', () => {
    const en = catalogueKeys('en')
    const hi = catalogueKeys('hi')
    expect(hi.filter(k => !en.includes(k))).toEqual([])
    expect(en.filter(k => !hi.includes(k))).toEqual([])
  })

  it('has no empty translations in either language', () => {
    for (const lang of ['en', 'hi'] as const) {
      setLanguage(lang)
      const empty = catalogueKeys(lang).filter(k => t(k as MessageKey).trim() === '')
      expect(empty).toEqual([])
    }
  })

  it('has no Hindi value that still contains mojibake markers', () => {
    setLanguage('hi')
    const bad = catalogueKeys('hi').filter(k => /[\u00c2\u00c3\u00e0][\u0080-\u00bf\u2018-\u203a]/.test(t(k as MessageKey)))
    expect(bad).toEqual([])
  })
})
