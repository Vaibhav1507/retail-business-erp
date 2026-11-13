import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { GenerateInvoice } from './GenerateInvoice'
import { resetCsrf } from './api'

const calls: { url: string; method: string }[] = []
const products = [{ id: 'p1', name: 'Test Cable' }]
const locations = [{ id: 'l1', name: 'Main Store' }]

beforeEach(() => {
  resetCsrf()
  vi.unstubAllGlobals()
  calls.length = 0
  Object.defineProperty(window, 'print', { configurable: true, value: vi.fn() })
  vi.stubGlobal('fetch', vi.fn(async (url: string, init?: RequestInit) => {
    const method = init?.method ?? 'GET'
    calls.push({ url, method })
    if (url === '/api/csrf') return new Response(JSON.stringify({ token: 'csrf-test' }))
    if (url === '/api/business-profile') return new Response(JSON.stringify({
      businessName: 'Pilot Shop', address: 'Main Road', city: 'Kanpur', state: 'Uttar Pradesh',
      pinCode: '208001', mobile: '9876543210', gstin: null
    }))
    if (url === '/api/products') return new Response(JSON.stringify(products))
    if (url === '/api/sales' && method === 'POST') return new Response(JSON.stringify({ id: 'sale-1' }))
    if (url === '/api/sales/sale-1') return new Response(JSON.stringify({
      sale: { number: 1, whatsappStatus: 'ConsentRequired' }
    }))
    return new Response(JSON.stringify({ error: 'not found' }), { status: 404 })
  }))
})

describe('GenerateInvoice WhatsApp independence', () => {
  it('keeps automatic browser printing when WhatsApp consent is not confirmed', async () => {
    const reload = vi.fn()
    render(<GenerateInvoice products={products} locations={locations} reload={reload} />)

    await userEvent.type(await screen.findByPlaceholderText('Search or type a new product'), 'Test Cable')
    await userEvent.type(screen.getByLabelText('Price (INR)'), '100')
    await userEvent.click(screen.getByRole('button', { name: 'Add product' }))
    await userEvent.click(screen.getByRole('button', { name: 'Generate invoice' }))
    await userEvent.click(await screen.findByRole('button', { name: 'Confirm & print' }))

    expect(await screen.findByText(/WhatsApp invoice: Permission not confirmed/)).toBeInTheDocument()
    await waitFor(() => expect(window.print).toHaveBeenCalledTimes(1), { timeout: 2000 })
    expect(calls).toContainEqual({ url: '/api/sales', method: 'POST' })
    expect(calls.some(x => x.url.includes('/whatsapp'))).toBe(false)
    expect(reload).toHaveBeenCalled()
  })
})
