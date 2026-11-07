import { Paged } from './Paged'
import { useCallback, useEffect, useState } from 'react'
import { api, ApiError } from './api'
import './billing-forms.css'
import { InvoiceDocument } from './InvoiceDocument'
import { BarcodeScanner, type ScanResult } from './BarcodeScanner'

type Customer = { id: string; name: string; phone: string | null; notes: string | null; isActive: boolean; owed: number }
type SaleRow = { id: string; number: number; date: string; saleType: string; total: number; amountPaid: number; balance: number; customer: string }
type SaleDetail = { sale: SaleRow & { payMethod: string | null; location: string; customerPhone: string | null; customerNotes: string | null; gstRate: number | null }; lines: { product: string; sku: string; quantity: number; unitPrice: number; discount: number; lineTotal: number; serials?: string[] }[] }
type Opt = { id: string; name: string }
type BizProfile = { businessName: string; address: string; city: string; state: string; pinCode: string; mobile: string; gstStatus: string; gstin: string | null }
type Line = { productId: string; quantity: number; unitPrice: number; discount: number; serials: string[] }

const newKey = () => crypto.randomUUID()
const today = () => new Date().toISOString().slice(0, 10)
const msg = (e: unknown) => (e instanceof ApiError && e.status >= 500 ? 'Server error. Retrying is safe.' : e instanceof Error ? e.message : 'Something went wrong')
const lineTotal = (l: Line) => Math.round(l.quantity * l.unitPrice * 100) / 100 - l.discount

export type SellPart = 'bill' | 'customers' | 'bills'

export function Sales({ products, locations, reload, show }: { products: Opt[]; locations: Opt[]; reload: () => void; show?: SellPart }) {
  const v = (p: SellPart) => !show || show === p
  const [customers, setCustomers] = useState<Customer[]>([])
  const [sales, setSales] = useState<SaleRow[]>([])
  const [detail, setDetail] = useState<SaleDetail | null>(null)
  const [error, setError] = useState('')
  const [done, setDone] = useState('')
  const [cname, setCname] = useState('')
  const [cphone, setCphone] = useState(''); const [cnotes, setCnotes] = useState(''); const [custEditId, setCustEditId] = useState<string | null>(null)
  const [custSearch, setCustSearch] = useState('')
  const [saleSearch, setSaleSearch] = useState('')
  const [customerId, setCustomerId] = useState('')
  const [locationId, setLocationId] = useState('')
  const [date, setDate] = useState(today)
  const [saleType, setSaleType] = useState('Cash')
  const [paid, setPaid] = useState('')
  const [payMethod, setPayMethod] = useState('Cash')
  const [productId, setProductId] = useState('')
  const [qty, setQty] = useState('')
  const [price, setPrice] = useState('')
  const [discount, setDiscount] = useState('')
  const [lines, setLines] = useState<Line[]>([])
  const [key, setKey] = useState(newKey)
  const [biz, setBiz] = useState<BizProfile | null>(null)
  const [scanning, setScanning] = useState(false)
  const [typedCode, setTypedCode] = useState('')
  const [serialText, setSerialText] = useState('')
  const [scanningSerial, setScanningSerial] = useState(false)
  useEffect(() => { api<BizProfile>('GET', '/business-profile').then(setBiz).catch(() => undefined) }, [])

  const loadCustomers = useCallback(async () => setCustomers(await api<Customer[]>('GET', '/customers' + (custSearch.trim() ? '?q=' + encodeURIComponent(custSearch.trim()) : ''))), [custSearch])
  const loadSales = useCallback(async () => setSales(await api<SaleRow[]>('GET', '/sales' + (saleSearch.trim() ? '?q=' + encodeURIComponent(saleSearch.trim()) : ''))), [saleSearch])
  useEffect(() => { loadCustomers().catch(() => undefined) }, [loadCustomers])
  useEffect(() => { loadSales().catch(() => undefined) }, [loadSales])
  const loc = locationId || locations[0]?.id || ''
  const nameOf = (id: string) => products.find(p => p.id === id)?.name ?? id
  const total = lines.reduce((s, l) => s + lineTotal(l), 0)
  const owedNow = customers.find(c => c.id === customerId)?.owed ?? 0
  const run = async (fn: () => Promise<string>) => {
    setError(''); setDone('')
    try { setDone(await fn()); await Promise.all([loadCustomers(), loadSales()]); reload() } catch (e) { setError(msg(e)) }
  }
  const addCustomer = () => run(async () => { const body = { name: cname, phone: cphone || null, notes: cnotes || null, isActive: customers.find(c => c.id === custEditId)?.isActive ?? true }; const was = custEditId; if (was) await api('PUT', '/customers/' + was, body); else await api('POST', '/customers', body); setCname(''); setCphone(''); setCnotes(''); setCustEditId(null); return was ? 'Customer updated.' : 'Customer added.' })
  const addLine = () => {
    const serials = serialText.split(/[\s,;]+/).map(x => x.trim().toUpperCase()).filter(Boolean); const quantity = Number(qty); const unitPrice = Number(price); const disc = Number(discount || 0)
    if (!productId || !(quantity > 0) || !(unitPrice > 0) || disc < 0) { setError('Choose a product and enter a positive quantity and price.'); return }
    setError(''); setLines([...lines.filter(l => l.productId !== productId), { productId, quantity, unitPrice, discount: disc, serials }]); setSerialText(''); setQty(''); setPrice(''); setDiscount('')
  }
  const onScan = async (r: ScanResult) => {
	setScanning(false); setTypedCode(''); setError(''); setDone('')
	try {
	  const p = await api<{ productId: string; name: string }>('GET', '/products/by-barcode?code=' + encodeURIComponent(r.code))
	  const existing = lines.find(l => l.productId === p.productId)
	  if (existing) { setLines(lines.map(l => l.productId === p.productId ? { ...l, quantity: l.quantity + 1 } : l)); setDone(p.name + ': quantity is now ' + (existing.quantity + 1) + '.') }
	  else { setProductId(p.productId); setQty('1'); setPrice(''); setDone(p.name + ' selected. Enter the unit price, then Add line.') }
	} catch (e) { setError(e instanceof ApiError && e.status === 404 ? 'No product has code ' + r.code + '. Pick the product manually or add it in Manage Products.' : msg(e)) }
  }
  const sell = () => run(async () => {
    const body = { customerId: customerId || null, locationId: loc, saleDate: date, saleType, amountPaid: saleType === 'Credit' ? Number(paid || 0) : null, payMethod: saleType === 'Credit' && Number(paid || 0) > 0 ? payMethod : null, lines: lines.map(l => ({ productId: l.productId, quantity: l.quantity, unitPrice: l.unitPrice, discount: l.discount, serials: l.serials })) }
    const r = await api<{ id: string; total: number; balance: number; replayed?: boolean; serialWarnings?: string[] }>('POST', '/sales', body, key)
    setLines([]); setPaid(''); setKey(newKey())
    try { setDetail(await api<SaleDetail>('GET', '/sales/' + r.id)) } catch { /* bill preview is optional */ }
    return 'Sale posted: ' + r.total.toFixed(2) + ' INR' + (r.balance > 0 ? ', balance due ' + r.balance.toFixed(2) + ' INR' : ', paid in full') + (r.replayed ? ' (already posted earlier)' : '') + '.' + (r.serialWarnings?.length ? ' WARNING: serial/IMEI ' + r.serialWarnings.join(', ') + ' was sold before.' : '')
  })
  const open = async (id: string) => { setError(''); try { setDetail(await api<SaleDetail>('GET', '/sales/' + id)) } catch (e) { setError(msg(e)) } }
  return (
    <section aria-label="Sales" className="billing-screen">
      <h2>Sales and customers</h2>
      {v('bill') && <>
      <h3>New sale</h3>
      <label>Customer <select value={customerId} onChange={e => setCustomerId(e.target.value)}><option value="">Walk-in customer</option>{customers.filter(c => c.isActive).map(c => <option key={c.id} value={c.id}>{c.name}</option>)}</select></label>
      {customerId && <p>Customer owes {owedNow.toFixed(2)} INR before this sale.</p>}
      <label>Location <select value={loc} onChange={e => setLocationId(e.target.value)}>{locations.map(l => <option key={l.id} value={l.id}>{l.name}</option>)}</select></label>
      <label>Sale date <input type="date" value={date} onChange={e => setDate(e.target.value)} /></label>
      <label>Sale type <select value={saleType} onChange={e => setSaleType(e.target.value)}><option>Cash</option><option>UPI</option><option>Credit</option></select></label>
      <div className="no-print">
		<button type="button" className="secondary" onClick={() => setScanning(true)}>Scan barcode</button>{' '}
		<label>Scan or type code <input value={typedCode} autoComplete="off" onChange={e => setTypedCode(e.target.value)} onKeyDown={e => { if (e.key === 'Enter' && typedCode.trim()) { e.preventDefault(); onScan({ code: typedCode.trim(), symbology: 'MANUAL' }) } }} /></label>
	  </div>
	  {scanning && <BarcodeScanner title="Scan item to bill" onScan={onScan} onClose={() => setScanning(false)} />}
      <div className="line-grid no-print">
        <label className="wide">Product <select value={productId} onChange={e => setProductId(e.target.value)}><option value="">Select</option>{products.map(p => <option key={p.id} value={p.id}>{p.name}</option>)}</select></label>
        <label>Quantity <input inputMode="decimal" value={qty} onChange={e => setQty(e.target.value)} /></label>
        <label>Unit price (INR) <input inputMode="decimal" value={price} onChange={e => setPrice(e.target.value)} /></label>
        <label>Discount (INR) <input inputMode="decimal" value={discount} onChange={e => setDiscount(e.target.value)} /></label>
        <label className="wide">Serial / IMEI (optional, separate with commas) <input value={serialText} autoComplete="off" onChange={e => setSerialText(e.target.value)} /></label>
		<button type="button" className="secondary" onClick={() => setScanningSerial(true)}>Scan serial</button>
		{scanningSerial && <BarcodeScanner title="Scan serial / IMEI" onScan={r => { setSerialText(s => (s.trim() ? s.trim() + ', ' : '') + r.code); setScanningSerial(false) }} onClose={() => setScanningSerial(false)} />}
		<button type="button" onClick={addLine}>Add line</button>
      </div>
      <ul>{lines.map(l => <li key={l.productId}>{nameOf(l.productId)}: {l.quantity} x {l.unitPrice}{l.discount > 0 ? ' - ' + l.discount : ''} = {lineTotal(l).toFixed(2)}{l.serials.length > 0 ? ' [' + l.serials.join(', ') + ']' : ''} <button type="button" onClick={() => setLines(lines.filter(x => x.productId !== l.productId))}>Remove</button></li>)}</ul>
      <p data-testid="sale-total">Sale total: {total.toFixed(2)} INR</p>
      {saleType === 'Credit' && <div>
        <label>Paid now (INR) <input inputMode="decimal" value={paid} onChange={e => setPaid(e.target.value)} /></label>
        <label>Payment method <select value={payMethod} onChange={e => setPayMethod(e.target.value)}><option>Cash</option><option>Bank</option><option>UPI</option></select></label>
        <p>Balance on credit: {Math.max(0, total - Number(paid || 0)).toFixed(2)} INR</p>
      </div>}
      <button type="button" onClick={sell} disabled={lines.length === 0 || total <= 0 || (saleType === 'Credit' && !customerId)}>Post sale</button>
      {saleType === 'Credit' && !customerId && <p>Choose a named customer for a credit sale.</p>}
      {error && <p role="alert">{error}</p>}
      </>}
      {done && <p role="status">{done}</p>}
      {v('customers') && <>
      <h3>Customers</h3>
      <div>
        <label>Customer name <input value={cname} onChange={e => setCname(e.target.value)} /></label>
        <label>Phone <input value={cphone} onChange={e => setCphone(e.target.value)} /></label>
        <label>Notes <input value={cnotes} onChange={e => setCnotes(e.target.value)} /></label>
		<button type="button" onClick={addCustomer} disabled={!cname.trim()}>{custEditId ? 'Save changes' : 'Add customer'}</button>
		{custEditId && <button type="button" className="secondary" onClick={() => { setCustEditId(null); setCname(''); setCphone(''); setCnotes('') }}>Cancel</button>}
      </div>
      <label>Search customers <input value={custSearch} onChange={e => setCustSearch(e.target.value)} /></label>
      <Paged rows={customers}>{page => (
      <table aria-label="Customers">
        <thead><tr><th>Customer</th><th>Phone</th><th>Notes</th><th>Outstanding (INR)</th><th>Actions</th></tr></thead>
        <tbody>{page.map(c => <tr key={c.id}><td>{c.name}</td><td>{c.phone}</td><td>{c.notes}</td><td>{c.owed.toFixed(2)}</td><td><button type="button" className="secondary" onClick={() => { setCustEditId(c.id); setCname(c.name); setCphone(c.phone ?? ''); setCnotes(c.notes ?? ''); window.scrollTo({ top: 0, behavior: 'smooth' }) }}>Edit</button></td></tr>)}</tbody>
      </table>
      )}</Paged>
      </>}
      {v('bills') && <>
      <h3>Sales history</h3>
      <label>Search sales <input value={saleSearch} onChange={e => setSaleSearch(e.target.value)} placeholder="mobile number, customer, Cash, Credit, bill no." /></label>
      <Paged rows={sales}>{page => (
      <table aria-label="Sales history">
        <thead><tr><th>No.</th><th>Date</th><th>Customer</th><th>Type</th><th>Total (INR)</th><th>Paid (INR)</th><th>Balance (INR)</th><th></th></tr></thead>
        <tbody>{page.map(s => <tr key={s.id}><td>{s.number}</td><td>{s.date}</td><td>{s.customer}</td><td>{s.saleType}</td><td>{s.total.toFixed(2)}</td><td>{s.amountPaid.toFixed(2)}</td><td>{s.balance.toFixed(2)}</td><td><button type="button" onClick={() => open(s.id)}>View</button></td></tr>)}</tbody>
      </table>
      )}</Paged>
	  {detail && <div className="invoice-modal" role="dialog" aria-modal="true" aria-label="Sale detail" onClick={e => { if (e.target === e.currentTarget) setDetail(null) }}>
		<div className="invoice-modal-box">
		  <div className="invoice-tools no-print">
			<button type="button" onClick={() => window.print()}>Print bill</button>
			<button type="button" className="secondary" onClick={() => setDetail(null)}>Close</button>
		  </div>
		  <InvoiceDocument biz={biz} showGst={!!biz?.gstin && detail.sale.gstRate != null} rate={detail.sale.gstRate ?? 0} invoiceNo={'INV-' + String(detail.sale.number).padStart(6, '0')} date={detail.sale.date} payment={detail.sale.payMethod ?? detail.sale.saleType} location={locations.length > 1 ? detail.sale.location : undefined} customerName={detail.sale.customer} customerPhone={detail.sale.customerPhone} customerAddress={(detail.sale.customerNotes ?? '').split('|').map(x => x.trim()).filter(Boolean).join(', ')} items={detail.lines.map(l => ({ name: l.product, quantity: l.quantity, unitPrice: l.unitPrice, discount: l.discount, serials: l.serials }))} paid={detail.sale.amountPaid} credit={detail.sale.saleType === 'Credit'} />
		</div>
	  </div>}
      </>}
    </section>
  )
}
