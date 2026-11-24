import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { api, ApiError } from './api'
import './invoice.css'
import './billing-forms.css'
import { InvoiceDocument } from './InvoiceDocument'
import { ItemTagCapture, type CapturedInvoiceItem } from './ItemTagCapture'
import { SalesInvoiceCapture, type ExtractedSalesInvoice } from './SalesInvoiceCapture'
import { t as tr } from './i18n'
import { whatsappStatusLabel } from './whatsappStatus'
import { discardOfflineSale, listOfflineSales, queueOfflineSale, syncOfflineSales, type OfflineSaleBody, type OfflineSaleItem } from './offlineSales'
import { readOfflineRecords, saveOfflineRecord } from './offlineStore'

type Opt = { id: string; name: string }
type Customer = { id: string; name: string; phone: string | null; notes: string | null; isActive: boolean }
type Biz = { businessName: string; address: string; city: string; state: string; pinCode: string; mobile: string; gstin: string | null }
type Item = { productId: string; name: string; quantity: number; unitPrice: number; discount: number; serials: string[]; imei1s: string[]; imei2s: string[]; barcode?: string; barcodeSymbology?: string; brand?: string; model?: string; category?: string; mrp?: number | null; hsn?: string }

const GST_RATES = [0, 3, 5, 12, 18, 28]
const money = (n: number) => n.toLocaleString('en-IN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
const HELD_KEY = 'rbos.heldBills'
type Held = { id: string; label: string; data: Record<string, any> } // eslint-disable-line @typescript-eslint/no-explicit-any
const readLegacyHeld = (): Held[] => { try { return JSON.parse(localStorage.getItem(HELD_KEY) ?? '[]') as Held[] } catch { return [] } }
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

export function GenerateInvoice({ products, locations, reload, offlineMode = false, ownerKey = '' }: { products: Opt[]; locations: Opt[]; reload: () => void; offlineMode?: boolean; ownerKey?: string }) {
  const [step, setStep] = useState<'form' | 'review' | 'done' | 'pending'>('form')
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
  const [selectedProductId, setSelectedProductId] = useState('')
  const [tagCaptureKey, setTagCaptureKey] = useState(0)
  const [showTagDetails, setShowTagDetails] = useState(false)
  const [salesInvoiceDraft, setSalesInvoiceDraft] = useState<ExtractedSalesInvoice | null>(null)
  const [salesInvoiceWarnings, setSalesInvoiceWarnings] = useState<string[]>([])
  const [sourceInvoiceRef, setSourceInvoiceRef] = useState('')
  const [sourceInvoiceDate, setSourceInvoiceDate] = useState('')
  const [brandText, setBrandText] = useState('')
  const [modelText, setModelText] = useState('')
  const [categoryText, setCategoryText] = useState('')
  const [barcodeText, setBarcodeText] = useState('')
  const [barcodeSymbology, setBarcodeSymbology] = useState('')
  const [imei1Text, setImei1Text] = useState('')
  const [imei2Text, setImei2Text] = useState('')
  const [mrpText, setMrpText] = useState('')
  const [hsnText, setHsnText] = useState('')
  const [showProducts, setShowProducts] = useState(false)
  const [qty, setQty] = useState('1')
  const [price, setPrice] = useState('')
  const [disc, setDisc] = useState('')
  const [error, setError] = useState('')
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({})
  const [showSerialNotice, setShowSerialNotice] = useState(false)
  const [busy, setBusy] = useState(false)
  const [key, setKey] = useState(() => crypto.randomUUID())
  const [invNo, setInvNo] = useState('')
  const [whatsappStatus, setWhatsappStatus] = useState('NotAvailable')
  const [serialText, setSerialText] = useState('')
  const [warn, setWarn] = useState('')
    const [billDisc, setBillDisc] = useState('')
    const [held, setHeld] = useState<Held[]>([])
  const skipSearch = useRef(false)
  useEffect(() => {
    if (!ownerKey) return
    let active = true
    void (async () => {
      try {
        const records = await readOfflineRecords<Held[]>(ownerKey, 'held-bills')
        const saved = records.find(x => x.id === 'held-bills')
        if (saved) {
          if (active) setHeld(saved.value)
          return
        }
        const legacy = readLegacyHeld()
        if (legacy.length) {
          await saveOfflineRecord(ownerKey, 'held-bills', 'held-bills', 'ready', legacy)
          localStorage.removeItem(HELD_KEY)
          if (active) setHeld(legacy)
        }
      } catch { /* Existing held bills remain in the form; offline outbox storage reports its own errors. */ }
    })()
    return () => { active = false }
  }, [ownerKey])
  const [offlineQueue, setOfflineQueue] = useState<OfflineSaleItem[]>([])
  const [syncingOffline, setSyncingOffline] = useState(false)
  const refreshOfflineQueue = useCallback(async () => {
    if (!ownerKey) { setOfflineQueue([]); return }
    try { setOfflineQueue(await listOfflineSales(ownerKey)) }
    catch { setError('Saved offline bills could not be read. Do not clear this browser storage; reconnect and retry.') }
  }, [ownerKey])
  const syncQueue = useCallback(async () => {
    if (!ownerKey || !navigator.onLine) return
    setSyncingOffline(true)
    try { await syncOfflineSales(ownerKey) }
    catch { setError('Offline bills could not be synchronized. Your saved drafts have been retained; retry when the connection is stable.') }
    finally { setSyncingOffline(false); await refreshOfflineQueue() }
  }, [ownerKey, refreshOfflineQueue])
  async function removeQueueItem(item: OfflineSaleItem) {
    const label = item.state === 'posted' ? 'Remove this already-posted bill from this device?' : 'Discard this rejected offline draft?'
    if (!window.confirm(label)) return
    try {
      await discardOfflineSale(ownerKey, item.id)
      await refreshOfflineQueue()
    } catch { setError('Could not remove this offline bill from local storage.') }
  }
  useEffect(() => {
    void refreshOfflineQueue()
    const onOnline = () => { void syncQueue() }
    window.addEventListener('online', onOnline)
    if (navigator.onLine) void syncQueue()
    return () => window.removeEventListener('online', onOnline)
  }, [refreshOfflineQueue, syncQueue])

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
  const selectedProduct = products.find(p => p.id === selectedProductId)
  const suggestions = useMemo(() => (typed ? products.filter(p => p.name.toLowerCase().includes(typed)) : products).slice(0, 8), [products, typed])

  function applyCapturedItem(item: CapturedInvoiceItem) {
    // Keep the invoice editor as the single source of truth for both manual entry
    // and tag-assisted entry. The scan fills these fields; the same Add product
    // action then creates the invoice row.
    setPname(item.name || '')
    setSelectedProductId(item.productId || '')
    setBrandText(item.brand || '')
    setModelText(item.model || '')
    setCategoryText(item.category || '')
    setBarcodeText(item.barcode || '')
    setBarcodeSymbology(item.barcodeSymbology || '')
    setSerialText(item.serials.join(', '))
    setImei1Text(item.imei1 || '')
    setImei2Text(item.imei2 || '')
    setMrpText(item.mrp == null ? '' : String(item.mrp))
    setHsnText(item.hsn || '')
    setQty('1')
    setShowProducts(false)
    setShowTagDetails(true)
    setError('')
    setFieldErrors({})
  }

  async function removeInvoiceItem(index: number) {
    setItems(current => current.filter((_, i) => i !== index))
  }

  function applySalesInvoiceDraft(draft: ExtractedSalesInvoice, warnings: string[]) {
    setSalesInvoiceDraft(draft)
    setSalesInvoiceWarnings(warnings)
    setSourceInvoiceRef(draft.invoiceNumber?.trim() ?? '')
    setSourceInvoiceDate(draft.date && /^\d{4}-\d{2}-\d{2}$/.test(draft.date) ? draft.date : '')
    if (draft.customerName) { setCustomerId(''); setName(draft.customerName) }
    if (draft.customerPhone) setPhone(draft.customerPhone)
    if (draft.date && /^\d{4}-\d{2}-\d{2}$/.test(draft.date)) setDate(draft.date)
  }

  function updateCapturedLine(index: number, field: 'description' | 'brand' | 'model' | 'hsn' | 'quantity' | 'unitPrice' | 'discount', value: string) {
    setSalesInvoiceDraft(current => current ? ({
      ...current,
      lines: current.lines.map((line, lineIndex) => lineIndex !== index ? line : {
        ...line,
        [field]: ['quantity', 'unitPrice', 'discount'].includes(field)
          ? (value.trim() === '' ? null : Number.isFinite(Number(value)) ? Number(value) : null)
          : value
      })
    }) : current)
  }

  function addCapturedLines() {
    if (!salesInvoiceDraft?.lines.length) { setError('No invoice lines were extracted. Add the products manually.'); return }
    const invalid = salesInvoiceDraft.lines.findIndex(line =>
      !(line.description?.trim() || line.model?.trim()) ||
      !(line.quantity != null && line.quantity > 0) ||
      !(line.unitPrice != null && line.unitPrice > 0) ||
      (line.discount != null && line.discount < 0))
    if (invalid >= 0) {
      setError('Complete a description or model number, quantity and unit price for every extracted line before adding them to the bill.')
      return
    }
    const next: Item[] = salesInvoiceDraft.lines.map(line => ({
      productId: '',
      name: line.description?.trim() || line.model!.trim(),
      quantity: line.quantity!,
      unitPrice: line.unitPrice!,
      discount: line.discount ?? 0,
      serials: [], imei1s: [], imei2s: [],
      brand: line.brand?.trim() || undefined,
      model: line.model?.trim() || undefined,
      hsn: line.hsn?.trim() || undefined
    }))
    setItems(current => [...current, ...next])
    setSalesInvoiceDraft(null)
    setSalesInvoiceWarnings([])
    setError('')
  }

  function addItem() {
    const serials = serialText.split(/[\s,;]+/).map(x => x.trim()).filter(Boolean)
    const quantity = Number(qty), unitPrice = Number(price), discount = Number(disc || 0)
    const mrp = mrpText.trim() ? Number(mrpText) : null
    const manualLabel = pname.trim() || modelText.trim()
    const nextErrors: Record<string, string> = {}
    if (!manualLabel) nextErrors.product = 'Enter a product description or model number.'
    if (!Number.isFinite(quantity) || quantity <= 0) nextErrors.quantity = 'Enter a quantity greater than zero.'
    if (!Number.isFinite(unitPrice) || unitPrice <= 0) nextErrors.price = 'Enter a selling price greater than zero.'
    if (!Number.isFinite(discount) || discount < 0) nextErrors.discount = 'Discount cannot be negative.'
    if (Number.isFinite(quantity) && Number.isFinite(unitPrice) && discount > quantity * unitPrice) nextErrors.discount = 'Discount cannot exceed the line amount.'
    if (mrp !== null && (!Number.isFinite(mrp) || mrp < 0)) nextErrors.mrp = 'Enter a valid MRP.'
    const productId = selectedProductId || match?.id || ''
    const identifiers = [...serials, ...(imei1Text.trim() ? [imei1Text.trim()] : []), ...(imei2Text.trim() ? [imei2Text.trim()] : [])]
    const existingIdentifiers = items.flatMap(item => [...item.serials, ...item.imei1s, ...item.imei2s]).map(x => x.trim().toLowerCase())
    const normalizedIdentifiers = identifiers.map(value => value.trim().toLowerCase())
    if (new Set(normalizedIdentifiers).size !== normalizedIdentifiers.length || normalizedIdentifiers.some(value => existingIdentifiers.includes(value)))
      nextErrors.serial = 'This serial/IMEI is already present in this invoice.'
    if (serials.some(value => value.length < 4 || value.length > 40 || /\\s/.test(value)) || serials.length > quantity)
      nextErrors.serial = 'Enter valid serial numbers (4–40 characters each, no more than the quantity).'
    const validImei = (value: string) => {
      if (!/^\\d{15}$/.test(value)) return false
      let sum = 0
      for (let i = 0; i < 15; i++) {
        let digit = Number(value[i])
        if (i % 2 === 1) { digit *= 2; if (digit > 9) digit -= 9 }
        sum += digit
      }
      return sum % 10 === 0
    }
    if ((imei1Text.trim() && !validImei(imei1Text.trim())) || (imei2Text.trim() && !validImei(imei2Text.trim())))
      nextErrors.serial = 'IMEI must contain 15 digits and pass its check digit.'
    if (Object.keys(nextErrors).length) { setFieldErrors(nextErrors); setError(''); return }
    setFieldErrors({})
    const newItem: Item = {
      productId, name: selectedProduct?.name ?? match?.name ?? manualLabel, quantity, unitPrice, discount, serials,
      imei1s: imei1Text.trim() ? [imei1Text.trim()] : [],
      imei2s: imei2Text.trim() ? [imei2Text.trim()] : [],
      barcode: barcodeText.trim() || undefined,
      barcodeSymbology: barcodeSymbology || undefined,
      brand: brandText.trim() || undefined,
      model: modelText.trim() || undefined,
      category: categoryText.trim() || undefined,
      mrp,
      hsn: hsnText.trim() || undefined
    }
    setError('')
    setItems(current => [...current, newItem])
    setPname(''); setSelectedProductId('')
    setBrandText(''); setModelText(''); setCategoryText(''); setBarcodeText(''); setBarcodeSymbology('')
    setQty('1'); setPrice(''); setDisc(''); setSerialText('')
    setImei1Text(''); setImei2Text(''); setMrpText(''); setHsnText('')
    setTagCaptureKey(value => value + 1)
    setShowTagDetails(false)
  }

  function review() {
    const nextErrors: Record<string, string> = {}
    if (!name.trim()) nextErrors.customerName = 'Customer name is required.'
    const mobileDigits = phone.replace(/\D/g, '')
    if (mobileDigits.length < 10 || mobileDigits.length > 15) nextErrors.customerPhone = 'Enter a valid mobile number (10–15 digits).'
    if (Object.keys(nextErrors).length) {
      setFieldErrors(nextErrors)
      setError('')
      document.getElementById(Object.keys(nextErrors)[0] === 'customerName' ? 'billing-name' : 'billing-phone')?.focus()
      return
    }
    if (!items.length) { setError('Add at least one product.'); return }
    setFieldErrors({})
    setShowSerialNotice(items.some(item => item.serials.length === 0))
    setError('')
    setStep('review')
  }

  async function confirm() {
    setBusy(true); setError('')
    let saleBody: OfflineSaleBody | null = null
    let queued = false
    try {
      if (offlineMode || !navigator.onLine) {
        if (!ownerKey) throw new Error('This device has no provisioned offline workspace. Connect and sign in before creating offline bills.')
        if (!customerId && (name.trim() || phone.trim() || address.trim() || city.trim() || state.trim() || pin.trim())) {
          throw new Error('Offline bills can use an existing selected customer or walk-in only. New customer creation requires a connection.')
        }
      }

      let cid = customerId
      if (!offlineMode && navigator.onLine) {
        const notes = [address, city, state, pin].every(x => !x) ? null : [address, city, state, pin].join(' | ')
        if (cid) {
          await api('PUT', '/customers/' + cid, { name: name.trim(), phone: phone.trim() || null, notes })
        } else if (name.trim()) {
          const customer = await api<{ id: string }>('POST', '/customers', { name: name.trim(), phone: phone.trim() || null, notes })
          cid = customer.id
        }
      }

      const lines = effItems.map(item => ({
        productId: item.productId || null,
        description: item.name,
        brand: item.brand ?? null,
        model: item.model ?? null,
        hsnCode: item.hsn ?? null,
        quantity: item.quantity,
        unitPrice: item.unitPrice,
        discount: item.discount,
        serials: item.serials,
        imei1s: item.imei1s,
        imei2s: item.imei2s,
        barcode: item.barcode ?? null,
        barcodeSymbology: item.barcodeSymbology ?? null
      }))
      const credit = mode === 'Credit'
      const part = Number(paid || 0)
      const part2 = Number(paid2 || 0)
      const payments = credit && part2 > 0 ? [{ method: payMethod, amount: part }, { method: payMethod2, amount: part2 }].filter(x => x.amount > 0) : undefined
      if (!cid) throw new Error('Customer name and mobile number must be saved before posting this invoice.')
      saleBody = { customerId: cid, requireCustomerDetails: true, locationId: loc, saleDate: date, saleType: mode, amountPaid: credit ? part : null, payMethod: credit && part > 0 ? payMethod : null, payments, lines, gstRate: gst ? rate : null, placeOfSupply: gst && state.trim() ? state.trim() : null,
        sourceInvoiceRef: sourceInvoiceRef.trim() || null, sourceInvoiceDate: sourceInvoiceDate || null }

      if (offlineMode || !navigator.onLine) {
        await queueOfflineSale(ownerKey, saleBody, key)
        queued = true
        setKey(crypto.randomUUID())
        setStep('pending')
        await refreshOfflineQueue()
        return
      }

      const result = await api<{ id: string; serialWarnings?: string[] }>('POST', '/sales', saleBody, key)
      const detail = await api<{ sale: { number: number; whatsappStatus: string } }>('GET', '/sales/' + result.id)
      setWarn(result.serialWarnings?.length ? 'Warning: serial/IMEI ' + result.serialWarnings.join(', ') + ' was sold before.' : '')
      setInvNo('INV-' + String(detail.sale.number).padStart(6, '0'))
      setWhatsappStatus(detail.sale.whatsappStatus)
      setKey(crypto.randomUUID())
      setStep('done')
      reload()
      setTimeout(() => window.print(), 300)
    } catch (e) {
      const temporaryFailure = e instanceof TypeError || !navigator.onLine || (e instanceof ApiError && e.status >= 500)
      if (saleBody && ownerKey && temporaryFailure && !queued) {
        try {
          await queueOfflineSale(ownerKey, saleBody, key)
          queued = true
          setKey(crypto.randomUUID())
          setStep('pending')
          await refreshOfflineQueue()
        } catch { setError('The server could not confirm this bill and local storage also failed. Keep this screen open and retry; do not create a second bill.') }
      } else if (!queued) {
        setError(msg(e))
      }
    } finally { setBusy(false) }
  }

  function clearForm() {
    setItems([]); setSelectedProductId(''); setPname(''); setFieldErrors({}); setShowSerialNotice(false)
    setBrandText(''); setModelText(''); setCategoryText(''); setBarcodeText(''); setBarcodeSymbology('')
    setImei1Text(''); setImei2Text(''); setMrpText(''); setHsnText('')
    setQty('1'); setPrice(''); setDisc(''); setSerialText(''); setBillDisc('')
    setTagCaptureKey(value => value + 1)
    setShowTagDetails(false)
    setSalesInvoiceDraft(null); setSalesInvoiceWarnings([]); setSourceInvoiceRef(''); setSourceInvoiceDate('')
    setWhatsappStatus('NotAvailable'); setCustomerId(''); setPhone(''); setName(''); setAddress(''); setCity(''); setState(''); setPin(''); setPaid(''); setPaid2(''); setMode('Cash'); setGst(false)
  }

  async function hold() {
    if (!items.length || !ownerKey) return
    const data = { customerId, phone, name, address, city, state, pin, date, mode, paid, payMethod, paid2, payMethod2, gst, rate, items, billDisc, sourceInvoiceRef, sourceInvoiceDate }
    const label = (name.trim() || phone.trim() || 'Walk-in') + ' - ' + items.length + ' item(s)'
    const next = [{ id: crypto.randomUUID(), label, data }, ...held].slice(0, 20)
    await saveOfflineRecord(ownerKey, 'held-bills', 'held-bills', 'ready', next); setHeld(next); clearForm()
  }

  async function resume(h: Held) {
    const d = h.data
    skipSearch.current = true
    setCustomerId(d.customerId); setPhone(d.phone); setName(d.name); setAddress(d.address); setCity(d.city); setState(d.state); setPin(d.pin)
    setDate(d.date); setMode(d.mode); setPaid(d.paid); setPayMethod(d.payMethod); setPaid2(d.paid2); setPayMethod2(d.payMethod2)
    setGst(d.gst); setRate(d.rate);
    setItems((d.items ?? []).map((item: Item) => ({
      ...item, imei1s: item.imei1s ?? [], imei2s: item.imei2s ?? []
    })));
    setSourceInvoiceRef(d.sourceInvoiceRef ?? ''); setSourceInvoiceDate(d.sourceInvoiceDate ?? '');
    setBillDisc(d.billDisc ?? '')
    const next = held.filter(x => x.id !== h.id); await saveOfflineRecord(ownerKey, 'held-bills', 'held-bills', 'ready', next); setHeld(next)
  }

  async function discardHeld(id: string) {
    if (!window.confirm('Discard this held bill?')) return
    const next = held.filter(x => x.id !== id); await saveOfflineRecord(ownerKey, 'held-bills', 'held-bills', 'ready', next); setHeld(next)
  }

  function reset() {
    clearForm(); setPayMethod('Cash'); setInvNo(''); setWarn(''); setError(''); setStep('form')
  }

  if (step === 'pending') return (
    <section className="invoice-wrap" aria-label="Pending offline invoice">
      <h2>Bill saved on this device</h2>
      <p role="status"><strong>Pending sync</strong> — this is a draft, not a posted invoice. It has no official invoice number yet.</p>
      <p>When the connection returns, RBOS will submit it using the same idempotency key. The server will recheck stock, tax, prices, permissions and accounting rules before assigning an invoice number.</p>
      <button type="button" onClick={() => setStep('form')}>Back to billing</button>
    </section>
  )

  if (step !== 'form') {
    const showGst = gst
    return (
      <section aria-label="Invoice overview" className="invoice-wrap">
        <div className="invoice-tools no-print">
          {step === 'review' && <><button type="button" disabled={busy} onClick={confirm}>{busy ? 'Saving\u2026' : 'Confirm & print'}</button><button type="button" className="secondary" disabled={busy} onClick={() => setStep('form')}>Back to edit</button></>}
          {step === 'done' && <><span role="status">Saved as {invNo}.{warn && ' ' + warn}</span><span role="status">WhatsApp invoice: {whatsappStatusLabel(whatsappStatus)}</span><select aria-label="Paper size" value={paper} onChange={e => setPaper(e.target.value)}><option value="a4">A4</option><option value="thermal">80 mm thermal</option></select><style>{paper === 'thermal' ? '@page{size:80mm auto;margin:3mm} @media print{.invoice{font-size:11px}}' : '@page{size:A4;margin:10mm}'}</style><button type="button" onClick={() => window.print()}>Print again</button><button type="button" onClick={reset}>New invoice</button></>}
        </div>
        {error && <p role="alert">{error}</p>}
        {showSerialNotice && <p className="billing-hint" role="status">Serial number not entered for one or more items. You can still continue; serial, IMEI and barcode are optional.</p>}
        {sourceInvoiceRef && <p className="billing-hint">Source invoice reference: {sourceInvoiceRef}{sourceInvoiceDate ? ` · ${sourceInvoiceDate}` : ''}</p>}
        <InvoiceDocument biz={biz} showGst={showGst} rate={rate} invoiceNo={invNo || 'Pending'} date={date} payment={mode} location={locations.length > 1 ? locName : undefined} customerName={name} customerPhone={phone} customerAddress={[address, city, state, pin].filter(Boolean).join(', ')} items={effItems} paid={mode === 'Credit' ? Number(paid || 0) + Number(paid2 || 0) : total} credit={mode === 'Credit'} />
      </section>
    )
  }

  return (
    <section aria-label="Generate invoice" className="billing-page">
      <div className="invoice-page-header">
        <div>
          <h2>Generate Invoice</h2>
          <p className="billing-hint">{tr('onboarding.saleGuidance')}</p>
        </div>
        <div className="invoice-import-action">
          <span className="billing-hint">Have a handwritten bill?</span>
          <SalesInvoiceCapture disabled={offlineMode || !navigator.onLine} onExtract={applySalesInvoiceDraft} />
        </div>
      </div>
            {held.length > 0 && <div className="billing-card"><h3>Held bills</h3><ul>{held.map(h => <li key={h.id}>{h.label} <button type="button" className="secondary" onClick={() => { void resume(h) }}>Resume</button> <button type="button" className="secondary" onClick={() => { void discardHeld(h.id) }}>Discard</button></li>)}</ul></div>}
      {offlineQueue.length > 0 && <div className="billing-card" aria-label="Offline bill queue">
        <h3>Offline bills and sync status</h3>
        <p>{offlineQueue.filter(x => x.state === 'pending-sync' || x.state === 'syncing').length} waiting to sync · {offlineQueue.filter(x => x.state === 'needs-attention').length} need attention · {offlineQueue.filter(x => x.state === 'posted').length} posted</p>
        <button type="button" className="secondary" disabled={!navigator.onLine || syncingOffline} onClick={() => void syncQueue()}>{syncingOffline ? 'Syncing…' : 'Sync pending bills'}</button>
        <ul>{offlineQueue.slice(0, 10).map(item => <li key={item.id}>
          {new Date(item.createdAt).toLocaleString('en-IN')} — {item.state === 'posted' ? (item.value.invoiceNumber ? 'Posted as INV-' + String(item.value.invoiceNumber).padStart(6, '0') : 'Posted; invoice details are still loading') : item.state === 'needs-attention' ? 'Needs attention: ' + (item.value.lastError ?? 'Server rejected this draft') : item.state === 'syncing' ? 'Syncing…' : 'Pending sync' + (item.value.lastError ? ': ' + item.value.lastError : '')}
          {(item.state === 'posted' || item.state === 'needs-attention') && <button type="button" className="secondary" onClick={() => { void removeQueueItem(item) }}>Remove from device</button>}
        </li>)}</ul>
      </div>}
      {error && <p role="alert" className="billing-err">{error}</p>}

      <div className="billing-card">
        <h3>Customer</h3>
        <div className="billing-grid">
          <div className="billing-field billing-col-4 billing-rel">
            <label htmlFor="billing-phone">Mobile number</label>
            <input id="billing-phone" inputMode="tel" autoComplete="off" required aria-invalid={Boolean(fieldErrors.customerPhone)} className={fieldErrors.customerPhone ? 'field-invalid' : ''} placeholder="Type number to find customer" value={phone}
              onChange={e => { setPhone(e.target.value.replace(/[^\d+ ]/g, '')); setCustomerId(''); setFieldErrors(current => ({ ...current, customerPhone: '' })) }}
              onFocus={() => setShowFound(found.length > 0)} onBlur={() => setTimeout(() => setShowFound(false), 150)} />
            {fieldErrors.customerPhone && <small className="field-error">{fieldErrors.customerPhone}</small>}
            {showFound && found.length > 0 && <ul className="billing-menu" role="listbox" aria-label="Matching customers">
              {found.slice(0, 6).map(c => <li key={c.id} role="option" aria-selected={false} onMouseDown={e => { e.preventDefault(); pick(c) }}><strong>{c.name}</strong><span>{c.phone}</span></li>)}
            </ul>}
          </div>
          <div className="billing-field billing-col-8"><label htmlFor="billing-name">Customer name *</label><input id="billing-name" required aria-invalid={Boolean(fieldErrors.customerName)} className={fieldErrors.customerName ? 'field-invalid' : ''} value={name} onChange={e => { setName(e.target.value); setFieldErrors(current => ({ ...current, customerName: '' })) }} />{fieldErrors.customerName && <small className="field-error">{fieldErrors.customerName}</small>}</div>
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
        <div className="billing-grid invoice-product-editor">
          <div className="billing-field billing-col-5 billing-rel">
            <label htmlFor="billing-prod">{tr('bill.product')}</label>
            <div className="invoice-product-entry">
              <input id="billing-prod" autoComplete="off" required aria-invalid={Boolean(fieldErrors.product)} className={fieldErrors.product ? 'field-invalid' : ''} placeholder="Search or type a new product" value={pname}
                onChange={e => { setPname(e.target.value); setSelectedProductId(''); setFieldErrors(current => ({ ...current, product: '' })); setShowProducts(true) }} onFocus={() => setShowProducts(true)} onBlur={() => setTimeout(() => setShowProducts(false), 150)} />
              <ItemTagCapture key={tagCaptureKey} disabled={offlineMode || !navigator.onLine} onExtract={applyCapturedItem} />
            </div>
            {fieldErrors.product && <small className="field-error">{fieldErrors.product}</small>}
            {showProducts && suggestions.length > 0 && !match && <ul className="billing-menu" role="listbox" aria-label="Products">
              {suggestions.map(p => <li key={p.id} role="option" aria-selected={false} onMouseDown={e => { e.preventDefault(); setPname(p.name); setSelectedProductId(p.id); setShowProducts(false) }}><strong>{p.name}</strong></li>)}
            </ul>}
          </div>
          <div className="billing-field billing-col-2"><label htmlFor="billing-qty">{tr('bill.qty')}</label><input id="billing-qty" aria-invalid={Boolean(fieldErrors.quantity)} className={fieldErrors.quantity ? 'field-invalid' : ''} inputMode="decimal" onKeyDown={e => { if (e.key === 'Enter') { e.preventDefault(); addItem() } }} value={qty} onChange={e => { setQty(e.target.value); setFieldErrors(current => ({ ...current, quantity: '' })) }} />{fieldErrors.quantity && <small className="field-error">{fieldErrors.quantity}</small>}</div>
          <div className="billing-field billing-col-3"><label htmlFor="billing-price">{tr('bill.price')}</label><input id="billing-price" aria-invalid={Boolean(fieldErrors.price)} className={fieldErrors.price ? 'field-invalid' : ''} inputMode="decimal" onKeyDown={e => { if (e.key === 'Enter') { e.preventDefault(); addItem() } }} value={price} onChange={e => { setPrice(e.target.value); setFieldErrors(current => ({ ...current, price: '' })) }} />{fieldErrors.price && <small className="field-error">{fieldErrors.price}</small>}</div>
          <div className="billing-field billing-col-2"><label htmlFor="billing-disc">{tr('bill.disc')}</label><input id="billing-disc" aria-invalid={Boolean(fieldErrors.discount)} className={fieldErrors.discount ? 'field-invalid' : ''} inputMode="decimal" value={disc} onChange={e => { setDisc(e.target.value); setFieldErrors(current => ({ ...current, discount: '' })) }} />{fieldErrors.discount && <small className="field-error">{fieldErrors.discount}</small>}</div>
        </div>
        <details className="invoice-product-more-details" open={showTagDetails} onToggle={event => setShowTagDetails(event.currentTarget.open)}>
          <summary>Optional product details</summary>
          <div className="billing-grid">
            <div className="billing-field billing-col-4"><label htmlFor="billing-brand">Brand</label><input id="billing-brand" autoComplete="off" value={brandText} onChange={e => setBrandText(e.target.value)} /></div>
            <div className="billing-field billing-col-4"><label htmlFor="billing-model">Model</label><input id="billing-model" autoComplete="off" value={modelText} onChange={e => setModelText(e.target.value)} /></div>
            <div className="billing-field billing-col-4"><label htmlFor="billing-serial">Serial (optional)</label><input id="billing-serial" aria-invalid={Boolean(fieldErrors.serial)} className={fieldErrors.serial ? 'field-invalid' : ''} autoComplete="off" value={serialText} onChange={e => { setSerialText(e.target.value); setFieldErrors(current => ({ ...current, serial: '' })) }} />{fieldErrors.serial && <small className="field-error">{fieldErrors.serial}</small>}</div>
            <div className="billing-field billing-col-4"><label htmlFor="billing-barcode">Barcode</label><input id="billing-barcode" autoComplete="off" value={barcodeText} onChange={e => setBarcodeText(e.target.value)} /></div>
            <div className="billing-field billing-col-4"><label htmlFor="billing-imei1">IMEI 1</label><input id="billing-imei1" inputMode="numeric" value={imei1Text} onChange={e => setImei1Text(e.target.value)} /></div>
            <div className="billing-field billing-col-4"><label htmlFor="billing-imei2">IMEI 2</label><input id="billing-imei2" inputMode="numeric" value={imei2Text} onChange={e => setImei2Text(e.target.value)} /></div>
            <div className="billing-field billing-col-6"><label htmlFor="billing-mrp">MRP (reference only)</label><input id="billing-mrp" aria-invalid={Boolean(fieldErrors.mrp)} className={fieldErrors.mrp ? 'field-invalid' : ''} inputMode="decimal" value={mrpText} onChange={e => { setMrpText(e.target.value); setFieldErrors(current => ({ ...current, mrp: '' })) }} />{fieldErrors.mrp && <small className="field-error">{fieldErrors.mrp}</small>}</div>
            <div className="billing-field billing-col-6"><label htmlFor="billing-hsn">HSN</label><input id="billing-hsn" value={hsnText} onChange={e => setHsnText(e.target.value)} /></div>
          </div>
        </details>
        <div className="billing-row invoice-product-actions">
          <button type="button" onClick={addItem}>{tr('bill.add')}</button>
          {pname.trim() && !match && <span className="billing-hint">This will be billed as an ad-hoc line and will not create a catalogue product.</span>}
        </div>
        {salesInvoiceDraft && <div className="billing-card" role="region" aria-label="Review extracted sales invoice">
          <h4>Review extracted invoice before adding it to the bill</h4>
          {salesInvoiceDraft.invoiceNumber && <p>Source invoice reference: <strong>{salesInvoiceDraft.invoiceNumber}</strong> (not the RBOS invoice number)</p>}
          {salesInvoiceDraft.total != null && <p>Handwritten total detected: {money(salesInvoiceDraft.total)} INR — verify it against the line values.</p>}
          {salesInvoiceWarnings.length > 0 && <ul className="billing-hint">{salesInvoiceWarnings.map((warning, index) => <li key={index}>{warning}</li>)}</ul>}
          {salesInvoiceDraft.lines.length === 0 && <p>No usable lines were detected. You can still enter the bill manually.</p>}
          {salesInvoiceDraft.lines.length > 0 && <div className="billing-table-wrap"><table className="billing-table">
            <thead><tr><th>Description / model</th><th>Brand</th><th>Model</th><th>HSN</th><th>Qty</th><th>Unit price</th><th>Discount</th></tr></thead>
            <tbody>{salesInvoiceDraft.lines.map((line, index) => <tr key={index}>
              <td><input aria-label={`Extracted line ${index + 1} description`} value={line.description ?? ''} onChange={e => updateCapturedLine(index, 'description', e.target.value)} />{line.confidence != null && <small>Extraction confidence: {Math.round(line.confidence * 100)}%</small>}</td>
              <td><input aria-label={`Extracted line ${index + 1} brand`} value={line.brand ?? ''} onChange={e => updateCapturedLine(index, 'brand', e.target.value)} /></td>
              <td><input aria-label={`Extracted line ${index + 1} model`} value={line.model ?? ''} onChange={e => updateCapturedLine(index, 'model', e.target.value)} /></td>
              <td><input aria-label={`Extracted line ${index + 1} HSN`} value={line.hsn ?? ''} onChange={e => updateCapturedLine(index, 'hsn', e.target.value)} /></td>
              <td><input aria-label={`Extracted line ${index + 1} quantity`} inputMode="decimal" value={line.quantity ?? ''} onChange={e => updateCapturedLine(index, 'quantity', e.target.value)} /></td>
              <td><input aria-label={`Extracted line ${index + 1} unit price`} inputMode="decimal" value={line.unitPrice ?? ''} onChange={e => updateCapturedLine(index, 'unitPrice', e.target.value)} /></td>
              <td><input aria-label={`Extracted line ${index + 1} discount`} inputMode="decimal" value={line.discount ?? ''} onChange={e => updateCapturedLine(index, 'discount', e.target.value)} /></td>
            </tr>)}</tbody>
          </table></div>}
          <div className="billing-row">
            <button type="button" onClick={addCapturedLines} disabled={salesInvoiceDraft.lines.length === 0}>Add reviewed lines to bill</button>
            <button type="button" className="secondary" onClick={() => { setSalesInvoiceDraft(null); setSalesInvoiceWarnings([]); setSourceInvoiceRef(''); setSourceInvoiceDate('') }}>Discard extraction</button>
          </div>
        </div>}
        {items.length > 0 && <table className="billing-table">
          <thead><tr><th>{tr('bill.item')}</th><th>{tr('bill.qty')}</th><th>{tr('bill.price')}</th><th>{tr('bill.disc')}</th><th>{tr('bill.amount')}</th><th /></tr></thead>
          <tbody>{items.map((i, n) => <tr key={n}><td>{i.name}{i.brand && <small> Brand: {i.brand}</small>}{i.model && <small> Model: {i.model}</small>}{i.serials.length > 0 && <small> Serial: {i.serials.join(', ')}</small>}{i.imei1s.length > 0 && <small> IMEI 1: {i.imei1s.join(', ')}</small>}{i.imei2s.length > 0 && <small> IMEI 2: {i.imei2s.join(', ')}</small>}{i.barcode && <small> Barcode: {i.barcode}</small>}</td><td>{i.quantity}</td><td>{money(i.unitPrice)}</td><td>{money(i.discount)}</td><td>{money(itemTotal(i))}</td><td><button type="button" className="secondary" onClick={() => { void removeInvoiceItem(n) }}>{tr('bill.remove')}</button></td></tr>)}</tbody>
        </table>}
      </div>

      {showSerialNotice && <p className="billing-hint" role="status">Serial number not entered for one or more items. You can still continue; serial, IMEI and barcode are optional.</p>}
      <div className="billing-card billing-foot invoice-summary-bar">
        <div className="billing-gst">
          <label className="billing-check"><input type="checkbox" checked={gst} onChange={e => setGst(e.target.checked)} /> GST invoice</label>
          {gst && <select aria-label="GST rate" value={rate} onChange={e => setRate(Number(e.target.value))}>{GST_RATES.map(r => <option key={r} value={r}>{r}%</option>)}</select>}
        </div>
        <div className="billing-field"><label htmlFor="billing-bill-disc">{tr('bill.billDisc')}</label><input id="billing-bill-disc" inputMode="decimal" value={billDisc} onChange={e => setBillDisc(e.target.value)} /></div>
        <div className="billing-total">{tr('bill.total')} <strong>{money(total)} INR</strong>{gst && <small> incl. GST {money(tax)}</small>}</div>
        <button type="button" onClick={review}>{tr('bill.generate')}</button>
        <button type="button" className="secondary" onClick={() => { void hold() }} disabled={!items.length}>{tr('bill.hold')}</button>
      </div>
    </section>
  )
}
