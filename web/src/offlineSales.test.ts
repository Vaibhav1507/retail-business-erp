import { beforeEach, describe, expect, it, vi } from 'vitest'

const memory = vi.hoisted(() => ({
  records: [] as { ownerKey: string; id: string; kind: string; state: string; value: unknown; createdAt: string; updatedAt: string }[],
  api: vi.fn()
}))

vi.mock('./offlineStore', () => ({
  deleteOfflineRecord: vi.fn(async (ownerKey: string, id: string) => { memory.records = memory.records.filter(x => x.ownerKey !== ownerKey || x.id !== id) }),
  saveOfflineRecord: vi.fn(async (ownerKey: string, id: string, kind: string, state: string, value: unknown) => {
    const old = memory.records.findIndex(x => x.ownerKey === ownerKey && x.id === id)
    const record = { ownerKey, id, kind, state, value, createdAt: new Date().toISOString(), updatedAt: new Date().toISOString() }
    if (old >= 0) memory.records[old] = record
    else memory.records.push(record)
  }),
  readOfflineRecords: vi.fn(async (ownerKey: string, kind: string) => memory.records
    .filter(x => x.ownerKey === ownerKey && x.kind === kind)
    .map(({ id, state, value, createdAt, updatedAt }) => ({ id, state, value, createdAt, updatedAt })))
}))

vi.mock('./api', async importOriginal => {
  const actual = await importOriginal<typeof import('./api')>()
  return { ...actual, api: memory.api }
})

import { ApiError } from './api'
import { listOfflineSales, queueOfflineSale, syncOfflineSales, type OfflineSaleBody } from './offlineSales'

const ownerKey = 'owner@example.test:tenant-1'
const body: OfflineSaleBody = {
  customerId: null, locationId: 'location-1', saleDate: '2026-11-17', saleType: 'Cash',
  amountPaid: null, payMethod: null, lines: [{ productId: 'product-1', quantity: 2, unitPrice: 50, discount: 0, serials: [] }],
  gstRate: null, placeOfSupply: null
}

beforeEach(() => {
  memory.records = []
  memory.api.mockReset()
  Object.defineProperty(navigator, 'onLine', { configurable: true, value: true })
})

describe('offline sales outbox', () => {
  it('persists a draft with its original idempotency key', async () => {
    const id = await queueOfflineSale(ownerKey, body, 'stable-command-key')
    const queue = await listOfflineSales(ownerKey)
    expect(queue).toHaveLength(1)
    expect(queue[0].id).toBe(id)
    expect(queue[0].state).toBe('pending-sync')
    expect(queue[0].value.idempotencyKey).toBe('stable-command-key')
    expect(queue[0].value.body).toEqual(body)
  })

  it('posts once and records the server-issued invoice number', async () => {
    await queueOfflineSale(ownerKey, body, 'stable-command-key')
    memory.api.mockResolvedValueOnce({ id: 'server-sale-1' }).mockResolvedValueOnce({ sale: { number: 42 } })
    await syncOfflineSales(ownerKey)
    const queue = await listOfflineSales(ownerKey)
    expect(memory.api).toHaveBeenNthCalledWith(1, 'POST', '/sales', body, 'stable-command-key')
    expect(memory.api).toHaveBeenNthCalledWith(2, 'GET', '/sales/server-sale-1')
    expect(queue[0].state).toBe('posted')
    expect(queue[0].value.invoiceNumber).toBe(42)
    expect(queue[0].value.serverSaleId).toBe('server-sale-1')
  })

  it('retains a rejected draft as needs-attention instead of deleting it', async () => {
    await queueOfflineSale(ownerKey, body, 'stable-command-key')
    memory.api.mockRejectedValueOnce(new ApiError(409, 'Stock is no longer available.'))
    await syncOfflineSales(ownerKey)
    const queue = await listOfflineSales(ownerKey)
    expect(queue).toHaveLength(1)
    expect(queue[0].state).toBe('needs-attention')
    expect(queue[0].value.lastError).toBe('Stock is no longer available.')
  })

  it('does not post twice when the server accepted the bill but invoice details are temporarily unavailable', async () => {
    await queueOfflineSale(ownerKey, body, 'stable-command-key')
    memory.api.mockResolvedValueOnce({ id: 'server-sale-2' }).mockRejectedValueOnce(new ApiError(500, 'Temporary detail failure'))
    await syncOfflineSales(ownerKey)
    let queue = await listOfflineSales(ownerKey)
    expect(queue[0].state).toBe('posted')
    expect(queue[0].value.serverSaleId).toBe('server-sale-2')
    memory.api.mockResolvedValueOnce({ sale: { number: 43 } })
    await syncOfflineSales(ownerKey)
    queue = await listOfflineSales(ownerKey)
    expect(memory.api.mock.calls.filter(call => call[0] === 'POST')).toHaveLength(1)
    expect(queue[0].state).toBe('posted')
    expect(queue[0].value.invoiceNumber).toBe(43)
  })

  it('does not attempt synchronization while the browser is offline', async () => {
    await queueOfflineSale(ownerKey, body, 'stable-command-key')
    Object.defineProperty(navigator, 'onLine', { configurable: true, value: false })
    await syncOfflineSales(ownerKey)
    expect(memory.api).not.toHaveBeenCalled()
    const queue = await listOfflineSales(ownerKey)
    expect(queue[0].state).toBe('pending-sync')
  })

  it('rejects an oversized or unprovisioned offline queue entry', async () => {
    await expect(queueOfflineSale('', body, 'stable-command-key')).rejects.toThrow()
    await expect(queueOfflineSale(ownerKey, { ...body, lines: [] }, 'stable-command-key')).rejects.toThrow()
  })
})
