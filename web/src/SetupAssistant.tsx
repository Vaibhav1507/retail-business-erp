import './invoice-upload.css'
import { Paged } from './Paged'
import { useState } from 'react'
import { api } from './api'
import { prepare, toBase64 } from './InvoiceUpload'
import { FIELDS, MAX_ROWS, SAMPLE_CSV, autoMap, missingRequired, parseCsv, toImportRows, type FieldKey, type ImportRow, type Mapping } from './csv'

type RowResult = { row: number; status: 'ok' | 'duplicate' | 'duplicate_in_file' | 'invalid'; message: string | null }
type Preview = { rows: RowResult[]; ok: number; duplicate: number; invalid: number; newCategories: string[] }
type Commit = { created: number; skipped: number; replayed: boolean }
type FileRead = {
  rows: string[][]
  usedAi?: boolean
  pages?: number[] | null
  documentKind?: string | null
  contentHash?: string | null
  incomplete?: boolean
  warning?: string | null
}
type SourceForm = { documentName: string; sourceUrl: string; publishedOn: string; versionLabel: string; documentKind: string; contentHash: string }
const NO_SOURCE: SourceForm = { documentName: '', sourceUrl: '', publishedOn: '', versionLabel: '', documentKind: 'catalogue', contentHash: '' }

const msg = (e: unknown) => (e instanceof Error ? e.message : 'Something went wrong')
const label: Record<RowResult['status'], string> = { ok: 'Will be added', duplicate: 'Skipped: already exists', duplicate_in_file: 'Skipped: repeated in file', invalid: 'Skipped: invalid' }
const MAX_BYTES = 12 * 1024 * 1024

export function SetupAssistant({ reload }: { reload: () => void }) {
  const [step, setStep] = useState<1 | 2 | 3 | 4>(1)
  const [rows, setRows] = useState<string[][]>([])
  const [fromAi, setFromAi] = useState(false)
  const [mapping, setMapping] = useState<Mapping>({})
  const [data, setData] = useState<ImportRow[]>([])
  const [preview, setPreview] = useState<Preview | null>(null)
  const [result, setResult] = useState<Commit | null>(null)
  const [key, setKey] = useState(() => crypto.randomUUID())
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  const [pages, setPages] = useState<number[]>([])
  const [source, setSource] = useState<SourceForm>(NO_SOURCE)
  const sourceBody = source.documentName.trim() ? { ...source, brand: null } : undefined

  const headers = rows[0] ?? []

  function accept(parsed: string[][], ai: boolean) {
    setError('')
    if (parsed.length < 2) { setError('The file needs a header row and at least one product row.'); return }
    if (parsed.length - 1 > MAX_ROWS) { setError(`Too many rows (${parsed.length - 1}). Import at most ${MAX_ROWS} products at a time.`); return }
    setRows(parsed); setFromAi(ai); setMapping(autoMap(parsed[0])); setStep(2)
  }

  async function onFile(file: File | undefined) {
    if (!file) return
    setError(''); setBusy(true)
    try {
      const name = file.name.toLowerCase()
      if (name.endsWith('.csv') || file.type === 'text/csv') {
        if (file.size > 40_000) throw new Error('File is too large. Keep it under 40 KB (about 200 products).')
        accept(parseCsv(await file.text()), false)
      } else if (name.endsWith('.xlsx') || name.endsWith('.docx') || name.endsWith('.pdf')) {
        if (file.size > MAX_BYTES) throw new Error('File is larger than 12 MB. Split or compress it and try again.')
        const r = await api<FileRead>('POST', '/products/import/read', { fileName: file.name, contentType: file.type, data: await toBase64(file) })
        accept(r.rows, r.usedAi ?? false)
        if (r.warning) setError(r.warning)
        if (name.endsWith('.pdf')) {
          setPages(r.pages ?? [])
          setSource({ ...NO_SOURCE, documentName: file.name, documentKind: r.documentKind ?? 'catalogue', contentHash: r.contentHash ?? '' })
        }
      } else if (name.endsWith('.xls') || name.endsWith('.doc')) {
        throw new Error('Old .xls / .doc files are not supported. Open the file, choose Save As .xlsx / .docx (or CSV), then upload again.')
      } else if (file.type.startsWith('image/')) {
        const img = await prepare(file)
        const r = await api<FileRead>('POST', '/products/import/read', { fileName: file.name, contentType: img.contentType, data: img.data })
        accept(r.rows, r.usedAi ?? false)
      } else {
        throw new Error('Choose an Excel (.xlsx), Word (.docx), CSV or PDF file, or a photo.')
      }
    } catch (e) { setError(msg(e)) } finally { setBusy(false) }
  }

  function setCell(r: number, c: number, v: string) {
    setRows(rows.map((row, i) => (i !== r ? row : headers.map((_, ci) => (ci === c ? v : (row[ci] ?? ''))))))
  }

  async function runPreview() {
    setError(''); setBusy(true)
    try {
      const d = toImportRows(rows.slice(1), mapping, headers).map((x, i) => (pages.length > i + 1 ? { ...x, sourcePage: pages[i + 1] } : x))
      setData(d)
      setPreview(await api<Preview>('POST', '/products/import/preview', { rows: d, source: sourceBody }))
      setStep(3)
    } catch (e) { setError(msg(e)) } finally { setBusy(false) }
  }

  async function commit() {
    setError(''); setBusy(true)
    try {
      setResult(await api<Commit>('POST', '/products/import/commit', { rows: data, source: sourceBody }, key))
      setStep(4); reload()
    } catch (e) { setError(msg(e)) } finally { setBusy(false) }
  }

  function restart() { setRows([]); setPages([]); setSource(NO_SOURCE); setFromAi(false); setMapping({}); setData([]); setPreview(null); setResult(null); setKey(crypto.randomUUID()); setError(''); setStep(1) }

  const missing = missingRequired(mapping)

  return (
    <section aria-label="Stock setup assistant">
      <h3>Add many products at once</h3>

      {step === 1 && (
        <div role="region" aria-label="Upload product list" className="invoice-upload">
          <label className="invoice-upload-pick">
            Upload a product list (optional). Excel, Word, CSV or text PDF catalogues are read directly. Scanned PDFs can be processed using server-side OCR when available. A photo of a list is read with AI.
            <input type="file" accept="*/*" disabled={busy} onChange={e => { void onFile(e.target.files?.[0]); e.target.value = '' }} />
          </label>
          {busy && <p role="status">Reading file...</p>}
          <p>or <button type="button" className="secondary" onClick={() => accept(parseCsv(SAMPLE_CSV), false)}>Try with sample data</button></p>
        </div>
      )}

      {step === 2 && (
        <div className="invoice-upload">
          {fromAi ? (
            <>
              <p className="invoice-upload-meta">We identified these products from your document using AI. Check the model, brand, category and specifications, correct anything needed, then preview the import.</p>
              <div className="table-scroll">
                <table>
                  <thead><tr>{headers.map((h, i) => <th key={i}>{h}</th>)}</tr></thead>
                  <tbody>
                    {rows.slice(1).map((r, ri) => (
                      <tr key={ri}>{headers.map((h, ci) => <td key={ci}><input aria-label={`${h} row ${ri + 1}`} value={r[ci] ?? ''} onChange={e => setCell(ri + 1, ci, e.target.value)} /></td>)}</tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </>
          ) : (
            <>
              <p>Tell us which column holds each field. Name and Category are required. A product with no brand and no model is treated as generic.</p>
              <table>
                <tbody>
                  {FIELDS.map(f => (
                    <tr key={f.key}>
                      <th scope="row"><label htmlFor={`map-${f.key}`}>{f.label}{f.required ? ' *' : ''}</label></th>
                      <td>
                        <select id={`map-${f.key}`} value={mapping[f.key as FieldKey] ?? ''} onChange={e => setMapping({ ...mapping, [f.key]: e.target.value === '' ? undefined : Number(e.target.value) })}>
                          <option value="">(not in file)</option>
                          {headers.map((h, i) => <option key={i} value={i}>{h || `Column ${i + 1}`}</option>)}
                        </select>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
              <p>{rows.length - 1} rows found. First row: {rows[1]?.join(' | ')}</p>
            </>
          )}
          {missing.length > 0 && <p role="alert">Map required field(s): {missing.join(', ')}</p>}
          <div className="invoice-upload-actions">
            <button type="button" className="secondary" onClick={restart}>Back</button>
            <button type="button" disabled={missing.length > 0 || busy} onClick={runPreview}>Preview import</button>
          </div>
        </div>
      )}

      {step === 3 && preview && (
        <div className="invoice-upload">
          <p role="status">{preview.ok} will be added - {preview.duplicate} duplicates skipped - {preview.invalid} invalid skipped{preview.newCategories.length > 0 ? ` - new categories: ${preview.newCategories.join(', ')}` : ''}</p>
          <Paged rows={preview.rows}>{page => (
            <div className="table-scroll">
              <table>
                <thead><tr><th>Row</th><th>Name</th><th>Category</th><th>Unit</th><th>Brand / Model</th><th>Specifications</th><th>Result</th></tr></thead>
                <tbody>
                  {page.map(r => {
                    const d = data[r.row - 1]
                    return (
                      <tr key={r.row}>
                        <td>{r.row}</td><td>{d.name}</td><td>{d.categoryName}</td><td>{d.baseUnit}</td><td>{d.isGeneric ? 'Generic' : `${d.brand ?? ''} ${d.model ?? ''}`}</td>
                        <td>{Object.entries({ ...(d.attributes ?? {}), ...(d.specs ?? {}) }).map(([k, v]) => `${k.replace(/_/g, ' ')}: ${v}`).join(' · ') || 'No specifications extracted'}</td>
                        <td>{label[r.status]}{r.message && r.status === 'invalid' ? `: ${r.message}` : ''}</td>
                      </tr>
                    )
                  })}
                </tbody>
              </table>
            </div>
          )}</Paged>
          <div className="invoice-upload-actions">
            <button type="button" className="secondary" onClick={() => setStep(2)}>Back</button>
            <button type="button" disabled={preview.ok === 0 || busy} onClick={commit}>Import {preview.ok} products</button>
          </div>
        </div>
      )}

      {step === 4 && result && (
        <div className="invoice-upload">
          <p role="status">{result.replayed ? 'This import was already completed. ' : ''}Imported {result.created} products. Skipped {result.skipped}.</p>
          <div className="invoice-upload-actions"><button type="button" onClick={restart}>Import another file</button></div>
        </div>
      )}

      {error && <p role="alert">{error}</p>}
    </section>
  )
}
