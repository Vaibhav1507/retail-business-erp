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
  it('keeps tag capture beside the product field and separates handwritten import', () => {
    const { container } = render(<GenerateInvoice products={products} locations={locations} reload={vi.fn()} />)
    const productInput = screen.getByPlaceholderText('Search or enter a product/model name')
    const productRow = productInput.closest('.invoice-product-entry')
    expect(productRow).toContainElement(screen.getByRole('button', { name: 'Scan tag' }))
    const actions = container.querySelector('.invoice-product-actions')
    expect(actions?.querySelectorAll('button')).toHaveLength(1)
    expect(actions?.querySelector('button')).toHaveTextContent('Add product')
    expect(actions).not.toContainElement(screen.getByRole('button', { name: 'Upload handwritten bill' }))
    expect(container.querySelector('.invoice-summary-bar')).toBeInTheDocument()
  })

  it('keeps automatic browser printing when WhatsApp consent is not confirmed', async () => {
    const reload = vi.fn()
    render(<GenerateInvoice products={products} locations={locations} reload={reload} />)

    await userEvent.type(screen.getByLabelText('Mobile number *'), '9876543210')
    await userEvent.type(screen.getByLabelText('Customer name *'), 'Test Customer')
    await userEvent.type(await screen.findByPlaceholderText('Search or enter a product/model name'), 'Test Cable')
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

    const { container } = render(<GenerateInvoice products={products} locations={locations} reload={vi.fn()} />)
    expect(screen.queryByRole('button', { name: 'Scan product' })).not.toBeInTheDocument()
    await userEvent.click(screen.getByText('Optional product details'))
    expect(screen.getByLabelText('Brand')).toBeInTheDocument()
    expect(screen.getByLabelText('Model')).toBeInTheDocument()

    await userEvent.click(screen.getByRole('button', { name: 'Scan tag' }))
    const photoInputs = container.querySelectorAll('.invoice-product-entry input[type="file"]')
    const file = new File([new Uint8Array([0xFF, 0xD8, 0xFF, 0xD9])], 'washing-machine.jpg', { type: 'image/jpeg' })
    fireEvent.change(photoInputs[1], { target: { files: [file] } })

    expect(await screen.findByDisplayValue('LG')).toBeInTheDocument()
    expect(screen.getByDisplayValue('PX900H7SD4Z')).toBeInTheDocument()
    expect(screen.getByDisplayValue('610NWHS203785')).toBeInTheDocument()
    expect(screen.getAllByRole('button', { name: 'Add product' })).toHaveLength(1)
    expect(screen.queryByAltText('Captured product tag preview')).not.toBeInTheDocument()

    await userEvent.type(screen.getByLabelText('Price (INR)'), '15000')
    await userEvent.click(screen.getByRole('button', { name: 'Add product' }))

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

    expect(await screen.findByPlaceholderText('Search or enter a product/model name')).toHaveValue('ZX-90')
    expect(screen.getByDisplayValue('Local')).toBeInTheDocument()
    expect(screen.getByDisplayValue('SERIAL-0090')).toBeInTheDocument()
  })

  it('requires customer name and a valid mobile number with associated field errors', async () => {
    render(<GenerateInvoice products={products} locations={locations} reload={vi.fn()} />)
    const nameInput = screen.getByLabelText('Customer name *')
    const phoneInput = screen.getByLabelText('Mobile number *')

    await userEvent.click(screen.getByRole('button', { name: 'Generate invoice' }))
    expect(screen.getByText('Customer name is required.')).toBeInTheDocument()
    expect(screen.getByText('Enter a valid mobile number (10–15 digits).')).toBeInTheDocument()
    expect(nameInput).toHaveAttribute('aria-invalid', 'true')
    expect(nameInput).toHaveAttribute('aria-describedby', 'billing-name-error')
    expect(phoneInput).toHaveAttribute('aria-invalid', 'true')
    expect(phoneInput).toHaveAttribute('aria-describedby', 'billing-phone-error')
    expect(document.activeElement).toBe(nameInput)

    await userEvent.type(nameInput, 'Test Customer')
    await userEvent.type(phoneInput, '123456789')
    await userEvent.click(screen.getByRole('button', { name: 'Generate invoice' }))
    expect(screen.queryByText('Customer name is required.')).not.toBeInTheDocument()
    expect(screen.getByText('Enter a valid mobile number (10–15 digits).')).toBeInTheDocument()
    expect(document.activeElement).toBe(phoneInput)
  })

  it('shows a non-blocking missing-serial warning during invoice review', async () => {
    render(<GenerateInvoice products={products} locations={locations} reload={vi.fn()} />)
    await userEvent.type(screen.getByLabelText('Mobile number *'), '9876543210')
    await userEvent.type(screen.getByLabelText('Customer name *'), 'Test Customer')
    await userEvent.type(await screen.findByPlaceholderText('Search or enter a product/model name'), 'Test Cable')
    await userEvent.type(screen.getByLabelText('Price (INR)'), '100')
    await userEvent.click(screen.getByRole('button', { name: 'Add product' }))
    await userEvent.click(screen.getByRole('button', { name: 'Generate invoice' }))

    expect(await screen.findByRole('status')).toHaveTextContent(
      'Serial number not entered for one or more items. You can still continue; serial, IMEI and barcode are optional.'
    )
    expect(screen.getByRole('button', { name: 'Confirm & print' })).toBeEnabled()
  })
})
