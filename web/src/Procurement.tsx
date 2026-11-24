import { t as tr } from './i18n'
import './billing-forms.css'
import './buy-procurement.css'
import { Paged } from './Paged'
import { InvoiceUpload } from './InvoiceUpload'
import type { InvoiceDraft } from './InvoiceUpload'
import { useCallback, useEffect, useState } from 'react'
import { api, ApiError } from './api'
import { BarcodeScanner, type ScanResult } from './BarcodeScanner'
import { SearchableSelect } from './SearchableSelect'

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
  const [paySupplierId, setPaySupplierId] = useState('')
  const [locationId, setLocationId] = useState('')
  const [date, setDate] = useState(today)
  const [ref, setRef] = useState('')
  const [productId, setProductId] = useState('')
  const [qty, setQty] = useState('')
  const [cost, setCost] = useState('')
  const [gst, setGst] = useState('')
  const [lines, setLines] = useState<Line[]>([])
  const [reviewReceipt, setReviewReceipt] = useState(false)
  const [showGst, setShowGst] = useState(false)
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
  const estimatedTotal = lines.reduce((sum, line) => sum + line.quantity * line.unitCost + (line.gstAmount ?? 0), 0)
  const addLine = () => {
    const quantity = Number(qty); const unitCost = Number(cost); const gstAmount = Number(gst) || 0
    if (!productId || !(quantity > 0) || !(unitCost > 0)) { setError('Choose a product and enter a positive quantity and unit cost.'); return }
    if (gstAmount < 0 || gstAmount > quantity * unitCost) { setError('GST amount must be between 0 and the line value before tax.'); return }
    setError(''); setLines([...lines.filter(l => l.productId !== productId), { productId, quantity, unitCost, gstAmount }]); setQty(''); setCost(''); setGst(''); setReviewReceipt(false)
  }
  const receive = () => run(async () => {
    const r = await api<{ total: number; replayed?: boolean }>('POST', '/purchases', { supplierId: sup, locationId: loc, receiptDate: date, supplierRef: ref || null, lines, invoiceFileId }, rkey)
    setLines([]); setRef(''); setInvoiceFileId(null); setRkey(newKey()); setReviewReceipt(false)
    return 'Receipt posted' + (r.replayed ? ' (already posted earlier).' : '.')
  })
  const pay = () => run(async () => {
    await api('POST', '/supplier-payments', { supplierId: sup, paymentDate: today(), amount: Number(amount), method, reference: pref || null }, pkey)
    setAmount(''); setPref(''); setPkey(newKey())
    return 'Payment recorded.'
  })
  const [statement, setStatement] = useState<{ supplier: string; outstanding: number; entries: { id: string; type: string; date: string; debit: number; credit: number; balance: number }[] } | null>(null)
  const [billId, setBillId] = useState(''); const [openBills, setOpenBills] = useState<{ id: string; date: string; reference: string | null; open: number }[]>([])
  useEffect(() => { setBillId(''); setStatement(null); if (!paySup) { setOpenBills([]); return } api<typeof openBills>('GET', '/suppliers/' + paySup + '/open-bills').then(setOpenBills).catch(() => setOpenBills([])) }, [paySup, payments])
  const showStatement = () => run(async () => { const id = v('pay') ? paySup : sup; setStatement(await api<NonNullable<typeof statement>>('GET', '/suppliers/' + id + '/statement')); return '' })
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
        <tbody>{page.map(s => <tr key={s.id}><td>{s.name}</td><td>{s.phone}</td><td>{s.notes}</td><td>{s.owed.toFixed(2)}</td><td><button type="button" className="secondary" onClick={() => { setSupEditId(s.id); setNam      {v('receive') && <div className="buy-receive">
        <header className="buy-screen-heading"><h2>{tr('buy.addTitle')}</h2><p>{tr('buy.addIntro')}</p></header>
        <section className="buy-section buy-upload-section" aria-label={tr('buy.bill')}>
          <div><h3>{tr('buy.bill')} <span>{tr('buy.optional')}</span></h3><p>{tr('buy.billHelp')}</p></div>
          <InvoiceUpload products={products} suppliers={suppliers.filter(s => s.isActive)} onSupplierAdded={() => { load().catch(() => undefined) }} onProductAdded={reload} onUse={(d: InvoiceDraft, picked) => { if (d.supplierId) setSupplierId(d.supplierId); if (d.date) setDate(d.date); if (d.invoiceNumber) setRef(d.invoiceNumber); setLines(picked); setInvoiceFileId(d.fileId); setReviewReceipt(false) }} />
        </section>
        {suppliers.filter(s => s.isActive).length === 0 && <p className="buy-notice">{tr('buy.noSupplier')} <a href="#/parties/suppliers">{tr('buy.addSupplier')}</a>.</p>}
        <section className="buy-section">
          <h3>{tr('buy.supplierReceipt')}</h3>
          <div className="buy-fields">
            <label>{tr('buy.supplier')} <select value={sup} onChange={e => { setSupplierId(e.target.value); setReviewReceipt(false) }}><option value="">{tr('buy.selectSupplier')}</option>{suppliers.filter(s => s.isActive).map(s => <option key={s.id} value={s.id}>{s.name}</option>)}</select></label>
            <label>{tr('buy.location')} <select value={loc} onChange={e => { setLocationId(e.target.value); setReviewReceipt(false) }}>{locations.map(l => <option key={l.id} value={l.id}>{l.name}</option>)}</select></label>
            <label>{tr('buy.receiptDate')} <input type="date" value={date} onChange={e => { setDate(e.target.value); setReviewReceipt(false) }} /></label>
            <label>{tr('buy.invoiceNo')} <input value={ref} onChange={e => { setRef(e.target.value); setReviewReceipt(false) }} /></label>
          </div>
        </section>
        <section className="buy-section">
          <div className="buy-section-heading"><div><h3>{tr('buy.items')}</h3><p>{tr('buy.itemsHelp')}</p></div><span className="buy-count">{lines.length} {lines.length === 1 ? tr('buy.item') : tr('buy.itemsPlural')}</span></div>
          <div className="buy-item-entry">
            <label className="buy-product-field">{tr('buy.product')} <select value={productId} onChange={e => { setProductId(e.target.value); setReviewReceipt(false) }}><option value="">{tr('buy.selectProduct')}</option>{products.map(p => <option key={p.id} value={p.id}>{p.name}</option>)}</select></label>
            <label>{tr('buy.quantity')} <input inputMode="decimal" value={qty} onChange={e => { setQty(e.target.value); setReviewReceipt(false) }} /></label>
            <label>{tr('buy.unitCost')} <input inputMode="decimal" value={cost} onChange={e => { setCost(e.target.value); setReviewReceipt(false) }} /></label>
            <button type="button" className="secondary buy-scan" onClick={() => setScanning(true)}>{tr('buy.scan')}</button>
            <button type="button" className="secondary buy-add-line" onClick={addLine}>{tr('buy.addItem')}</button>
          </div>
          <button type="button" className="buy-gst-toggle" aria-expanded={showGst || Number(gst) > 0} onClick={() => setShowGst(x => !x)}>{showGst || Number(gst) > 0 ? tr('buy.hideGst') : tr('buy.gstDetails')}</button>
          {(showGst || Number(gst) > 0) && <label className="buy-gst-field">{tr('buy.gstAmount')} <input inputMode="decimal" value={gst} onChange={e => { setGst(e.target.value); setReviewReceipt(false) }} /></label>}
          {scanning && <BarcodeScanner title={tr('buy.scan')} onScan={onScan} onClose={() => setScanning(false)} />}
          {unknownCode && <p role="status" className="buy-notice">{tr('buy.barcodeNew')} <strong>{unknownCode.code}</strong> <button type="button" className="secondary" onClick={linkUnknown}>{tr('buy.linkBarcode')}</button> {tr('buy.missingProduct')}</p>}
          {lines.length > 0 && <div className="buy-line-list" aria-label={tr('buy.items')}>{lines.map(l => <article className="buy-line-card" key={l.productId}>
            <div><strong>{nameOf(l.productId)}</strong><p>{l.quantity} × ₹{l.unitCost.toFixed(2)}{l.gstAmount ? ` · GST ₹${l.gstAmount.toFixed(2)}` : ''}</p></div>
            <div className="buy-line-end"><strong>₹{(l.quantity * l.unitCost + (l.gstAmount ?? 0)).toFixed(2)}</strong><button type="button" className="secondary" onClick={() => { setLines(current => current.filter(x => x.productId !== l.productId)); setReviewReceipt(false) }} aria-label={`${tr('buy.remove')} ${nameOf(l.productId)}`}>{tr('buy.remove')}</button></div>
          </article>)}</div>}
          {lines.length > 0 && <div className="buy-estimate"><span>{tr('buy.estimatedTotal')}</span><strong>₹{estimatedTotal.toFixed(2)}</strong><small>{tr('buy.estimateNote')}</small></div>}
        </section>
        <p className="buy-guidance">{tr('onboarding.purchaseGuidance')}</p>
        <button type="button" className="buy-primary" onClick={() => setReviewReceipt(true)} disabled={lines.length === 0 || !sup || !loc}>{tr('buy.review')}</button>
        {reviewReceipt && <section className="buy-review" role="region" aria-label={tr('buy.review')}>
          <h3>{tr('buy.review')}</h3><p>{tr('onboarding.purchaseReviewNote')}</p>
          <dl><dt>{tr('buy.supplier')}</dt><dd>{suppliers.find(s => s.id === sup)?.name ?? 'Not selected'}</dd><dt>{tr('buy.location')}</dt><dd>{locations.find(l => l.id === loc)?.name ?? 'Not selected'}</dd><dt>{tr('buy.receiptDate')}</dt><dd>{date}</dd><dt>{tr('buy.invoiceNo')}</dt><dd>{ref || 'Not entered'}</dd></dl>
          <ul>{lines.map(l => <li key={l.productId}>{nameOf(l.productId)} — {l.quantity} × ₹{l.unitCost.toFixed(2)} before GST{l.gstAmount ? `; GST entered ₹${l.gstAmount.toFixed(2)}` : '; no GST amount entered'}</li>)}</ul>
          <p className="buy-review-total"><span>{tr('buy.totalPreview')}</span><strong>₹{estimatedTotal.toFixed(2)}</strong></p>
          <p className="buy-guidance">{tr('buy.postingNote')}</p>
          <div className="buy-review-actions"><button type="button" className="secondary" onClick={() => setReviewReceipt(false)}>{tr('buy.edit')}</button><button type="button" onClick={receive}>{tr('buy.post')}</button></div>
        </section>}
      </div>}
ton type="button" className="secondary" onClick={() => setReviewReceipt(false)}>Back to edit</button><button type="button" onClick={receive}>Post receipt</button></div>
      </div>}
      </>}
      {v('pay') && <div className="buy-pay">
        <header className="buy-screen-heading"><h2>Pay supplier</h2><p>Record a payment made to your supplier.</p></header>
        <section className="buy-pay-form">
          <SearchableSelect id="buy-pay-supplier" label="Supplier" value={paySup} options={suppliers.filter(s => s.isActive).map(s => ({value:s.id,label:s.name,description:'Currently owed: ₹'+s.owed.toFixed(2),searchText:s.phone ?? ''}))} onChange={value => {setPaySupplierId(value);setBillId('');setStatement(null);setError('');setDone('')}} placeholder="Search and select supplier" emptyMessage="No supplier matches this search." />
          {paySup && <div className="buy-balance"><span>Currently owed</span><strong>₹{payOwed.toFixed(2)}</strong></div>}
          {!paySup && <p className="buy-pay-hint">Choose a supplier to see the amount due.</p>}
          {paySup && payOwed <= 0 && <p className="buy-pay-hint">No amount is currently due to this supplier.</p>}
          {paySup && payOwed > 0 && <>
            <label className="buy-pay-amount">Amount to pay (INR) <input inputMode="decimal" value={amount} aria-invalid={!!amountError} aria-describedby={amountError ? 'buy-pay-amount-error' : undefined} onChange={e=>setAmount(e.target.value)} /></label>
            {amountError && <p className="buy-field-error" id="buy-pay-amount-error">{amountError}</p>}
            {openBills.length>0 && <div className="buy-pay-allocation"><SearchableSelect id="buy-pay-bill" label="Apply to purchase bill (optional)" value={billId} options={openBills.map(b=>({value:b.id,label:b.date+(b.reference?' · '+b.reference:''),description:'Open amount: ₹'+b.open.toFixed(2),searchText:b.id}))} onChange={setBillId} placeholder="On supplier account (no specific bill)" emptyMessage="No open purchase bills." />{selectedBill&&<p>Will allocate ₹{allocationAmount.toFixed(2)}{Number(amount)>selectedBill.open?' · Any remaining amount stays on the supplier account.':''}</p>}</div>}
            <fieldset className="buy-methods"><legend>Paid by</legend>{(['Cash','Bank','UPI'] as const).map(value=><label key={value} className={method===value?'is-selected':''}><input type="radio" name="buy-payment-method" value={value} checked={method===value} onChange={()=>setMethod(value)}/>{value}</label>)}</fieldset>
            <label>Reference number (optional) <input value={pref} onChange={e=>setPref(e.target.value)}/></label>
            <button type="button" className="buy-pay-primary" onClick={pay} disabled={!paySup||!!amountError||payOwed<=0}>Save payment</button>
          </>}
          {paySup&&<button type="button" className="buy-statement-link" onClick={showStatement}>View supplier statement</button>}
          {statement&&v('pay')&&<div className="buy-statement" role="region" aria-label="Supplier statement"><h3>Statement for {statement.supplier}</h3><p>Currently owed: ₹{statement.outstanding.toFixed(2)}</p><Paged rows={statement.entries}>{page=><table><thead><tr><th>Date</th><th>Type</th><th>Purchase</th><th>Paid / returned</th><th>Balance</th></tr></thead><tbody>{page.map(e=><tr key={e.id}><td>{e.date}</td><td>{e.type}</td><td>{e.debit?e.debit.toFixed(2):'—'}</td><td>{e.credit?e.credit.toFixed(2):'—'}</td><td>{e.balance.toFixed(2)}</td></tr>)}</tbody></table>}</Paged><button type="button" className="secondary" onClick={()=>setStatement(null)}>Close statement</button></div>}
        </section>
      </div>}
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
