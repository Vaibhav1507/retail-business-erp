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
const capture = {
  id: 'capture-1',
  status: 'ReadingTag',
  contentType: 'image/jpeg',
  sizeBytes: 4,
  extraction: { productName: 'Test Cable', brand: 'RBOS', model: 'CB-1', serial: null },
  suggestedProduct: { id: 'p1', name: 'Test Cable', reason: 'Exact brand/model' },
  review: null
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
    const method = init?.method ?? 'GET'
    if (url === '/api/csrf') return new Response(JSON.stringify({ token: 'csrf-test' }))
    if (url === '/api/sales/item-tag-captures' && method === 'POST') {
      return new Response(JSON.stringify(capture), { status: 200 })
    }
    if (url === '/api/sales/item-tag-captures/capture-1' && method === 'GET') {
      return new Response(JSON.stringify({ ...capture, status: 'ReadyForReview' }), { status: 200 })
    }
    if (url === '/api/sales/item-tag-captures/capture-1/review' && method === 'PUT') {
      return new Response(JSON.stringify({ ok: true }), { status: 200 })
    }
    if (url === '/api/sales/item-tag-captures/capture-1' && method === 'DELETE') {
      return new Response(JSON.stringify({ deleted: true }), { status: 200 })
    }
    return new Response(JSON.stringify({ error: 'not found' }), { status: 404 })
  })
})

describe('ItemTagCapture', () => {
  it('offers camera and gallery inputs and uploads, reviews and adds a tag photo', async () => {
    const onAdd = vi.fn(() => true)
    const onRemove = vi.fn()
    const { container } = render(
      <ItemTagCapture products={[{ id: 'p1', name: 'Test Cable' }]} disabled={false} onAdd={onAdd} onRemove={onRemove} />
    )
    const inputs = container.querySelectorAll('input[type="file"]')
    expect(inputs).toHaveLength(2)
    expect(inputs[0]).toHaveAttribute('capture', 'environment')
    expect(inputs[1]).toHaveAttribute('multiple')

    const file = new File([new Uint8Array([0xFF, 0xD8, 0xFF, 0xD9])], 'tag.jpg', { type: 'image/jpeg' })
    fireEvent.change(inputs[1], { target: { files: [file] } })

    expect(await screen.findByText(/Photo saved|Photo ready/)).toBeInTheDocument()
    expect(await screen.findByAltText('Captured product tag preview')).toBeInTheDocument()
    expect(screen.getAllByDisplayValue('Test Cable')).toHaveLength(2)
    await screen.findByRole('button', { name: 'Confirm details & add item' })
    fireEvent.click(screen.getByRole('button', { name: 'Confirm details & add item' }))

    await waitFor(() => expect(onAdd).toHaveBeenCalledWith({
      captureId: 'capture-1', productId: 'p1', name: 'Test Cable', serials: []
    }))
    expect(fetchMock).toHaveBeenCalledWith('/api/sales/item-tag-captures', expect.objectContaining({
      method: 'POST',
      headers: expect.objectContaining({ 'Idempotency-Key': expect.any(String), 'Content-Type': 'image/jpeg' })
    }))
  })

  it('shows a helpful error for unsupported images without uploading', async () => {
    const { container } = render(
      <ItemTagCapture products={[]} disabled={false} onAdd={() => true} onRemove={() => undefined} />
    )
    const input = container.querySelectorAll('input[type="file"]')[1]
    const file = new File(['not an image'], 'tag.txt', { type: 'text/plain' })
    fireEvent.change(input, { target: { files: [file] } })
    expect(await screen.findByRole('alert')).toHaveTextContent('Use a JPEG, PNG or WebP photo.')
    expect(fetchMock).not.toHaveBeenCalledWith('/api/sales/item-tag-captures', expect.anything())
  })

  it('disables capture while offline', () => {
    render(
      <ItemTagCapture products={[]} disabled onAdd={() => true} onRemove={() => undefined} />
    )
    const buttons = screen.getAllByRole('button')
    expect(buttons.find(button => button.textContent === 'Take photo')).toBeDisabled()
    expect(buttons.find(button => button.textContent === 'Choose photo(s)')).toBeDisabled()
  })
})
