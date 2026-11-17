import { api, ApiError } from './api'
import { readOfflineRecords, saveOfflineRecord } from './offlineStore'

export type OfflineSaleBody = {
  customerId: string | null
  locationId: string
  saleDate: string
  saleType: string
  amountPaid: number | null
  payMethod: string | null
  payments?: { method: string; amount: number }[]
  lines: { productId: string; quantity: number; unitPrice: number; discount: number; serials: string[] }[]
  gstRate: number | null
  placeOfSupply: string | null
}

type QueuedSale = {
  idempotencyKey: string
  body: OfflineSaleBody
  createdAt: string
  serverSaleId?: string
  invoiceNumber?: number
  lastError?: string
}

export type OfflineSaleItem = {
  id: string
  state: 'pending-sync' | 'syncing' | 'posted' | 'needs-attention'
  createdAt: string
  value: QueuedSale
}

const activeSync = new Map<string, Promise<void>>()

export async function queueOfflineSale(ownerKey: string, body: OfflineSaleBody, idempotencyKey: string) {
  const id = 'offline-sale:' + crypto.randomUUID()
  const value: QueuedSale = { idempotencyKey, body, createdAt: new Date().toISOString() }
  await saveOfflineRecord(ownerKey, id, 'sale', 'pending-sync', value)
  return id
}

export async function listOfflineSales(ownerKey: string): Promise<OfflineSaleItem[]> {
  const records = await readOfflineRecords<QueuedSale>(ownerKey, 'sale')
  return records.map(x => ({ id: x.id, state: x.state as OfflineSaleItem['state'], createdAt: x.createdAt, value: x.value }))
    .sort((a, b) => b.createdAt.localeCompare(a.createdAt))
}

export async function syncOfflineSales(ownerKey: string): Promise<void> {
  if (!ownerKey || !navigator.onLine) return
  const existing = activeSync.get(ownerKey)
  if (existing) return existing
  const task = (async () => {
    const queue = await listOfflineSales(ownerKey)
    for (const item of queue.filter(x => x.state === 'pending-sync' || x.state === 'syncing')) {
      if (!navigator.onLine) break
      await saveOfflineRecord(ownerKey, item.id, 'sale', 'syncing', item.value)
      try {
        const posted = await api<{ id: string; serialWarnings?: string[] }>('POST', '/sales', item.value.body, item.value.idempotencyKey)
        const detail = await api<{ sale: { number: number } }>('GET', '/sales/' + posted.id)
        await saveOfflineRecord(ownerKey, item.id, 'sale', 'posted', {
          ...item.value, serverSaleId: posted.id, invoiceNumber: detail.sale.number, lastError: undefined
        })
      } catch (error) {
        if (error instanceof ApiError && [400, 401, 403, 404, 409, 422].includes(error.status)) {
          await saveOfflineRecord(ownerKey, item.id, 'sale', 'needs-attention', {
            ...item.value, lastError: error.message
          })
        } else {
          await saveOfflineRecord(ownerKey, item.id, 'sale', 'pending-sync', {
            ...item.value, lastError: error instanceof Error ? error.message : 'Temporary sync failure'
          })
          if (!navigator.onLine || error instanceof TypeError || (error instanceof ApiError && error.status >= 500)) break
        }
      }
    }
  })()
  activeSync.set(ownerKey, task)
  try { await task } finally { activeSync.delete(ownerKey) }
}
