import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { Procurement } from './Procurement'
import { BuyPage } from './Pages'
import { resetCsrf } from './api'
import { setLanguage } from './i18n'

const supplier = { id: 'sup-1', name: 'Asha Electronics', phone: '9876543210', notes: null, isActive: true, owed: 1500 }
const location = { id: 'loc-1', name: 'Main shop' }
const product = { id: 'prod-1', name: 'Mixer' }
const receipts = [{ id: 'receipt-1', date: '2026-11-23', supplier: supplier.name, supplierRef: 'INV-100', total: 220, hasInvoice: true }]

type Post = { url: string; body: any; headers: Record<string, string> }
let posts: Post[] = []

function mockProcurement(
  openBills: { id: string; date: string; reference: string | null; open: number }[] = [],
  extract?: () => Promise<Response>,
) {
  vi.stubGlobal('fetch', vi.fn(async (input: string, init?: RequestInit) => {
    const url = String(input)
    const method = init?.method ?? 'GET'
    if (url === '/api/csrf') return new Response(JSON.stringify({ token: 'csrf-test' }))
    if (url === '/api/suppliers' && method === 'GET') return new Response(JSON.stringify([supplier]))
    if (url === '/api/purchases' && method === 'GET') return new Response(JSON.stringify(receipts))
    if (url === '/api/supplier-payments' && method === 'GET') return new Response(JSON.stringify([{ id: 'payment-old', date: '2026-11-22', supplier: supplier.name, amount: 350, method: 'UPI', reference: 'UPI-12' }]))
    if (url === '/api/suppliers/sup-1/open-bills' && method === 'GET') return new Response(JSON.stringify(openBills))
    if (url === '/api/suppliers/sup-1/statement' && method === 'GET') return new Response(JSON.stringify({ supplier: supplier.name, outstanding: 1500, entries: [] }))
    if (url === '/api/purchases/extract' && method === 'POST' && extract) return extract()
    if (method === 'POST') {
      posts.push({ url, body: init?.body ? JSON.parse(init.body as string) : {}, headers: init?.headers as Record<string, string> })
      if (url === '/api/supplier-payments') return new Response(JSON.stringify({ id: 'payment-1', owed: 500 }))
      if (url === '/api/purchases') return new Response(JSON.stringify({ total: 220 }))
    }
    return new Response(JSON.stringify({ error: 'Unexpected request: ' + method + ' ' + url }), { status: 404 })
  }))
}

beforeEach(() => {
  resetCsrf()
  posts = []
  setLanguage('en')
  vi.unstubAllGlobals()

})

afterEach(() => {
  vi.unstubAllGlobals()
  setLanguage('en')
})

describe('Procurement Buy screens', () => {
  it('requires the shopkeeper to choose a supplier before paying', async () => {
    mockProcurement([{ id: 'bill-1', date: '2026-11-23', reference: 'INV-100', open: 900 }])
    render(<Procurement products={[]} locations={[]} reload={vi.fn()} show="pay" />)

    const supplierSearch = await screen.findByRole('combobox', { name: 'Supplier' })
    expect(supplierSearch).toHaveValue('')
    expect(screen.getByText('Choose a supplier to see the amount due.')).toBeInTheDocument()
    expect(screen.queryByLabelText('Amount to pay (INR)')).not.toBeInTheDocument()
    expect(posts).toHaveLength(0)
  })

  it('allocates a payment to the selected bill without exceeding that bill balance', async () => {
    const user = userEvent.setup()
    mockProcurement([{ id: 'bill-1', date: '2026-11-23', reference: 'INV-100', open: 600 }])
    render(<Procurement products={[]} locations={[]} reload={vi.fn()} show="pay" />)

    const supplierSearch = await screen.findByRole('combobox', { name: 'Supplier' })
    await user.type(supplierSearch, 'Asha')
    await user.click(await screen.findByRole('option', { name: /Asha Electronics/ }))

    const billSearch = await screen.findByRole('combobox', { name: 'Apply to purchase bill (optional)' })
    await user.type(billSearch, 'INV-100')
    await user.click(await screen.findByRole('option', { name: /INV-100/ }))
    await user.type(await screen.findByLabelText('Amount to pay (INR)'), '1000')
    expect(screen.getByText(/Will allocate ₹600.00/)).toBeInTheDocument()
    expect(screen.getByText(/Any remaining amount stays on the supplier account/)).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Save payment' }))
    await screen.findByText('Payment recorded. New supplier balance: ₹500.00.')
    const post = posts.find(x => x.url === '/api/supplier-payments')!
    expect(post.body).toMatchObject({
      supplierId: 'sup-1',
      amount: 1000,
      allocations: [{ billId: 'bill-1', amount: 600 }],
    })
    expect(post.headers['Idempotency-Key']).toBeTruthy()
  })

  it('keeps a payment on the supplier account when no bill is selected', async () => {
    const user = userEvent.setup()
    mockProcurement([])
    render(<Procurement products={[]} locations={[]} reload={vi.fn()} show="pay" />)

    const supplierSearch = await screen.findByRole('combobox', { name: 'Supplier' })
    await user.type(supplierSearch, 'Asha')
    await user.click(await screen.findByRole('option', { name: /Asha Electronics/ }))
    await user.type(await screen.findByLabelText('Amount to pay (INR)'), '350')
    await user.click(screen.getByRole('button', { name: 'Save payment' }))

    await screen.findByText('Payment recorded. New supplier balance: ₹500.00.')
    const post = posts.find(x => x.url === '/api/supplier-payments')!
    expect(post.body.supplierId).toBe('sup-1')
    expect(post.body.amount).toBe(350)
    expect(Object.hasOwn(post.body, 'allocations')).toBe(false)
  })

  it('records a reviewed purchase with its estimated GST-inclusive total', async () => {
    const user = userEvent.setup()
    mockProcurement()
    const reload = vi.fn()
    render(<Procurement products={[product]} locations={[location]} reload={reload} show="receive" />)

    await screen.findByRole('combobox', { name: 'Supplier' })
    await user.selectOptions(screen.getByRole('combobox', { name: 'Supplier' }), 'sup-1')
    await user.selectOptions(screen.getByRole('combobox', { name: 'Product' }), 'prod-1')
    await user.type(screen.getByRole('textbox', { name: 'Quantity' }), '2')
    await user.type(screen.getByRole('textbox', { name: 'Unit cost (INR, before GST)' }), '100')
    await user.click(screen.getByRole('button', { name: 'Add GST details (optional)' }))
    await user.type(screen.getByLabelText('GST on this line (INR)'), '20')
    await user.click(screen.getByRole('button', { name: 'Add item' }))

    expect(screen.getAllByText('₹220.00').length).toBeGreaterThan(0)
    await user.click(screen.getByRole('button', { name: 'Review purchase' }))
    expect(screen.getByRole('region', { name: 'Review purchase' })).toHaveTextContent('Mixer')
    await user.click(screen.getByRole('button', { name: 'Post purchase' }))

    await screen.findByText('Purchase receipt posted.')
    const post = posts.find(x => x.url === '/api/purchases')!
    expect(post.body).toMatchObject({
      supplierId: 'sup-1',
      locationId: 'loc-1',
      lines: [{ productId: 'prod-1', quantity: 2, unitCost: 100, gstAmount: 20 }],
    })
    expect(post.headers['Idempotency-Key']).toBeTruthy()
    expect(reload).toHaveBeenCalled()
  })

  it('shows separate receipt and payment history and requires a supplier for the statement', async () => {
    const user = userEvent.setup()
    mockProcurement()
    render(<Procurement products={[]} locations={[]} reload={vi.fn()} show="history" />)

    expect(await screen.findByRole('heading', { name: 'Past purchases' })).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'Purchase receipts' })).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'Supplier payments' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'View supplier bill' })).toHaveAttribute('href', '/api/purchases/receipt-1/invoice')
    expect(screen.getByText('UPI')).toBeInTheDocument()

    const supplierSearch = screen.getByRole('combobox', { name: 'Supplier statement' })
    const statementButton = screen.getByRole('button', { name: 'View supplier statement' })
    expect(statementButton).toBeDisabled()
    await user.type(supplierSearch, 'Asha')
    await user.click(await screen.findByRole('option', { name: /Asha Electronics/ }))
    expect(statementButton).toBeEnabled()
    await user.click(statementButton)
    expect(await screen.findByRole('region', { name: 'Supplier statement' })).toBeInTheDocument()
  })

  it('rejects payment amounts with more than two decimal places', async () => {
    const user = userEvent.setup()
    mockProcurement([])
    render(<Procurement products={[]} locations={[]} reload={vi.fn()} show="pay" />)
    const supplierSearch = await screen.findByRole('combobox', { name: 'Supplier' })
    await user.type(supplierSearch, 'Asha')
    await user.click(await screen.findByRole('option', { name: /Asha Electronics/ }))
    await user.type(await screen.findByLabelText('Amount to pay (INR)'), '100.123')
    expect(screen.getByText('Enter an amount with no more than 2 decimal places.')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Save payment' })).toBeDisabled()
    expect(posts).toHaveLength(0)
  })

  it('offers camera and multi-page upload actions with inline required markers', async () => {
    mockProcurement()
    render(<Procurement products={[product]} locations={[location]} reload={vi.fn()} show="receive" />)

    expect(await screen.findByRole('button', { name: 'Camera' })).toBeEnabled()
    expect(screen.getByRole('button', { name: 'Upload' })).toBeEnabled()
    expect(screen.getByLabelText('Take supplier bill photo')).toHaveAttribute('capture', 'environment')
    expect(screen.getByLabelText('Upload supplier bill files')).toHaveAttribute('multiple')
    for (const label of ['Supplier', 'Stock location', 'Receipt date', 'Product', 'Quantity', 'Unit cost (INR, before GST)']) {
      const field = screen.getByText(label, { exact: true })
      expect(field).toHaveClass('buy-field-label')
      expect(field.querySelector('.buy-required')).toBeInTheDocument()
    }
    expect(screen.getAllByText('0 items')).toHaveLength(2)
    expect(screen.getByRole('button', { name: /Review purchase/ })).toBeDisabled()
    expect(screen.getByText('Add products to continue')).toBeInTheDocument()
  })

  it('keeps the selected invoice filename visible while OCR is running', async () => {
      const user = userEvent.setup()
      let finishExtraction!: (response: Response) => void
      const extraction = new Promise<Response>(resolve => { finishExtraction = resolve })
      mockProcurement([], () => extraction)
      render(<Procurement products={[]} locations={[]} reload={vi.fn()} show="receive" />)
  
      const fileInput = await screen.findByLabelText('Upload supplier bill files')
      const file = new File(['%PDF-1.4 test invoice'], 'supplier-invoice.pdf', { type: 'application/pdf' })
      await user.upload(fileInput, file)
  
      expect(await screen.findByRole('status')).toHaveTextContent('Reading invoice')
      expect((fileInput as HTMLInputElement).files?.[0]?.name).toBe('supplier-invoice.pdf')
      expect(screen.getByText('supplier-invoice.pdf')).toBeInTheDocument()
      expect(fileInput).toBeDisabled()
  
      finishExtraction(new Response(JSON.stringify({
        fileId: 'invoice-file-1',
        supplierName: supplier.name,
        supplierId: supplier.id,
        invoiceNumber: 'INV-100',
        date: '2026-11-23',
        total: 220,
        lines: [],
        warnings: [],
      })))
      await waitFor(() => expect((fileInput as HTMLInputElement).files?.length).toBe(0))
      expect(screen.queryByRole('status')).not.toBeInTheDocument()
    })
  
    it('renders all four Buy destinations in the dedicated tab navigation', async () => {
      mockProcurement()
      render(<BuyPage tab="receive" c={{ products: [], locations: [], reload: vi.fn() }} />)
  
      const nav = await screen.findByRole('navigation', { name: 'Sections' })
      expect(nav).toHaveClass('tabs', 'buy-tabs')
      expect(Array.from(nav.querySelectorAll('a')).map(a => a.getAttribute('href'))).toEqual([
        '#/buy/receive',
        '#/buy/pay',
        '#/buy/history',
        '#/buy/return',
      ])
    })

})
