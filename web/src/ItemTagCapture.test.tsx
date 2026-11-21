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
    productName: 'Test Cable', brand: 'RBOS', model: 'CB-1',
    barcode: '4006381333931', barcodeSymbology: 'EAN_13', serial: 'SER12345'
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
  it('reads a tag transiently and returns verified invoice row fields without a capture record', async () => {
    const onAdd = vi.fn(() => true)
    const { container } = render(
      <ItemTagCapture products={[{ id: 'p1', name: 'Test Cable' }]} disabled={false} onAdd={onAdd} />
    )
    const inputs = container.querySelectorAll('input[type="file"]')
    expect(inputs).toHaveLength(2)
    expect(inputs[0]).toHaveAttribute('capture', 'environment')
    expect(inputs[1]).toHaveAttribute('multiple')

    const file = new File([new Uint8Array([0xFF, 0xD8, 0xFF, 0xD9])], 'tag.jpg', { type: 'image/jpeg' })
    fireEvent.change(inputs[1], { target: { files: [file] } })

    expect(await screen.findByText(/Tag read — verify/)).toBeInTheDocument()
    expect(await screen.findByAltText('Captured product tag preview')).toBeInTheDocument()
    expect(screen.getByDisplayValue('Test Cable')).toBeInTheDocument()
    expect(screen.getByDisplayValue('SER12345')).toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: 'Use details in invoice' }))

    await waitFor(() => expect(onAdd).toHaveBeenCalledWith({
      productId: 'p1', name: 'Test Cable', brand: 'RBOS', model: 'CB-1',
      category: '', barcode: '4006381333931', barcodeSymbology: 'EAN_13',
      serials: ['SER12345'], imei1: '', imei2: '', mrp: null, hsn: ''
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

  it('shows a helpful error for unsupported images without uploading', async () => {
    const { container } = render(
      <ItemTagCapture products={[]} disabled={false} onAdd={() => true} />
    )
    const input = container.querySelectorAll('input[type="file"]')[1]
    const file = new File(['not an image'], 'tag.txt', { type: 'text/plain' })
    fireEvent.change(input, { target: { files: [file] } })
    expect(await screen.findByRole('alert')).toHaveTextContent('Use a JPEG, PNG or WebP photo.')
    expect(fetchMock).not.toHaveBeenCalledWith('/api/sales/item-tag-scan', expect.anything())
  })

  it('disables capture while offline', () => {
    render(<ItemTagCapture products={[]} disabled onAdd={() => true} />)
    const buttons = screen.getAllByRole('button')
    expect(buttons.find(button => button.textContent === 'Take photo')).toBeDisabled()
    expect(buttons.find(button => button.textContent === 'Choose photo(s)')).toBeDisabled()
  })
})
