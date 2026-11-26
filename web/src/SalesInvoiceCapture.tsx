import { useState } from 'react'
import { apiBinary } from './api'
import { PhotoSourcePicker } from './PhotoSourcePicker'

export type ExtractedSalesInvoiceLine = {
  description: string | null
  brand: string | null
  model: string | null
  serialNumbers: string[]
  hsn: string | null
  quantity: number | null
  unitPrice: number | null
  discount: number | null
  amount: number | null
  gstRate: number | null
  gstAmount: number | null
  confidence: number | null
}

export type ExtractedSalesInvoice = {
  invoiceNumber: string | null
  date: string | null
  customerName: string | null
  customerPhone: string | null
  subtotal: number | null
  taxAmount: number | null
  total: number | null
  lines: ExtractedSalesInvoiceLine[]
}

type CaptureResponse = {
  status: string
  extraction: ExtractedSalesInvoice
  warnings: string[]
}

export function SalesInvoiceCapture({
  disabled, onExtract
}: {
  disabled: boolean
  onExtract: (draft: ExtractedSalesInvoice, warnings: string[]) => void
}) {
  const [busy, setBusy] = useState(false)
  const [status, setStatus] = useState('')
  const [error, setError] = useState('')

  async function readFile(file: File) {
    setError('')
    setStatus('')
    if (!['image/jpeg', 'image/png', 'image/webp', 'application/pdf'].includes(file.type)) {
      setError('Use a JPEG, PNG, WebP photo or PDF.')
      return
    }
    if (file.size <= 0 || file.size > 8 * 1024 * 1024) {
      setError('Invoice file must be 8 MB or smaller.')
      return
    }
    if (disabled) {
      setError('Invoice extraction needs an internet connection.')
      return
    }

    setBusy(true)
    setStatus('Reading handwritten invoice…')
    try {
      const result = await apiBinary<CaptureResponse>('/sales/invoice-image-extract', file)
      onExtract(result.extraction, result.warnings ?? [])
      setStatus(result.status === 'NeedsAttention'
        ? 'Some details could not be read. Review the extracted fields and enter anything missing.'
        : 'Invoice details extracted. Review every field before adding the lines to your bill.')
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Could not read this invoice. Try a clearer photo or enter the details manually.')
      setStatus('')
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="item-tag-uploader sales-invoice-uploader invoice-feedback-flow">
      <PhotoSourcePicker
        triggerLabel="Import bill ↗"
        title="Add handwritten invoice"
        description="Take a photo of the bill or choose an image or PDF from your device."
        disabled={disabled || busy}
        cameraAccept="image/jpeg,image/png,image/webp"
        galleryAccept="image/jpeg,image/png,image/webp,application/pdf"
        onFileSelected={file => { void readFile(file) }}
      />
      {busy && <span className="item-tag-feedback" role="status">{status}</span>}
      {!busy && status && <span className="item-tag-feedback" role="status">{status}</span>}
      {error && <span className="item-tag-error" role="alert">{error}</span>}
    </div>
  )
}
