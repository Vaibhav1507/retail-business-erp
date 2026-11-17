import { describe, expect, it } from 'vitest'
import { offlineRecordStorageId } from './offlineStore'

describe('offline record ownership keys', () => {
  it('keeps the same logical record ID isolated between user/tenant scopes', () => {
    const tenantA = offlineRecordStorageId('hashed-owner-a', 'tenant-snapshot')
    const tenantB = offlineRecordStorageId('hashed-owner-b', 'tenant-snapshot')
    expect(tenantA).not.toBe(tenantB)
    expect(tenantA).toBe('hashed-owner-a:tenant-snapshot')
    expect(tenantB).toBe('hashed-owner-b:tenant-snapshot')
  })

  it('keeps IDs stable within one owner scope', () => {
    expect(offlineRecordStorageId('hashed-owner-a', 'held-bills'))
      .toBe(offlineRecordStorageId('hashed-owner-a', 'held-bills'))
  })
})
