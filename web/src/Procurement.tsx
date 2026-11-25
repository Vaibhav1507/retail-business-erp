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
  const [historySupplierId, setHistorySupplierId] = useState('')
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
  const paySup = paySupplierId
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
        setDone(tr('buy.barcodeQuantityAdded', { product: hit.name, quantity: hit.packQuantity }))
      } else {
        setProductId(hit.productId); setQty(String(hit.packQuantity)); setCost('')
        setDone(tr('buy.barcodeProductSelected', { product: hit.name, quantity: hit.packQuantity }))
      }
    } catch (e) {
      if (msg(e).includes('No product has this barcode')) setUnknownCode(r); else setError(msg(e))
    }
  }
  async function linkUnknown() {
    if (!unknownCode || !productId) { setError(tr('buy.linkBarcodeSelectProduct')); return }
    try { await api('POST', `/products/${productId}/barcodes`, { code: unknownCode.code, symbology: unknownCode.symbology, packQuantity: Number(qty) || 1 }); setUnknownCode(null); setDone(tr('buy.barcodeLinked')) } catch (e) { setError(msg(e)) }
  }
  const estimatedTotal = lines.reduce((sum, line) => sum + line.quantity * line.unitCost + (line.gstAmount ?? 0), 0)
  const addLine = () => {
    const quantity = Number(qty); const unitCost = Number(cost); const gstAmount = Number(gst) || 0
    if (!productId || !(quantity > 0) || !(unitCost > 0)) { setError(tr('buy.lineFieldsRequired')); return }
    if (gstAmount < 0 || gstAmount > quantity * unitCost) { setError(tr('buy.gstAmountRange')); return }
    setError(''); setLines([...lines.filter(l => l.productId !== productId), { productId, quantity, unitCost, gstAmount }]); setQty(''); setCost(''); setGst(''); setReviewReceipt(false)
  }
  const receive = () => run(async () => {
    const r = await api<{ total: number; replayed?: boolean }>('POST', '/purchases', { supplierId: sup, locationId: loc, receiptDate: date, supplierRef: ref || null, lines, invoiceFileId }, rkey)
    setLines([]); setRef(''); setInvoiceFileId(null); setRkey(newKey()); setReviewReceipt(false)
    return tr('buy.receiptPosted') + (r.replayed ? ' ' + tr('buy.receiptPostedEarlier') : '')
  })
  const pay = () => run(async () => {
    const allocations = selectedBill && allocationAmount > 0 ? [{ billId: selectedBill.id, amount: allocationAmount }] : undefined
    const result = await api<{ owed: number }>('POST', '/supplier-payments', { supplierId: paySup, paymentDate: today(), amount: Number(amount), method, reference: pref || null, ...(allocations ? { allocations } : {}) }, pkey)
    setAmount(''); setPref(''); setBillId(''); setPkey(newKey())
    return tr('buy.paymentSuccess', { balance: result.owed.toFixed(2) })
  })
  const [statement, setStatement] = useState<{ supplier: string; outstanding: number; entries: { id: string; type: string; date: string; debit: number; credit: number; balance: number }[] } | null>(null)
  const [billId, setBillId] = useState(''); const [openBills, setOpenBills] = useState<{ id: string; date: string; reference: string | null; open: number }[]>([])
  const selectedBill = openBills.find(b => b.id === billId)
  const allocationAmount = selectedBill ? Math.min(Number(amount) || 0, selectedBill.open) : 0
  const payOwed = suppliers.find(s => s.id === paySup)?.owed ?? 0
  const amountError = amount.trim() === '' ? tr('buy.amountRequired') : !(Number(amount) > 0) ? tr('buy.amountPositive') : !/^\d+(?:\.\d{1,2})?$/.test(amount) ? tr('buy.amountDecimals') : Number(amount) > payOwed ? tr('buy.amountBalance') : ''
  useEffect(() => { setBillId(''); if (!paySup) { setOpenBills([]); return } api<typeof openBills>('GET', '/suppliers/' + paySup + '/open-bills').then(setOpenBills).catch(() => setOpenBills([])) }, [paySup, payments])
  const showStatement = () => run(async () => { const id = v('pay') ? paySup : historySupplierId; setStatement(await api<NonNullable<typeof statement>>('GET', '/suppliers/' + id + '/statement')); return '' })
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
            {v('receive') && <div className="buy-receive">
        <div className="buy-purchase-card">
        <header className="buy-screen-heading">
          <div className="buy-step-heading"><span>{tr('buy.newStockEntry')}</span><span className="buy-step-count">{reviewReceipt ? tr('buy.stepTwoOfTwo') : tr('buy.stepOneOfTwo')}</span></div>
          <h2>{tr('buy.addTitle')}</h2><p>{tr('buy.addIntro')}</p>
        </header>
        <section className="buy-section buy-upload-section" aria-label={tr('buy.bill')}>
          <div><h3>{tr('buy.bill')} <span>{tr('buy.optional')}</span></h3><p>{tr('buy.billHelp')}</p></div>
          <InvoiceUpload products={products} suppliers={suppliers.filter(s => s.isActive)} onSupplierAdded={() => { load().catch(() => undefined) }} onProductAdded={reload} onUse={(d: InvoiceDraft, picked) => { if (d.supplierId) setSupplierId(d.supplierId); if (d.date) setDate(d.date); if (d.invoiceNumber) setRef(d.invoiceNumber); setLines(picked); setInvoiceFileId(d.fileId); setReviewReceipt(false) }} />
        </section>
        {suppliers.filter(s => s.isActive).length === 0 && <p className="buy-notice">{tr('buy.noSupplier')} <a href="#/parties/suppliers">{tr('buy.addSupplier')}</a>.</p>}
        <section className="buy-section buy-receipt-section">
          <h3>{tr('buy.supplierReceipt')}</h3>
          <div className="buy-fields">
            <label><span className="buy-field-label">{tr('buy.supplier')} <span className="buy-required" aria-hidden="true">*</span></span><select required aria-required="true" value={sup} onChange={e => { setSupplierId(e.target.value); setReviewReceipt(false) }}><option value="">{tr('buy.selectSupplier')}</option>{suppliers.filter(s => s.isActive).map(s => <option key={s.id} value={s.id}>{s.name}</option>)}</select></label>
            <label><span className="buy-field-label">{tr('buy.location')} <span className="buy-required" aria-hidden="true">*</span></span><select required aria-required="true" value={loc} onChange={e => { setLocationId(e.target.value); setReviewReceipt(false) }}>{locations.map(l => <option key={l.id} value={l.id}>{l.name}</option>)}</select></label>
            <label><span className="buy-field-label">{tr('buy.receiptDate')} <span className="buy-required" aria-hidden="true">*</span></span><input required aria-required="true" type="date" value={date} onChange={e => { setDate(e.target.value); setReviewReceipt(false) }} /></label>
            <label><span className="buy-field-label">{tr('buy.invoiceNo')}</span><input value={ref} onChange={e => { setRef(e.target.value); setReviewReceipt(false) }} /></label>
          </div>
        </section>
        <section className="buy-section buy-items-section">
          <div className="buy-section-heading"><div><h3>{tr('buy.items')}</h3><p>{tr('buy.itemsHelp')}</p></div><span className="buy-count">{lines.length} {lines.length === 1 ? tr('buy.item') : tr('buy.itemsPlural')}</span></div>
          <div className="buy-item-entry">
            <label className="buy-product-field"><span className="buy-field-label">{tr('buy.product')} <span className="buy-required" aria-hidden="true">*</span></span><select required aria-required="true" value={productId} onChange={e => { setProductId(e.target.value); setReviewReceipt(false) }}><option value="">{tr('buy.selectProduct')}</option>{products.map(p => <option key={p.id} value={p.id}>{p.name}</option>)}</select></label>
            <label><span className="buy-field-label">{tr('buy.quantity')} <span className="buy-required" aria-hidden="true">*</span></span><input required aria-required="true" inputMode="decimal" value={qty} onChange={e => { setQty(e.target.value); setReviewReceipt(false) }} /></label>
            <label><span className="buy-field-label">{tr('buy.unitCostShort')} <span className="buy-required" aria-hidden="true">*</span></span><input required aria-required="true" aria-label={tr('buy.unitCost')} inputMode="decimal" value={cost} onChange={e => { setCost(e.target.value); setReviewReceipt(false) }} /></label>
            <button type="button" className="secondary buy-scan" onClick={() => setScanning(true)}>
              <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M4 5v14M7 5v14M10 5v14M13 5v14M17 5v14M20 5v14" /></svg>
              {tr('buy.scan')}
            </button>
            <button type="button" className="secondary buy-add-line" onClick={addLine}>
              <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M12 5v14M5 12h14" /></svg>
              {tr('buy.addItem')}
            </button>
          </div>
          <p className="buy-item-entry-help">{tr('buy.productEntryHelp')}</p>
          <div className="buy-gst-disclosure">
            <button type="button" className="buy-gst-toggle" aria-expanded={showGst || Number(gst) > 0} onClick={() => setShowGst(x => !x)}>
              <span className="buy-gst-copy"><strong>{tr('buy.gstHeading')}</strong><small>{tr('buy.gstOptionalHelp')}</small></span>
              <span className="buy-gst-chevron" aria-hidden="true">⌄</span>
            </button>
            {(showGst || Number(gst) > 0) && <label className="buy-gst-field">{tr('buy.gstAmount')} <input inputMode="decimal" value={gst} onChange={e => { setGst(e.target.value); setReviewReceipt(false) }} /></label>}
          </div>
          {scanning && <BarcodeScanner title={tr('buy.scan')} onScan={onScan} onClose={() => setScanning(false)} />}
          {unknownCode && <p role="status" className="buy-notice">{tr('buy.barcodeNew')} <strong>{unknownCode.code}</strong> <button type="button" className="secondary" onClick={linkUnknown}>{tr('buy.linkBarcode')}</button> {tr('buy.missingProduct')}</p>}
          {lines.length > 0 && <div className="buy-line-list" aria-label={tr('buy.items')}>{lines.map(l => <article className="buy-line-card" key={l.productId}>
            <div><strong>{nameOf(l.productId)}</strong><p>{l.quantity} × ₹{l.unitCost.toFixed(2)}{l.gstAmount ? ` · GST ₹${l.gstAmount.toFixed(2)}` : ''}</p></div>
            <div className="buy-line-end"><strong>₹{(l.quantity * l.unitCost + (l.gstAmount ?? 0)).toFixed(2)}</strong><button type="button" className="secondary" onClick={() => { setLines(current => current.filter(x => x.productId !== l.productId)); setReviewReceipt(false) }} aria-label={`${tr('buy.remove')} ${nameOf(l.productId)}`}>{tr('buy.remove')}</button></div>
          </article>)}</div>}
          {lines.length > 0 && <div className="buy-estimate"><span>{tr('buy.estimatedTotal')}</span><strong>₹{estimatedTotal.toFixed(2)}</strong><small>{tr('buy.estimateNote')}</small></div>}
        </section>
        <p className="buy-guidance">{tr('onboarding.purchaseGuidance')}</p>
        </div>
        {!reviewReceipt && <div className="buy-review-bar">
          <div className="buy-review-bar-summary"><strong>{lines.length} {lines.length === 1 ? tr('buy.item') : tr('buy.itemsPlural')}</strong><span>{lines.length ? tr('buy.reviewBarTotal', { amount: estimatedTotal.toFixed(2) }) : tr('buy.addProductsToContinue')}</span></div>
          <button type="button" className="buy-review-bar-action" onClick={() => { setReviewReceipt(true); window.scrollTo({ top: 0, behavior: 'smooth' }) }} disabled={lines.length === 0 || !sup || !loc}>{tr('buy.review')} <span aria-hidden="true">›</span></button>
        </div>}
        {reviewReceipt && <section className="buy-review" role="region" aria-label={tr('buy.review')}>
          <h3>{tr('buy.review')}</h3><p>{tr('onboarding.purchaseReviewNote')}</p>
          <dl><dt>{tr('buy.supplier')}</dt><dd>{suppliers.find(s => s.id === sup)?.name ?? tr('buy.notSelected')}</dd><dt>{tr('buy.location')}</dt><dd>{locations.find(l => l.id === loc)?.name ?? tr('buy.notSelected')}</dd><dt>{tr('buy.receiptDate')}</dt><dd>{date}</dd><dt>{tr('buy.invoiceNo')}</dt><dd>{ref || tr('buy.notEntered')}</dd></dl>
          <ul>{lines.map(l => <li key={l.productId}>{nameOf(l.productId)} — {l.quantity} × ₹{l.unitCost.toFixed(2)} {tr('buy.beforeGst')}{l.gstAmount ? `; ${tr('buy.gstEntered', { amount: l.gstAmount.toFixed(2) })}`  : '; ' + tr('buy.noGstEntered')}</li>)}</ul>
          <p className="buy-review-total"><span>{tr('buy.totalPreview')}</span><strong>₹{estimatedTotal.toFixed(2)}</strong></p>
          <p className="buy-guidance">{tr('buy.postingNote')}</p>
          <div className="buy-review-actions"><button type="button" className="secondary" onClick={() => setReviewReceipt(false)}>{tr('buy.edit')}</button><button type="button" onClick={receive}>{tr('buy.post')}</button></div>
        </section>}
      </div>}

      {v('pay') && <div className="buy-pay">
        <header className="buy-screen-heading"><h2>{tr('buy.payTitle')}</h2><p>{tr('buy.payIntro')}</p></header>
        <section className="buy-pay-form">
          <SearchableSelect id="buy-pay-supplier" label={tr('buy.supplier')} value={paySup} options={suppliers.filter(s => s.isActive).map(s => ({value:s.id,label:s.name,description:tr('buy.statementOwed', { amount: s.owed.toFixed(2) }),searchText:s.phone ?? ''}))} onChange={value => {setPaySupplierId(value);setBillId('');setStatement(null);setError('');setDone('')}} placeholder={tr('buy.searchSupplier')} emptyMessage={tr('buy.noSupplierMatch')} />
          {paySup && <div className="buy-balance"><span>{tr('buy.currentlyOwed')}</span><strong>₹{payOwed.toFixed(2)}</strong></div>}
          {!paySup && <p className="buy-pay-hint">{tr('buy.chooseSupplier')}</p>}
          {paySup && payOwed <= 0 && <p className="buy-pay-hint">{tr('buy.noDues')}</p>}
          {paySup && payOwed > 0 && <>
            <label className="buy-pay-amount">{tr('buy.amountToPay')} <input inputMode="decimal" value={amount} aria-invalid={!!amountError} aria-describedby={amountError ? 'buy-pay-amount-error' : undefined} onChange={e=>setAmount(e.target.value)} /></label>
            {amountError && <p className="buy-field-error" id="buy-pay-amount-error">{amountError}</p>}
            {openBills.length>0 && <div className="buy-pay-allocation"><SearchableSelect id="buy-pay-bill" label={tr('buy.applyToBill')} value={billId} options={openBills.map(b=>({value:b.id,label:b.date+(b.reference?' · '+b.reference:''),description:tr('buy.billOpen')+': ₹'+b.open.toFixed(2),searchText:b.id}))} onChange={setBillId} placeholder={tr('buy.onAccount')} emptyMessage={tr('buy.noOpenBills')} />{selectedBill&&<p>{tr('buy.willAllocate')} ₹{allocationAmount.toFixed(2)}{Number(amount)>selectedBill.open?' · '+tr('buy.excessOnAccount'):''}</p>}</div>}
            <fieldset className="buy-methods"><legend>{tr('buy.paidBy')}</legend>{(['Cash','Bank','UPI'] as const).map(value=><label key={value} className={method===value?'is-selected':''}><input type="radio" name="buy-payment-method" value={value} checked={method===value} onChange={()=>setMethod(value)}/>{tr(value === 'Cash' ? 'buy.cash' : value === 'Bank' ? 'buy.bank' : 'buy.upi')}</label>)}</fieldset>
            <label>{tr('buy.reference')} <input value={pref} onChange={e=>setPref(e.target.value)}/></label>
            <button type="button" className="buy-pay-primary" onClick={pay} disabled={!paySup||!!amountError||payOwed<=0}>{tr('buy.savePayment')}</button>
          </>}
          {paySup&&<button type="button" className="buy-statement-link" onClick={showStatement}>{tr('buy.viewStatement')}</button>}
          {statement&&v('pay')&&<div className="buy-statement" role="region" aria-label={tr('buy.supplierStatement')}><h3>{tr('buy.statementFor')} {statement.supplier}</h3><p>{tr('buy.statementOwed', { amount: statement.outstanding.toFixed(2) })}</p><Paged rows={statement.entries}>{page=><table><thead><tr><th>{tr('buy.date')}</th><th>{tr('buy.type')}</th><th>{tr('buy.purchase')}</th><th>{tr('buy.paidReturned')}</th><th>{tr('buy.balance')}</th></tr></thead><tbody>{page.map(e=><tr key={e.id}><td>{e.date}</td><td>{e.type === 'Purchase' ? tr('buy.purchase') : e.type === 'Payment' ? tr('buy.payment') : e.type === 'Return' ? tr('buy.returnType') : e.type}</td><td>{e.debit?e.debit.toFixed(2):'—'}</td><td>{e.credit?e.credit.toFixed(2):'—'}</td><td>{e.balance.toFixed(2)}</td></tr>)}</tbody></table>}</Paged><button type="button" className="secondary" onClick={()=>setStatement(null)}>{tr('buy.closeStatement')}</button></div>}
        </section>
      </div>}
      {error && <p role="alert">{error}</p>}
      {done && <p role="status">{done}</p>}
      {v('history') && <div className="buy-history">
        <header className="buy-screen-heading"><h2>{tr('buy.historyTitle')}</h2><p>{tr('buy.historyIntro')}</p></header>
        <section className="buy-history-section">
          <h3>{tr('buy.purchaseReceipts')}</h3>
          <Paged rows={receipts} empty={tr('buy.noReceipts')}>{page => <div className="buy-history-list">{page.map(r => <article className="buy-history-card" key={r.id}>
            <div className="buy-history-top"><strong>{r.supplier}</strong><strong>₹{r.total.toFixed(2)}</strong></div>
            <dl><dt>{tr('buy.date')}</dt><dd>{r.date}</dd><dt>{tr('buy.invoiceNo')}</dt><dd>{r.supplierRef || '—'}</dd></dl>
            <div className="buy-history-actions">{r.hasInvoice ? <a href={`/api/purchases/${r.id}/invoice`} target="_blank" rel="noreferrer">{tr('buy.viewBill')}</a> : <span className="buy-muted">{tr('buy.noBill')}</span>}</div>
          </article>)}</div>}</Paged>
        </section>
        <section className="buy-history-section">
          <h3>{tr('buy.supplierPayments')}</h3>
          <Paged rows={payments} empty={tr('buy.noPayments')}>{page => <div className="buy-history-list">{page.map(payment => <article className="buy-history-card" key={payment.id}>
            <div className="buy-history-top"><strong>{payment.supplier}</strong><strong>₹{payment.amount.toFixed(2)}</strong></div>
            <dl><dt>{tr('buy.date')}</dt><dd>{payment.date}</dd><dt>{tr('buy.paidBy')}</dt><dd>{payment.method === 'Cash' ? tr('buy.cash') : payment.method === 'Bank' ? tr('buy.bank') : payment.method === 'UPI' ? tr('buy.upi') : payment.method}</dd><dt>{tr('buy.reference')}</dt><dd>{payment.reference || '—'}</dd></dl>
          </article>)}</div>}</Paged>
        </section>
        <section className="buy-history-statement">
          <SearchableSelect id="buy-history-supplier" label={tr("buy.supplierStatement")} value={historySupplierId} options={suppliers.filter(s => s.isActive).map(s => ({value:s.id,label:s.name,description:tr('buy.currentlyOwed')+': ₹'+s.owed.toFixed(2)}))} onChange={value => {setHistorySupplierId(value);setStatement(null)}} placeholder={tr("buy.searchSupplier")} emptyMessage={tr("buy.noSupplierMatch")} />
          <button type="button" className="secondary" onClick={showStatement} disabled={!historySupplierId}>{tr('buy.viewStatement')}</button>
          {statement && <div role="region" aria-label={tr('buy.supplierStatement')}><h3>{tr('buy.statementFor')} {statement.supplier} · {tr('buy.currentlyOwed')} ₹{statement.outstanding.toFixed(2)}</h3><Paged rows={statement.entries}>{page => <table><thead><tr><th>{tr('buy.date')}</th><th>{tr('buy.type')}</th><th>{tr('buy.purchase')}</th><th>{tr('buy.paidReturned')}</th><th>{tr('buy.balance')}</th></tr></thead><tbody>{page.map(e=><tr key={e.id}><td>{e.date}</td><td>{e.type === 'Purchase' ? tr('buy.purchase') : e.type === 'Payment' ? tr('buy.payment') : e.type === 'Return' ? tr('buy.returnType') : e.type}</td><td>{e.debit?e.debit.toFixed(2):'—'}</td><td>{e.credit?e.credit.toFixed(2):'—'}</td><td>{e.balance.toFixed(2)}</td></tr>)}</tbody></table>}</Paged><button type="button" className="secondary" onClick={()=>setStatement(null)}>{tr('buy.closeStatement')}</button></div>}
        </section>
      </div>}

          </section>
  )
}
