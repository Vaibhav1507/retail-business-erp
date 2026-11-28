import { useState } from 'react'
import { apiBinary } from './api'
import { PhotoSourcePicker } from './PhotoSourcePicker'

export type SalesInvoiceReviewIssue = {
  field: string
  lineIndex: number | null
  message: string
  severity: 'warning' | 'info'
}

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
  fieldConfidence?: Record<string, number | null>
  fieldStates?: Record<string, SalesInvoiceFieldState>
  identifierCandidates?: SalesInvoiceIdentifierCandidate[]
}

export type SalesInvoiceFieldState = 'source_read' | 'not_present' | 'unreadable' | 'ambiguous' | 'calculated' | 'user_entered' | 'user_corrected'
export type SalesInvoiceIdentifierCandidate = {
  value: string
  classification: 'model' | 'serial' | 'imei' | 'barcode' | 'part_number' | 'unknown'
  confirmed?: boolean
  luhnValid?: boolean | null
}

export type SalesInvoiceOcrEvidence = {
  status: string
  haikuOrPrimaryReading: string | null
  ocrCandidate: string | null
  ocrAgrees: boolean | null
  pageNumber: number | null
  region?: { left: number; top: number; width: number; height: number } | null
  verificationCandidate?: string | null
  verificationStatus?: string | null
}

export type ExtractedSalesInvoice = {
  invoiceNumber: string | null
  date: string | null
  customerName: string | null
  customerPhone: string | null
  subtotal: number | null
  taxAmount: number | null
  total: number | null
  fieldConfidence?: Record<string, number | null>
  fieldStates?: Record<string, SalesInvoiceFieldState>
  ocrEvidence?: Record<string, SalesInvoiceOcrEvidence>
  lines: ExtractedSalesInvoiceLine[]
}

type CaptureResponse = {
  status: string
  sourceDocumentId: string
  extraction: ExtractedSalesInvoice
  warnings: string[]
  reviewIssues?: SalesInvoiceReviewIssue[]
}

export function SalesInvoiceCapture({
  disabled, onExtract
}: {
  disabled: boolean
  onExtract: (draft: ExtractedSalesInvoice, warnings: string[], reviewIssues: SalesInvoiceReviewIssue[], sourceDocumentId: string, sourceMime: string) => void
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
      const result = await apiBinary<CaptureResponse>(
        '/sales/invoice-image-extract', file, undefined, undefined,
        { 'X-Source-File-Name': file.name })
      if (!result.sourceDocumentId) throw new Error('The original bill was not retained. Please retry the upload.')
      onExtract(result.extraction, result.warnings ?? [], result.reviewIssues ?? [], result.sourceDocumentId, file.type)
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
