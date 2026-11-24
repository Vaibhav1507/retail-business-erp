import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { AfterSales } from './AfterSales'
import { resetCsrf } from './api'
import { setLanguage } from './i18n'

const receipt = {
  id: 'receipt-1', date: '2026-11-20', supplier: 'Asha Electronics',
  supplierRef: 'INV-100', total: 300, hasInvoice: false,
}
const detail = {
  receipt: { id: 'receipt-1', number: 12, date: '2026-11-20', supplier: 'Asha Electronics', supplierRef: 'INV-100', total: 300, location: 'Main shop' },
  lines: [{ productId: 'prod-1', product: 'Mixer', sku: 'MIX-001', quantity: 3, unitCost: 100, lineTotal: 300, returned: 1, remaining: 2 }],
}
const posts: { url: string; body: any; headers: Record<string, string> }[] = []

beforeEach(() => {
  resetCsrf()
  posts.length = 0
  setLanguage('en')
  vi.unstubAllGlobals()
  vi.stubGlobal('fetch', vi.fn(async (input: string, init?: RequestInit) => {
    const url = String(input)
    const method = init?.method ?? 'GET'
    if (url === '/api/csrf') return new Response(JSON.stringify({ token: 'csrf-test' }))
    if (url === '/api/purchases' && method === 'GET') return new Response(JSON.stringify([receipt]))
    if (url === '/api/purchase-returns' && method === 'GET') return new Response(JSON.stringify([]))
    if (url === '/api/purchases/receipt-1' && method === 'GET') return new Response(JSON.stringify(detail))
    if (url === '/api/purchase-returns' && method === 'POST') {
      posts.push({ url, body: init?.body ? JSON.parse(init.body as string) : {}, headers: init?.headers as Record<string, string> })
      return new Response(JSON.stringify({ total: 200 }))
    }
    return new Response(JSON.stringify({ error: 'Unexpected request: ' + method + ' ' + url }), { status: 404 })
  }))
})

afterEach(() => {
  vi.unstubAllGlobals()
  setLanguage('en')
})

describe('Return to Supplier', () => {
  it('starts from the original receipt and blocks quantities above what remains', async () => {
    const user = userEvent.setup()
    render(<AfterSales reload={vi.fn()} show="preturn" />)

    const original = await screen.findByLabelText('Original purchase')
    await user.selectOptions(original, 'receipt-1')
    const quantity = await screen.findByRole('spinbutton', { name: 'Quantity to return for Mixer' })

    expect(screen.getByText('Receipt no.')).toBeInTheDocument()
    expect(screen.getByText('Already returned')).toBeInTheDocument()
    expect(screen.getByText('Remaining on receipt')).toBeInTheDocument()

    await user.type(quantity, '3')
    expect(screen.getByText('Cannot exceed the remaining quantity of 2.')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Post supplier return' })).toBeDisabled()
    expect(posts).toHaveLength(0)

    await user.clear(quantity)
    await user.type(quantity, '2')
    expect(screen.getByRole('button', { name: 'Post supplier return' })).toBeEnabled()
    await user.click(screen.getByRole('button', { name: 'Post supplier return' }))

    expect(await screen.findByText('Supplier return posted: ₹200.00. Stock decreased.')).toBeInTheDocument()
    const post = posts.find(x => x.url === '/api/purchase-returns')!
    expect(post.body).toMatchObject({ receiptId: 'receipt-1', lines: [{ productId: 'prod-1', quantity: 2 }] })
    expect(post.headers['Idempotency-Key']).toBeTruthy()
  })
})
