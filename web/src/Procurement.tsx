import { t as tr } from './i18n'
import './billing-forms.css'
import { Paged } from './Paged'
import { InvoiceUpload } from './InvoiceUpload'
import type { InvoiceDraft } from './InvoiceUpload'
import { useCallback, useEffect, useState } from 'react'
import { api, ApiError } from './api'
import { BarcodeScanner, type ScanResult } from './BarcodeScanner'

type Supplier = { id: string; name: string; phone: string | null; notes: string | null; isActive: boolean; owed: number }
type Receipt = { id: string; date: string; supplier: string; supplierRef: string | null; total: number; hasInvoice?: boolean }
type Payment = { id: string; date: string; supplier: string; amount: number; method: string; reference: string | null }
type Opt = { id: string; name: string }
type Line = { productId: string; quantity: number; unitCost: number; gstAmount?: number }

const newKey = () => crypto.randomUUID()
const today = () => new Date().toISOString().slice(0, 10)
const msg = (e: unknown) => (e instanceof ApiError && e.status >= 500 ? 'Server error. Retrying is safe.' : e instanceof Error ? e.message : 'Something went wrong')

export type BuyPart = 'receive' | 'pay' | 'suppliers' | 'history'

export function Procurement({ products, locations, reload, show }: { products: (Opt & { sku?: string })[]; locations: Opt[]; reload: () => void; show?: BuyPart }) {
  const v = (p: BuyPart) => !show || show === p
  const [suppliers, setSuppliers] = useState<Supplier[]>([])
  const [receipts, setReceipts] = useState<Receipt[]>([])
  const [payments, setPayments] = useState<Payment[]>([])
  const [error, setError] = useState('')
  const [done, setDone] = useState('')
  const [name, setName] = useState('')
  const [phone, setPhone] = useState(''); const [supNotes, setSupNotes] = useState(''); const [supEditId, setSupEditId] = useState<string | null>(null)
  const [supplierId, setSupplierId] = useState('')
  const [locationId, setLocationId] = useState('')
  const [date, setDate] = useState(today)
  const [ref, setRef] = useState('')
  const [productId, setProductId] = useState('')
  const [qty, setQty] = useState('')
  const [cost, setCost] = useState('')
  const [gst, setGst] = useState('')
  const [lines, setLines] = useState<Line[]>([])
  const [rkey, setRkey] = useState(newKey)
  const [invoiceFileId, setInvoiceFileId] = useState<string | null>(null)
  const [amount, setAmount] = useState('')
  const [method, setMethod] = useState('Cash')
  const [pref, setPref] = useState('')
  const [pkey, setPkey] = useState(newKey)

  const load = useCallback(async () => {
    const [s, r, p] = await Promise.all([api<Supplier[]>('GET', '/suppliers'), api<Receipt[]>('GET', '/purchases'), api<Payment[]>('GET', '/supplier-payments')])
    setSuppliers(s); setReceipts(r); setPayments(p)
  }, [])
  useEffect(() => { load().catch(e => setError(msg(e))) }, [load])
  const sup = supplierId || suppliers.find(s => s.isActive)?.id || ''
  const loc = locationId || locations[0]?.id || ''
  const nameOf = (id: string) => products.find(p => p.id === id)?.name ?? id
  const run = async (fn: () => Promise<string>) => {
    setError(''); setDone('')
    try { setDone(await fn()); await load(); reload() } catch (e) { setError(msg(e)) }
  }
  const addSupplier = () => run(async () => { const body = { name, phone: phone || null, notes: supNotes || null, isActive: suppliers.find(s => s.id === supEditId)?.isActive ?? true }; const was = supEditId; if (was) await api('PUT', '/suppliers/' + was, body); else await api('POST', '/suppliers', body); setName(''); setPhone(''); setSupNotes(''); setSupEditId(null); return was ? 'Supplier updated.' : 'Supplier added.' })
  const [scanning, setScanning] = useState(false)
  const [unknownCode, setUnknownCode] = useState<ScanResult | null>(null)
  async function onScan(r: ScanResult) {
    setScanning(false); setError(''); setDone('')
    try {
      const hit = await api<{ productId: string; packQuantity: number; name: string }>('GET', `/products/by-barcode?code=${encodeURIComponent(r.code)}`)
      setUnknownCode(null)
      if (lines.some(l => l.productId === hit.productId)) {
        setLines(lines.map(l => l.productId === hit.productId ? { ...l, quantity: l.quantity + hit.packQuantity } : l))
        setDone(`${hit.name}: added ${hit.packQuantity} more. Check the quantity before posting.`)
      } else {
        setProductId(hit.productId); setQty(String(hit.packQuantity)); setCost('')
        setDone(`${hit.name} selected with quantity ${hit.packQuantity}. Enter the unit cost and choose Add line.`)
      }
    } catch (e) {
      if (msg(e).includes('No product has this barcode')) setUnknownCode(r); else setError(msg(e))
    }
  }
  async function linkUnknown() {
    if (!unknownCode || !productId) { setError('Choose the product to link this barcode to first.'); return }
    try { await api('POST', `/products/${productId}/barcodes`, { code: unknownCode.code, symbology: unknownCode.symbology, packQuantity: Number(qty) || 1 }); setUnknownCode(null); setDone('Barcode linked to the selected product.') } catch (e) { setError(msg(e)) }
  }
  const addLine = () => {
    const quantity = Number(qty); const unitCost = Number(cost); const gstAmount = Number(gst) || 0
    if (!productId || !(quantity > 0) || !(unitCost > 0)) { setError('Choose a product and enter a positive quantity and unit cost.'); return }
    if (gstAmount < 0 || gstAmount > quantity * unitCost) { setError('GST amount must be between 0 and the line value before tax.'); return }
    setError(''); setLines([...lines.filter(l => l.productId !== productId), { productId, quantity, unitCost, gstAmount }]); setQty(''); setCost(''); setGst('')
  }
  const receive = () => run(async () => {
    const r = await api<{ total: number; replayed?: boolean }>('POST', '/purchases', { supplierId: sup, locationId: loc, receiptDate: date, supplierRef: ref || null, lines, invoiceFileId }, rkey)
    setLines([]); setRef(''); setInvoiceFileId(null); setRkey(newKey())
    return 'Receipt posted' + (r.replayed ? ' (already posted earlier).' : '.')
  })
  const pay = () => run(async () => {
    await api('POST', '/supplier-payments', { supplierId: sup, paymentDate: today(), amount: Number(amount), method, reference: pref || null }, pkey)
    setAmount(''); setPref(''); setPkey(newKey())
    return 'Payment recorded.'
  })
  const [statement, setStatement] = useState<{ supplier: string; outstanding: number; entries: { id: string; type: string; date: string; debit: number; credit: number; balance: number }[] } | null>(null)
  const [billId, setBillId] = useState(''); const [openBills, setOpenBills] = useState<{ id: string; date: string; reference: string | null; open: number }[]>([])
  useEffect(() => { setBillId(''); if (!sup) { setOpenBills([]); return } api<typeof openBills>('GET', '/suppliers/' + sup + '/open-bills').then(setOpenBills).catch(() => setOpenBills([])) }, [sup, payments])
  const showStatement = () => run(async () => { setStatement(await api<NonNullable<typeof statement>>('GET', '/suppliers/' + sup + '/statement')); return '' })
  const owed = suppliers.find(s => s.id === sup)?.owed ?? 0
  return (
    <section aria-label="Purchasing" className="billing-screen">
      {!show && <h2>Suppliers and purchasing</h2>}
      {v('suppliers') && <>
      <div>
        <label>Supplier name <input value={name} onChange={e => setName(e.target.value)} /></label>
        <label>Phone <input value={phone} onChange={e => setPhone(e.target.value)} /></label>
        <label>Notes <input value={supNotes} onChange={e => setSupNotes(e.target.value)} /></label>
		<button type="button" onClick={addSupplier} disabled={!name.trim()}>{supEditId ? 'Save changes' : 'Add supplier'}</button>
		{supEditId && <button type="button" className="secondary" onClick={() => { setSupEditId(null); setName(''); setPhone(''); setSupNotes('') }}>Cancel</button>}
      </div>
      <Paged rows={suppliers} empty={tr('sup.none')}>{page => (
      <table>
        <thead><tr><th>Supplier</th><th>Phone</th><th>Notes</th><th>You owe</th><th>Actions</th></tr></thead>
        <tbody>{page.map(s => <tr key={s.id}><td>{s.name}</td><td>{s.phone}</td><td>{s.notes}</td><td>{s.owed.toFixed(2)}</td><td><button type="button" className="secondary" onClick={() => { setSupEditId(s.id); setName(s.name); setPhone(s.phone ?? ''); setSupNotes(s.notes ?? ''); window.scrollTo({ top: 0, behavior: 'smooth' }) }}>Edit</button></td></tr>)}</tbody>
      </table>
      )}</Paged>
      </>}
      {v('receive') && <>
      
      <InvoiceUpload products={products} suppliers={suppliers.filter(s => s.isActive)} onSupplierAdded={() => { load().catch(() => undefined) }} onProductAdded={reload} onUse={(d: InvoiceDraft, picked) => { if (d.supplierId) setSupplierId(d.supplierId); if (d.date) setDate(d.date); if (d.invoiceNumber) setRef(d.invoiceNumber); setLines(picked); setInvoiceFileId(d.fileId) }} />
      <label>Supplier <select value={sup} onChange={e => setSupplierId(e.target.value)}>{suppliers.filter(s => s.isActive).map(s => <option key={s.id} value={s.id}>{s.name}</option>)}</select></label>
      <label>Location <select value={loc} onChange={e => setLocationId(e.target.value)}>{locations.map(l => <option key={l.id} value={l.id}>{l.name}</option>)}</select></label>
      <label>Receipt date <input type="date" value={date} onChange={e => setDate(e.target.value)} /></label>
      <label>Supplier invoice no. <input value={ref} onChange={e => setRef(e.target.value)} /></label>
      <div>
        <label>Product <select value={productId} onChange={e => setProductId(e.target.value)}><option value="">Select</option>{products.map(p => <option key={p.id} value={p.id}>{p.name}</option>)}</select></label>
        <label>Quantity <input inputMode="decimal" value={qty} onChange={e => setQty(e.target.value)} /></label>
        <label>Unit cost (INR, before GST) <input inputMode="decimal" value={cost} onChange={e => setCost(e.target.value)} /></label>
                <label>GST on this line (INR, optional) <input inputMode="decimal" value={gst} onChange={e => setGst(e.target.value)} /></label>
        <button type="button" className="secondary" onClick={() => setScanning(true)}>Scan barcode</button>
        <button type="button" onClick={addLine}>Add line</button>
      </div>
      {scanning && <BarcodeScanner title="Scan product barcode" onScan={onScan} onClose={() => setScanning(false)} />}
      {unknownCode && <p role="status">Barcode <strong>{unknownCode.code}</strong> is not in your products. Pick the matching product above and <button type="button" className="secondary" onClick={linkUnknown}>Link barcode to selected product</button>, or add it as a new product under Manage Products and scan it there.</p>}
      <ul>{lines.map(l => <li key={l.productId}>{nameOf(l.productId)}: {l.quantity} x {l.unitCost}{l.gstAmount ? ` + GST ${l.gstAmount}` : ''}</li>)}</ul>
      <button type="button" onClick={receive} disabled={lines.length === 0 || !sup}>Post receipt</button>
      </>}
      {v('pay') && <>
      
      <p>You owe this supplier: Rs. {owed.toFixed(2)}</p>
      <label>Amount to pay (Rs.) <input inputMode="decimal" value={amount} onChange={e => setAmount(e.target.value)} /></label>
      {openBills.length > 0 && <label>Apply to bill (optional) <select value={billId} onChange={e => setBillId(e.target.value)}><option value="">On account (no specific bill)</option>{openBills.map(b => <option key={b.id} value={b.id}>{b.date} {b.reference ?? ""} - open {b.open.toFixed(2)}</option>)}</select></label>}
      <label>Paid by <select value={method} onChange={e => setMethod(e.target.value)}><option>Cash</option><option>Bank</option><option>UPI</option></select></label>
      <label>Note or reference no. <input value={pref} onChange={e => setPref(e.target.value)} /></label>
      <button type="button" onClick={pay} disabled={!sup || !(Number(amount) > 0)}>Save payment</button>
      </>}
      {error && <p role="alert">{error}</p>}
      {done && <p role="status">{done}</p>}
      {v('history') && <>
      <h3>Past purchases</h3>
      <Paged rows={receipts}>{page => (
      <table><thead><tr><th>Date</th><th>Supplier</th><th>Invoice no.</th><th>Total</th><th>Bill copy</th></tr></thead><tbody>{page.map(r => <tr key={r.id}><td data-label="Date">{r.date}</td><td data-label="Supplier">{r.supplier}</td><td data-label="Invoice no.">{r.supplierRef}</td><td data-label="Total">{r.total.toFixed(2)}</td><td data-label="Bill copy">{r.hasInvoice ? <a href={`/api/purchases/${r.id}/invoice`} target="_blank" rel="noreferrer">View</a> : "-"}</td></tr>)}</tbody></table>
      )}</Paged>
      {sup && <button type="button" onClick={showStatement}>Show supplier statement</button>}
      {statement && <div role="region" aria-label="Supplier statement"><h4>Statement: {statement.supplier} (we owe {statement.outstanding.toFixed(2)} INR)</h4><Paged rows={statement.entries}>{page => (<table><thead><tr><th>Date</th><th>Type</th><th>Bill</th><th>Paid/Returned</th><th>Balance</th></tr></thead><tbody>{page.map(e => <tr key={e.id}><td>{e.date}</td><td>{e.type}</td><td>{e.debit ? e.debit.toFixed(2) : ""}</td><td>{e.credit ? e.credit.toFixed(2) : ""}</td><td>{e.balance.toFixed(2)}</td></tr>)}</tbody></table>)}</Paged><button type="button" onClick={() => setStatement(null)}>Close statement</button></div>}
      <h3>Payments made to suppliers</h3>
      <Paged rows={payments}>{page => (
      <table><thead><tr><th>Date</th><th>Supplier</th><th>Method</th><th>Amount paid</th></tr></thead><tbody>{page.map(p => <tr key={p.id}><td data-label="Date">{p.date}</td><td data-label="Supplier">{p.supplier}</td><td data-label="Method">{p.method}</td><td data-label="Amount paid">{p.amount.toFixed(2)}</td></tr>)}</tbody></table>
      )}</Paged>
            </>}
          </section>
  )
}
