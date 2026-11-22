import { useRef, useState, type ChangeEvent } from 'react'
import { apiBinary } from './api'
import { BrowserMultiFormatReader } from '@zxing/browser'
import { BarcodeFormat, DecodeHintType } from '@zxing/library'

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

export function ItemTagCapture({
  disabled, onExtract
}: {
  disabled: boolean
  onExtract: (item: CapturedInvoiceItem) => void
}) {
  const [showChoices, setShowChoices] = useState(false)
  const [busy, setBusy] = useState(false)
  const [status, setStatus] = useState('')
  const [error, setError] = useState('')
  const [warning, setWarning] = useState('')
  const cameraRef = useRef<HTMLInputElement>(null)
  const galleryRef = useRef<HTMLInputElement>(null)

  async function readFile(file: File | undefined) {
    setShowChoices(false)
    setError('')
    setWarning('')
    setStatus('')
    if (!file) return
    if (!['image/jpeg', 'image/png', 'image/webp'].includes(file.type)) {
      setError('Use a JPEG, PNG or WebP photo.')
      return
    }
    if (file.size <= 0 || file.size > 8 * 1024 * 1024) {
      setError('Photo must be 8 MB or smaller.')
      return
    }
    if (disabled) {
      setError('Tag scanning needs an internet connection.')
      return
    }

    setBusy(true)
    setStatus('Reading product tag…')
    let imageUrl: string | null = null
    try {
      let decodedBarcode: { code: string; symbology: string } | null = null
      imageUrl = URL.createObjectURL(file)
      // Decode the original photo on-device. If no supported barcode is found,
      // the server-side vision extraction remains available.
      try {
        const hints = new Map()
        hints.set(DecodeHintType.POSSIBLE_FORMATS, [
          BarcodeFormat.EAN_13, BarcodeFormat.EAN_8, BarcodeFormat.UPC_A,
          BarcodeFormat.UPC_E, BarcodeFormat.CODE_128
        ])
        hints.set(DecodeHintType.TRY_HARDER, true)
        const reader = new BrowserMultiFormatReader(hints)
        const decoded = await reader.decodeFromImageUrl(imageUrl)
        const format = decoded.getBarcodeFormat()
        const symbology = format === BarcodeFormat.EAN_13 ? 'EAN_13'
          : format === BarcodeFormat.EAN_8 ? 'EAN_8'
          : format === BarcodeFormat.UPC_A ? 'UPC_A'
          : format === BarcodeFormat.UPC_E ? 'UPC_E'
          : format === BarcodeFormat.CODE_128 ? 'CODE_128'
          : null
        if (symbology) decodedBarcode = { code: decoded.getText(), symbology }
      } catch {
        // Vision extraction remains available for difficult or damaged barcodes.
      }

      const scan = await apiBinary<ScanResponse>(
        '/sales/item-tag-scan', file, undefined, crypto.randomUUID(),
        decodedBarcode ? {
          'X-RBOS-Detected-Barcode': decodedBarcode.code,
          'X-RBOS-Detected-Symbology': decodedBarcode.symbology
        } : undefined
      )
      const extraction = scan.extraction ?? {}
      onExtract({
        productId: scan.suggestedProduct?.id ?? '',
        name: extraction.productName ?? '',
        brand: extraction.brand ?? '',
        model: extraction.model ?? '',
        category: extraction.category ?? '',
        barcode: decodedBarcode?.code ?? extraction.barcode ?? '',
        barcodeSymbology: decodedBarcode?.symbology ?? extraction.barcodeSymbology ?? '',
        serials: extraction.serial ? [extraction.serial] : [],
        imei1: extraction.imei1 ?? '',
        imei2: extraction.imei2 ?? '',
        mrp: extraction.mrp ?? null,
        hsn: extraction.hsn ?? ''
      })
      setWarning((scan.warnings ?? []).join(' '))
      setStatus(scan.status === 'NeedsAttention'
        ? 'Tag read partially. Review or complete the product fields below.'
        : 'Tag details filled in. Review the product fields and add to invoice.')
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Could not read this tag. Try another photo or enter the details manually.')
      setStatus('')
    } finally {
      if (imageUrl) URL.revokeObjectURL(imageUrl)
      setBusy(false)
    }
  }

  function handleFileChange(event: ChangeEvent<HTMLInputElement>) {
    const file = event.currentTarget.files?.[0]
    event.currentTarget.value = ''
    void readFile(file)
  }

  return (
    <div className="item-tag-uploader">
      <button type="button" className="secondary" disabled={disabled || busy}
        aria-expanded={showChoices} onClick={() => setShowChoices(value => !value)}>
        Add tag photo
      </button>
      {showChoices && <div className="item-tag-source-options">
        <button type="button" className="secondary" disabled={disabled || busy}
          onClick={() => cameraRef.current?.click()}>Take photo</button>
        <button type="button" className="secondary" disabled={disabled || busy}
          onClick={() => galleryRef.current?.click()}>Choose from device</button>
      </div>}
      <input ref={cameraRef} hidden type="file" accept="image/jpeg,image/png,image/webp"
        capture="environment" onChange={handleFileChange} />
      <input ref={galleryRef} hidden type="file" accept="image/jpeg,image/png,image/webp"
        onChange={handleFileChange} />
      {busy && <span className="item-tag-feedback" role="status">Reading image and extracting product details…</span>}
      {!busy && status && <span className="item-tag-feedback" role="status">{status}</span>}
      {warning && <span className="item-tag-warning" role="status">{warning}</span>}
      {error && <span className="item-tag-error" role="alert">{error}</span>}
    </div>
  )
}
