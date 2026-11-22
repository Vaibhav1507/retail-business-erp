import { useState } from 'react'
import { apiBinary } from './api'
import { BrowserMultiFormatReader } from '@zxing/browser'
import { BarcodeFormat, DecodeHintType } from '@zxing/library'
import { PhotoSourcePicker } from './PhotoSourcePicker'

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
  const [busy, setBusy] = useState(false)
  const [status, setStatus] = useState('')
  const [error, setError] = useState('')
  const [warning, setWarning] = useState('')

  async function readFile(file: File) {
    setError('')
    setWarning('')
    setStatus('')
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
      // Keep the existing local barcode fast path and upload the original file bytes.
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
      const matchedProductName = scan.suggestedProduct?.name?.trim() ?? ''
      const productName = matchedProductName || extraction.productName?.trim() || ''
      const hasProductIdentity = Boolean(
        productName ||
        extraction.brand?.trim() ||
        extraction.model?.trim() ||
        extraction.serial?.trim() ||
        extraction.imei1?.trim() ||
        extraction.imei2?.trim() ||
        scan.suggestedProduct?.id
      )
      const hasBarcode = Boolean(extraction.barcode?.trim() || decodedBarcode?.code)

      onExtract({
        productId: scan.suggestedProduct?.id ?? '',
        // A verified catalogue match is more reliable than an empty or guessed model
        // product name, and must be shown in the shared invoice editor.
        name: productName,
        brand: extraction.brand ?? '',
        model: extraction.model ?? '',
        category: extraction.category ?? '',
        // Prefer the server-returned value because it has passed server validation.
        barcode: extraction.barcode ?? decodedBarcode?.code ?? '',
        barcodeSymbology: extraction.barcodeSymbology ?? decodedBarcode?.symbology ?? '',
        serials: extraction.serial ? [extraction.serial] : [],
        imei1: extraction.imei1 ?? '',
        imei2: extraction.imei2 ?? '',
        mrp: extraction.mrp ?? null,
        hsn: extraction.hsn ?? ''
      })
      setWarning((scan.warnings ?? []).join(' '))

      if (scan.status === 'NeedsAttention' || !hasProductIdentity) {
        setStatus(hasBarcode
          ? 'Barcode detected, but product details could not be read. Review or enter the product name, brand or model.'
          : 'Tag read partially. Review or complete the product fields below.')
      } else if (matchedProductName) {
        setStatus(`Catalogue match: ${matchedProductName}. Review the fields and add to invoice.`)
      } else {
        setStatus('Product details detected. Review the fields and add to invoice.')
      }
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Could not read this tag. Try another photo or enter the details manually.')
      setStatus('')
    } finally {
      if (imageUrl) URL.revokeObjectURL(imageUrl)
      setBusy(false)
    }
  }

  return (
    <div className="item-tag-uploader">
      <PhotoSourcePicker
        triggerLabel="Add tag photo"
        title="Add product tag photo"
        description="Take a photo of the product label or choose an existing image."
        disabled={disabled || busy}
        cameraAccept="image/jpeg,image/png,image/webp"
        galleryAccept="image/jpeg,image/png,image/webp"
        onFileSelected={file => { void readFile(file) }}
      />
      {busy && <span className="item-tag-feedback" role="status">Reading image and extracting product details…</span>}
      {!busy && status && <span className="item-tag-feedback" role="status">{status}</span>}
      {warning && <span className="item-tag-warning" role="status">{warning}</span>}
      {error && <span className="item-tag-error" role="alert">{error}</span>}
    </div>
  )
}
