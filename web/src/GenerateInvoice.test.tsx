import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { GenerateInvoice } from './GenerateInvoice'
import { resetCsrf } from './api'

vi.mock('@zxing/browser', () => ({
  BrowserMultiFormatReader: class {
    async decodeFromImageUrl() { throw new Error('No barcode in test image') }
  }
}))

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
    if (url === '/api/customers' && method === 'POST') return new Response(JSON.stringify({ id: 'customer-1' }))
    if (url === '/api/sales' && method === 'POST') return new Response(JSON.stringify({ id: 'sale-1' }))
    if (url === '/api/sales/sale-1') return new Response(JSON.stringify({
      sale: { number: 1, whatsappStatus: 'ConsentRequired' }
    }))
    return new Response(JSON.stringify({ error: 'not found' }), { status: 404 })
  }))
})

describe('GenerateInvoice WhatsApp independence', () => {
  it('keeps tag capture beside the product field and separates handwritten import', async () => {
    const { container } = render(<GenerateInvoice products={products} locations={locations} reload={vi.fn()} businessCategory="electronics_mobile" />)
    const productInput = screen.getByPlaceholderText('Search product or type a name')
    expect(screen.getByLabelText('Address (optional)')).toBeInTheDocument()
    expect(screen.queryByLabelText('City')).not.toBeInTheDocument()
    expect(screen.queryByLabelText('State')).not.toBeInTheDocument()
    expect(screen.queryByLabelText('Pincode')).not.toBeInTheDocument()
    expect(screen.getByLabelText('Brand')).toBeInTheDocument()
    expect(screen.getByLabelText('Model number')).toBeInTheDocument()
    expect(screen.getByLabelText('Serial number (optional)')).toBeInTheDocument()
    expect(screen.getByLabelText('Brand').closest('.invoice-brand-model')).toContainElement(screen.getByLabelText('Model number'))
    expect(screen.getByLabelText('Quantity *').closest('.invoice-quantity-price')).toContainElement(screen.getByLabelText('Selling price (₹) *'))
    expect(screen.getByLabelText('Brand').closest('details')).toBeNull()
    expect(screen.getByLabelText('Payment mode').closest('details')).toBeNull()
    expect(container.querySelector('.invoice-more-details #billing-date')).not.toBeNull()
    expect(productInput.tagName).toBe('TEXTAREA')
    expect(productInput).toHaveClass('invoice-product-description')
    expect(productInput).toHaveStyle({ minHeight: '76px', resize: 'none', overflowY: 'hidden' })
    expect(container.querySelector('.invoice-import-prompt')).toHaveTextContent('Have a handwritten bill?')
    expect(screen.getByRole('button', { name: 'Scan tag' })).toHaveTextContent('📷')
    expect(container.querySelector('.invoice-product-entry > .item-tag-uploader')).toHaveClass('invoice-feedback-flow')
    const productRow = productInput.closest('.invoice-product-entry')
    expect(productRow).toContainElement(screen.getByRole('button', { name: 'Scan tag' }))
    const actions = container.querySelector('.invoice-add-item-action')
    expect(actions?.querySelectorAll('button')).toHaveLength(1)
    expect(actions?.querySelector('button')).toHaveTextContent('Add item')
    expect(screen.getByRole('button', { name: /Import bill/ })).toBeInTheDocument()
    expect(container.querySelector('.invoice-bill-summary')).toBeInTheDocument()
    expect(container.querySelector('.invoice-sticky-review')).toBeInTheDocument()
  })


  it('replaces manual item entry with extracted review and clears import feedback on discard', async () => {
    vi.stubGlobal('fetch', vi.fn(async (url: string, init?: RequestInit) => {
      const method = init?.method ?? 'GET'
      calls.push({ url, method })
      if (url === '/api/csrf') return new Response(JSON.stringify({ token: 'csrf-test' }))
      if (url === '/api/business-profile') return new Response(JSON.stringify({ businessName: 'Pilot Shop', address: '', city: '', state: '', pinCode: '', mobile: '', gstin: null }))
      if (url === '/api/products') return new Response(JSON.stringify(products))
      if (url === '/api/customers' && method === 'POST') return new Response(JSON.stringify({ id: 'customer-1' }), { status: 200 })
      if (url === '/api/sales/invoice-image-extract' && method === 'POST') return new Response(JSON.stringify({
        status: 'ReadyForReview',
        extraction: {
          invoiceNumber: '468', date: '2026-11-20', customerName: 'Test Customer', customerPhone: '9876543210',
          subtotal: 10000, taxAmount: 0, total: 10000,
          lines: [{ description: 'Samsung LED Television\nModel UA43\nS/N SN12345678', brand: 'Samsung', model: 'UA43', serialNumbers: ['SN12345678'], hsn: '8528', quantity: 1, unitPrice: 10000, discount: 0, amount: 10000, gstRate: 0, gstAmount: 0, confidence: 0.9 }]
        }, warnings: []
      }), { status: 200 })
      return new Response(JSON.stringify({ error: 'not found' }), { status: 404 })
    }))
    const { container } = render(<GenerateInvoice products={products} locations={locations} reload={vi.fn()} />)
    await userEvent.click(screen.getByRole('button', { name: /Import bill/ }))
    expect(screen.getByRole('dialog', { name: 'Add handwritten invoice' })).toBeInTheDocument()
    const inputs = container.querySelectorAll('.sales-invoice-uploader input[type="file"]')
    expect(inputs).toHaveLength(2)
    const file = new File([new Uint8Array([0xFF, 0xD8, 0xFF, 0xD9])], 'handwritten-bill.jpg', { type: 'image/jpeg' })
    fireEvent.change(inputs[1] as HTMLInputElement, { target: { files: [file] } })
    await waitFor(() => expect(calls).toContainEqual({ url: '/api/sales/invoice-image-extract', method: 'POST' }))
    await waitFor(() => expect(screen.getByRole('status')).toHaveTextContent(/Invoice details extracted|Some details could not be read/))
    const importStatus = screen.getByRole('status')
    expect(importStatus.parentElement).toHaveClass('sales-invoice-uploader', 'invoice-feedback-flow')
    expect(container.querySelector('.invoice-import-callout')).toContainElement(importStatus)
    expect(await screen.findByRole('region', { name: 'Review detected items' })).toBeInTheDocument()
    const extractedDescription = screen.getByLabelText('Extracted line 1 description') as HTMLTextAreaElement
    expect(extractedDescription.tagName).toBe('TEXTAREA')
    expect(extractedDescription).toHaveValue('Samsung LED Television\nModel UA43\nS/N SN12345678')
    expect(extractedDescription).toHaveStyle({ resize: 'none', overflowY: 'hidden' })
    expect(screen.getByLabelText('Extracted line 1 serial numbers')).toHaveValue('SN12345678')
    Object.defineProperty(extractedDescription, 'scrollHeight', { configurable: true, value: 120 })
    fireEvent.change(extractedDescription, { target: { value: 'Long handwritten product\nmodel and serial details' } })
    await waitFor(() => expect(extractedDescription.style.height).toBe('120px'))
    expect(screen.getByLabelText('Extracted line 1 quantity')).toHaveValue('1')
    expect(screen.getByLabelText('Extracted line 1 unit price')).toHaveValue('10000')
    expect(container.querySelector('.invoice-product-editor')).not.toHaveAttribute('hidden')
    expect(container.querySelector('#billing-prod')?.closest('.billing-field')).toHaveAttribute('hidden')
    expect(container.querySelector('.invoice-brand-model')).toHaveAttribute('hidden')
    expect(container.querySelector('.invoice-quantity-price')).toHaveAttribute('hidden')
    expect(container.querySelector('.invoice-product-more-details')).toHaveAttribute('hidden')
    expect(container.querySelector('.invoice-add-item-action')).toHaveAttribute('hidden')
    expect(screen.getByLabelText('Customer name *')).toHaveValue('Test Customer')
    expect(screen.getByText(/Invoice details extracted/)).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Discard extraction' }))
    expect(screen.queryByRole('region', { name: 'Review detected items' })).not.toBeInTheDocument()
    expect(screen.queryByText(/Invoice details extracted/)).not.toBeInTheDocument()
    expect(container.querySelector('.invoice-product-editor')).not.toHaveAttribute('hidden')
    expect(screen.getByRole('button', { name: /Add item/ })).toBeInTheDocument()
  })


  it('carries extracted serial numbers into the added sale item and final sale request', async () => {
    let postedSale: { lines: { serials: string[] }[] } | null = null
    vi.stubGlobal('fetch', vi.fn(async (url: string, init?: RequestInit) => {
      const method = init?.method ?? 'GET'
      calls.push({ url, method })
      if (url === '/api/csrf') return new Response(JSON.stringify({ token: 'csrf-test' }))
      if (url === '/api/business-profile') return new Response(JSON.stringify({ businessName: 'Pilot Shop', address: '', city: '', state: '', pinCode: '', mobile: '', gstin: null }))
      if (url === '/api/products') return new Response(JSON.stringify(products))
      if (url.endsWith('/customers') && method === 'POST') return new Response(JSON.stringify({ id: 'customer-1' }), { status: 200 })
      if (url.endsWith('/sales') && method === 'POST') {
        postedSale = JSON.parse(String(init?.body)) as { lines: { serials: string[] }[] }
        return new Response(JSON.stringify({ id: 'sale-1' }), { status: 200 })
      }
      if (url.endsWith('/sales/sale-1')) return new Response(JSON.stringify({ sale: { number: 1, whatsappStatus: 'ConsentRequired' } }), { status: 200 })
      if (url.endsWith('/customers') && method === 'POST') return new Response(JSON.stringify({ id: 'customer-1' }), { status: 200 })
      if (url.endsWith('/sales') && method === 'POST') {
        postedSale = JSON.parse(String(init?.body)) as { lines: { serials: string[] }[] }
        return new Response(JSON.stringify({ id: 'sale-1' }), { status: 200 })
      }
      if (url.endsWith('/sales/sale-1')) return new Response(JSON.stringify({ sale: { number: 1, whatsappStatus: 'ConsentRequired' } }), { status: 200 })
      if (url === '/api/sales/invoice-image-extract' && method === 'POST') return new Response(JSON.stringify({
        status: 'ReadyForReview',
        extraction: {
          invoiceNumber: '454', date: '2026-11-06', customerName: 'Test Customer', customerPhone: '9876543210',
          subtotal: 17500, taxAmount: null, total: 17500,
          lines: [{ description: 'Voltas Beko WM\nModel WTL8066\nS/N SN12345678', brand: 'Voltas Beko', model: 'WTL8066', serialNumbers: ['SN12345678'], hsn: null, quantity: 1, unitPrice: 17500, discount: null, amount: 17500, gstRate: null, gstAmount: null, confidence: 0.8 }]
        }, warnings: []
      }), { status: 200 })
      return new Response(JSON.stringify({ error: `not found: ${method} ${url}` }), { status: 404 })
    }))
    const { container } = render(<GenerateInvoice products={products} locations={locations} reload={vi.fn()} businessCategory="electronics_mobile" />)
    await userEvent.click(screen.getByRole('button', { name: /Import bill/ }))
    const inputs = container.querySelectorAll('.sales-invoice-uploader input[type="file"]')
    const file = new File([new Uint8Array([0xFF, 0xD8, 0xFF, 0xD9])], 'monika-bill.jpg', { type: 'image/jpeg' })
    fireEvent.change(inputs[1] as HTMLInputElement, { target: { files: [file] } })
    expect(await screen.findByLabelText('Extracted line 1 serial numbers')).toHaveValue('SN12345678')
    await userEvent.click(screen.getByRole('button', { name: 'Add reviewed lines to bill' }))
    await waitFor(() => expect(container.querySelector('.invoice-added-item strong')?.textContent).toContain('Voltas Beko WM'))
    expect(container.querySelector('.invoice-added-item')?.textContent).toContain('Serial: SN12345678')
    expect(screen.queryByRole('region', { name: 'Review detected items' })).not.toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: /Review bill/ }))
    await userEvent.click(await screen.findByRole('button', { name: 'Confirm & print' }))
    await waitFor(() => expect(postedSale?.lines[0]?.serials).toEqual(['SN12345678']))
  })

  it('keeps model and serial in optional details for non-electronics businesses', async () => {
    const { container } = render(<GenerateInvoice products={products} locations={locations} reload={vi.fn()} businessCategory="general_retail" />)
    const details = container.querySelector('.invoice-product-more-details') as HTMLDetailsElement
    expect(details.open).toBe(false)
    expect(container.querySelector('#billing-model')?.closest('details')).toBe(details)
    expect(container.querySelector('#billing-serial')?.closest('details')).toBe(details)
    await userEvent.click(details.querySelector('summary') as HTMLElement)
    expect(details.open).toBe(true)
    expect(screen.getByLabelText('Model number')).toBeInTheDocument()
    expect(screen.getByLabelText('Serial number (optional)')).toBeInTheDocument()
  })

  it('keeps automatic browser printing when WhatsApp consent is not confirmed', async () => {
    const reload = vi.fn()
    render(<GenerateInvoice products={products} locations={locations} reload={reload} />)

    await userEvent.type(screen.getByLabelText('Mobile number *'), '9876543210')
    await userEvent.type(screen.getByLabelText('Customer name *'), 'Test Customer')
    await userEvent.type(await screen.findByPlaceholderText('Search product or type a name'), 'Test Cable')
    await userEvent.type(screen.getByLabelText('Selling price (₹) *'), '100')
    await userEvent.click(screen.getByRole('button', { name: /Add item/ }))
    await userEvent.click(screen.getByRole('button', { name: /Review bill/ }))
    await userEvent.click(await screen.findByRole('button', { name: 'Confirm & print' }))

    expect(await screen.findByText(/WhatsApp invoice: Customer consent needed/)).toBeInTheDocument()
    expect(screen.getByText('Ask the customer for permission before sending this bill on WhatsApp.')).toBeInTheDocument()
    await waitFor(() => expect(window.print).toHaveBeenCalledTimes(1), { timeout: 2000 })
    expect(calls).toContainEqual({ url: '/api/sales', method: 'POST' })
    expect(calls.some(x => x.url.includes('/whatsapp'))).toBe(false)
    expect(reload).toHaveBeenCalled()
  })

  it('uses one shared product editor for tag capture and adds the populated row directly', async () => {
    vi.stubGlobal('URL', {
      ...URL,
      createObjectURL: vi.fn(() => 'blob:invoice-tag-test'),
      revokeObjectURL: vi.fn()
    })
    vi.stubGlobal('fetch', vi.fn(async (url: string, init?: RequestInit) => {
      const method = init?.method ?? 'GET'
      calls.push({ url, method })
      if (url === '/api/csrf') return new Response(JSON.stringify({ token: 'csrf-test' }))
      if (url === '/api/business-profile') return new Response(JSON.stringify({
        businessName: 'Pilot Shop', address: '', city: '', state: '',
        pinCode: '', mobile: '', gstin: null
      }))
      if (url === '/api/products') return new Response(JSON.stringify(products))
      if (url === '/api/sales/item-tag-scan' && method === 'POST') return new Response(JSON.stringify({
        status: 'ReadyForReview',
        extraction: {
          productName: 'Semi Automatic Washing Machine',
          brand: 'LG',
          model: 'PX900H7SD4Z',
          serial: '610NWHS203785',
          barcode: '4006381333931'
        },
        suggestedProduct: null,
        warnings: []
      }))
      return new Response(JSON.stringify({ error: 'not found' }), { status: 404 })
    }))

    const { container } = render(<GenerateInvoice products={products} locations={locations} reload={vi.fn()} businessCategory="electronics_mobile" />)
    expect(screen.queryByRole('button', { name: 'Scan product' })).not.toBeInTheDocument()
    expect(screen.getByLabelText('Model number')).toBeInTheDocument()
    expect(screen.getByLabelText('Serial number (optional)')).toBeInTheDocument()

    await userEvent.click(screen.getByRole('button', { name: 'Scan tag' }))
    const photoInputs = container.querySelectorAll('.invoice-product-entry input[type="file"]')
    const file = new File([new Uint8Array([0xFF, 0xD8, 0xFF, 0xD9])], 'washing-machine.jpg', { type: 'image/jpeg' })
    fireEvent.change(photoInputs[1], { target: { files: [file] } })

    expect(await screen.findByDisplayValue('LG')).toBeInTheDocument()
    expect(screen.getByDisplayValue('PX900H7SD4Z')).toBeInTheDocument()
    expect(screen.getByDisplayValue('610NWHS203785')).toBeInTheDocument()
    expect(container.querySelector('.invoice-product-entry .item-tag-feedback')).toBeInTheDocument()
    expect(screen.getAllByRole('button', { name: /Add item/ })).toHaveLength(1)
    expect(container.querySelector('.invoice-product-more-details')?.hasAttribute('open')).toBe(false)
    expect(screen.queryByAltText('Captured product tag preview')).not.toBeInTheDocument()

    await userEvent.type(screen.getByLabelText('Selling price (₹) *'), '15000')
    await userEvent.click(screen.getByRole('button', { name: /Add item/ }))

    expect(screen.getByText('Semi Automatic Washing Machine')).toBeInTheDocument()
    expect(screen.getByText(/Serial: 610NWHS203785/)).toBeInTheDocument()
    expect(screen.getByText(/Barcode: 4006381333931/)).toBeInTheDocument()
  })

  it('fills the product field from a verified catalogue match when AI returns only a barcode', async () => {
    vi.stubGlobal('URL', {
      ...URL,
      createObjectURL: vi.fn(() => 'blob:invoice-tag-match-test'),
      revokeObjectURL: vi.fn()
    })
    vi.stubGlobal('fetch', vi.fn(async (url: string, init?: RequestInit) => {
      const method = init?.method ?? 'GET'
      calls.push({ url, method })
      if (url === '/api/csrf') return new Response(JSON.stringify({ token: 'csrf-test' }))
      if (url === '/api/business-profile') return new Response(JSON.stringify({
        businessName: 'Pilot Shop', address: '', city: '', state: '',
        pinCode: '', mobile: '', gstin: null
      }))
      if (url === '/api/products') return new Response(JSON.stringify(products))
      if (url === '/api/sales/item-tag-scan' && method === 'POST') return new Response(JSON.stringify({
        status: 'ReadyForReview',
        extraction: { barcode: '8806096305935', barcodeSymbology: 'EAN_13' },
        suggestedProduct: {
          id: 'p1',
          name: 'LG Fully Automatic Washing Machine',
          reason: 'Exact local barcode'
        },
        warnings: []
      }))
      return new Response(JSON.stringify({ error: 'not found' }), { status: 404 })
    }))

    const { container } = render(<GenerateInvoice products={products} locations={locations} reload={vi.fn()} />)
    await userEvent.click(screen.getByRole('button', { name: 'Scan tag' }))
    expect(screen.getByRole('dialog', { name: 'Add product tag photo' })).toBeInTheDocument()
    const photoInputs = container.querySelectorAll('.invoice-product-entry input[type="file"]')
    const file = new File([new Uint8Array([0xFF, 0xD8, 0xFF, 0xD9])], 'washing-machine.jpg', { type: 'image/jpeg' })
    fireEvent.change(photoInputs[0], { target: { files: [file] } })

    expect(await screen.findByDisplayValue('LG Fully Automatic Washing Machine')).toBeInTheDocument()
    expect(screen.getByDisplayValue('8806096305935')).toBeInTheDocument()
    expect(screen.getByText(/Catalogue match: LG Fully Automatic Washing Machine/)).toBeInTheDocument()
  })


  it('falls back to the extracted model number when the tag has no product name', async () => {
    vi.stubGlobal('URL', {
      ...URL,
      createObjectURL: vi.fn(() => 'blob:invoice-tag-model-only'),
      revokeObjectURL: vi.fn()
    })
    vi.stubGlobal('fetch', vi.fn(async (url: string, init?: RequestInit) => {
      const method = init?.method ?? 'GET'
      calls.push({ url, method })
      if (url === '/api/csrf') return new Response(JSON.stringify({ token: 'csrf-test' }))
      if (url === '/api/business-profile') return new Response(JSON.stringify({
        businessName: 'Pilot Shop', address: '', city: '', state: '', pinCode: '', mobile: '', gstin: null
      }))
      if (url === '/api/products') return new Response(JSON.stringify(products))
      if (url === '/api/sales/item-tag-scan' && method === 'POST') return new Response(JSON.stringify({
        status: 'NeedsAttention',
        extraction: { model: 'ZX-90', brand: 'Local', serial: 'SERIAL-0090' },
        suggestedProduct: null,
        warnings: []
      }))
      return new Response(JSON.stringify({ error: 'not found' }), { status: 404 })
    }))

    const { container } = render(<GenerateInvoice products={products} locations={locations} reload={vi.fn()} />)
    await userEvent.click(screen.getByRole('button', { name: 'Scan tag' }))
    const photoInputs = container.querySelectorAll('.invoice-product-entry input[type="file"]')
    const file = new File([new Uint8Array([0xFF, 0xD8, 0xFF, 0xD9])], 'model-only.jpg', { type: 'image/jpeg' })
    fireEvent.change(photoInputs[0], { target: { files: [file] } })

    expect(await screen.findByPlaceholderText('Search product or type a name')).toHaveValue('ZX-90')
    expect(screen.getByDisplayValue('Local')).toBeInTheDocument()
    expect(screen.getByDisplayValue('SERIAL-0090')).toBeInTheDocument()
  })

  it('requires customer name and a valid mobile number with associated field errors', async () => {
    render(<GenerateInvoice products={products} locations={locations} reload={vi.fn()} />)
    const nameInput = screen.getByLabelText('Customer name *')
    const phoneInput = screen.getByLabelText('Mobile number *')

    await userEvent.type(screen.getByPlaceholderText('Search product or type a name'), 'Test Cable')
    await userEvent.type(screen.getByLabelText('Selling price (₹) *'), '100')
    await userEvent.click(screen.getByRole('button', { name: /Add item/ }))
    await userEvent.click(screen.getByRole('button', { name: /Review bill/ }))
    expect(screen.getByText('Customer name is required.')).toBeInTheDocument()
    expect(screen.getByText('Enter a valid mobile number (10–15 digits).')).toBeInTheDocument()
    expect(nameInput).toHaveAttribute('aria-invalid', 'true')
    expect(nameInput).toHaveAttribute('aria-describedby', 'billing-name-error')
    expect(phoneInput).toHaveAttribute('aria-invalid', 'true')
    expect(phoneInput).toHaveAttribute('aria-describedby', 'billing-phone-error')
    expect(document.activeElement).toBe(nameInput)

    await userEvent.type(nameInput, 'Test Customer')
    await userEvent.type(phoneInput, '123456789')
    await userEvent.click(screen.getByRole('button', { name: /Review bill/ }))
    expect(screen.queryByText('Customer name is required.')).not.toBeInTheDocument()
    expect(screen.getByText('Enter a valid mobile number (10–15 digits).')).toBeInTheDocument()
    expect(document.activeElement).toBe(phoneInput)
  })

  it('shows a non-blocking missing-serial warning during invoice review', async () => {
    render(<GenerateInvoice products={products} locations={locations} reload={vi.fn()} />)
    await userEvent.type(screen.getByLabelText('Mobile number *'), '9876543210')
    await userEvent.type(screen.getByLabelText('Customer name *'), 'Test Customer')
    await userEvent.type(await screen.findByPlaceholderText('Search product or type a name'), 'Test Cable')
    await userEvent.type(screen.getByLabelText('Selling price (₹) *'), '100')
    await userEvent.click(screen.getByRole('button', { name: /Add item/ }))
    await userEvent.click(screen.getByRole('button', { name: /Review bill/ }))

    expect(await screen.findByRole('status')).toHaveTextContent(
      'Serial number not entered for one or more items. You can still continue; serial, IMEI and barcode are optional.'
    )
    expect(screen.getByRole('button', { name: 'Confirm & print' })).toBeEnabled()
  })
})
