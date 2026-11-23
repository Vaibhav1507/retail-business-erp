import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { HomePage } from './Home'
import { GstWorkspace } from './GstWorkspace'
import { TeamAccess } from './TeamAccess'
import { resetCsrf } from './api'
import { setLanguage, t } from './i18n'
import * as csvModule from './exportCsv'

type Route = (init?: RequestInit) => { status?: number; body?: unknown }

function mock(routes: Record<string, Route>) {
  vi.stubGlobal('fetch', vi.fn(async (url: string, init?: RequestInit) => {
    const path = url.split('?')[0]
    const r = routes[path]?.(init) ?? { status: 404, body: { error: 'not found' } }
    return new Response(r.body === undefined ? '' : JSON.stringify(r.body), { status: r.status ?? 200 })
  }))
}

const csrf: Record<string, Route> = { '/api/csrf': () => ({ body: { token: 't' } }) }

beforeEach(() => resetCsrf())
afterEach(() => { setLanguage('en'); vi.unstubAllGlobals(); vi.restoreAllMocks() })

describe('HomePage overview and reminders (G5, G10)', () => {
  const dash = { salesToday: { bills: 0, total: 0 }, moneyToCollect: 250, moneyToPay: 0, lowStockCount: 2, cashAndBank: 100 }

  it('shows overdue-order and unclosed-day reminders from /api/reminders', async () => {
    mock({ ...csrf, '/api/dashboard': () => ({ body: dash }), '/api/reminders': () => ({ body: [{ kind: 'orders', severity: 'warn', count: 3, amount: null }, { kind: 'dayClosing', severity: 'info', count: 2, amount: null }] }) })
    render(<HomePage />)
    expect(await screen.findByText('3 purchase order(s) are overdue.')).toBeTruthy()
    expect(screen.getByText('2 recent day(s) are not closed yet.')).toBeTruthy()
  })

  it('hides the reminders list when there is nothing to remind', async () => {
    mock({ ...csrf, '/api/dashboard': () => ({ body: dash }), '/api/reminders': () => ({ body: [{ kind: 'collect', severity: 'info', count: null, amount: 250 }] }) })
    render(<HomePage />)
    await screen.findByRole('heading', { name: "Today's overview" })
    expect(screen.queryByLabelText('Reminders')).toBeNull()
  })

  it('still renders the home screen when reminders fail to load', async () => {
    mock({ ...csrf, '/api/dashboard': () => ({ body: dash }), '/api/reminders': () => ({ status: 500, body: { error: 'boom' } }) })
    render(<HomePage />)
    await screen.findByRole('heading', { name: "Today's overview" })
    expect(screen.queryByLabelText('Reminders')).toBeNull()
  })

  it('renders live dashboard figures in the overview cards', async () => {
    mock({ ...csrf, '/api/dashboard': () => ({ body: dash }), '/api/reminders': () => ({ body: [] }) })
    render(<HomePage />)
    expect(await screen.findByRole('link', { name: /Sales today/ })).toHaveTextContent('₹0.00')
    expect(screen.getByRole('link', { name: /Money to collect/ })).toHaveTextContent('₹250.00')
    expect(screen.getByRole('link', { name: /Low stock/ })).toHaveTextContent('2')
    expect(screen.getByRole('link', { name: /Money you need to pay suppliers/ })).toHaveTextContent('₹0.00')
    expect(screen.getByRole('link', { name: /Cash and bank/ })).toHaveTextContent('₹100.00')
  })

  it('shows an error message when the dashboard fails', async () => {
    mock({ ...csrf, '/api/dashboard': () => ({ status: 500, body: { error: 'x' } }), '/api/reminders': () => ({ body: [] }) })
    render(<HomePage />)
    expect(await screen.findByRole('alert')).toBeTruthy()
  })
})

describe('GstWorkspace (G3)', () => {
  const summary = {
    from: '2026-04-01', to: '2026-04-30', note: 'Output tax only.',
    lines: [{ rate: 18, interState: false, invoices: 2, taxable: 100, cgst: 9, sgst: 9, igst: 0, tax: 18, total: 118 }],
    totals: { taxable: 100, cgst: 9, sgst: 9, igst: 0, tax: 18 },
  }

  it('loads the summary and shows the tax split', async () => {
    mock({ ...csrf, '/api/reports/gst-summary': () => ({ body: summary }) })
    render(<GstWorkspace />)
    await userEvent.click(screen.getByRole('button', { name: 'Show GST summary' }))
    expect(await screen.findByText('Intra-state')).toBeTruthy()
    expect(screen.getByText('Output tax only.')).toBeTruthy()
  })

  it('shows the empty message when the period has no GST invoices', async () => {
    mock({ ...csrf, '/api/reports/gst-summary': () => ({ body: { ...summary, lines: [], totals: { taxable: 0, cgst: 0, sgst: 0, igst: 0, tax: 0 } } }) })
    render(<GstWorkspace />)
    await userEvent.click(screen.getByRole('button', { name: 'Show GST summary' }))
    expect(await screen.findByText('No GST invoices in this period.')).toBeTruthy()
    expect((screen.getByRole('button', { name: 'Download CSV for CA' }) as HTMLButtonElement).disabled).toBe(true)
  })

  it('downloads a CSV for the CA once data is loaded', async () => {
    const spy = vi.spyOn(csvModule, 'downloadCsv').mockImplementation(() => {})
    mock({ ...csrf, '/api/reports/gst-summary': () => ({ body: summary }) })
    render(<GstWorkspace />)
    await userEvent.click(screen.getByRole('button', { name: 'Show GST summary' }))
    await screen.findByText('Intra-state')
    await userEvent.click(screen.getByRole('button', { name: 'Download CSV for CA' }))
    expect(spy).toHaveBeenCalledTimes(1)
  })

  it('renders the Hindi button label when the language is Hindi', () => {
    setLanguage('hi')
    mock({ ...csrf })
    render(<GstWorkspace />)
    expect(screen.queryByRole('button', { name: 'Show GST summary' })).toBeNull()
    expect(screen.getByRole('button', { name: t('gst.show') })).toBeTruthy()
    expect(t('gst.show')).not.toBe('Show GST summary')
  })
})

describe('TeamAccess (G1)', () => {
  const roles = [{ role: 'Owner', permissions: [] }, { role: 'Cashier', permissions: ['sales.create'] }]
  const members = [
    { userId: 'u1', email: 'owner@example.test', role: 'Owner', active: true, isYou: true },
    { userId: 'u2', email: 'cashier@example.test', role: 'Cashier', active: true, isYou: false },
  ]

  it('lists members and only offers disable for non-owner others', async () => {
    mock({ ...csrf, '/api/staff': () => ({ body: members }), '/api/staff/roles': () => ({ body: roles }) })
    render(<TeamAccess />)
    expect(await screen.findByText('cashier@example.test')).toBeTruthy()
    expect(screen.getAllByRole('button', { name: 'Disable' }).length).toBe(1)
  })

  it('keeps the add button disabled until email and password are entered', async () => {
    mock({ ...csrf, '/api/staff': () => ({ body: members }), '/api/staff/roles': () => ({ body: roles }) })
    render(<TeamAccess />)
    await screen.findByText('cashier@example.test')
    const add = screen.getByRole('button', { name: 'Add team member' }) as HTMLButtonElement
    expect(add.disabled).toBe(true)
    await userEvent.type(screen.getByLabelText('Email'), 'new@example.test')
    await userEvent.type(screen.getByLabelText('Temporary password (12+ characters)'), 'a-long-password-1')
    expect(add.disabled).toBe(false)
  })

  it('shows an alert when staff cannot be loaded', async () => {
    mock({ ...csrf, '/api/staff': () => ({ status: 403, body: { error: 'Forbidden' } }), '/api/staff/roles': () => ({ status: 403, body: { error: 'Forbidden' } }) })
    render(<TeamAccess />)
    await waitFor(() => expect(screen.getByRole('alert')).toBeTruthy())
  })
})
