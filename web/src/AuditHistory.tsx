import { useEffect, useState } from 'react'
import { api } from './api'
import { t as tr } from './i18n'

type Row = { at: string; action: string; entityType: string; entityId: string; summary: string; correlationId: string | null; actor: string }

export function AuditHistory() {
  const [q, setQ] = useState('')
  const [from, setFrom] = useState('')
  const [to, setTo] = useState('')
  const [rows, setRows] = useState<Row[] | null>(null)
  const [error, setError] = useState('')

  useEffect(() => {
    const h = setTimeout(() => {
      const p = new URLSearchParams()
      if (q.trim()) p.set('q', q.trim())
      if (from) p.set('from', from)
      if (to) p.set('to', to)
      api<Row[]>('GET', '/audit?' + p.toString()).then(r => { setRows(r); setError('') }).catch(e => setError(e instanceof Error ? e.message : 'Could not load history'))
    }, 250)
    return () => clearTimeout(h)
  }, [q, from, to])

  return (
    <section aria-label="Audit history">
      <div role="search">
        <label>{tr('hist.search')}<input type="search" value={q} onChange={e => setQ(e.target.value)} placeholder="Action, type or details" /></label>
        <label>{tr('hist.from')}<input type="date" value={from} onChange={e => setFrom(e.target.value)} /></label>
        <label>{tr('hist.to')}<input type="date" value={to} onChange={e => setTo(e.target.value)} /></label>
      </div>
      {error && <p role="alert">{error}</p>}
      {rows === null && !error && <p role="status">Loading...</p>}
      {rows !== null && rows.length === 0 && <p className="empty">{tr('hist.none')}</p>}
      {rows !== null && rows.length > 0 && <div className="table-scroll"><table>
        <thead><tr><th>When</th><th>Action</th><th>Details</th><th>Type</th></tr></thead>
        <tbody>{rows.map((r, i) => <tr key={r.entityId + r.at + i}><td data-label="When">{new Date(r.at).toLocaleString('en-IN')}</td><td data-label="Action">{r.action}</td><td data-label="Details">{r.summary}</td><td data-label="Type">{r.entityType}</td></tr>)}</tbody>
      </table></div>}
    </section>
  )
}
