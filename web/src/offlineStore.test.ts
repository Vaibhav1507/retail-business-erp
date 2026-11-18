import { describe, expect, it } from 'vitest'
import { describeOfflineStorageError, offlineRecordStorageId } from './offlineStore'

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

describe('offline storage diagnostics', () => {
  it('preserves browser storage error names and actionable messages', () => {
    expect(describeOfflineStorageError(new DOMException('The operation was blocked', 'SecurityError')))
      .toBe('SecurityError: The operation was blocked')
  })

  it('handles non-Error failures without leaking object contents', () => {
    expect(describeOfflineStorageError({ secret: 'must not be displayed' }))
      .toBe('Unknown browser storage error')
  })
})
