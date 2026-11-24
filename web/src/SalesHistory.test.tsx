import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { SalesHistory } from './SalesHistory'
import { resetCsrf } from './api'

const sales = [
  { id: 'sale-1', number: 12, invoiceNo: 'INV-000012', date: '2026-11-23', saleType: 'Credit', total: 1500, amountPaid: 500, balance: 1000, customer: 'Asha Stores', whatsappStatus: 'ConsentRequired' },
  { id: 'sale-2', number: 11, invoiceNo: 'INV-000011', date: '2026-11-22', saleType: 'Cash', total: 300, amountPaid: 300, balance: 0, customer: 'Ravi Kumar', whatsappStatus: 'Sent' }
]
const detail = {
  sale: { ...sales[0], payMethod: 'Cash', location: 'Main Store', customerPhone: '9876543210', customerNotes: null, gstRate: null },
  lines: [{ product: 'Mixer', sku: 'MX-1', quantity: 1, unitPrice: 1500, discount: 0, lineTotal: 1500, serials: [] }]
}

beforeEach(() => {
  resetCsrf()
  vi.unstubAllGlobals()
  vi.stubGlobal('fetch', vi.fn(async (input: string) => {
    const url = String(input)
    if (url === '/api/business-profile') return new Response(JSON.stringify({
      businessName: 'Pilot Shop', address: '', city: '', state: '', pinCode: '', mobile: '', gstStatus: 'NotRegistered', gstin: null
    }))
    if (url === '/api/sales') return new Response(JSON.stringify(sales))
    if (url === '/api/sales?q=Asha') return new Response(JSON.stringify([sales[0]]))
    if (url === '/api/sales/sale-1') return new Response(JSON.stringify(detail))
    return new Response(JSON.stringify({ error: 'not found' }), { status: 404 })
  }))
})

describe('SalesHistory', () => {
  it('shows searchable sale cards and filters unpaid bills', async () => {
    render(<SalesHistory locations={[{ id: 'l1', name: 'Main Store' }]} />)
    expect(await screen.findByText('Asha Stores')).toBeInTheDocument()
    expect(screen.getByText('Ravi Kumar')).toBeInTheDocument()

    await userEvent.click(screen.getByLabelText('Unpaid only'))
    expect(screen.getByText('Asha Stores')).toBeInTheDocument()
    expect(screen.queryByText('Ravi Kumar')).not.toBeInTheDocument()

    await userEvent.clear(screen.getByLabelText('Search sales'))
    await userEvent.type(screen.getByLabelText('Search sales'), 'Asha')
    await waitFor(() => expect(screen.getByText('Asha Stores')).toBeInTheDocument())
    expect(screen.queryByText('Ravi Kumar')).not.toBeInTheDocument()
  })

  it('opens and closes the historical invoice detail dialog', async () => {
    render(<SalesHistory locations={[{ id: 'l1', name: 'Main Store' }]} />)
    await screen.findByText('Asha Stores')
    const card = screen.getByText('Asha Stores').closest('li')
    expect(card).not.toBeNull()
    await userEvent.click(within(card as HTMLElement).getByRole('button', { name: 'View bill' }))
    const dialog = await screen.findByRole('dialog', { name: 'Bill details' })
    expect(dialog).toHaveTextContent('INV-000012')
    expect(dialog).toHaveTextContent('Mixer')
    await userEvent.click(within(dialog).getByRole('button', { name: 'Close' }))
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
  })
})
