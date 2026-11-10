import { useEffect, useState } from 'react'
import { api } from './api'

type Source = { brand: string; category: string; sourceUrl: string; sourceType: string; availability: string; lastChecked: string; officialWebsiteVerified: boolean; catalogueDiscovered: boolean; downloadableFileAvailable: boolean; productPagesAvailable: boolean; manualUploadRequired: boolean; accessRestricted: boolean }

export function statusLabels(s: Source): string[] {
  const out: string[] = []
  if (s.officialWebsiteVerified) out.push('Official website verified')
  if (s.catalogueDiscovered) out.push('Product catalogue discovered')
  if (s.downloadableFileAvailable) out.push('Downloadable PDF/Excel/CSV available')
  if (s.productPagesAvailable) out.push('Product pages available')
  if (s.manualUploadRequired) out.push('Manual document upload required')
  if (s.accessRestricted) out.push('Access restricted/unavailable')
  return out
}

export function ManufacturerRegistry() {
  const [rows, setRows] = useState<Source[]>([])
  const [q, setQ] = useState('')
  const [error, setError] = useState('')
  useEffect(() => { api<Source[]>('GET', '/catalogue-sources').then(setRows).catch(e => setError(e instanceof Error ? e.message : 'Could not load manufacturers')) }, [])
  const shown = rows.filter(r => (r.brand + ' ' + r.category).toLowerCase().includes(q.trim().toLowerCase()))
  return (
    <details className="import-block">
      <summary>Manufacturer catalogue sources</summary>
      <p className="hint-line">Availability shows only what was actually verified. Download official files yourself and import them above.</p>
      <label>Search manufacturers <input value={q} onChange={e => setQ(e.target.value)} /></label>
      {error && <p role="alert" className="error">{error}</p>}
      <table>
        <thead><tr><th>Brand</th><th>Category</th><th>Source</th><th>Status</th><th>Last checked</th></tr></thead>
        <tbody>
          {shown.map(r => (
            <tr key={r.brand}><td>{r.brand}</td><td>{r.category}</td><td><a href={r.sourceUrl} target="_blank" rel="noopener noreferrer">{r.sourceUrl}</a></td><td><ul>{statusLabels(r).map(l => <li key={l}>{l}</li>)}</ul><small>{r.availability}</small></td><td>{r.lastChecked}</td></tr>
          ))}
        </tbody>
      </table>
    </details>
  )
}
