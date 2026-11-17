const DB_NAME = 'rbos-offline-v1'
const DB_VERSION = 1
const KEY_STORE = 'keys'
const RECORD_STORE = 'records'

type StoredRecord = {
  id: string
  ownerKey: string
  kind: string
  state: string
  createdAt: string
  updatedAt: string
  iv: string
  ciphertext: string
}

function openDb(): Promise<IDBDatabase> {
  if (!('indexedDB' in window) || !('crypto' in window) || !crypto.subtle) {
    return Promise.reject(new Error('This browser does not support protected offline storage.'))
  }
  return new Promise((resolve, reject) => {
    const request = indexedDB.open(DB_NAME, DB_VERSION)
    request.onupgradeneeded = () => {
      const db = request.result
      if (!db.objectStoreNames.contains(KEY_STORE)) db.createObjectStore(KEY_STORE, { keyPath: 'ownerKey' })
      if (!db.objectStoreNames.contains(RECORD_STORE)) {
        const store = db.createObjectStore(RECORD_STORE, { keyPath: 'id' })
        store.createIndex('ownerKey', 'ownerKey', { unique: false })
      }
    }
    request.onsuccess = () => {
      const db = request.result
      db.onversionchange = () => db.close()
      resolve(db)
    }
    request.onerror = () => reject(request.error ?? new Error('Could not open offline storage.'))
    request.onblocked = () => reject(new Error('Offline storage upgrade is blocked by another open tab.'))
  })
}

function requestValue<T>(request: IDBRequest<T>): Promise<T> {
  return new Promise((resolve, reject) => {
    request.onsuccess = () => resolve(request.result)
    request.onerror = () => reject(request.error ?? new Error('Offline storage request failed.'))
  })
}
function transactionDone(tx: IDBTransaction) {
  return new Promise<void>((resolve, reject) => {
    tx.oncomplete = () => resolve()
    tx.onabort = () => reject(tx.error ?? new Error('Offline storage transaction aborted.'))
    tx.onerror = () => reject(tx.error ?? new Error('Offline storage transaction failed.'))
  })
}
const encode = (bytes: Uint8Array) => btoa(String.fromCharCode(...bytes))
const decode = (value: string) => Uint8Array.from(atob(value), c => c.charCodeAt(0))

async function keyFor(ownerKey: string): Promise<CryptoKey> {
  const db = await openDb()
  try {
    const read = db.transaction(KEY_STORE, 'readonly')
    const existing = await requestValue<{ ownerKey: string; key: CryptoKey } | undefined>(read.objectStore(KEY_STORE).get(ownerKey))
    await transactionDone(read)
    if (existing?.key) return existing.key
    const key = await crypto.subtle.generateKey({ name: 'AES-GCM', length: 256 }, false, ['encrypt', 'decrypt'])
    const write = db.transaction(KEY_STORE, 'readwrite')
    write.objectStore(KEY_STORE).put({ ownerKey, key })
    await transactionDone(write)
    return key
  } finally { db.close() }
}

async function save<T>(ownerKey: string, id: string, kind: string, state: string, value: T): Promise<void> {
  const key = await keyFor(ownerKey)
  const iv = crypto.getRandomValues(new Uint8Array(12))
  const aad = new TextEncoder().encode(ownerKey + ':' + id + ':' + kind)
  const plain = new TextEncoder().encode(JSON.stringify(value))
  const encrypted = await crypto.subtle.encrypt({ name: 'AES-GCM', iv, additionalData: aad }, key, plain)
  const record: StoredRecord = {
    id, ownerKey, kind, state, createdAt: new Date().toISOString(), updatedAt: new Date().toISOString(),
    iv: encode(iv), ciphertext: encode(new Uint8Array(encrypted))
  }
  const db = await openDb()
  try {
    const tx = db.transaction(RECORD_STORE, 'readwrite')
    tx.objectStore(RECORD_STORE).put(record)
    await transactionDone(tx)
  } finally { db.close() }
}

async function read<T>(record: StoredRecord): Promise<T> {
  const key = await keyFor(record.ownerKey)
  const aad = new TextEncoder().encode(record.ownerKey + ':' + record.id + ':' + record.kind)
  const plain = await crypto.subtle.decrypt({ name: 'AES-GCM', iv: decode(record.iv), additionalData: aad }, key, decode(record.ciphertext))
  return JSON.parse(new TextDecoder().decode(plain)) as T
}

async function recordsFor(ownerKey: string): Promise<StoredRecord[]> {
  const db = await openDb()
  try {
    const tx = db.transaction(RECORD_STORE, 'readonly')
    const records = await requestValue<StoredRecord[]>(tx.objectStore(RECORD_STORE).index('ownerKey').getAll(ownerKey))
    await transactionDone(tx)
    return records
  } finally { db.close() }
}

export type OfflineSnapshot = {
  savedAt: string
  me: { email: string; tenantId: string; profileComplete: boolean; businessCategory: string | null }
  products: unknown[]
  locations: unknown[]
  stock: unknown[]
}

export async function saveOfflineSnapshot(ownerKey: string, snapshot: Omit<OfflineSnapshot, 'savedAt'>) {
  await save(ownerKey, 'tenant-snapshot', 'snapshot', 'ready', { ...snapshot, savedAt: new Date().toISOString() })
}

export async function loadOfflineSnapshot(ownerKey: string): Promise<OfflineSnapshot | null> {
  const records = await recordsFor(ownerKey)
  const record = records.find(x => x.id === 'tenant-snapshot' && x.kind === 'snapshot')
  return record ? read<OfflineSnapshot>(record) : null
}

export async function saveOfflineRecord<T>(ownerKey: string, id: string, kind: string, state: string, value: T) {
  await save(ownerKey, id, kind, state, value)
}

export async function readOfflineRecords<T>(ownerKey: string, kind: string): Promise<{ id: string; state: string; value: T; createdAt: string; updatedAt: string }[]> {
  const records = (await recordsFor(ownerKey)).filter(x => x.kind === kind)
  const result = []
  for (const record of records) {
    result.push({ id: record.id, state: record.state, value: await read<T>(record), createdAt: record.createdAt, updatedAt: record.updatedAt })
  }
  return result
}

export async function updateOfflineRecordState(ownerKey: string, id: string, state: string) {
  const db = await openDb()
  try {
    const tx = db.transaction(RECORD_STORE, 'readwrite')
    const store = tx.objectStore(RECORD_STORE)
    const record = await requestValue<StoredRecord | undefined>(store.get(id))
    if (record && record.ownerKey === ownerKey) {
      record.state = state
      record.updatedAt = new Date().toISOString()
      store.put(record)
    }
    await transactionDone(tx)
  } finally { db.close() }
}

export async function deleteOfflineRecord(ownerKey: string, id: string) {
  const db = await openDb()
  try {
    const tx = db.transaction(RECORD_STORE, 'readwrite')
    const store = tx.objectStore(RECORD_STORE)
    const record = await requestValue<StoredRecord | undefined>(store.get(id))
    if (record && record.ownerKey === ownerKey) store.delete(id)
    await transactionDone(tx)
  } finally { db.close() }
}

export async function clearOfflineWorkspace(ownerKey: string) {
  const db = await openDb()
  try {
    const tx = db.transaction([KEY_STORE, RECORD_STORE], 'readwrite')
    tx.objectStore(KEY_STORE).delete(ownerKey)
    const store = tx.objectStore(RECORD_STORE)
    const records = await requestValue<StoredRecord[]>(store.index('ownerKey').getAll(ownerKey))
    records.forEach(record => store.delete(record.id))
    await transactionDone(tx)
  } finally { db.close() }
}
