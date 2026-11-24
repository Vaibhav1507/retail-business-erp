import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { Collections } from './Collections'
import { resetCsrf } from './api'

const posts: { url: string; body: any; headers: Record<string, string> }[] = []

beforeEach(() => {
  resetCsrf()
  vi.unstubAllGlobals()
  posts.length = 0
  vi.stubGlobal('fetch', vi.fn(async (input: string, init?: RequestInit) => {
    const url = String(input)
    const method = init?.method ?? 'GET'
    if (method === 'POST') posts.push({ url, body: init?.body ? JSON.parse(init.body as string) : {}, headers: init?.headers as Record<string, string> })
    if (url === '/api/csrf') return new Response(JSON.stringify({ token: 'csrf-test' }))
    if (url === '/api/customers' && method === 'GET') return new Response(JSON.stringify([{ id: 'c1', name: 'Asha Stores', owed: 1200 }]))
    if (url === '/api/customer-payments' && method === 'GET') return new Response(JSON.stringify([]))
    if (url === '/api/customers/c1/open-sales') return new Response(JSON.stringify([{ id: 'sale-1', date: '2026-11-23', invoiceNo: 'INV-000012', number: 12, open: 1200 }]))
    if (url === '/api/customer-payments' && method === 'POST') return new Response(JSON.stringify({ owed: 700 }))
    return new Response(JSON.stringify({ error: 'not found' }), { status: 404 })
  }))
})

describe('Collections', () => {
  it('records a payment against a selected bill and shows the updated balance', async () => {
    render(<Collections reload={vi.fn()} />)
    const customer = await screen.findByRole('combobox', { name: 'Customer' })
    await waitFor(() => expect(customer).toBeEnabled())
    await waitFor(() => expect(customer).toBeEnabled())
    await userEvent.type(customer, 'Asha')
    await userEvent.click(await screen.findByRole('option', { name: /Asha Stores/ }))
    expect(await screen.findByText('₹1,200.00')).toBeInTheDocument()
    await userEvent.type(screen.getByLabelText('Amount received (INR)'), '500')
    const bill = screen.getByRole('combobox', { name: 'Apply to a bill (optional)' })
    await waitFor(() => expect(bill).toBeEnabled())
    await userEvent.type(bill, 'INV-000012')
    await userEvent.click(await screen.findByRole('option', { name: /INV-000012/ }))
    await userEvent.click(screen.getByRole('button', { name: 'Record payment' }))

    expect(await screen.findByText(/Customer now owes 700\.00 INR/)).toBeInTheDocument()
    await waitFor(() => expect(posts.some(p => p.url === '/api/customer-payments')).toBe(true))
    const post = posts.find(p => p.url === '/api/customer-payments')!
    expect(post.body).toMatchObject({ customerId: 'c1', amount: 500, method: 'Cash', allocations: [{ billId: 'sale-1', amount: 500 }] })
    expect(post.headers['Idempotency-Key']).toBeTruthy()
  })

  it('does not enable recording without a customer and positive amount', async () => {
    render(<Collections reload={vi.fn()} />)
    expect(screen.queryByLabelText('Amount received (INR)')).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Record payment' })).not.toBeInTheDocument()
    const customer = screen.getByRole('combobox', { name: 'Customer' })
    await waitFor(() => expect(customer).toBeEnabled())
    await userEvent.type(customer, 'Asha')
    await userEvent.click(await screen.findByRole('option', { name: /Asha Stores/ }))
    expect(screen.getByRole('button', { name: 'Record payment' })).toBeDisabled()
  })
})
