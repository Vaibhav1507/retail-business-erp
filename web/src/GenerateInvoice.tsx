import { useEffect, useMemo, useRef, useState } from 'react'
import { api, ApiError } from './api'
import './invoice.css'
import './billing-forms.css'
import { InvoiceDocument } from './InvoiceDocument'
import { BarcodeScanner, type ScanResult } from './BarcodeScanner'
import { t as tr } from './i18n'

type Opt = { id: string; name: string }
type Customer = { id: string; name: string; phone: string | null; notes: string | null; isActive: boolean }
type Biz = { businessName: string; address: string; city: string; state: string; pinCode: string; mobile: string; gstin: string | null }
type Item = { productId: string; name: string; quantity: number; unitPrice: number; discount: number; serials: string[] }

const GST_RATES = [0, 3, 5, 12, 18, 28]
const money = (n: number) => n.toLocaleString('en-IN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
const HELD_KEY = 'rbos.heldBills'
type Held = { id: string; label: string; data: Record<string, any> } // eslint-disable-line @typescript-eslint/no-explicit-any
const readHeld = (): Held[] => { try { return JSON.parse(localStorage.getItem(HELD_KEY) ?? '[]') as Held[] } catch { return [] } }
const writeHeld = (h: Held[]) => localStorage.setItem(HELD_KEY, JSON.stringify(h))
function spreadDiscount(items: Item[], billDisc: number): Item[] {
  const gross = items.reduce((s, i) => s + itemTotal(i), 0)
  if (!(billDisc > 0) || !(gross > 0)) return items
  const disc = Math.min(billDisc, gross)
  let left = disc
  return items.map((i, n) => {
    const share = n === items.length - 1 ? left : Math.min(left, Math.round(itemTotal(i) / gross * disc * 100) / 100)
    left = Math.round((left - share) * 100) / 100
    return { ...i, discount: Math.round((i.discount + share) * 100) / 100 }
  })
}
const itemTotal = (i: Item) => Math.round(i.quantity * i.unitPrice * 100) / 100 - i.discount
const msg = (e: unknown) => (e instanceof ApiError && e.status >= 500 ? 'Server error. Retrying is safe.' : e instanceof Error ? e.message : 'Something went wrong')

export function GenerateInvoice({ products, locations, reload }: { products: Opt[]; locations: Opt[]; reload: () => void }) {
  const [step, setStep] = useState<'form' | 'review' | 'done'>('form')
  const [biz, setBiz] = useState<Biz | null>(null)
  const [found, setFound] = useState<Customer[]>([])
  const [showFound, setShowFound] = useState(false)
  const [customerId, setCustomerId] = useState('')
  const [phone, setPhone] = useState('')
  const [name, setName] = useState('')
  const [address, setAddress] = useState('')
  const [city, setCity] = useState('')
  const [state, setState] = useState('')
  const [pin, setPin] = useState('')
  const [locationId, setLocationId] = useState('')
  const [date, setDate] = useState(() => new Date().toISOString().slice(0, 10))
  const [mode, setMode] = useState('Cash')
  const [paid, setPaid] = useState('')
  const [payMethod, setPayMethod] = useState('Cash')
  const [paid2, setPaid2] = useState('')
  const [payMethod2, setPayMethod2] = useState('UPI')
  const [paper, setPaper] = useState('a4')
  const [gst, setGst] = useState(false)
  const [rate, setRate] = useState(18)
  const [items, setItems] = useState<Item[]>([])
  const [pname, setPname] = useState('')
  const [showProducts, setShowProducts] = useState(false)
  const [qty, setQty] = useState('1')
  const [price, setPrice] = useState('')
  const [disc, setDisc] = useState('')
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  const [key, setKey] = useState(() => crypto.randomUUID())
  const [invNo, setInvNo] = useState('')
  const [serialText, setSerialText] = useState('')
  const [scanning, setScanning] = useState(false)
  const [warn, setWarn] = useState('')
    const [billDisc, setBillDisc] = useState('')
    const [held, setHeld] = useState<Held[]>(readHeld)
  const skipSearch = useRef(false)

  useEffect(() => { api<Biz>('GET', '/business-profile').then(setBiz).catch(() => undefined) }, [])
  const [prodRates, setProdRates] = useState<Record<string, number>>({})
  useEffect(() => {
    api<{ id: string; gstRate?: number | null }[]>('GET', '/products').then(list => {
      const m: Record<string, number> = {}
      for (const p of list) if (p.gstRate != null) m[p.id] = p.gstRate
      setProdRates(m)
    }).catch(() => undefined)
  }, [])
  useEffect(() => {
    if (!gst || items.length === 0) return
    const rates = new Set(items.map(i => prodRates[i.productId]).filter((r): r is number => r !== undefined))
    if (rates.size === 1) setRate([...rates][0])
  }, [gst, items, prodRates])

  const pick = (c: Customer) => {
    skipSearch.current = true
    setCustomerId(c.id); setName(c.name); setPhone(c.phone ?? ''); setFound([]); setShowFound(false)
    const m = /^(.*?)\s*\|\s*(.*?)\s*\|\s*(.*?)\s*\|\s*(\d*)$/.exec(c.notes ?? '')
    setAddress(m ? m[1] : ''); setCity(m ? m[2] : ''); setState(m ? m[3] : ''); setPin(m ? m[4] : '')
  }

  useEffect(() => {
    if (skipSearch.current) { skipSearch.current = false; return }
    const q = phone.trim()
    if (q.length < 3) { setFound([]); return }
    const t = setTimeout(() => {
      api<Customer[]>('GET', '/customers?q=' + encodeURIComponent(q)).then(r => {
        const list = r.filter(c => c.isActive && (c.phone ?? '').includes(q))
        setFound(list)
        const exact = list.filter(c => (c.phone ?? '') === q)
        if (exact.length === 1 && !customerId) pick(exact[0])
        else setShowFound(list.length > 0)
      }).catch(() => undefined)
    }, 250)
    return () => clearTimeout(t)
  }, [phone]) // eslint-disable-line react-hooks/exhaustive-deps

  const loc = locationId || locations[0]?.id || ''
  const locName = locations.find(l => l.id === loc)?.name ?? ''
  const grossTotal = items.reduce((s, i) => s + itemTotal(i), 0)
    const billDiscount = Math.min(Math.max(Number(billDisc) || 0, 0), grossTotal)
    const effItems = useMemo(() => spreadDiscount(items, billDiscount), [items, billDiscount])
    const total = effItems.reduce((s, i) => s + itemTotal(i), 0)
  const taxable = gst ? total / (1 + rate / 100) : total
  const tax = total - taxable
  const typed = pname.trim().toLowerCase()
  const match = products.find(p => p.name.toLowerCase() === typed)
  const suggestions = useMemo(() => (typed ? products.filter(p => p.name.toLowerCase().includes(typed)) : products).slice(0, 8), [products, typed])

  async function onScan(r: ScanResult) {
	setScanning(false); setError('')
	try {
	  const p = await api<{ productId: string; name: string }>('GET', '/products/by-barcode?code=' + encodeURIComponent(r.code))
	  setPname(p.name); setQty('1'); setShowProducts(false)
	} catch (e) { setError(e instanceof ApiError && e.status === 404 ? 'No product has code ' + r.code + '. Type the product name instead.' : msg(e)) }
  }

  function addItem() {
    const serials = serialText.split(/[\s,;]+/).map(x => x.trim().toUpperCase()).filter(Boolean)
    const quantity = Number(qty), unitPrice = Number(price), discount = Number(disc || 0)
    if (!typed || !(quantity > 0) || !(unitPrice > 0) || discount < 0) { setError('Enter a product, quantity and price.'); return }
    setError('')
    setItems(a => [...a, { productId: match?.id ?? '', name: match?.name ?? pname.trim(), quantity, unitPrice, discount, serials }])
    setPname(''); setQty('1'); setPrice(''); setDisc(''); setSerialText('')
  }

  function review() {
    if (!items.length) { setError('Add at least one product.'); return }
    if (mode === 'Credit' && !(customerId || name.trim())) { setError('A credit bill needs a customer.'); return }
    setError(''); setStep('review')
  }

  async function confirm() {
    setBusy(true); setError('')
    try {
      let cid = customerId
      const notes = [address, city, state, pin].every(x => !x) ? null : [address, city, state, pin].join(' | ')
      if (cid) {
        await api('PUT', '/customers/' + cid, { name: name.trim(), phone: phone.trim() || null, notes })
      } else if (name.trim()) {
        const c = await api<{ id: string }>('POST', '/customers', { name: name.trim(), phone: phone.trim() || null, notes })
        cid = c.id
      }
      const lines = []
      for (const i of effItems) {
        let pid = i.productId
        if (!pid) {
          const p = await api<{ id: string }>('POST', '/products', { name: i.name, categoryName: 'Uncategorised', isGeneric: true, baseUnit: 'pcs' })
          pid = p.id
        }
        lines.push({ productId: pid, quantity: i.quantity, unitPrice: i.unitPrice, discount: i.discount, serials: i.serials })
      }
      const credit = mode === 'Credit'
      const part = Number(paid || 0)
      const part2 = Number(paid2 || 0)
      const payments = credit && part2 > 0 ? [{ method: payMethod, amount: part }, { method: payMethod2, amount: part2 }].filter(x => x.amount > 0) : undefined
      const body = { customerId: cid || null, locationId: loc, saleDate: date, saleType: mode, amountPaid: credit ? part : null, payMethod: credit && part > 0 ? payMethod : null, payments, lines, gstRate: gst ? rate : null, placeOfSupply: gst && state.trim() ? state.trim() : null }
      const r = await api<{ id: string; serialWarnings?: string[] }>('POST', '/sales', body, key)
      const d = await api<{ sale: { number: number } }>('GET', '/sales/' + r.id)
      setWarn(r.serialWarnings?.length ? 'Warning: serial/IMEI ' + r.serialWarnings.join(', ') + ' was sold before.' : ''); setInvNo('INV-' + String(d.sale.number).padStart(6, '0')); setKey(crypto.randomUUID()); setStep('done'); reload()
      // Saving the sale is the final step here; printing/sharing is an explicit next action.
    } catch (e) { setError(msg(e)) } finally { setBusy(false) }
  }

  function clearForm() {
    setItems([]); setBillDisc(''); setCustomerId(''); setPhone(''); setName(''); setAddress(''); setCity(''); setState(''); setPin(''); setPaid(''); setPaid2(''); setMode('Cash'); setGst(false)
  }

  function hold() {
    if (!items.length) return
    const data = { customerId, phone, name, address, city, state, pin, date, mode, paid, payMethod, paid2, payMethod2, gst, rate, items, billDisc }
    const label = (name.trim() || phone.trim() || 'Walk-in') + ' - ' + items.length + ' item(s)'
    const next = [{ id: crypto.randomUUID(), label, data }, ...held].slice(0, 20)
    writeHeld(next); setHeld(next); clearForm()
  }

  function resume(h: Held) {
    const d = h.data
    skipSearch.current = true
    setCustomerId(d.customerId); setPhone(d.phone); setName(d.name); setAddress(d.address); setCity(d.city); setState(d.state); setPin(d.pin)
    setDate(d.date); setMode(d.mode); setPaid(d.paid); setPayMethod(d.payMethod); setPaid2(d.paid2); setPayMethod2(d.payMethod2)
    setGst(d.gst); setRate(d.rate); setItems(d.items); setBillDisc(d.billDisc ?? '')
    const next = held.filter(x => x.id !== h.id); writeHeld(next); setHeld(next)
  }

  function discardHeld(id: string) {
    if (!window.confirm('Discard this held bill?')) return
    const next = held.filter(x => x.id !== id); writeHeld(next); setHeld(next)
  }

  function reset() {
    clearForm(); setPayMethod('Cash'); setInvNo(''); setWarn(''); setError(''); setStep('form')
  }

  if (step !== 'form') {
    const showGst = gst
    return (
      <section aria-label="Invoice overview" className="invoice-wrap">
        <div className="invoice-tools no-print">
          {step === 'review' && <><button type="button" disabled={busy} onClick={confirm}>{busy ? 'Saving\u2026' : 'Confirm sale'}</button><button type="button" className="secondary" disabled={busy} onClick={() => setStep('form')}>Back to edit</button></>}
          {step === 'done' && <><span role="status">Saved as {invNo}.{warn && ' ' + warn}</span><p className="billing-hint">Save the invoice as PDF first. Then open WhatsApp and attach the saved PDF to the message.</p><select aria-label="Paper size" value={paper} onChange={e => setPaper(e.target.value)}><option value="a4">A4</option><option value="thermal">80 mm thermal</option></select><style>{paper === 'thermal' ? '@page{size:80mm auto;margin:3mm} @media print{.invoice{font-size:11px}}' : '@page{size:A4;margin:10mm}'}</style><button type="button" onClick={() => window.print()}>Save as PDF / Print</button><button type="button" className="secondary" disabled={!phone.trim()} onClick={() => { const digits = phone.replace(/\D/g, ''); const number = digits.length === 10 ? '91' + digits : digits; const message = `Hello ${name.trim() || 'there'}, thank you for your purchase. Your invoice ${invNo} dated ${date} is for ₹${money(total)}. Please contact us if you need another copy of the invoice. Regards, ${biz?.businessName ?? 'our store'}.`; window.open(`https://wa.me/${number}?text=${encodeURIComponent(message)}`, '_blank', 'noopener,noreferrer') }}>Open WhatsApp</button><button type="button" onClick={reset}>New invoice</button></>}
        </div>
        {error && <p role="alert">{error}</p>}
        <InvoiceDocument biz={biz} showGst={showGst} rate={rate} invoiceNo={invNo || 'Pending'} date={date} payment={mode} location={locations.length > 1 ? locName : undefined} customerName={name} customerPhone={phone} customerAddress={[address, city, state, pin].filter(Boolean).join(', ')} items={effItems} paid={mode === 'Credit' ? Number(paid || 0) + Number(paid2 || 0) : total} credit={mode === 'Credit'} />
      </section>
    )
  }

  return (
    <section aria-label="Generate invoice" className="billing-page">
      <h2>Generate Invoice</h2>
            {held.length > 0 && <div className="billing-card"><h3>Held bills</h3><ul>{held.map(h => <li key={h.id}>{h.label} <button type="button" className="secondary" onClick={() => resume(h)}>Resume</button> <button type="button" className="secondary" onClick={() => discardHeld(h.id)}>Discard</button></li>)}</ul></div>}
      {error && <p role="alert" className="billing-err">{error}</p>}

      <div className="billing-card">
        <h3>Customer</h3>
        <div className="billing-grid">
          <div className="billing-field billing-col-4 billing-rel">
            <label htmlFor="billing-phone">Mobile number</label>
            <input id="billing-phone" inputMode="tel" autoComplete="off" placeholder="Type number to find customer" value={phone}
              onChange={e => { setPhone(e.target.value.replace(/[^\d+ ]/g, '')); setCustomerId('') }}
              onFocus={() => setShowFound(found.length > 0)} onBlur={() => setTimeout(() => setShowFound(false), 150)} />
            {showFound && found.length > 0 && <ul className="billing-menu" role="listbox" aria-label="Matching customers">
              {found.slice(0, 6).map(c => <li key={c.id} role="option" aria-selected={false} onMouseDown={e => { e.preventDefault(); pick(c) }}><strong>{c.name}</strong><span>{c.phone}</span></li>)}
            </ul>}
          </div>
          <div className="billing-field billing-col-8"><label htmlFor="billing-name">Customer name</label><input id="billing-name" value={name} onChange={e => setName(e.target.value)} /></div>
          <div className="billing-field billing-col-12"><label htmlFor="billing-addr">Address</label><input id="billing-addr" value={address} onChange={e => setAddress(e.target.value)} /></div>
          <div className="billing-field billing-col-3"><label htmlFor="billing-pin">Pincode</label><input id="billing-pin" inputMode="numeric" maxLength={6} value={pin} onChange={e => setPin(e.target.value.replace(/\D/g, ''))} /></div>
          <div className="billing-field billing-col-5"><label htmlFor="billing-city">City</label><input id="billing-city" value={city} onChange={e => setCity(e.target.value)} /></div>
          <div className="billing-field billing-col-4"><label htmlFor="billing-state">State</label><input id="billing-state" value={state} onChange={e => setState(e.target.value)} /></div>
        </div>
        <p className="billing-hint">{customerId ? 'Existing customer. Edit any detail and it is saved when you confirm.' : 'New number? Fill the details once; they are saved for next time.'}</p>
      </div>

      <div className="billing-card">
        <h3>Invoice details</h3>
        <div className="billing-grid">
          {locations.length > 1 && <div className="billing-field billing-col-4"><label htmlFor="billing-store">Store</label><select id="billing-store" value={loc} onChange={e => setLocationId(e.target.value)}>{locations.map(l => <option key={l.id} value={l.id}>{l.name}</option>)}</select></div>}
          <div className="billing-field billing-col-4"><label htmlFor="billing-date">Sale date</label><input id="billing-date" type="date" value={date} onChange={e => setDate(e.target.value)} /></div>
          <div className="billing-field billing-col-4"><label htmlFor="billing-mode">Payment mode</label><select id="billing-mode" value={mode} onChange={e => setMode(e.target.value)}><option>Cash</option><option>UPI</option><option>Credit</option></select></div>
          {mode === 'Credit' && <div className="billing-field billing-col-4"><label htmlFor="billing-paid">{tr('bill.paidNow')}</label><input id="billing-paid" inputMode="decimal" value={paid} onChange={e => setPaid(e.target.value)} /></div>}
          {mode === 'Credit' && <><div className="billing-field billing-col-4"><label htmlFor="billing-paid-method">Paid by</label><select id="billing-paid-method" value={payMethod} onChange={e => setPayMethod(e.target.value)}><option>Cash</option><option value="Bank">Card / Bank</option><option>UPI</option></select></div><div className="billing-field billing-col-4"><label htmlFor="billing-paid2">Second payment (INR, optional)</label><input id="billing-paid2" inputMode="decimal" value={paid2} onChange={e => setPaid2(e.target.value)} /></div><div className="billing-field billing-col-4"><label htmlFor="billing-paid2-method">Second paid by</label><select id="billing-paid2-method" value={payMethod2} onChange={e => setPayMethod2(e.target.value)}><option>Cash</option><option value="Bank">Card / Bank</option><option>UPI</option></select></div></>}
        </div>
      </div>

      <div className="billing-card">
        <h3>{tr('bill.products')}</h3>
          <div className="billing-row"><button type="button" onClick={addItem}>{tr('bill.add')}</button><button type="button" className="secondary" onClick={() => setScanning(true)}>{tr('bill.scan')}</button>{pname.trim() && !match && <span className="billing-hint">Not in your products. It will be added as a new product when you confirm.</span>}</div>
        <div className="billing-grid">
          <div className="billing-field billing-col-6 billing-rel">
            <label htmlFor="billing-prod">{tr('bill.product')}</label>
            <input id="billing-prod" autoComplete="off" placeholder="Search or type a new product" value={pname}
              onChange={e => { setPname(e.target.value); setShowProducts(true) }} onFocus={() => setShowProducts(true)} onBlur={() => setTimeout(() => setShowProducts(false), 150)} />
            {showProducts && suggestions.length > 0 && !match && <ul className="billing-menu" role="listbox" aria-label="Products">
              {suggestions.map(p => <li key={p.id} role="option" aria-selected={false} onMouseDown={e => { e.preventDefault(); setPname(p.name); setShowProducts(false) }}><strong>{p.name}</strong></li>)}
            </ul>}
          </div>
          <div className="billing-field billing-col-2"><label htmlFor="billing-qty">{tr('bill.qty')}</label><input id="billing-qty" inputMode="decimal" onKeyDown={e => { if (e.key === 'Enter') { e.preventDefault(); addItem() } }} value={qty} onChange={e => setQty(e.target.value)} /></div>
          <div className="billing-field billing-col-2"><label htmlFor="billing-price">{tr('bill.price')}</label><input id="billing-price" inputMode="decimal" onKeyDown={e => { if (e.key === 'Enter') { e.preventDefault(); addItem() } }} value={price} onChange={e => setPrice(e.target.value)} /></div>
          <div className="billing-field billing-col-2"><label htmlFor="billing-disc">{tr('bill.disc')}</label><input id="billing-disc" inputMode="decimal" value={disc} onChange={e => setDisc(e.target.value)} /></div>
          <div className="billing-field billing-col-4"><label htmlFor="billing-serial">Serial / IMEI (optional)</label><input id="billing-serial" autoComplete="off" value={serialText} onChange={e => setSerialText(e.target.value)} /></div>
          
          {scanning && <BarcodeScanner title="Scan product" onScan={onScan} onClose={() => setScanning(false)} />}
        </div>
        {items.length > 0 && <table className="billing-table">
          <thead><tr><th>{tr('bill.item')}</th><th>{tr('bill.qty')}</th><th>{tr('bill.price')}</th><th>{tr('bill.disc')}</th><th>{tr('bill.amount')}</th><th /></tr></thead>
          <tbody>{items.map((i, n) => <tr key={n}><td>{i.name}{i.serials.length > 0 && <small> Serial/IMEI: {i.serials.join(', ')}</small>}</td><td>{i.quantity}</td><td>{money(i.unitPrice)}</td><td>{money(i.discount)}</td><td>{money(itemTotal(i))}</td><td><button type="button" className="secondary" onClick={() => setItems(a => a.filter((_, k) => k !== n))}>{tr('bill.remove')}</button></td></tr>)}</tbody>
        </table>}
      </div>

      <div className="billing-card billing-foot">
        <div className="billing-gst">
          <label className="billing-check"><input type="checkbox" checked={gst} onChange={e => setGst(e.target.checked)} /> GST invoice</label>
          {gst && <select aria-label="GST rate" value={rate} onChange={e => setRate(Number(e.target.value))}>{GST_RATES.map(r => <option key={r} value={r}>{r}%</option>)}</select>}
        </div>
        <div className="billing-field"><label htmlFor="billing-bill-disc">{tr('bill.billDisc')}</label><input id="billing-bill-disc" inputMode="decimal" value={billDisc} onChange={e => setBillDisc(e.target.value)} /></div>
        <div className="billing-total">{tr('bill.total')} <strong>{money(total)} INR</strong>{gst && <small> incl. GST {money(tax)}</small>}</div>
        <button type="button" onClick={review}>{tr('bill.generate')}</button>
        <button type="button" className="secondary" onClick={hold} disabled={!items.length}>{tr('bill.hold')}</button>
      </div>
    </section>
  )
}
