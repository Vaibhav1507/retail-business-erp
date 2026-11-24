import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { CustomerReturns } from './CustomerReturns'
import { resetCsrf } from './api'

const posts: { url: string; body: any; headers: Record<string, string> }[] = []
const sale = { id: 'sale-1', number: 12, invoiceNo: 'INV-000012', date: '2026-11-23', customer: 'Asha Stores', total: 1500, cancelled: false }

beforeEach(() => {
  resetCsrf()
  vi.unstubAllGlobals()
  posts.length = 0
  vi.stubGlobal('fetch', vi.fn(async (input: string, init?: RequestInit) => {
    const url = String(input)
    const method = init?.method ?? 'GET'
    if (method === 'POST') posts.push({ url, body: init?.body ? JSON.parse(init.body as string) : {}, headers: init?.headers as Record<string, string> })
    if (url === '/api/csrf') return new Response(JSON.stringify({ token: 'csrf-test' }))
    if (url === '/api/sales') return new Response(JSON.stringify([sale]))
    if (url === '/api/sale-returns' && method === 'GET') return new Response(JSON.stringify([]))
    if (url === '/api/sales/sale-1/returnable') return new Response(JSON.stringify([
      { saleLineId: 'line-1', productId: 'p1', product: 'Mixer', sold: 2, returned: 0, remaining: 2 }
    ]))
    if (url === '/api/sale-returns' && method === 'POST') return new Response(JSON.stringify({ total: 750, balanceCredit: 500, cashRefund: 250 }))
    if (url === '/api/sales/sale-1/cancel' && method === 'POST') return new Response(JSON.stringify({ cancelled: true }))
    return new Response(JSON.stringify({ error: 'not found' }), { status: 404 })
  }))
})

describe('CustomerReturns', () => {
  it('prevents quantities above remaining and posts a return by sale-line id', async () => {
    render(<CustomerReturns reload={vi.fn()} />)
    const saleSearch = await screen.findByRole('combobox', { name: 'Choose the original bill' })
    await userEvent.type(saleSearch, 'Asha')
    await userEvent.click(await screen.findByRole('option', { name: /INV-000012/ }))
    const quantity = await screen.findByLabelText('Return quantity')
    await userEvent.type(quantity, '3')
    await userEvent.click(screen.getByRole('button', { name: 'Post customer return' }))
    expect(await screen.findByText('Cannot return more than 2.')).toBeInTheDocument()
    expect(posts.some(p => p.url === '/api/sale-returns')).toBe(false)

    await userEvent.clear(quantity)
    await userEvent.type(quantity, '1')
    await userEvent.click(screen.getByRole('button', { name: 'Post customer return' }))
    expect(await screen.findByText(/Customer balance credit: ₹500\.00/)).toBeInTheDocument()
    const post = posts.find(p => p.url === '/api/sale-returns')!
    expect(post.body).toMatchObject({ saleId: 'sale-1', lines: [{ saleLineId: 'line-1', productId: 'p1', quantity: 1 }] })
    expect(post.headers['Idempotency-Key']).toBeTruthy()
  })

  it('keeps return history collapsed until the shopkeeper opens it', async () => {
    render(<CustomerReturns reload={vi.fn()} />)
    const history = screen.getByText('Customer return history').closest('details')
    expect(history).not.toHaveAttribute('open')
    await userEvent.click(screen.getByText('Customer return history'))
    expect(history).toHaveAttribute('open')
    expect(await screen.findByText('No customer returns recorded yet.')).toBeInTheDocument()
  })

  it('requires a separate explicit confirmation to cancel the entire sale', async () => {
    render(<CustomerReturns reload={vi.fn()} />)
    const saleSearch = await screen.findByRole('combobox', { name: 'Choose the original bill' })
    await userEvent.type(saleSearch, 'Asha')
    await userEvent.click(await screen.findByRole('option', { name: /INV-000012/ }))
    await userEvent.click(await screen.findByRole('button', { name: 'Cancel entire sale' }))
    const dialog = screen.getByRole('dialog', { name: 'Cancel entire sale?' })
    expect(dialog).toHaveTextContent('restores stock')
    expect(posts.some(p => p.url.includes('/cancel'))).toBe(false)
    await userEvent.click(within(dialog).getByRole('button', { name: 'Keep sale' }))
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Cancel entire sale' }))
    await userEvent.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Confirm sale cancellation' }))
    await waitFor(() => expect(posts.some(p => p.url === '/api/sales/sale-1/cancel')).toBe(true))
  })
})
