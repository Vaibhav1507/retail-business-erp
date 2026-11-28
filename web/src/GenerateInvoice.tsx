import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { api, ApiError } from './api'
import './invoice.css'
import './billing-forms.css'
import { InvoiceDocument } from './InvoiceDocument'
import { ItemTagCapture, type CapturedInvoiceItem } from './ItemTagCapture'
import { SalesInvoiceCapture, type ExtractedSalesInvoice, type SalesInvoiceReviewIssue, type SalesInvoiceIdentifierCandidate } from './SalesInvoiceCapture'
import { t as tr } from './i18n'
import { whatsappStatusDescription, whatsappStatusLabel } from './whatsappStatus'
import { discardOfflineSale, listOfflineSales, queueOfflineSale, syncOfflineSales, type OfflineSaleBody, type OfflineSaleItem } from './offlineSales'
import { readOfflineRecords, saveOfflineRecord } from './offlineStore'

type Opt = { id: string; name: string }
type Customer = { id: string; name: string; phone: string | null; notes: string | null; isActive: boolean }
type Biz = { businessName: string; address: string; city: string; state: string; pinCode: string; mobile: string; gstin: string | null }
type Item = { productId: string; name: string; quantity: number; unitPrice: number; discount: number; serials: string[]; imei1s: string[]; imei2s: string[]; barcode?: string; barcodeSymbology?: string; brand?: string; model?: string; category?: string; mrp?: number | null; hsn?: string }

const GST_RATES = [0, 3, 5, 12, 18, 28]
const money = (n: number) => n.toLocaleString('en-IN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
const validImei = (value: string) => {
  if (!/^\\d{15}$/.test(value)) return false
  let sum = 0
  for (let index = 0; index < 15; index++) {
    let digit = Number(value[index])
    if (index % 2 === 1) { digit *= 2; if (digit > 9) digit -= 9 }
    sum += digit
  }
  return sum % 10 === 0
}
const sourcePageUrl = (id: string) => '/api/documents/' + encodeURIComponent(id) + '/pages/1'
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

function AutoGrowTextarea({
  id, value, onChange, className, ariaLabel, placeholder, minHeight = 52, rows = 1,
  required = false, ariaInvalid, onFocus, onBlur, autoComplete = 'off'
}: {
  id?: string
  value: string
  onChange: (value: string) => void
  className?: string
  ariaLabel: string
  placeholder?: string
  minHeight?: number
  rows?: number
  required?: boolean
  ariaInvalid?: boolean
  onFocus?: () => void
  onBlur?: () => void
  autoComplete?: string
}) {
  const ref = useRef<HTMLTextAreaElement>(null)
  useEffect(() => {
    const element = ref.current
    if (!element) return
    element.style.height = 'auto'
    element.style.height = `${Math.max(minHeight, element.scrollHeight)}px`
  }, [value, minHeight])
  return <textarea id={id} ref={ref} rows={rows} className={className} aria-label={ariaLabel}
    required={required} aria-invalid={ariaInvalid || undefined}
    autoComplete={autoComplete} placeholder={placeholder} value={value}
    onChange={event => onChange(event.target.value)} onFocus={onFocus} onBlur={onBlur}
    style={{ minHeight, resize: 'none', overflowY: 'hidden' }} />
}

export function GenerateInvoice({ products, locations, reload, offlineMode = false, ownerKey = '', businessCategory = null }: { products: Opt[]; locations: Opt[]; reload: () => void; offlineMode?: boolean; ownerKey?: string; businessCategory?: string | null }) {
  const isElectronicsBusiness = businessCategory === 'electronics_mobile'
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
  const [salesInvoiceCaptureKey, setSalesInvoiceCaptureKey] = useState(0)
  const [salesInvoiceWarnings, setSalesInvoiceWarnings] = useState<string[]>([])
  const [salesInvoiceReviewIssues, setSalesInvoiceReviewIssues] = useState<SalesInvoiceReviewIssue[]>([])
  const [sourceDocumentId, setSourceDocumentId] = useState<string | null>(null)
  const [sourceDocumentMime, setSourceDocumentMime] = useState('')
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
    setAddress(m ? [m[1], m[2], m[3], m[4]].filter(Boolean).join(', ') : ''); setCity(''); setState(''); setPin('')
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
    setError('')
    setFieldErrors({})
  }

  async function removeInvoiceItem(index: number) {
    setItems(current => current.filter((_, i) => i !== index))
  }

  function applySalesInvoiceDraft(draft: ExtractedSalesInvoice, warnings: string[], reviewIssues: SalesInvoiceReviewIssue[], documentId: string, sourceMime: string) {
    setSourceDocumentId(documentId)
    setSourceDocumentMime(sourceMime)
    setSalesInvoiceDraft(draft)
    setSalesInvoiceWarnings(warnings)
    setSalesInvoiceReviewIssues(reviewIssues)
    setSourceInvoiceRef(draft.invoiceNumber?.trim() ?? '')
    setSourceInvoiceDate(draft.date && /^\d{4}-\d{2}-\d{2}$/.test(draft.date) ? draft.date : '')
    if (draft.customerName) { setCustomerId(''); setName(draft.customerName) }
    if (draft.customerPhone) setPhone(draft.customerPhone)
    if (draft.date && /^\d{4}-\d{2}-\d{2}$/.test(draft.date)) setDate(draft.date)
  }

  function salesInvoiceFieldIssue(field: string, lineIndex: number | null = null) {
    return salesInvoiceReviewIssues
      .filter(issue => issue.field === field && issue.lineIndex === lineIndex)
      .map(issue => issue.message)
      .join(' ')
  }

  function updateCapturedLine(index: number, field: 'description' | 'brand' | 'model' | 'serialNumbers' | 'hsn' | 'quantity' | 'unitPrice' | 'discount', value: string) {
    setSalesInvoiceReviewIssues(current => current.filter(issue => issue.lineIndex !== index || issue.field !== field))
    setSalesInvoiceDraft(current => current ? ({
      ...current,
      lines: current.lines.map((line, lineIndex) => lineIndex !== index ? line : {
        ...line,
        [field]: ['quantity', 'unitPrice', 'discount'].includes(field)
          ? (value.trim() === '' ? null : Number.isFinite(Number(value)) ? Number(value) : null)
          : field === 'serialNumbers'
            ? value.split(/[\r\n,;]+/).map(part => part.trim()).filter(Boolean)
            : value
      })
    }) : current)
  }

  function updateCapturedIdentifier(index: number, candidateIndex: number, field: 'value' | 'classification' | 'confirmed', value: string | boolean) {
    setSalesInvoiceDraft(current => current ? ({
      ...current,
      lines: current.lines.map((line, lineIndex) => lineIndex !== index ? line : ({
        ...line,
        identifierCandidates: (line.identifierCandidates ?? []).map((candidate, indexOfCandidate) =>
          indexOfCandidate !== candidateIndex ? candidate : ({
            ...candidate,
            [field]: field === 'confirmed'
              ? Boolean(value)
              : field === 'classification'
                ? value as SalesInvoiceIdentifierCandidate['classification']
                : String(value),
            ...(field === 'value' && typeof value === 'string'
              ? { luhnValid: candidate.classification === 'imei' ? validImei(value.trim()) : null }
              : {}),
            ...(field === 'classification' && value === 'imei'
              ? { luhnValid: validImei(candidate.value.trim()) }
              : {})
          }))
      }))
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
    const excessiveDiscount = salesInvoiceDraft.lines.findIndex(line =>
      line.discount != null && line.discount > line.quantity! * line.unitPrice!)
    if (excessiveDiscount >= 0) {
      setError(`Discount cannot exceed the line amount for item ${excessiveDiscount + 1}.`)
      return
    }
    const knownSerials = new Set(items.flatMap(item => [...item.serials, ...item.imei1s, ...item.imei2s]).map(value => value.trim().toLowerCase()))
    for (const line of salesInvoiceDraft.lines) {
      const candidates = line.identifierCandidates ?? []
      const confirmedSerials = candidates.filter(candidate => candidate.classification === 'serial' && candidate.confirmed === true).map(candidate => candidate.value.trim())
      const confirmedImeis = candidates.filter(candidate => candidate.classification === 'imei' && candidate.confirmed === true).map(candidate => candidate.value.trim())
      if (confirmedImeis.some(value => !validImei(value))) {
        setError('A confirmed IMEI is not valid. Check its 15 digits and check digit, or change its classification.')
        return
      }
      const serials = [...(line.serialNumbers ?? []).map(value => value.trim()).filter(Boolean), ...confirmedSerials]
      if (serials.some(value => value.length < 4 || value.length > 40 || /\s/.test(value)) || serials.length > (line.quantity ?? 0)) {
        setError('Check the extracted serial numbers. Each must be 4–40 characters and the count cannot exceed the item quantity.')
        return
      }
      for (const serial of serials) {
        const normalized = serial.toLowerCase()
        if (knownSerials.has(normalized)) {
          setError('A serial number is repeated in this bill. Correct the serial number before adding the extracted lines.')
          return
        }
        knownSerials.add(normalized)
      }
    }
    const next: Item[] = salesInvoiceDraft.lines.map(line => {
      const candidates = line.identifierCandidates ?? []
      const confirmedImeis = candidates.filter(candidate => candidate.classification === 'imei' && candidate.confirmed === true).map(candidate => candidate.value.trim())
      const confirmedBarcodes = candidates.filter(candidate => candidate.classification === 'barcode' && candidate.confirmed === true).map(candidate => candidate.value.trim())
      const confirmedModels = candidates.filter(candidate => candidate.classification === 'model' && candidate.confirmed === true).map(candidate => candidate.value)
      return {
        productId: '',
        name: line.description && line.description.trim() ? line.description : line.model!.trim(),
        quantity: line.quantity!,
        unitPrice: line.unitPrice!,
        discount: line.discount ?? 0,
        serials: [...(line.serialNumbers ?? []), ...candidates.filter(candidate => candidate.classification === 'serial' && candidate.confirmed === true).map(candidate => candidate.value.trim())],
        imei1s: confirmedImeis.slice(0, 1),
        imei2s: confirmedImeis.slice(1, 2),
        barcode: confirmedBarcodes[0] || undefined,
        brand: line.brand?.trim() || undefined,
        model: line.model?.trim() || confirmedModels[0] || undefined,
        hsn: line.hsn?.trim() || undefined
      }
    })
    setItems(current => [...current, ...next])
    setSalesInvoiceDraft(null)
    setSalesInvoiceWarnings([])
    setSalesInvoiceReviewIssues([])
    setSalesInvoiceCaptureKey(value => value + 1)
    setError('')
  }

  async function discardSalesInvoiceDraft() {
    if (sourceDocumentId) {
      try {
        await api('POST', '/sales/invoice-image-extract/' + encodeURIComponent(sourceDocumentId) + '/discard', {})
      } catch (error) {
        setError(error instanceof Error ? 'The original bill could not be discarded. ' + error.message : 'The original bill could not be discarded. Retry before continuing.')
        return
      }
    }
    setSourceDocumentId(null)
    setSourceDocumentMime('')
    setSalesInvoiceDraft(null)
    setSalesInvoiceWarnings([])
    setSalesInvoiceReviewIssues([])
    setSourceInvoiceRef('')
    setSourceInvoiceDate('')
    setSalesInvoiceCaptureKey(value => value + 1)
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
    if (serials.some(value => value.length < 4 || value.length > 40 || /\s/.test(value)) || serials.length > quantity)
      nextErrors.serial = 'Enter valid serial numbers (4–40 characters each, no more than the quantity).'
    if ((imei1Text.trim() && !validImei(imei1Text.trim())) || (imei2Text.trim() && !validImei(imei2Text.trim())))
      nextErrors.serial = 'IMEI must contain 15 digits and pass its check digit.'
    if (Object.keys(nextErrors).length) { if (nextErrors.serial) setShowTagDetails(true); setFieldErrors(nextErrors); setError(''); return }
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
        const notes = address.trim() ? [address.trim(), '', '', ''].join(' | ') : null
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
        sourceInvoiceRef: sourceInvoiceRef.trim() || null, sourceInvoiceDate: sourceInvoiceDate || null,
        sourceDocumentId }

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
    setSalesInvoiceDraft(null); setSalesInvoiceWarnings([]); setSourceDocumentId(null); setSourceDocumentMime(''); setSourceInvoiceRef(''); setSourceInvoiceDate(''); setSalesInvoiceCaptureKey(value => value + 1)
    setWhatsappStatus('NotAvailable'); setCustomerId(''); setPhone(''); setName(''); setAddress(''); setCity(''); setState(''); setPin(''); setPaid(''); setPaid2(''); setMode('Cash'); setGst(false)
  }

  async function hold() {
    if (!items.length || !ownerKey) return
    const data = { customerId, phone, name, address, city, state, pin, date, mode, paid, payMethod, paid2, payMethod2, gst, rate, items, billDisc, sourceInvoiceRef, sourceInvoiceDate, sourceDocumentId, sourceDocumentMime }
    const label = (name.trim() || phone.trim() || 'Walk-in') + ' - ' + items.length + ' item(s)'
    const next = [{ id: crypto.randomUUID(), label, data }, ...held].slice(0, 20)
    await saveOfflineRecord(ownerKey, 'held-bills', 'held-bills', 'ready', next); setHeld(next); clearForm()
  }

  async function resume(h: Held) {
    const d = h.data
    skipSearch.current = true
    setCustomerId(d.customerId); setPhone(d.phone); setName(d.name)
    setAddress([d.address, d.city, d.state, d.pin].filter(Boolean).join(', ')); setCity(''); setState(''); setPin('')
    setDate(d.date); setMode(d.mode); setPaid(d.paid); setPayMethod(d.payMethod); setPaid2(d.paid2); setPayMethod2(d.payMethod2)
    setGst(d.gst); setRate(d.rate);
    setItems((d.items ?? []).map((item: Item) => ({
      ...item, imei1s: item.imei1s ?? [], imei2s: item.imei2s ?? []
    })));
    setSourceInvoiceRef(d.sourceInvoiceRef ?? ''); setSourceInvoiceDate(d.sourceInvoiceDate ?? '');
    setSourceDocumentId(d.sourceDocumentId ?? null); setSourceDocumentMime(d.sourceDocumentMime ?? '');
    setBillDisc(d.billDisc ?? '')
    const next = held.filter(x => x.id !== h.id); await saveOfflineRecord(ownerKey, 'held-bills', 'held-bills', 'ready', next); setHeld(next)
  }

  async function discardHeld(id: string) {
    if (!window.confirm('Discard this held bill?')) return
    const heldBill = held.find(x => x.id === id)
    if (heldBill?.data.sourceDocumentId && navigator.onLine) {
      try { await api('POST', '/sales/invoice-image-extract/' + encodeURIComponent(heldBill.data.sourceDocumentId) + '/discard', {}) }
      catch { setError('The held bill was removed, but its retained source file could not be marked discarded.'); return }
    }
    const next = held.filter(x => x.id !== id); await saveOfflineRecord(ownerKey, 'held-bills', 'held-bills', 'ready', next); setHeld(next)
  }

  function reset() {
    clearForm(); setPayMethod('Cash'); setInvNo(''); setWarn(''); setError(''); setStep('form')
  }

  if (step === 'pending') return (
    <section className="invoice-wrap" aria-label="Pending offline invoice">
      <h2>Bill saved on this device</h2>
      <p role="status"><strong>Pending sync</strong> — this is a draft, not a posted invoice. It has no official invoice number yet.</p>
      <p>When the connection returns, Dhandha Mitra will submit it using the same idempotency key. The server will recheck stock, tax, prices, permissions and accounting rules before assigning an invoice number.</p>
      <button type="button" onClick={() => setStep('form')}>Back to billing</button>
    </section>
  )

  if (step !== 'form') {
    const showGst = gst
    return (
      <section aria-label="Invoice overview" className="invoice-wrap">
        <div className="invoice-tools no-print">
          {step === 'review' && <><button type="button" disabled={busy} onClick={confirm}>{busy ? 'Saving\u2026' : 'Confirm & print'}</button><button type="button" className="secondary" disabled={busy} onClick={() => setStep('form')}>Back to edit</button></>}
          {step === 'done' && <><span role="status">Saved as {invNo}.{warn && ' ' + warn}</span><span role="status">WhatsApp invoice: {whatsappStatusLabel(whatsappStatus)}{whatsappStatusDescription(whatsappStatus) && <small className="invoice-whatsapp-help">{whatsappStatusDescription(whatsappStatus)}</small>}</span><select aria-label="Paper size" value={paper} onChange={e => setPaper(e.target.value)}><option value="a4">A4</option><option value="thermal">80 mm thermal</option></select><style>{paper === 'thermal' ? '@page{size:80mm auto;margin:3mm} @media print{.invoice{font-size:11px}}' : '@page{size:A4;margin:10mm}'}</style><button type="button" onClick={() => window.print()}>Print again</button><button type="button" onClick={reset}>New invoice</button></>}
        </div>
        {error && <p role="alert">{error}</p>}
        {showSerialNotice && <p className="billing-hint" role="status">Serial number not entered for one or more items. You can still continue; serial, IMEI and barcode are optional.</p>}
        {sourceInvoiceRef && <p className="billing-hint">Source invoice reference: {sourceInvoiceRef}{sourceInvoiceDate ? ` · ${sourceInvoiceDate}` : ''}</p>}
        <InvoiceDocument biz={biz} showGst={showGst} rate={rate} invoiceNo={invNo || 'Pending'} date={date} payment={mode} location={locations.length > 1 ? locName : undefined} customerName={name} customerPhone={phone} customerAddress={[address, city, state, pin].filter(Boolean).join(', ')} items={effItems} paid={mode === 'Credit' ? Number(paid || 0) + Number(paid2 || 0) : total} credit={mode === 'Credit'} />
      </section>
    )
  }

  return (
    <section aria-label="Generate invoice" className="billing-page invoice-create-page">
      <header className="invoice-new-sale-header">
        <a href="#/sell/menu" className="invoice-back" aria-label="Back to Billing and Sales">‹</a>
        <div className="invoice-new-sale-copy">
          <h1>New sale</h1>
          <p>Create a bill for your customer</p>
        </div>
        <span className="invoice-draft-pill">DRAFT</span>
      </header>

      <ol className="invoice-stepper" aria-label="Invoice steps">
        <li aria-current="step"><strong>1</strong> Add items</li>
        <li className="invoice-stepper-line" aria-hidden="true" />
        <li><strong>2</strong> Review</li>
      </ol>

      {held.length > 0 && <section className="invoice-utility-card" aria-label="Held bills">
        <h3>Held bills</h3>
        <ul>{held.map(h => <li key={h.id}>{h.label} <button type="button" className="secondary" onClick={() => { void resume(h) }}>Resume</button> <button type="button" className="secondary" onClick={() => { void discardHeld(h.id) }}>Discard</button></li>)}</ul>
      </section>}
      {offlineQueue.length > 0 && <section className="invoice-utility-card" aria-label="Offline bill queue">
        <h3>Offline bills and sync status</h3>
        <p>{offlineQueue.filter(x => x.state === 'pending-sync' || x.state === 'syncing').length} waiting to sync · {offlineQueue.filter(x => x.state === 'needs-attention').length} need attention · {offlineQueue.filter(x => x.state === 'posted').length} posted</p>
        <button type="button" className="secondary" disabled={!navigator.onLine || syncingOffline} onClick={() => void syncQueue()}>{syncingOffline ? 'Syncing…' : 'Sync pending bills'}</button>
        <ul>{offlineQueue.slice(0, 10).map(item => <li key={item.id}>
          {new Date(item.createdAt).toLocaleString('en-IN')} — {item.state === 'posted' ? (item.value.invoiceNumber ? 'Posted as INV-' + String(item.value.invoiceNumber).padStart(6, '0') : 'Posted; invoice details are still loading') : item.state === 'needs-attention' ? 'Needs attention: ' + (item.value.lastError ?? 'Server rejected this draft') : item.state === 'syncing' ? 'Syncing…' : 'Pending sync' + (item.value.lastError ? ': ' + item.value.lastError : '')}
          {(item.state === 'posted' || item.state === 'needs-attention') && <button type="button" className="secondary" onClick={() => { void removeQueueItem(item) }}>Remove from device</button>}
        </li>)}</ul>
      </section>}
      {error && <p role="alert" className="billing-err invoice-form-error">{error}</p>}

      <section className="invoice-section-card invoice-customer-card" aria-labelledby="invoice-customer-title">
        <div className="invoice-section-eyebrow"><span>CUSTOMER</span><b>01</b></div>
        <h2 id="invoice-customer-title">Who are you billing?</h2>
        <div className="invoice-customer-fields">
          <div className="billing-field billing-rel">
            <label htmlFor="billing-phone">Mobile number *</label>
            <input id="billing-phone" inputMode="tel" autoComplete="off" required aria-invalid={Boolean(fieldErrors.customerPhone)} aria-describedby={fieldErrors.customerPhone ? "billing-phone-error" : undefined} className={fieldErrors.customerPhone ? 'field-invalid' : ''} placeholder="Enter 10-digit mobile number" value={phone}
              onChange={e => { setPhone(e.target.value.replace(/[^\d+ ]/g, '')); setCustomerId(''); setFieldErrors(current => ({ ...current, customerPhone: '' })); setSalesInvoiceReviewIssues(current => current.filter(issue => issue.field !== 'customerPhone')) }}
              onFocus={() => setShowFound(found.length > 0)} onBlur={() => setTimeout(() => setShowFound(false), 150)} />
            {salesInvoiceFieldIssue('customerPhone') && <small className="invoice-extraction-field-warning">{salesInvoiceFieldIssue('customerPhone')}</small>}
            {fieldErrors.customerPhone && <small id="billing-phone-error" className="field-error">{fieldErrors.customerPhone}</small>}
            {showFound && found.length > 0 && <ul className="billing-menu" role="listbox" aria-label="Matching customers">
              {found.slice(0, 6).map(c => <li key={c.id} role="option" aria-selected={false} onMouseDown={e => { e.preventDefault(); pick(c) }}><strong>{c.name}</strong><span>{c.phone}</span></li>)}
            </ul>}
          </div>
          <div className="billing-field">
            <label htmlFor="billing-name">Customer name *</label>
            <input id="billing-name" required aria-invalid={Boolean(fieldErrors.customerName)} aria-describedby={fieldErrors.customerName ? "billing-name-error" : undefined} className={fieldErrors.customerName ? 'field-invalid' : ''} placeholder="Name on the bill" value={name} onChange={e => { setName(e.target.value); setFieldErrors(current => ({ ...current, customerName: '' })); setSalesInvoiceReviewIssues(current => current.filter(issue => issue.field !== 'customerName')) }} />
            {fieldErrors.customerName && <small id="billing-name-error" className="field-error">{fieldErrors.customerName}</small>}
            {salesInvoiceFieldIssue('customerName') && <small className="invoice-extraction-field-warning">{salesInvoiceFieldIssue('customerName')}</small>}
          </div>
          <div className="billing-field">
            <label htmlFor="billing-address">Address (optional)</label>
            <input id="billing-address" autoComplete="street-address" placeholder="House / shop / street / area" value={address} onChange={e => setAddress(e.target.value)} />
          </div>
        </div>
        <p className="invoice-helper">{customerId ? 'Existing customer. You can edit their details before confirming the bill.' : 'New customer? Enter their name and number to add them with this bill.'}</p>
      </section>

      <section className="invoice-section-card invoice-items-card" aria-labelledby="invoice-items-title">
        <div className="invoice-section-eyebrow"><span>ITEMS</span><b>02</b></div>
        <h2 id="invoice-items-title">Add products</h2>
        <p className="invoice-section-subtitle">Search your catalogue or enter the item manually.</p>

        <div className="invoice-import-callout">
          <span className="invoice-import-prompt">Have a handwritten bill?</span>
          <SalesInvoiceCapture key={salesInvoiceCaptureKey} disabled={offlineMode || !navigator.onLine} onExtract={applySalesInvoiceDraft} />
        </div>

        <div className="billing-grid invoice-product-editor">
          <div className="billing-field billing-col-12 billing-rel" hidden={Boolean(salesInvoiceDraft)}>
            <label htmlFor="billing-prod">Item description *</label>
            <div className="invoice-product-entry">
              <AutoGrowTextarea id="billing-prod" className={`invoice-product-description${fieldErrors.product ? ' field-invalid' : ''}`} rows={2} minHeight={76} autoComplete="off" required ariaLabel="Item description *" ariaInvalid={Boolean(fieldErrors.product)} placeholder="Search product or type a name" value={pname}
                onChange={value => { setPname(value); setSelectedProductId(''); setFieldErrors(current => ({ ...current, product: '' })); setShowProducts(true) }} onFocus={() => setShowProducts(true)} onBlur={() => setTimeout(() => setShowProducts(false), 150)} />
              <ItemTagCapture key={tagCaptureKey} disabled={offlineMode || !navigator.onLine} onExtract={applyCapturedItem} />
            </div>
            {fieldErrors.product && <small className="field-error">{fieldErrors.product}</small>}
            {showProducts && suggestions.length > 0 && !match && <ul className="billing-menu" role="listbox" aria-label="Products">
              {suggestions.map(p => <li key={p.id} role="option" aria-selected={false} onMouseDown={e => { e.preventDefault(); setPname(p.name); setSelectedProductId(p.id); setShowProducts(false) }}><strong>{p.name}</strong></li>)}
            </ul>}
          </div>

          <div className={`invoice-electronics-fields invoice-brand-model billing-col-12${isElectronicsBusiness ? '' : ' invoice-brand-only'}`} hidden={Boolean(salesInvoiceDraft)}>
            <div className="billing-field">
              <label htmlFor="billing-brand">Brand</label>
              <input id="billing-brand" autoComplete="off" value={brandText} onChange={e => setBrandText(e.target.value)} />
            </div>
            {isElectronicsBusiness && <div className="billing-field">
              <label htmlFor="billing-model">Model number</label>
              <input id="billing-model" autoComplete="off" placeholder="Enter model if available" value={modelText} onChange={e => setModelText(e.target.value)} />
            </div>}
          </div>
          <div className="invoice-electronics-fields invoice-quantity-price billing-col-12" hidden={Boolean(salesInvoiceDraft)}>
            <div className="billing-field">
              <label htmlFor="billing-qty">Quantity *</label>
              <input id="billing-qty" aria-invalid={Boolean(fieldErrors.quantity)} aria-describedby={fieldErrors.quantity ? "billing-qty-error" : undefined} className={fieldErrors.quantity ? 'field-invalid' : ''} inputMode="decimal" onKeyDown={e => { if (e.key === 'Enter') { e.preventDefault(); addItem() } }} value={qty} onChange={e => { setQty(e.target.value); setFieldErrors(current => ({ ...current, quantity: '' })) }} />
              {fieldErrors.quantity && <small id="billing-qty-error" className="field-error">{fieldErrors.quantity}</small>}
            </div>
            <div className="billing-field">
              <label htmlFor="billing-price">Selling price (₹) *</label>
              <input id="billing-price" inputMode="decimal" placeholder="0.00" aria-invalid={Boolean(fieldErrors.price)} aria-describedby={fieldErrors.price ? "billing-price-error" : undefined} className={fieldErrors.price ? 'field-invalid' : ''} onKeyDown={e => { if (e.key === 'Enter') { e.preventDefault(); addItem() } }} value={price} onChange={e => { setPrice(e.target.value); setFieldErrors(current => ({ ...current, price: '' })) }} />
              {fieldErrors.price && <small id="billing-price-error" className="field-error">{fieldErrors.price}</small>}
            </div>
          </div>
          {isElectronicsBusiness && <div className="billing-field billing-col-12" hidden={Boolean(salesInvoiceDraft)}>
            <label htmlFor="billing-serial">Serial number (optional)</label>
            <input id="billing-serial" aria-invalid={Boolean(fieldErrors.serial)} aria-describedby={fieldErrors.serial ? "billing-serial-error" : undefined} className={fieldErrors.serial ? 'field-invalid' : ''} autoComplete="off" placeholder="Enter serial number if available" value={serialText} onChange={e => { setSerialText(e.target.value); setFieldErrors(current => ({ ...current, serial: '' })) }} />
            {fieldErrors.serial && <small id="billing-serial-error" className="field-error">{fieldErrors.serial}</small>}
          </div>}
        </div>

        <details className="invoice-product-more-details" hidden={Boolean(salesInvoiceDraft)} open={showTagDetails} onToggle={event => setShowTagDetails(event.currentTarget.open)}>
          <summary><span>More item details</span><small>Optional</small></summary>
          <div className="billing-grid invoice-product-extra-grid">
            <div className="billing-field billing-col-6"><label htmlFor="billing-disc">{tr('bill.disc')}</label><input id="billing-disc" aria-invalid={Boolean(fieldErrors.discount)} className={fieldErrors.discount ? 'field-invalid' : ''} inputMode="decimal" value={disc} onChange={e => { setDisc(e.target.value); setFieldErrors(current => ({ ...current, discount: '' })) }} />{fieldErrors.discount && <small className="field-error">{fieldErrors.discount}</small>}</div>
            {!isElectronicsBusiness && <div className="billing-field billing-col-6"><label htmlFor="billing-model">Model number</label><input id="billing-model" autoComplete="off" placeholder="Enter model if available" value={modelText} onChange={e => setModelText(e.target.value)} /></div>}
            {!isElectronicsBusiness && <div className="billing-field billing-col-6"><label htmlFor="billing-serial">Serial number (optional)</label><input id="billing-serial" aria-invalid={Boolean(fieldErrors.serial)} aria-describedby={fieldErrors.serial ? "billing-serial-error" : undefined} className={fieldErrors.serial ? 'field-invalid' : ''} autoComplete="off" value={serialText} onChange={e => { setSerialText(e.target.value); setFieldErrors(current => ({ ...current, serial: '' })) }} />{fieldErrors.serial && <small id="billing-serial-error" className="field-error">{fieldErrors.serial}</small>}</div>}
            <div className="billing-field billing-col-6"><label htmlFor="billing-barcode">Barcode</label><input id="billing-barcode" autoComplete="off" value={barcodeText} onChange={e => setBarcodeText(e.target.value)} /></div>
            <div className="billing-field billing-col-6"><label htmlFor="billing-imei1">IMEI 1</label><input id="billing-imei1" inputMode="numeric" value={imei1Text} onChange={e => setImei1Text(e.target.value)} /></div>
            <div className="billing-field billing-col-6"><label htmlFor="billing-imei2">IMEI 2</label><input id="billing-imei2" inputMode="numeric" value={imei2Text} onChange={e => setImei2Text(e.target.value)} /></div>
            <div className="billing-field billing-col-6"><label htmlFor="billing-mrp">MRP (reference only)</label><input id="billing-mrp" aria-invalid={Boolean(fieldErrors.mrp)} className={fieldErrors.mrp ? 'field-invalid' : ''} inputMode="decimal" value={mrpText} onChange={e => { setMrpText(e.target.value); setFieldErrors(current => ({ ...current, mrp: '' })) }} />{fieldErrors.mrp && <small className="field-error">{fieldErrors.mrp}</small>}</div>
            <div className="billing-field billing-col-6"><label htmlFor="billing-hsn">HSN</label><input id="billing-hsn" value={hsnText} onChange={e => setHsnText(e.target.value)} /></div>
          </div>
        </details>

        <div className="invoice-add-item-action" hidden={Boolean(salesInvoiceDraft)}>
          <button type="button" onClick={addItem}>＋ Add item</button>
          {pname.trim() && !match && <span className="billing-hint">This will be billed as an ad-hoc line and will not create a catalogue product.</span>}
        </div>

        {salesInvoiceDraft && <section className="invoice-extraction-review" role="region" aria-label="Review extracted sales invoice" aria-labelledby="invoice-extraction-title">
          <header className="invoice-extraction-header">
            <div><p className="invoice-extraction-eyebrow">HANDWRITTEN BILL</p><h3 id="invoice-extraction-title">Review detected items</h3></div>
            <span className="invoice-extraction-count">{salesInvoiceDraft.lines.length} {salesInvoiceDraft.lines.length === 1 ? 'item' : 'items'}</span>
          </header>
          <div className="invoice-extraction-header-fields">
            <label>Source bill number<input aria-label="Source bill number" value={sourceInvoiceRef} onChange={e => { setSourceInvoiceRef(e.target.value); setSalesInvoiceDraft(current => current ? { ...current, fieldStates: { ...(current.fieldStates ?? {}), invoiceNumber: 'user_corrected' } } : current); setSalesInvoiceReviewIssues(current => current.filter(issue => issue.field !== 'invoiceNumber')) }} />{salesInvoiceFieldIssue('invoiceNumber') && <small className="invoice-extraction-field-warning">{salesInvoiceFieldIssue('invoiceNumber')}</small>}</label>
            <label>Bill date<input aria-label="Source bill date" type="date" value={sourceInvoiceDate} onChange={e => { setSourceInvoiceDate(e.target.value); setDate(e.target.value); setSalesInvoiceDraft(current => current ? { ...current, fieldStates: { ...(current.fieldStates ?? {}), date: 'user_corrected' } } : current); setSalesInvoiceReviewIssues(current => current.filter(issue => issue.field !== 'date')) }} />{salesInvoiceFieldIssue('date') && <small className="invoice-extraction-field-warning">{salesInvoiceFieldIssue('date')}</small>}</label>
          </div>
          <p className="invoice-extraction-total">Handwritten total <strong>{salesInvoiceDraft.total != null ? `₹${money(salesInvoiceDraft.total)}` : 'Not read'}</strong><small>Compare this with the extracted line values before continuing.</small>{salesInvoiceFieldIssue('total') && <small className="invoice-extraction-field-warning">{salesInvoiceFieldIssue('total')}</small>}</p>
          {salesInvoiceWarnings.length > 0 && <ul className="invoice-extraction-warnings">{salesInvoiceWarnings.map((warning, index) => <li key={index}>{warning}</li>)}</ul>}
          {salesInvoiceReviewIssues.some(issue => issue.lineIndex === null && !['customerName', 'customerPhone', 'invoiceNumber', 'date', 'total'].includes(issue.field)) &&
            <ul className="invoice-extraction-warnings">{salesInvoiceReviewIssues.filter(issue => issue.lineIndex === null && !['customerName', 'customerPhone', 'invoiceNumber', 'date', 'total'].includes(issue.field)).map((issue, index) => <li key={index}>{issue.message}</li>)}</ul>}
          {salesInvoiceDraft.lines.length === 0 && <p className="invoice-extraction-empty">No usable items were detected. Discard this import to enter items manually.</p>}
          {salesInvoiceDraft.lines.length > 0 && <div className="invoice-extraction-lines">
            {salesInvoiceDraft.lines.map((line, index) => <article className="invoice-extracted-line" key={index}>
              <div className="invoice-extracted-line-heading"><strong>Item {index + 1}</strong>{salesInvoiceReviewIssues.some(issue => issue.lineIndex === index) && <span>Check highlighted fields</span>}</div>
              <label>Product description (as written)<AutoGrowTextarea ariaLabel={'Extracted line ' + (index + 1) + ' description'} className="invoice-extracted-description" value={line.description ?? ''} minHeight={48} onChange={value => updateCapturedLine(index, 'description', value)} />{salesInvoiceFieldIssue('description', index) && <small className="invoice-extraction-field-warning">{salesInvoiceFieldIssue('description', index)}</small>}</label>
              <label>Serial number(s) (optional)<AutoGrowTextarea ariaLabel={'Extracted line ' + (index + 1) + ' serial numbers'} className="invoice-extracted-serials" value={(line.serialNumbers ?? []).join('\n')} minHeight={44} placeholder="One serial number per line" onChange={value => updateCapturedLine(index, 'serialNumbers', value)} />{salesInvoiceFieldIssue('serialNumbers', index) && <small className="invoice-extraction-field-warning">{salesInvoiceFieldIssue('serialNumbers', index)}</small>}</label>
              <div className="invoice-extracted-line-values">
                <label>Qty<input aria-label={'Extracted line ' + (index + 1) + ' quantity'} inputMode="decimal" value={line.quantity ?? ''} onChange={e => updateCapturedLine(index, 'quantity', e.target.value)} />{salesInvoiceFieldIssue('quantity', index) && <small className="invoice-extraction-field-warning">{salesInvoiceFieldIssue('quantity', index)}</small>}</label>
                <label>Unit price (₹)<input aria-label={'Extracted line ' + (index + 1) + ' unit price'} inputMode="decimal" value={line.unitPrice ?? ''} onChange={e => updateCapturedLine(index, 'unitPrice', e.target.value)} />{salesInvoiceFieldIssue('unitPrice', index) && <small className="invoice-extraction-field-warning">{salesInvoiceFieldIssue('unitPrice', index)}</small>}</label>
              </div>
              <details className="invoice-extracted-line-details">
                <summary>Brand, model, HSN and discount {salesInvoiceReviewIssues.some(issue => issue.lineIndex === index && ['brand', 'model', 'hsn', 'discount'].includes(issue.field)) && ' · Check details'}</summary>
                <div className="invoice-extracted-line-extra">
                  <label>Brand<input aria-label={'Extracted line ' + (index + 1) + ' brand'} value={line.brand ?? ''} onChange={e => updateCapturedLine(index, 'brand', e.target.value)} />{salesInvoiceFieldIssue('brand', index) && <small className="invoice-extraction-field-warning">{salesInvoiceFieldIssue('brand', index)}</small>}</label>
                  <label>Model<input aria-label={'Extracted line ' + (index + 1) + ' model'} value={line.model ?? ''} onChange={e => updateCapturedLine(index, 'model', e.target.value)} />{salesInvoiceFieldIssue('model', index) && <small className="invoice-extraction-field-warning">{salesInvoiceFieldIssue('model', index)}</small>}</label>
                  <label>HSN<input aria-label={'Extracted line ' + (index + 1) + ' HSN'} value={line.hsn ?? ''} onChange={e => updateCapturedLine(index, 'hsn', e.target.value)} />{salesInvoiceFieldIssue('hsn', index) && <small className="invoice-extraction-field-warning">{salesInvoiceFieldIssue('hsn', index)}</small>}</label>
                  <label>Discount (₹)<input aria-label={'Extracted line ' + (index + 1) + ' discount'} inputMode="decimal" value={line.discount ?? ''} onChange={e => updateCapturedLine(index, 'discount', e.target.value)} />{salesInvoiceFieldIssue('discount', index) && <small className="invoice-extraction-field-warning">{salesInvoiceFieldIssue('discount', index)}</small>}</label>
                </div>
              </details>
              {(line.gstRate != null || line.gstAmount != null || line.amount != null) && <p className="invoice-extracted-line-meta">
                {line.amount != null && <strong>Line amount ₹{money(line.amount)}</strong>}{line.gstRate != null && <span>GST {line.gstRate}%</span>}{line.gstAmount != null && <span>Tax ₹{money(line.gstAmount)}</span>}
              </p>}
              {salesInvoiceReviewIssues.some(issue => issue.lineIndex === index && !['description', 'serialNumbers', 'quantity', 'unitPrice', 'brand', 'model', 'hsn', 'discount'].includes(issue.field)) &&
                <ul className="invoice-extraction-warnings">{salesInvoiceReviewIssues.filter(issue => issue.lineIndex === index && !['description', 'serialNumbers', 'quantity', 'unitPrice', 'brand', 'model', 'hsn', 'discount'].includes(issue.field)).map((issue, issueIndex) => <li key={issueIndex}>{issue.message}</li>)}</ul>}
            </article>)}
          </div>}
          <div className="invoice-extraction-actions">
            <button type="button" onClick={addCapturedLines} disabled={salesInvoiceDraft.lines.length === 0}>Add reviewed lines to bill</button>
            <button type="button" className="secondary" onClick={discardSalesInvoiceDraft}>Discard extraction</button>
          </div>
        </section>}

        {items.length > 0 && <div className="invoice-added-items" aria-label="Items added to this bill">
          {items.map((i, n) => <article className="invoice-added-item" key={n}>
            <div className="invoice-added-item-main"><strong>{i.name}</strong>
              {i.brand && <small>Brand: {i.brand}</small>}
              {i.model && <small>Model: {i.model}</small>}
              {i.serials.length > 0 && <small>Serial: {i.serials.join(', ')}</small>}
              {i.imei1s.length > 0 && <small>IMEI 1: {i.imei1s.join(', ')}</small>}
              {i.imei2s.length > 0 && <small>IMEI 2: {i.imei2s.join(', ')}</small>}
              {i.barcode && <small>Barcode: {i.barcode}</small>}
              <span>{i.quantity} × ₹{money(i.unitPrice)}</span>
            </div>
            <div className="invoice-added-item-total"><strong>₹{money(itemTotal(i))}</strong><button type="button" className="secondary" onClick={() => { void removeInvoiceItem(n) }}>Remove</button></div>
          </article>)}
        </div>}
      </section>

      {showSerialNotice && <p className="billing-hint invoice-serial-notice" role="status">Serial number not entered for one or more items. You can still continue; serial, IMEI and barcode are optional.</p>}

      <section className="invoice-section-card invoice-bill-summary" aria-label="Bill summary">
        <div className="invoice-gst-row">
          <label className="invoice-gst-toggle">
            <input type="checkbox" checked={gst} onChange={e => setGst(e.target.checked)} />
            <span><strong>GST invoice</strong><small>Include GST details on this bill</small></span>
          </label>
          {gst && <label className="invoice-gst-rate">GST rate
            <select aria-label="GST rate" value={rate} onChange={e => setRate(Number(e.target.value))}>{GST_RATES.map(r => <option key={r} value={r}>{r}%</option>)}</select>
          </label>}
        </div>
        <div className="invoice-summary-line"><span>Subtotal</span><strong>₹{money(grossTotal)}</strong></div>
        <div className="invoice-summary-line invoice-discount-line">
          <label htmlFor="billing-bill-disc">Bill discount (₹)</label>
          <input id="billing-bill-disc" inputMode="decimal" value={billDisc} onChange={e => setBillDisc(e.target.value)} />
        </div>
        <div className="invoice-summary-total"><strong>Amount to collect</strong><strong>₹{money(total)}</strong></div>
        {gst && <small className="invoice-summary-tax">Includes GST of ₹{money(tax)}</small>}
      </section>

      <div className="invoice-payment-mode">
        <div className="billing-field">
          <label htmlFor="billing-mode">Payment mode</label>
          <select id="billing-mode" value={mode} onChange={e => setMode(e.target.value)}><option>Cash</option><option>UPI</option><option>Credit</option></select>
        </div>
      </div>
      <details className="invoice-more-details">
        <summary><span>Payment & bill details</span><small>Optional</small></summary>
        <div className="invoice-more-details-content billing-grid">
          {locations.length > 1 && <div className="billing-field billing-col-12"><label htmlFor="billing-store">Store</label><select id="billing-store" value={loc} onChange={e => setLocationId(e.target.value)}>{locations.map(l => <option key={l.id} value={l.id}>{l.name}</option>)}</select></div>}
          <div className="billing-field billing-col-6"><label htmlFor="billing-date">Sale date</label><input id="billing-date" type="date" value={date} onChange={e => setDate(e.target.value)} /></div>
          {mode === 'Credit' && <div className="billing-field billing-col-6"><label htmlFor="billing-paid">{tr('bill.paidNow')}</label><input id="billing-paid" inputMode="decimal" value={paid} onChange={e => setPaid(e.target.value)} /></div>}
          {mode === 'Credit' && <><div className="billing-field billing-col-6"><label htmlFor="billing-paid-method">Paid by</label><select id="billing-paid-method" value={payMethod} onChange={e => setPayMethod(e.target.value)}><option>Cash</option><option value="Bank">Card / Bank</option><option>UPI</option></select></div><div className="billing-field billing-col-6"><label htmlFor="billing-paid2">Second payment (INR, optional)</label><input id="billing-paid2" inputMode="decimal" value={paid2} onChange={e => setPaid2(e.target.value)} /></div><div className="billing-field billing-col-6"><label htmlFor="billing-paid2-method">Second paid by</label><select id="billing-paid2-method" value={payMethod2} onChange={e => setPayMethod2(e.target.value)}><option>Cash</option><option value="Bank">Card / Bank</option><option>UPI</option></select></div></>}
          <div className="invoice-more-details-actions billing-col-12">
            <button type="button" className="secondary" onClick={() => { void hold() }} disabled={!items.length}>Hold bill</button>
          </div>
        </div>
      </details>



      <footer className="invoice-sticky-review" aria-label="Bill total and review">
        <div className="invoice-sticky-amount">
          <span>{items.length} {items.length === 1 ? 'item' : 'items'}</span>
          <strong>₹{money(total)}</strong>
        </div>
        <button type="button" onClick={review} disabled={!items.length}>Review bill <span aria-hidden="true">→</span></button>
      </footer>
    </section>
  )
}
