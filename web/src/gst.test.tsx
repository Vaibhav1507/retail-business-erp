import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { Procurement } from './Procurement'
import { InvoiceUpload } from './InvoiceUpload'
import type { InvoiceDraft, Picked } from './InvoiceUpload'
import { resetCsrf } from './api'

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
afterEach(() => { vi.unstubAllGlobals(); vi.restoreAllMocks() })

const products = [{ id: 'p1', name: 'Battery' }, { id: 'p2', name: 'Cable' }]

function draft(over: Partial<InvoiceDraft>): InvoiceDraft {
  return { fileId: 'f1', supplierName: null, supplierId: null, invoiceNumber: null, date: null, total: null, lines: [], warnings: [], ...over }
}

async function readInvoice(d: InvoiceDraft) {
  const used: { picked?: Picked[] } = {}
  mock({ ...csrf, '/api/purchases/extract': () => ({ body: d }) })
  render(<InvoiceUpload products={products} suppliers={[]} onUse={(_d, picked) => { used.picked = picked }} />)
  const file = new File(['x'], 'bill.pdf', { type: 'application/pdf' })
  await userEvent.upload(screen.getByLabelText('Upload supplier bill files'), file)
  await screen.findByLabelText('Supplier for this invoice')
  return used
}

describe('InvoiceUpload GST handling', () => {
  it('spreads invoice-level tax across lines in proportion and keeps the total exact', async () => {
    const used = await readInvoice(draft({
      taxAmount: 180, subtotal: 1000, total: 1180,
      lines: [
        { description: 'Battery', quantity: 1, unitCost: 600, amount: 600, productId: 'p1', productName: 'Battery' },
        { description: 'Cable', quantity: 1, unitCost: 400, amount: 400, productId: 'p2', productName: 'Cable' },
      ],
    }))
    await userEvent.click(screen.getByRole('button', { name: /Use 2 lines/ }))
    expect(used.picked).toEqual([
      { productId: 'p1', quantity: 1, unitCost: 600, gstAmount: 108 },
      { productId: 'p2', quantity: 1, unitCost: 400, gstAmount: 72 },
    ])
  })

  it('fills every row GST box from the invoice tax, and a single matched line gets only its own share', async () => {
    const used = await readInvoice(draft({
      taxAmount: 11271.36, total: 73890,
      lines: [
        { description: 'A', quantity: 1, unitCost: 13389.83, amount: 13389.83, productId: 'p1', productName: 'Battery' },
        { description: 'B', quantity: 1, unitCost: 12033.89, amount: 12033.89, productId: null, productName: null },
        { description: 'C', quantity: 1, unitCost: 12203.39, amount: 12203.39, productId: null, productName: null },
        { description: 'D', quantity: 1, unitCost: 24991.52, amount: 24991.52, productId: null, productName: null },
      ],
    }))
    const boxes = screen.getAllByLabelText('GST amount') as HTMLInputElement[]
    expect(boxes.map(b => b.value)).toEqual(['2410.17', '2166.10', '2196.61', '4498.48'])
    expect(boxes.reduce((a, b) => a + Number(b.value), 0).toFixed(2)).toBe('11271.36')
    expect(screen.getByText(/3 line\(s\) are skipped.*GST ₹8861\.19 will NOT be recorded/)).toBeTruthy()
    await userEvent.click(screen.getByRole('button', { name: /Use 1 line/ }))
    expect(used.picked).toEqual([{ productId: 'p1', quantity: 1, unitCost: 13389.83, gstAmount: 2410.17 }])
  })

  it('uses explicit per-line GST instead of spreading invoice tax', async () => {
    const used = await readInvoice(draft({
      taxAmount: 180,
      lines: [
        { description: 'Battery', quantity: 2, unitCost: 100, amount: 200, productId: 'p1', productName: 'Battery', gst: 36 },
        { description: 'Cable', quantity: 1, unitCost: 50, amount: 50, productId: 'p2', productName: 'Cable', gst: 9 },
      ],
    }))
    await userEvent.click(screen.getByRole('button', { name: /Use 2 lines/ }))
    expect(used.picked?.map(p => p.gstAmount)).toEqual([36, 9])
  })

  it('shows the invoice GST in the review header', async () => {
    await readInvoice(draft({ taxAmount: 180, total: 1180, lines: [{ description: 'Battery', quantity: 1, unitCost: 1000, amount: 1000, productId: 'p1', productName: 'Battery' }] }))
    expect(screen.getByText(/GST 180\.00/)).toBeTruthy()
  })

  it('adds no GST when the invoice shows none', async () => {
    const used = await readInvoice(draft({ lines: [{ description: 'Battery', quantity: 3, unitCost: 10, amount: 30, productId: 'p1', productName: 'Battery' }] }))
    await userEvent.click(screen.getByRole('button', { name: /Use 1 line/ }))
    expect(used.picked).toEqual([{ productId: 'p1', quantity: 3, unitCost: 10, gstAmount: 0 }])
  })
})

describe('Procurement manual GST', () => {
  const routes = (post: Route): Record<string, Route> => ({
    ...csrf,
    '/api/suppliers': () => ({ body: [{ id: 's1', name: 'Acme', phone: null, notes: null, isActive: true, owed: 0 }] }),
    '/api/purchases': init => (init?.method === 'POST' ? post(init) : { body: [] }),
    '/api/supplier-payments': () => ({ body: [] }),
    '/api/suppliers/s1/open-bills': () => ({ body: [] }),
  })

  async function addLine(cost: string, gst: string) {
    render(<Procurement products={products} locations={[{ id: 'l1', name: 'Shop' }]} reload={() => undefined} show="receive" />)
    await screen.findByText('Acme')
    await userEvent.selectOptions(screen.getByRole('combobox', { name: /^Product/ }), 'p1')
    await userEvent.type(screen.getByLabelText(/^Quantity/), '10')
    await userEvent.type(screen.getByLabelText(/Unit cost/), cost)
    if (gst) {
      await userEvent.click(screen.getByRole('button', { name: 'Add GST details (optional)' }))
      await userEvent.type(screen.getByLabelText('GST on this line (INR)'), gst)
    }
    await userEvent.click(screen.getByRole('button', { name: 'Add item' }))
  }

  it('sends the entered GST amount with the purchase line', async () => {
    let body: { lines?: unknown } = {}
    mock(routes(init => { body = JSON.parse(String(init?.body)); return { body: { total: 1180 } } }))
    await addLine('100', '180')
    expect(screen.getByText(/GST ₹180\.00/)).toBeTruthy()
    await userEvent.click(screen.getByRole('button', { name: 'Review purchase' }))
    expect(screen.getByRole('region', { name: 'Review purchase' })).toBeTruthy()
    expect(body).toEqual({})
    await userEvent.click(screen.getByRole('button', { name: 'Post purchase' }))
    await waitFor(() => expect(body.lines).toEqual([{ productId: 'p1', quantity: 10, unitCost: 100, gstAmount: 180 }]))
  })

  it('rejects GST that is more than the line value', async () => {
    mock(routes(() => ({ body: { total: 0 } })))
    await addLine('100', '5000')
    expect(await screen.findByText(/GST amount must be between 0 and the line value/)).toBeTruthy()
  })

  it('treats a blank GST as zero', async () => {
    mock(routes(() => ({ body: { total: 1000 } })))
    await addLine('100', '')
    expect(screen.queryByText(/\+ GST/)).toBeNull()
    expect(screen.getByText(/10 × ₹100\.00/)).toBeTruthy()
  })
})
