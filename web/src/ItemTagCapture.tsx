import { useEffect, useRef, useState } from 'react'
import { api, apiBinary, ApiError } from './api'
import { BrowserMultiFormatReader } from '@zxing/browser'
import { BarcodeFormat } from '@zxing/library'

// Bound concurrent provider-backed uploads from a multi-select gallery.
let activeItemTagUploads = 0;
const waitingItemTagUploads: Array<() => void> = [];

async function withItemTagUploadSlot<T>(work: () => Promise<T>): Promise<T> {
  if (activeItemTagUploads >= 2) {
    await new Promise<void>(resolve => waitingItemTagUploads.push(resolve));
  } else {
    activeItemTagUploads++;
  }
  try {
    return await work();
  } finally {
    const next = waitingItemTagUploads.shift();
    if (next) next();
    else activeItemTagUploads--;
  }
}

type ProductOption = { id: string; name: string }
type Extraction = {
  productName?: string | null
  brand?: string | null
  category?: string | null
  model?: string | null
  barcode?: string | null
  barcodeSymbology?: string | null
  serial?: string | null
  imei1?: string | null
  imei2?: string | null
  mrp?: number | null
  hsn?: string | null
  warranty?: string | null
  specifications?: string | null
  otherText?: string | null
}
type CaptureResponse = {
  id: string
  status: string
  contentType: string
  sizeBytes: number
  extraction: Extraction
  suggestedProduct?: { id: string; name: string; reason?: string | null } | null
  review?: {
    productId?: string
    name?: string | null
    brand?: string | null
    model?: string | null
    barcode?: string | null
    barcodeSymbology?: string | null
    serial?: string | null
    imei1?: string | null
    imei2?: string | null
    mrp?: number | null
    hsnCode?: string | null
  } | null
}
type CaptureDraft = {
  key: string
  file: File | null
  preview: string
  busy: boolean
  error: string
  warning: string
  capture?: CaptureResponse
  name: string
  brand: string
  model: string
  barcode: string
  barcodeSymbology: string
  serial: string
  imei1: string
  imei2: string
  mrp: string
  hsn: string
  productId: string
  saving: boolean
  added: boolean
}

export type CapturedInvoiceItem = {
  captureId: string
  productId: string
  name: string
  serials: string[]
}

export function ItemTagCapture({
  products, disabled, onAdd, onRemove, linkedCaptureIds = []
}: {
  products: ProductOption[]
  disabled: boolean
  linkedCaptureIds?: string[]
  onAdd: (item: CapturedInvoiceItem) => boolean
  onRemove: (captureId: string, identifiers: string[]) => void
}) {
  const [entries, setEntries] = useState<CaptureDraft[]>([])
  const cameraRef = useRef<HTMLInputElement>(null)
  const galleryRef = useRef<HTMLInputElement>(null)
  const urls = useRef(new Set<string>())
  const loadedIds = useRef(new Set<string>())

  useEffect(() => () => {
    for (const url of urls.current) URL.revokeObjectURL(url)
    urls.current.clear()
  }, [])

  const linkedIdsKey = linkedCaptureIds.join('|')
  useEffect(() => {
    let active = true
    for (const id of linkedCaptureIds) {
      if (loadedIds.current.has(id)) continue
      loadedIds.current.add(id)
      void (async () => {
        try {
          const capture = await api<CaptureResponse>('GET', '/sales/item-tag-captures/' + id)
          const response = await fetch('/api/sales/item-tag-captures/' + id + '/image', { credentials: 'same-origin' })
          if (!response.ok) throw new Error('Saved photo could not be loaded.')
          const blob = await response.blob()
          const preview = URL.createObjectURL(blob)
          urls.current.add(preview)
          if (!active) { URL.revokeObjectURL(preview); urls.current.delete(preview); return }
          const review = capture.review
          setEntries(current => current.some(item => item.capture?.id === id) ? current : [...current, {
            key: 'saved:' + id, file: null, preview, busy: false, error: '', warning: '',
            capture, name: review?.name ?? capture.extraction?.productName ?? '',
            brand: review?.brand ?? capture.extraction?.brand ?? '',
            model: review?.model ?? capture.extraction?.model ?? '',
            barcode: review?.barcode ?? capture.extraction?.barcode ?? '',
            barcodeSymbology: review?.barcodeSymbology ?? capture.extraction?.barcodeSymbology ?? '',
            serial: review?.serial ?? capture.extraction?.serial ?? '',
            imei1: review?.imei1 ?? capture.extraction?.imei1 ?? '',
            imei2: review?.imei2 ?? capture.extraction?.imei2 ?? '',
            mrp: review?.mrp == null ? (capture.extraction?.mrp == null ? '' : String(capture.extraction.mrp)) : String(review.mrp),
            hsn: review?.hsnCode ?? capture.extraction?.hsn ?? '',
            productId: review?.productId ?? capture.suggestedProduct?.id ?? '',
            saving: false, added: capture.status === 'Reviewed' || capture.status === 'Attached'
          }])
        } catch {
          loadedIds.current.delete(id)
        }
      })()
    }
    return () => { active = false }
  }, [linkedIdsKey])

  function patch(key: string, values: Partial<CaptureDraft>) {
    setEntries(current => current.map(item => item.key === key ? { ...item, ...values } : item))
  }

  async function sendPhoto(draft: CaptureDraft) {
    if (!draft.file) return
    loadedIds.current.add(draft.key)
    patch(draft.key, { busy: true, error: '' })
    try {
      // Decode common retail barcodes locally before upload/AI processing.
      // QR/Data Matrix payloads are deliberately not treated as product barcodes.
      let decodedBarcode: { code: string; symbology: string } | null = null
      try {
        const reader = new BrowserMultiFormatReader()
        const decoded = await reader.decodeFromImageUrl(draft.preview)
        const format = decoded.getBarcodeFormat()
        const symbology = format === BarcodeFormat.EAN_13 ? 'EAN_13'
          : format === BarcodeFormat.EAN_8 ? 'EAN_8'
          : format === BarcodeFormat.UPC_A ? 'UPC_A'
          : format === BarcodeFormat.UPC_E ? 'UPC_E'
          : format === BarcodeFormat.CODE_128 ? 'CODE_128'
          : null
        if (symbology) {
          decodedBarcode = { code: decoded.getText(), symbology }
        }
      } catch { /* glare/blur/unsupported symbols fall back to the reviewable AI/manual path */ }

      // The API validates the barcode hint and performs the catalogue match itself.
      // Do not serialize upload behind a second barcode lookup request.
      const capture = await withItemTagUploadSlot(() => apiBinary<CaptureResponse>(
        '/sales/item-tag-captures', draft.file!, undefined, draft.key,
        decodedBarcode ? {
          'X-RBOS-Detected-Barcode': decodedBarcode.code,
          'X-RBOS-Detected-Symbology': decodedBarcode.symbology
        } : undefined))
      loadedIds.current.add(capture.id)

      patch(draft.key, {
        busy: false, warning: '', capture,
        name: capture.extraction?.productName ?? '',
        brand: capture.extraction?.brand ?? '',
        model: capture.extraction?.model ?? '',
        barcode: decodedBarcode?.code ?? capture.extraction?.barcode ?? '',
        barcodeSymbology: decodedBarcode?.symbology ?? capture.extraction?.barcodeSymbology ?? '',
        serial: capture.extraction?.serial ?? '',
        imei1: capture.extraction?.imei1 ?? '',
        imei2: capture.extraction?.imei2 ?? '',
        mrp: capture.extraction?.mrp == null ? '' : String(capture.extraction.mrp),
        hsn: capture.extraction?.hsn ?? '',
        productId: capture.suggestedProduct?.id ?? ''
      })
    } catch (e) {
      patch(draft.key, { busy: false, error: e instanceof Error ? e.message : 'Photo upload failed. Try again.' })
    }
  }

  function addFiles(files: FileList | null) {
    if (!files) return
    for (const file of Array.from(files)) {
      if (!['image/jpeg', 'image/png', 'image/webp'].includes(file.type)) {
        setEntries(current => [...current, {
          key: crypto.randomUUID(), file, preview: '', busy: false,
          error: 'Use a JPEG, PNG or WebP photo.', warning: '', name: '', brand: '', model: '',
          barcode: '', barcodeSymbology: '', serial: '', imei1: '', imei2: '', mrp: '', hsn: '',
          productId: '', saving: false, added: false
        }])
        continue
      }
      if (file.size <= 0 || file.size > 8 * 1024 * 1024) {
        setEntries(current => [...current, {
          key: crypto.randomUUID(), file, preview: '', busy: false,
          error: 'Photo must be 8 MB or smaller.', warning: '', name: '', brand: '', model: '',
          barcode: '', barcodeSymbology: '', serial: '', imei1: '', imei2: '', mrp: '', hsn: '',
          productId: '', saving: false, added: false
        }])
        continue
      }
      const preview = URL.createObjectURL(file)
      urls.current.add(preview)
      const draft: CaptureDraft = {
        key: crypto.randomUUID(), file, preview, busy: false, error: '',
        name: '', brand: '', model: '', barcode: '', barcodeSymbology: '', warning: '',
        serial: '', imei1: '', imei2: '', mrp: '', hsn: '', productId: '',
        saving: false, added: false
      }
      setEntries(current => [...current, draft])
      void sendPhoto(draft)
    }
  }

  async function removePhoto(item: CaptureDraft) {
    if (item.capture) {
      try { await api('DELETE', '/sales/item-tag-captures/' + item.capture.id) }
      catch (e) {
        setEntries(current => current.map(x => x.key === item.key
          ? { ...x, error: e instanceof Error ? e.message : 'Photo could not be removed.' } : x))
        return
      }
      onRemove(item.capture.id, [item.serial || item.imei1 || item.imei2].map(x => x.trim().toUpperCase()).filter(Boolean))
      loadedIds.current.delete(item.capture.id)
    }
    if (item.preview) {
      URL.revokeObjectURL(item.preview)
      urls.current.delete(item.preview)
    }
    setEntries(current => current.filter(x => x.key !== item.key))
  }

  async function createProduct(item: CaptureDraft) {
    if (!item.name.trim()) throw new Error('Enter the product name first.')
    const mrp = item.mrp.trim() ? Number(item.mrp) : null
    if (mrp !== null && (!Number.isFinite(mrp) || mrp < 0)) throw new Error('Enter a valid MRP.')
    const created = await api<{ id: string }>('POST', '/products', {
      name: item.name.trim(), categoryName: item.capture?.extraction?.category?.trim() || 'Uncategorised',
      isGeneric: !item.brand.trim() && !item.model.trim(), baseUnit: 'pcs',
      brand: item.brand.trim() || null, model: item.model.trim() || null,
      barcode: item.barcode.trim() || null, mrp, hsnCode: item.hsn.trim() || null
    })
    return created.id
  }

  async function addToBill(item: CaptureDraft) {
    if (!item.capture || item.busy || item.saving || item.added) return
    if (!item.name.trim()) { patch(item.key, { error: 'Enter the product name from the tag.' }); return }
    patch(item.key, { saving: true, error: '' })
    try {
      const productId = item.productId || await createProduct(item)
      if (!item.productId) patch(item.key, { productId })
      const reviewResult = await api<{ warnings?: string[] }>('PUT', '/sales/item-tag-captures/' + item.capture.id + '/review', {
        productId, productName: item.name.trim(), brand: item.brand.trim() || null,
        model: item.model.trim() || null, barcode: item.barcode.trim() || null,
        barcodeSymbology: item.barcodeSymbology.trim() || null,
        serial: item.serial.trim() || null, imei1: item.imei1.trim() || null,
        imei2: item.imei2.trim() || null,
        mrp: item.mrp.trim() ? Number(item.mrp) : null, hsnCode: item.hsn.trim() || null
      })
      const primaryIdentifier = (item.serial || item.imei1 || item.imei2).trim().toUpperCase()
      const accepted = onAdd({
        captureId: item.capture.id, productId, name: item.name.trim(),
        serials: primaryIdentifier ? [primaryIdentifier] : []
      })
      if (!accepted) throw new Error('Finish adding the item already being entered before selecting another tag photo.')
      patch(item.key, { saving: false, added: true, productId, warning: reviewResult?.warnings?.join(' ') ?? '' })
    } catch (e) {
      const message = e instanceof ApiError && e.status === 403
        ? 'Your role cannot create a product. Choose an existing product or ask an owner to add it.'
        : e instanceof Error ? e.message : 'The item could not be added. Review it and retry.'
      patch(item.key, { saving: false, error: message })
    }
  }

  const available = !disabled
  return (
    <div className="billing-card item-tag-card">
      <h3>Photograph item tag</h3>
      <p className="billing-hint">Take one clear photo for each physical item. RBOS reads the tag into a draft; check every field before adding it to the bill.</p>
      <div className="billing-row">
        <button type="button" disabled={!available} onClick={() => cameraRef.current?.click()}>Take photo</button>
        <button type="button" className="secondary" disabled={!available} onClick={() => galleryRef.current?.click()}>Choose photo(s)</button>
        <input ref={cameraRef} hidden type="file" accept="image/jpeg,image/png,image/webp" capture="environment"
          onChange={e => { addFiles(e.currentTarget.files); e.currentTarget.value = '' }} />
        <input ref={galleryRef} hidden type="file" accept="image/jpeg,image/png,image/webp" multiple
          onChange={e => { addFiles(e.currentTarget.files); e.currentTarget.value = '' }} />
      </div>
      {!available && <p className="billing-hint" role="status">Photo upload needs an internet connection. Existing reviewed photos can be kept with a held bill and submitted when online.</p>}
      {entries.map(item => (
        <section className="item-tag-review" key={item.key} aria-label="Review photographed item">
          {item.preview && <img src={item.preview} alt="Captured product tag preview" className="item-tag-preview" />}
          <div className="item-tag-status" role="status">
            {item.busy ? 'Uploading photo and reading tag…'
              : item.added ? 'Added to bill — photo evidence is linked to this item'
              : item.capture?.status === 'NeedsAttention' ? 'Photo saved. Automatic reading was unavailable; enter the details manually.'
              : item.capture ? 'Photo ready — review the extracted details'
              : item.error ? 'Needs attention'
              : 'Preparing photo'}
          </div>
          {item.error && <p className="billing-err" role="alert">{item.error}</p>}
          {item.warning && <p className="billing-hint" role="status">{item.warning}</p>}
          {!item.capture && !item.busy && item.error && item.preview && item.file &&
            <button type="button" className="secondary" disabled={!available} onClick={() => void sendPhoto(item)}>Retry upload</button>}
          {!item.capture && item.error &&
            <button type="button" className="secondary" onClick={() => void removePhoto(item)}>Remove photo</button>}
          {item.capture && !item.added && <>
            {item.capture.suggestedProduct && <p className="billing-hint">Possible match: {item.capture.suggestedProduct.name} ({item.capture.suggestedProduct.reason}). Confirm it below.</p>}
            <div className="billing-grid">
              <div className="billing-field billing-col-12">
                <label>Product in catalogue</label>
                <select value={item.productId} onChange={e => patch(item.key, { productId: e.target.value })}>
                  <option value="">Create a new product from these details</option>
                  {products.map(product => <option key={product.id} value={product.id}>{product.name}</option>)}
                </select>
              </div>
              <div className="billing-field billing-col-6"><label>Product name</label><input value={item.name} onChange={e => patch(item.key, { name: e.target.value })} /></div>
              <div className="billing-field billing-col-6"><label>Brand</label><input value={item.brand} onChange={e => patch(item.key, { brand: e.target.value })} /></div>
              <div className="billing-field billing-col-6"><label>Model</label><input value={item.model} onChange={e => patch(item.key, { model: e.target.value })} /></div>
              <div className="billing-field billing-col-6"><label>Barcode</label><input value={item.barcode} onChange={e => patch(item.key, { barcode: e.target.value })} /></div>
              <div className="billing-field billing-col-6"><label>Serial number</label><input value={item.serial} onChange={e => patch(item.key, { serial: e.target.value })} /></div>
              <div className="billing-field billing-col-6"><label>IMEI 1</label><input inputMode="numeric" value={item.imei1} onChange={e => patch(item.key, { imei1: e.target.value })} /></div>
              <div className="billing-field billing-col-6"><label>IMEI 2</label><input inputMode="numeric" value={item.imei2} onChange={e => patch(item.key, { imei2: e.target.value })} /></div>
              <div className="billing-field billing-col-6"><label>MRP (reference only)</label><input inputMode="decimal" value={item.mrp} onChange={e => patch(item.key, { mrp: e.target.value })} /></div>
              <div className="billing-field billing-col-6"><label>HSN</label><input value={item.hsn} onChange={e => patch(item.key, { hsn: e.target.value })} /></div>
            </div>
            {(item.capture.extraction?.warranty || item.capture.extraction?.specifications || item.capture.extraction?.otherText) && <details className="item-tag-details">
              <summary>More information read from the tag</summary>
              {item.capture.extraction.warranty && <p><strong>Warranty:</strong> {item.capture.extraction.warranty}</p>}
              {item.capture.extraction.specifications && <p><strong>Specifications:</strong> {item.capture.extraction.specifications}</p>}
              {item.capture.extraction.otherText && <p><strong>Other printed text:</strong> {item.capture.extraction.otherText}</p>}
            </details>}
            <div className="billing-row">
              <button type="button" disabled={item.saving || disabled} onClick={() => void addToBill(item)}>
                {item.saving ? 'Saving review…' : 'Confirm details & add item'}
              </button>
              <button type="button" className="secondary" disabled={item.saving} onClick={() => void removePhoto(item)}>Remove / retake</button>
            </div>
          </>}
          {item.added && <button type="button" className="secondary" onClick={() => void removePhoto(item)}>Remove from bill</button>}
        </section>
      ))}
    </div>
  )
}
