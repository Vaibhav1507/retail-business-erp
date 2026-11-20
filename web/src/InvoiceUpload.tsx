import './invoice-upload.css'
import { useState } from 'react'
import { api, apiForm } from './api'

type Opt = { id: string; name: string }
type DraftLine = { description: string; quantity: number | null; unitCost: number | null; amount: number | null; productId: string | null; productName: string | null; category?: string | null; brand?: string | null; gst?: number | null }
export type InvoiceDraft = { fileId: string; supplierName: string | null; supplierId: string | null; invoiceNumber: string | null; date: string | null; total: number | null; lines: DraftLine[]; warnings: string[]; subtotal?: number | null; taxAmount?: number | null }
type Row = { description: string; productId: string; qty: string; cost: string; gst: string; category?: string; brand?: string }
export type Picked = { productId: string; quantity: number; unitCost: number; gstAmount: number }

const MAX_BYTES = 8 * 1024 * 1024

export const toBase64 = (blob: Blob): Promise<string> => new Promise((resolve, reject) => {
  const reader = new FileReader()
  reader.onload = () => resolve(String(reader.result).split(',')[1] ?? '')
  reader.onerror = () => reject(reader.error ?? new Error('Could not read the file.'))
  reader.readAsDataURL(blob)
})

async function prepareImageBlob(file: File): Promise<Blob> {
  if (!['image/jpeg', 'image/png', 'image/webp'].includes(file.type))
    throw new Error('Choose a JPEG, PNG, WebP photo or a PDF.')
  const bitmap = await createImageBitmap(file)
  try {
    const scale = Math.min(1, 1800 / Math.max(bitmap.width, bitmap.height))
    const canvas = document.createElement('canvas')
    canvas.width = Math.max(1, Math.round(bitmap.width * scale))
    canvas.height = Math.max(1, Math.round(bitmap.height * scale))
    const context = canvas.getContext('2d')
    if (!context) throw new Error('Could not prepare the photo.')
    context.drawImage(bitmap, 0, 0, canvas.width, canvas.height)
    const blob = await new Promise<Blob | null>(resolve => canvas.toBlob(resolve, 'image/jpeg', 0.85))
    if (!blob) throw new Error('Could not prepare the photo.')
    if (blob.size === 0 || blob.size > MAX_BYTES) throw new Error('Prepared photo must be 8 MB or smaller.')
    return blob
  } finally {
    bitmap.close()
  }
}

// Compatibility helper used by the Stock Setup Assistant's existing JSON/base64
// product-catalogue import route. Keep that workflow unchanged while invoice
// purchases use prepareMultipartPage() below.
export async function prepare(file: File): Promise<{ contentType: string; data: string }> {
  if (file.type === 'application/pdf') {
    if (file.size === 0 || file.size > 12 * 1024 * 1024) throw new Error('PDF must be 12 MB or smaller.')
    return { contentType: file.type, data: await toBase64(file) }
  }
  const image = await prepareImageBlob(file)
  return { contentType: 'image/jpeg', data: await toBase64(image) }
}

// Binary-only preparation for the supplier invoice multipart route.
async function prepareMultipartPage(file: File): Promise<File> {
  if (file.type === 'application/pdf') {
    if (file.size === 0 || file.size > MAX_BYTES) throw new Error('PDF must be between 1 byte and 8 MB.')
    return file
  }
  const image = await prepareImageBlob(file)
  return new File([image], 'invoice-page.jpg', { type: 'image/jpeg' })
}

const NEW = '__new__'

const round2 = (n: number) => Math.round(n * 100) / 100
const lineNet = (r: Row) => Number(r.qty) * Number(r.cost)

function withSpreadTax(rows: Row[], tax: number): Row[] {
  const base = rows.filter(r => lineNet(r) > 0)
  const total = base.reduce((a, r) => a + lineNet(r), 0)
  if (!(tax > 0) || total <= 0 || rows.some(r => Number(r.gst) > 0)) return rows
  const last = base[base.length - 1]
  let left = round2(tax)
  return rows.map(r => {
    if (lineNet(r) <= 0) return r
    const g = r === last ? left : round2((tax * lineNet(r)) / total)
    left = round2(left - g)
    return { ...r, gst: g.toFixed(2) }
  })
}

export function InvoiceUpload({ products, suppliers, onUse, onProductAdded, onSupplierAdded }: { products: Opt[]; suppliers: Opt[]; onUse: (d: InvoiceDraft, lines: Picked[]) => void; onProductAdded?: () => void; onSupplierAdded?: () => void }) {
  const [supplierId, setSupplierId] = useState('')
  const [extraSup, setExtraSup] = useState<Opt[]>([])
  const supOptions = [...suppliers, ...extraSup.filter(x => !suppliers.some(s => s.id === x.id))]
  const [extra, setExtra] = useState<Opt[]>([])
  const options = [...products, ...extra.filter(x => !products.some(p => p.id === x.id))]
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const [draft, setDraft] = useState<InvoiceDraft | null>(null)
  const [rows, setRows] = useState<Row[]>([])

  const pick = async (files: File[]) => {
    if (files.length === 0) return
    if (files.length > 6) { setError('Choose at most 6 pages of one invoice.'); return }
    setBusy(true); setError(''); setDraft(null)
    try {
      const pages = await Promise.all(files.map(prepareMultipartPage))
      const totalBytes = pages.reduce((total, file) => total + file.size, 0)
      if (totalBytes > 12 * 1024 * 1024) throw new Error('The selected invoice pages together exceed 12 MB.')
      const form = new FormData()
      pages.forEach(file => form.append('pages', file, file.name))
      const d = await apiForm<InvoiceDraft>('POST', '/purchases/extract', form)
      setDraft(d); setSupplierId(d.supplierId ?? '')
      setRows(withSpreadTax(d.lines.map(l => ({ description: l.description, productId: l.productId ?? '', qty: l.quantity?.toString() ?? '',
        cost: l.unitCost?.toString() ?? '', gst: l.gst?.toString() ?? '', category: l.category ?? undefined, brand: l.brand ?? undefined })), d.taxAmount ?? 0))
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Could not read the invoice.')
    } finally {
      setBusy(false)
    }
  }

  const edit = (i: number, patch: Partial<Row>) => setRows(rows.map((r, n) => (n === i ? { ...r, ...patch } : r)))

  const create = async (items: Row[]) => {
    setError('')
    const made = new Map<string, string>()
    try {
      for (const it of items) {
        const name = it.description
        if (made.has(name)) continue
        const p = await api<{ id: string }>('POST', '/products', { name: name.trim().slice(0, 120), categoryName: it.category?.trim() || 'Uncategorised', isGeneric: !it.brand?.trim(), brand: it.brand?.trim() || null, baseUnit: 'pcs' })
        made.set(name, p.id)
        setExtra(x => [...x, { id: p.id, name: name.trim() }])
      }
      onProductAdded?.()
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Could not add the product.')
    }
    return made
  }
  const choose = async (i: number, value: string) => {
    if (value !== NEW) { edit(i, { productId: value }); return }
    const made = await create([rows[i]])
    const id = made.get(rows[i].description)
    if (id) setRows(rs => rs.map((r, n) => (n === i ? { ...r, productId: id } : r)))
  }
  const chooseSupplier = async (value: string) => {
    if (value !== NEW || !draft?.supplierName) { setSupplierId(value); return }
    setError('')
    const name = draft.supplierName.trim().slice(0, 120)
    try {
      const s = await api<{ id: string }>('POST', '/suppliers', { name })
      setExtraSup(x => [...x, { id: s.id, name }]); setSupplierId(s.id); onSupplierAdded?.()
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Could not add the supplier.')
    }
  }
  const unmatched = rows.filter(r => !r.productId)
  const addAll = async () => {
    const made = await create(unmatched)
    setRows(rs => rs.map(r => (!r.productId && made.has(r.description) ? { ...r, productId: made.get(r.description)! } : r)))
  }
  const valid = rows.filter(r => r.productId && Number(r.qty) > 0 && Number(r.cost) > 0)
  const skipped = rows.filter(r => !valid.includes(r) && lineNet(r) > 0)
  const skippedGst = round2(skipped.reduce((s, r) => s + (Number(r.gst) || 0), 0))
  const skippedNet = round2(skipped.reduce((s, r) => s + lineNet(r), 0))

  const apply = () => {
    if (!draft) return
    const byProduct = new Map<string, Picked>()
    valid.forEach(r => {
      const q = Number(r.qty); const c = Number(r.cost); const g = round2(Number(r.gst) || 0)
      const prev = byProduct.get(r.productId)
      if (!prev) byProduct.set(r.productId, { productId: r.productId, quantity: q, unitCost: c, gstAmount: g })
      else { const qty = prev.quantity + q; byProduct.set(r.productId, { productId: r.productId, quantity: qty, unitCost: Math.round(((prev.quantity * prev.unitCost + q * c) / qty) * 10000) / 10000, gstAmount: Math.round((prev.gstAmount + g) * 100) / 100 }) }
    })
    onUse({ ...draft, supplierId: supplierId || null }, [...byProduct.values()])
    setDraft(null); setRows([])
  }

  return (
    <div role="region" aria-label="Read invoice" className="invoice-upload">
      <label className="invoice-upload-pick">
        Upload invoice photo or PDF (optional). Select all pages of one invoice together.
        <input type="file" accept="image/jpeg,image/png,image/webp,application/pdf" multiple disabled={busy} onChange={e => { void pick(Array.from(e.target.files ?? [])); e.target.value = '' }} />
      </label>
      {busy && <p role="status">Reading invoice...</p>}
      {error && <p role="alert">{error}</p>}
      {draft && (
        <div className="invoice-upload-review">
          <p className="invoice-upload-meta">
            {draft.supplierName ? `Supplier: ${draft.supplierName}` : 'Supplier not found'}
            {draft.invoiceNumber ? ` | Invoice ${draft.invoiceNumber}` : ''}
            {draft.date ? ` | ${draft.date}` : ''}
            {draft.taxAmount != null ? ` | GST ${draft.taxAmount.toFixed(2)}` : ''}
            {draft.total != null ? ` | Total ${draft.total.toFixed(2)}` : ''}
          </p>
          <label>Supplier
            <select aria-label="Supplier for this invoice" value={supplierId} onChange={e => { void chooseSupplier(e.target.value) }}>
              <option value="">{draft.supplierName ? 'Choose supplier' : 'No supplier'}</option>
              {draft.supplierName && <option value={NEW}>+ Add "{draft.supplierName}" as new supplier</option>}
              {supOptions.map(s => <option key={s.id} value={s.id}>{s.name}</option>)}
            </select>
          </label>
          {draft.warnings.length > 0 && <ul className="invoice-upload-warn">{draft.warnings.map(w => <li key={w}>{w}</li>)}</ul>}
          {skipped.length > 0 && <p role="status" className="invoice-upload-warn">{skipped.length} line(s) are skipped: cost {skippedNet.toFixed(2)} and GST {skippedGst.toFixed(2)} will NOT be recorded. Choose a product for each line to record the full invoice.</p>}
          <div className="table-scroll">
            <table>
              <thead><tr><th>On invoice</th><th>Product</th><th>Quantity</th><th>Unit cost</th><th>GST amount</th></tr></thead>
              <tbody>
                {rows.map((r, i) => (
                  <tr key={i}>
                    <td>{r.description}</td>
                    <td>
                      <select aria-label={`Product for ${r.description}`} value={r.productId} onChange={e => { void choose(i, e.target.value) }}>
                        <option value="">Skip this line</option>
                        <option value={NEW}>+ Add "{r.description}" as new product</option>
                        {options.map(p => <option key={p.id} value={p.id}>{p.name}</option>)}
                      </select>
                    </td>
                    <td><input aria-label="Quantity" inputMode="decimal" value={r.qty} onChange={e => edit(i, { qty: e.target.value })} /></td>
                    <td><input aria-label="Unit cost" inputMode="decimal" value={r.cost} onChange={e => edit(i, { cost: e.target.value })} /></td>
                    <td><input aria-label="GST amount" inputMode="decimal" value={r.gst} onChange={e => edit(i, { gst: e.target.value })} /></td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <div className="invoice-upload-actions">
            {unmatched.length > 0 && <button type="button" className="secondary" onClick={() => { void addAll() }}>Add {unmatched.length} unmatched as new products</button>}
            <button type="button" onClick={apply} disabled={valid.length === 0}>Use {valid.length} line{valid.length === 1 ? '' : 's'}</button>
            <button type="button" className="secondary" onClick={() => { setDraft(null); setRows([]) }}>Discard</button>
          </div>
        </div>
      )}
    </div>
  )
}
