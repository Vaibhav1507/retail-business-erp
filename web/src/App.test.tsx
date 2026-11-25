import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import App from './App'
import { resetCsrf } from './api'

const product = { id: 'p1', name: 'Test Cable', category: 'Cables', isGeneric: true, sku: 'SKU-1', unit: 'pcs', brand: null, model: null }
const location = { id: 'l1', name: 'Main Store' }
let posts: { url: string; body: unknown; headers: Record<string, string> }[]

function mock(routes: Record<string, (init?: RequestInit) => { status?: number; body?: unknown }>) {
  posts = []
  vi.stubGlobal('fetch', vi.fn(async (url: string, init?: RequestInit) => {
    if (init?.method === 'POST') posts.push({ url, body: init.body ? JSON.parse(init.body as string) : undefined, headers: init.headers as Record<string, string> })
    const r = routes[url]?.(init) ?? { status: 404, body: { error: 'not found' } }
    return new Response(r.body === undefined ? '' : JSON.stringify(r.body), { status: r.status ?? 200 })
  }))
}

const signedIn = {
  '/api/csrf': () => ({ body: { token: 't' } }),
  '/api/me': () => ({ body: { email: 'owner@example.test', tenantId: 't1', profileComplete: true, businessCategory: null } }),
  '/api/products': () => ({ body: [product] }),
  '/api/locations': () => ({ body: [location] }),
  '/api/stock': () => ({ body: [] }),
  '/api/business-profile': () => ({ body: { exists: true, businessName: 'Test Shop', businessCategory: 'electronics_mobile', onboardingCompleted: true, language: 'en' } }),
  '/api/purchases': () => ({ body: [] }),
  '/api/sales': () => ({ body: [] }),
}

beforeEach(() => { resetCsrf(); vi.unstubAllGlobals(); window.location.hash = '' })

describe('App', () => {
  it('shows the sign-in form when not authenticated', async () => {
    mock({ '/api/me': () => ({ status: 401, body: { error: 'x' } }) })
    render(<App />)
    expect(await screen.findByRole('form', { name: 'Sign in' })).toBeInTheDocument()
  })

  it('shows business setup when signed in without a business', async () => {
    mock({ '/api/me': () => ({ body: { email: 'a@example.test', tenantId: null } }) })
    render(<App />)
    expect(await screen.findByRole('form', { name: 'Business profile' })).toBeInTheDocument()
  })

  it('shows a retry state instead of signing out during a temporary server outage', async () => {
    let attempts = 0
    vi.stubGlobal('fetch', vi.fn(async (url: string) => {
      if (url === '/api/me') {
        attempts++
        if (attempts === 1) return new Response(JSON.stringify({ error: 'Temporarily unavailable' }), { status: 503 })
        return new Response(JSON.stringify({ email: 'a@example.test', tenantId: null, profileComplete: false, businessCategory: null }), { status: 200 })
      }
      return new Response('', { status: 404 })
    }))
    render(<App />)
    const retry = await screen.findByRole('button', { name: 'Retry connection' })
    expect(screen.queryByRole('form', { name: 'Sign in' })).not.toBeInTheDocument()
    await userEvent.click(retry)
    expect(await screen.findByRole('form', { name: 'Business profile' })).toBeInTheDocument()
  })

  it('shows the action-first Sell hub with all four existing destinations', async () => {
    window.location.hash = '#/sell/menu'
    mock(signedIn)
    render(<App />)
    expect(await screen.findByRole('heading', { name: 'Billing / Sales' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /Create new bill/ })).toHaveAttribute('href', '#/sell/bill')
    expect(screen.getByRole('link', { name: /Sales history/ })).toHaveAttribute('href', '#/sell/bills')
    expect(screen.getByRole('link', { name: /Collect payment/ })).toHaveAttribute('href', '#/sell/collect')
    expect(screen.getByRole('link', { name: /Customer returns/ })).toHaveAttribute('href', '#/sell/return')
    expect(screen.getByRole('link', { name: /Create new bill/ })).toHaveClass('sell-hub-primary')
  })

  it('places the assistant in a floating launcher and keeps Sell subroutes to one page heading', async () => {
    window.location.hash = '#/sell/menu'
    mock(signedIn)
    const { container, unmount } = render(<App />)
    expect(await screen.findByRole('heading', { name: 'Billing / Sales' })).toBeInTheDocument()
    const assistant = await screen.findByRole('button', { name: 'Open assistant' })
    expect(container.querySelector('.assistant-launcher')).toBe(assistant)
    expect(container.querySelector('.topbar')).not.toContainElement(assistant)
    expect(assistant).toHaveAttribute('aria-expanded', 'false')

    unmount()
    window.location.hash = '#/sell/collect'
    mock({
      ...signedIn,
      '/api/customers': () => ({ body: [] }),
      '/api/customer-payments': () => ({ body: [] })
    })
    render(<App />)
    expect(await screen.findByRole('heading', { name: 'Collect payment' })).toBeInTheDocument()
    expect(screen.queryByRole('heading', { name: 'Billing / Sales' })).not.toBeInTheDocument()
    expect(screen.getByRole('link', { name: /Billing \/ Sales/ })).toHaveAttribute('href', '#/sell/menu')
  })

  it('shows the signed-in device management screen', async () => {
    window.location.hash = '#/devices'
    mock({ ...signedIn, '/api/auth/devices': () => ({ body: [] }) })
    render(<App />)
    expect(await screen.findByRole('heading', { name: 'Signed-in devices' })).toBeInTheDocument()
    expect(await screen.findByText(/No devices have been registered/)).toBeInTheDocument()
  })

  it('clears tenant-specific data after sign out', async () => {
    mock({ ...signedIn, '/api/auth/logout': () => ({}) })
    render(<App />)
    await userEvent.click(await screen.findByRole('button', { name: 'Profile menu' }))
    await userEvent.click(await screen.findByRole('menuitem', { name: 'Sign out' }))
    expect(await screen.findByRole('form', { name: 'Sign in' })).toBeInTheDocument()
    expect(screen.queryByText('Test Cable (SKU-1)')).not.toBeInTheDocument()
  })
})

describe('Opening stock form', () => {
  async function open() {
    window.location.hash = '#/stock/opening'
    mock(signedIn)
    render(<App />)
    const section = await screen.findByRole('region', { name: 'Opening stock' })
    await waitFor(() => expect(within(section).getByRole('option', { name: 'Test Cable (SKU-1)' })).toBeInTheDocument())
    return section
  }

  it('disables posting until a line is added', async () => {
    const s = await open()
    expect(within(s).getByRole('button', { name: 'Review starting stock' })).toBeDisabled()
  })

  it('rejects zero cost and zero quantity lines client-side', async () => {
    const s = await open()
    await userEvent.selectOptions(within(s).getByLabelText('Product'), 'p1')
    await userEvent.type(within(s).getByLabelText('Quantity'), '5')
    await userEvent.type(within(s).getByLabelText('Cost for one item (₹)'), '0')
    await userEvent.click(within(s).getByRole('button', { name: 'Add item' }))
    expect(await within(s).findByRole('alert')).toHaveTextContent('quantity and cost greater than zero')
    expect(within(s).getByRole('button', { name: 'Review starting stock' })).toBeDisabled()
  })

  it('posts a line with an idempotency key and shows the server total', async () => {
    window.location.hash = '#/stock/opening'
    mock({ ...signedIn, '/api/opening-stock': () => ({ body: { documentId: 'd1', total: 255, replayed: false } }) })
    render(<App />)
    const s = await screen.findByRole('region', { name: 'Opening stock' })
    await waitFor(() => expect(within(s).getByRole('option', { name: 'Test Cable (SKU-1)' })).toBeInTheDocument())
    await userEvent.selectOptions(within(s).getByLabelText('Product'), 'p1')
    await userEvent.type(within(s).getByLabelText('Quantity'), '10')
    await userEvent.type(within(s).getByLabelText('Cost for one item (₹)'), '25.5')
    await userEvent.click(within(s).getByRole('button', { name: 'Add item' }))
    await userEvent.click(within(s).getByRole('button', { name: 'Review starting stock' }))
    expect(await within(s).findByRole('region', { name: 'Review opening stock' })).toBeInTheDocument()
    await userEvent.click(within(s).getByRole('button', { name: 'Confirm & post opening stock' }))
    expect(await within(s).findByRole('status')).toHaveTextContent('255.00 INR')
    const post = posts.find(p => p.url === '/api/opening-stock')!
    expect(post.headers['Idempotency-Key']).toMatch(/.{8,}/)
    expect(post.body).toMatchObject({ locationId: 'l1', lines: [{ productId: 'p1', quantity: 10, unitCost: 25.5 }] })
    expect(post.body).not.toHaveProperty('total')
  })

  it('shows the server conflict when opening stock is already posted', async () => {
    window.location.hash = '#/stock/opening'
    mock({ ...signedIn, '/api/opening-stock': () => ({ status: 409, body: { error: 'Opening stock is already posted for this location.' } }) })
    render(<App />)
    const s = await screen.findByRole('region', { name: 'Opening stock' })
    await waitFor(() => expect(within(s).getByRole('option', { name: 'Test Cable (SKU-1)' })).toBeInTheDocument())
    await userEvent.selectOptions(within(s).getByLabelText('Product'), 'p1')
    await userEvent.type(within(s).getByLabelText('Quantity'), '1')
    await userEvent.type(within(s).getByLabelText('Cost for one item (₹)'), '1')
    await userEvent.click(within(s).getByRole('button', { name: 'Add item' }))
    await userEvent.click(within(s).getByRole('button', { name: 'Review starting stock' }))
    await userEvent.click(within(s).getByRole('button', { name: 'Confirm & post opening stock' }))
    expect(await within(s).findByRole('alert')).toHaveTextContent('already posted')
  })
})
