import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { resetCsrf } from './api'
import { ItemTagCapture } from './ItemTagCapture'

vi.mock('@zxing/browser', () => ({
  BrowserMultiFormatReader: class {
    async decodeFromImageUrl() { throw new Error('No barcode in test image') }
  }
}))

const fetchMock = vi.fn()
const scan = {
  status: 'ReadyForReview',
  extraction: {
    brand: 'RBOS', model: 'CB-1',
    barcode: '4006381333931', barcodeSymbology: 'EAN_13', serial: 'abC-01oI',
    imei1: '490154203237518', imei2: '490154203237526'
  },
  suggestedProduct: { id: 'p1', name: 'Test Cable', reason: 'Exact brand/model' },
  warnings: []
}

beforeEach(() => {
  resetCsrf()
  fetchMock.mockReset()
  vi.stubGlobal('fetch', fetchMock)
  vi.stubGlobal('URL', {
    ...URL,
    createObjectURL: vi.fn(() => 'blob:item-tag-test'),
    revokeObjectURL: vi.fn()
  })
  fetchMock.mockImplementation(async (url: string, init?: RequestInit) => {
    if (url === '/api/csrf') return new Response(JSON.stringify({ token: 'csrf-test' }))
    if (url === '/api/sales/item-tag-scan' && init?.method === 'POST') {
      return new Response(JSON.stringify(scan), { status: 200 })
    }
    return new Response(JSON.stringify({ error: 'not found' }), { status: 404 })
  })
})

describe('ItemTagCapture', () => {
  it('offers one compact photo action and fills the shared invoice editor without a preview or second form', async () => {
    const onExtract = vi.fn()
    const { container } = render(<ItemTagCapture disabled={false} onExtract={onExtract} />)

    expect(screen.getByRole('button', { name: 'Add tag photo' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Take a photo' })).not.toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: 'Add tag photo' }))
    expect(screen.getByRole('dialog', { name: 'Add product tag photo' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Take a photo' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Choose from device' })).toBeInTheDocument()

    const inputs = container.querySelectorAll('input[type="file"]')
    expect(inputs).toHaveLength(2)
    expect(inputs[0]).toHaveAttribute('capture', 'environment')
    expect(inputs[1]).not.toHaveAttribute('multiple')

    const file = new File([new Uint8Array([0xFF, 0xD8, 0xFF, 0xD9])], 'tag.jpg', { type: 'image/jpeg' })
    fireEvent.change(inputs[1], { target: { files: [file] } })

    expect(await screen.findByText(/Catalogue match: Test Cable/)).toBeInTheDocument()
    expect(screen.queryByAltText('Captured product tag preview')).not.toBeInTheDocument()
    expect(screen.queryByLabelText('Product name')).not.toBeInTheDocument()
    await waitFor(() => expect(onExtract).toHaveBeenCalledWith({
      productId: 'p1', name: 'Test Cable', brand: 'RBOS', model: 'CB-1',
      category: '', barcode: '4006381333931', barcodeSymbology: 'EAN_13',
      serials: ['abC-01oI'], imei1: '490154203237518', imei2: '490154203237526', mrp: null, hsn: ''
    }))
    expect(fetchMock).toHaveBeenCalledWith('/api/sales/item-tag-scan', expect.objectContaining({
      method: 'POST',
      headers: expect.objectContaining({
        'Idempotency-Key': expect.any(String),
        'Content-Type': 'image/jpeg'
      })
    }))
    expect(fetchMock.mock.calls.some(([url]) => String(url).includes('/sales/item-tag-captures'))).toBe(false)
  })

  it('reports barcode-only extraction as partial instead of claiming tag details were filled', async () => {
    fetchMock.mockImplementation(async (url: string, init?: RequestInit) => {
      if (url === '/api/csrf') return new Response(JSON.stringify({ token: 'csrf-test' }))
      if (url === '/api/sales/item-tag-scan' && init?.method === 'POST') {
        return new Response(JSON.stringify({
          status: 'ReadyForReview',
          extraction: { barcode: '8806096305935', barcodeSymbology: 'EAN_13' },
          suggestedProduct: null,
          warnings: []
        }), { status: 200 })
      }
      return new Response(JSON.stringify({ error: 'not found' }), { status: 404 })
    })
    const onExtract = vi.fn()
    const { container } = render(<ItemTagCapture disabled={false} onExtract={onExtract} />)
    fireEvent.click(screen.getByRole('button', { name: 'Add tag photo' }))
    const inputs = container.querySelectorAll('input[type="file"]')
    const file = new File([new Uint8Array([0xFF, 0xD8, 0xFF, 0xD9])], 'tag.jpg', { type: 'image/jpeg' })
    fireEvent.change(inputs[1], { target: { files: [file] } })

    expect(await screen.findByText(/Barcode detected, but product details could not be read/)).toBeInTheDocument()
    expect(screen.queryByText(/Tag details filled in/)).not.toBeInTheDocument()
    expect(onExtract).toHaveBeenCalledWith(expect.objectContaining({
      name: '', brand: '', model: '', barcode: '8806096305935'
    }))
  })

  it('shows a helpful error for unsupported images without uploading', async () => {
    const { container } = render(<ItemTagCapture disabled={false} onExtract={() => undefined} />)
    fireEvent.click(screen.getByRole('button', { name: 'Add tag photo' }))
    const input = container.querySelectorAll('input[type="file"]')[1]
    const file = new File(['not an image'], 'tag.txt', { type: 'text/plain' })
    fireEvent.change(input, { target: { files: [file] } })
    expect(await screen.findByRole('alert')).toHaveTextContent('Use a JPEG, PNG or WebP photo.')
    expect(fetchMock).not.toHaveBeenCalledWith('/api/sales/item-tag-scan', expect.anything())
  })

  it('disables tag upload while offline', () => {
    render(<ItemTagCapture disabled onExtract={() => undefined} />)
    expect(screen.getByRole('button', { name: 'Add tag photo' })).toBeDisabled()
  })
})
