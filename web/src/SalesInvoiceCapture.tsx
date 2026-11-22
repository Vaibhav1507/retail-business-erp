import { useRef, useState, type ChangeEvent } from 'react'
import { apiBinary } from './api'

export type ExtractedSalesInvoiceLine = {
  description: string | null
  brand: string | null
  model: string | null
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
  const [showChoices, setShowChoices] = useState(false)
  const [busy, setBusy] = useState(false)
  const [status, setStatus] = useState('')
  const [error, setError] = useState('')
  const cameraRef = useRef<HTMLInputElement>(null)
  const galleryRef = useRef<HTMLInputElement>(null)

  async function readFile(file: File | undefined) {
    setShowChoices(false)
    setError('')
    setStatus('')
    if (!file) return
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

  function handleFileChange(event: ChangeEvent<HTMLInputElement>) {
    const file = event.currentTarget.files?.[0]
    event.currentTarget.value = ''
    void readFile(file)
  }

  return (
    <div className="item-tag-uploader sales-invoice-uploader">
      <button type="button" className="secondary" disabled={disabled || busy}
        aria-expanded={showChoices} onClick={() => setShowChoices(value => !value)}>
        Upload handwritten bill
      </button>
      {showChoices && <div className="item-tag-source-options">
        <button type="button" className="secondary" disabled={disabled || busy}
          onClick={() => cameraRef.current?.click()}>Take photo</button>
        <button type="button" className="secondary" disabled={disabled || busy}
          onClick={() => galleryRef.current?.click()}>Choose from device</button>
      </div>}
      <input ref={cameraRef} hidden type="file" accept="image/jpeg,image/png,image/webp,application/pdf"
        capture="environment" onChange={handleFileChange} />
      <input ref={galleryRef} hidden type="file" accept="image/jpeg,image/png,image/webp,application/pdf"
        onChange={handleFileChange} />
      {busy && <span className="item-tag-feedback" role="status">{status}</span>}
      {!busy && status && <span className="item-tag-feedback" role="status">{status}</span>}
      {error && <span className="item-tag-error" role="alert">{error}</span>}
    </div>
  )
}
