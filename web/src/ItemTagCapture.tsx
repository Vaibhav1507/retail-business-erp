import { useEffect, useRef, useState } from 'react'
import { apiBinary } from './api'
import { BrowserMultiFormatReader } from '@zxing/browser'
import { BarcodeFormat } from '@zxing/library'

let activeUploads = 0
const waitingUploads: Array<() => void> = []

async function withUploadSlot<T>(work: () => Promise<T>): Promise<T> {
  if (activeUploads >= 2) await new Promise<void>(resolve => waitingUploads.push(resolve))
  else activeUploads++
  try {
    return await work()
  } finally {
    const next = waitingUploads.shift()
    if (next) next()
    else activeUploads--
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
type ScanResponse = {
  status: string
  extraction: Extraction
  suggestedProduct?: { id: string; name: string; reason?: string | null } | null
  warnings?: string[]
}
export type CapturedInvoiceItem = {
  productId: string
  name: string
  brand: string
  model: string
  category: string
  barcode: string
  barcodeSymbology: string
  serials: string[]
  imei1: string
  imei2: string
  mrp: number | null
  hsn: string
}

type ScanDraft = {
  key: string
  file: File
  preview: string
  busy: boolean
  error: string
  warning: string
  scan?: ScanResponse
  name: string
  brand: string
  model: string
  category: string
  barcode: string
  barcodeSymbology: string
  serial: string
  imei1: string
  imei2: string
  mrp: string
  hsn: string
  productId: string
}

export function ItemTagCapture({
  products, disabled, onAdd
}: {
  products: ProductOption[]
  disabled: boolean
  onAdd: (item: CapturedInvoiceItem) => boolean
}) {
  const [entries, setEntries] = useState<ScanDraft[]>([])
  const cameraRef = useRef<HTMLInputElement>(null)
  const galleryRef = useRef<HTMLInputElement>(null)
  const urls = useRef(new Set<string>())

  useEffect(() => () => {
    for (const url of urls.current) URL.revokeObjectURL(url)
    urls.current.clear()
  }, [])

  function patch(key: string, values: Partial<ScanDraft>) {
    setEntries(current => current.map(item => item.key === key ? { ...item, ...values } : item))
  }

  async function sendPhoto(draft: ScanDraft) {
    patch(draft.key, { busy: true, error: '', warning: '' })
    try {
      const result = await withUploadSlot(async () => {
        let decodedBarcode: { code: string; symbology: string } | null = null
        // Decode the original photo on-device before upload. This avoids OCR/AI digit
        // transcription for the most important identifier and saves model latency.
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
          if (symbology) decodedBarcode = { code: decoded.getText(), symbology }
        } catch { /* Vision extraction remains available for difficult or damaged barcodes. */ }

        const scan = await apiBinary<ScanResponse>(
          '/sales/item-tag-scan', draft.file, undefined, draft.key,
          decodedBarcode ? {
            'X-RBOS-Detected-Barcode': decodedBarcode.code,
            'X-RBOS-Detected-Symbology': decodedBarcode.symbology
          } : undefined)
        return { scan, decodedBarcode }
      })

      patch(draft.key, {
        busy: false,
        scan: result.scan,
        warning: [...(result.scan.warnings ?? []), ...(result.scan.status === 'NeedsAttention'
          ? ['Some tag details could not be read. Complete the fields manually before adding the invoice row.'] : [])].join(' '),
        name: result.scan.extraction?.productName ?? '',
        brand: result.scan.extraction?.brand ?? '',
        category: result.scan.extraction?.category ?? '',
        model: result.scan.extraction?.model ?? '',
        barcode: result.decodedBarcode?.code ?? result.scan.extraction?.barcode ?? '',
        barcodeSymbology: result.decodedBarcode?.symbology ?? result.scan.extraction?.barcodeSymbology ?? '',
        serial: result.scan.extraction?.serial ?? '',
        imei1: result.scan.extraction?.imei1 ?? '',
        imei2: result.scan.extraction?.imei2 ?? '',
        mrp: result.scan.extraction?.mrp == null ? '' : String(result.scan.extraction.mrp),
        hsn: result.scan.extraction?.hsn ?? '',
        productId: result.scan.suggestedProduct?.id ?? ''
      })
    } catch (e) {
      patch(draft.key, {
        busy: false,
        error: e instanceof Error ? e.message : 'Could not read this tag. Try another photo or enter the details manually.'
      })
    }
  }

  function addFiles(files: FileList | null) {
    if (!files) return
    for (const file of Array.from(files)) {
      if (!['image/jpeg', 'image/png', 'image/webp'].includes(file.type)) {
        setEntries(current => [...current, {
          key: crypto.randomUUID(), file, preview: '', busy: false,
          error: 'Use a JPEG, PNG or WebP photo.', warning: '',
          name: '', brand: '', model: '', category: '', barcode: '', barcodeSymbology: '',
          serial: '', imei1: '', imei2: '', mrp: '', hsn: '', productId: ''
        }])
        continue
      }
      if (file.size <= 0 || file.size > 8 * 1024 * 1024) {
        setEntries(current => [...current, {
          key: crypto.randomUUID(), file, preview: '', busy: false,
          error: 'Photo must be 8 MB or smaller.', warning: '',
          name: '', brand: '', model: '', category: '', barcode: '', barcodeSymbology: '',
          serial: '', imei1: '', imei2: '', mrp: '', hsn: '', productId: ''
        }])
        continue
      }
      const preview = URL.createObjectURL(file)
      urls.current.add(preview)
      const draft: ScanDraft = {
        key: crypto.randomUUID(), file, preview, busy: false, error: '', warning: '',
        name: '', brand: '', model: '', category: '', barcode: '', barcodeSymbology: '',
        serial: '', imei1: '', imei2: '', mrp: '', hsn: '', productId: ''
      }
      setEntries(current => [...current, draft])
      void sendPhoto(draft)
    }
  }

  function removePhoto(item: ScanDraft) {
    if (item.preview) {
      URL.revokeObjectURL(item.preview)
      urls.current.delete(item.preview)
    }
    setEntries(current => current.filter(x => x.key !== item.key))
  }

  function addToInvoice(item: ScanDraft) {
    if (item.busy) return
    if (!item.name.trim()) {
      patch(item.key, { error: 'Enter the product name from the tag.' })
      return
    }
    const mrp = item.mrp.trim() ? Number(item.mrp) : null
    if (mrp !== null && (!Number.isFinite(mrp) || mrp < 0)) {
      patch(item.key, { error: 'Enter a valid MRP.' })
      return
    }
    const identifiers = [item.serial, item.imei1, item.imei2]
      .map(value => value.trim().toUpperCase()).filter(Boolean)
    const accepted = onAdd({
      productId: item.productId,
      name: item.name.trim(),
      brand: item.brand.trim(),
      model: item.model.trim(),
      category: item.category.trim(),
      barcode: item.barcode.trim(),
      barcodeSymbology: item.barcodeSymbology.trim(),
      serials: [...new Set(identifiers)],
      imei1: item.imei1.trim(),
      imei2: item.imei2.trim(),
      mrp,
      hsn: item.hsn.trim()
    })
    if (!accepted) {
      patch(item.key, { error: 'Finish the current invoice product row before adding another scanned item.' })
      return
    }
    removePhoto(item)
  }

  const available = !disabled
  return (
    <div className="billing-card item-tag-card">
      <h3>Scan product tag</h3>
      <p className="billing-hint">Take a picture of the label on the product box. RBOS reads the barcode, model and serial number and fills an invoice row. Verify the details before adding it.</p>
      <div className="billing-row">
        <button type="button" disabled={!available} onClick={() => cameraRef.current?.click()}>Take photo</button>
        <button type="button" className="secondary" disabled={!available} onClick={() => galleryRef.current?.click()}>Choose photo(s)</button>
        <input ref={cameraRef} hidden type="file" accept="image/jpeg,image/png,image/webp" capture="environment"
          onChange={e => { addFiles(e.currentTarget.files); e.currentTarget.value = '' }} />
        <input ref={galleryRef} hidden type="file" accept="image/jpeg,image/png,image/webp" multiple
          onChange={e => { addFiles(e.currentTarget.files); e.currentTarget.value = '' }} />
      </div>
      {!available && <p className="billing-hint" role="status">Tag scanning needs an internet connection. You can enter the invoice row manually while offline.</p>}
      {entries.map(item => (
        <section className="item-tag-review" key={item.key} aria-label="Review scanned product tag">
          {item.preview && <img src={item.preview} alt="Captured product tag preview" className="item-tag-preview" />}
          <div className="item-tag-status" role="status">
            {item.busy ? 'Reading product tag…'
              : item.error && !item.scan ? 'Could not read tag'
              : item.scan?.status === 'NeedsAttention' ? 'Tag read partially — verify or complete the fields'
              : item.scan ? 'Tag read — verify the fields before adding the invoice row'
              : 'Preparing photo'}
          </div>
          {item.error && <p className="billing-err" role="alert">{item.error}</p>}
          {item.warning && <p className="billing-hint" role="status">{item.warning}</p>}
          {!item.scan && !item.busy && item.error && item.preview &&
            <button type="button" className="secondary" disabled={!available} onClick={() => void sendPhoto(item)}>Retry scan</button>}
          {item.scan && <>
            {item.scan.suggestedProduct && <p className="billing-hint">
              Barcode match: <strong>{item.scan.suggestedProduct.name}</strong>
              {item.scan.suggestedProduct.reason ? ' (' + item.scan.suggestedProduct.reason + ')' : ''}
            </p>}
            <div className="billing-grid">
              <div className="billing-field billing-col-12">
                <label>Match to catalogue product</label>
                <select value={item.productId} onChange={e => patch(item.key, { productId: e.target.value })}>
                  <option value="">Create product from tag details when invoice is confirmed</option>
                  {products.map(product => <option key={product.id} value={product.id}>{product.name}</option>)}
                </select>
              </div>
              <div className="billing-field billing-col-12"><label>Product name</label><input value={item.name} onChange={e => patch(item.key, { name: e.target.value })} /></div>
              <div className="billing-field billing-col-6"><label>Brand</label><input value={item.brand} onChange={e => patch(item.key, { brand: e.target.value })} /></div>
              <div className="billing-field billing-col-6"><label>Model</label><input value={item.model} onChange={e => patch(item.key, { model: e.target.value })} /></div>
              <div className="billing-field billing-col-6"><label>Barcode</label><input value={item.barcode} onChange={e => patch(item.key, { barcode: e.target.value })} /></div>
              <div className="billing-field billing-col-6"><label>Serial number</label><input value={item.serial} onChange={e => patch(item.key, { serial: e.target.value })} /></div>
              <div className="billing-field billing-col-6"><label>IMEI 1</label><input inputMode="numeric" value={item.imei1} onChange={e => patch(item.key, { imei1: e.target.value })} /></div>
              <div className="billing-field billing-col-6"><label>IMEI 2</label><input inputMode="numeric" value={item.imei2} onChange={e => patch(item.key, { imei2: e.target.value })} /></div>
              <div className="billing-field billing-col-6"><label>MRP (reference only)</label><input inputMode="decimal" value={item.mrp} onChange={e => patch(item.key, { mrp: e.target.value })} /></div>
              <div className="billing-field billing-col-6"><label>HSN</label><input value={item.hsn} onChange={e => patch(item.key, { hsn: e.target.value })} /></div>
            </div>
            {(item.scan.extraction?.warranty || item.scan.extraction?.specifications || item.scan.extraction?.otherText) && <details className="item-tag-details">
              <summary>More information read from the tag</summary>
              {item.scan.extraction.warranty && <p><strong>Warranty:</strong> {item.scan.extraction.warranty}</p>}
              {item.scan.extraction.specifications && <p><strong>Specifications:</strong> {item.scan.extraction.specifications}</p>}
              {item.scan.extraction.otherText && <p><strong>Other printed text:</strong> {item.scan.extraction.otherText}</p>}
            </details>}
            <div className="billing-row">
              <button type="button" disabled={item.busy || disabled} onClick={() => addToInvoice(item)}>Use details in invoice</button>
              <button type="button" className="secondary" onClick={() => removePhoto(item)}>Discard / retake</button>
            </div>
          </>}
        </section>
      ))}
    </div>
  )
}
