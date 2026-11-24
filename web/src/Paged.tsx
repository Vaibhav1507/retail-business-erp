import { useEffect, useMemo, useRef, useState, type MouseEvent, type ReactNode } from 'react'

const SIZES = [10, 25, 50]
type Sort = { col: number; dir: 1 | -1; keys: Map<unknown, string> }
const num = (s: string) => { const n = Number(s.replace(/[\u20B9,\s]/g, '')); return s.trim() !== '' && !Number.isNaN(n) ? n : null }

export function Paged<T>({ rows, children, label = 'rows', empty, resetKey }: { rows: T[]; children: (page: T[]) => ReactNode; label?: string; empty?: string; resetKey?: string }) {
  const [size, setSize] = useState(10)
  const [page, setPage] = useState(0)
  const [sort, setSort] = useState<Sort | null>(null)
  const ref = useRef<HTMLDivElement>(null)
  const sorted = useMemo(() => {
    if (!sort) return rows
    return [...rows].sort((a, b) => {
      const x = sort.keys.get(a) ?? '', y = sort.keys.get(b) ?? ''
      const nx = num(x), ny = num(y)
      const r = nx !== null && ny !== null ? nx - ny : x.localeCompare(y, undefined, { numeric: true, sensitivity: 'base' })
      return r * sort.dir
    })
  }, [rows, sort])
  const pages = Math.max(1, Math.ceil(rows.length / size))
  const cur = Math.min(page, pages - 1)
  const from = rows.length === 0 ? 0 : cur * size + 1
  const to = Math.min(rows.length, (cur + 1) * size)
  useEffect(() => { if (page > pages - 1) setPage(pages - 1) }, [page, pages])
  useEffect(() => { setPage(0) }, [resetKey])
  useEffect(() => {
    ref.current?.querySelectorAll('table').forEach(t => {
      const b = t.tBodies[0]
      if (b) Array.from(b.rows).forEach((tr, i) => { tr.style.display = i >= cur * size && i < cur * size + size ? '' : 'none' })
      t.tHead?.querySelectorAll('th').forEach((th, i) => {
        const has = (th.textContent ?? '').trim() !== ''
        th.classList.toggle('sortable', has)
        if (has && sort && sort.col === i) th.setAttribute('data-sort', sort.dir === 1 ? 'asc' : 'desc'); else th.removeAttribute('data-sort')
      })
    })
  })
  const onSort = (e: MouseEvent<HTMLDivElement>) => {
    const th = (e.target as HTMLElement).closest('th')
    const table = th?.closest('table')
    if (!th || !table || !table.tHead?.contains(th) || !th.classList.contains('sortable')) return
    const col = Array.from(th.parentElement!.children).indexOf(th)
    const trs = table.tBodies[0]?.rows
    if (!trs) return
    const keys = new Map<unknown, string>()
    sorted.forEach((r, i) => keys.set(r, (trs[i]?.cells[col]?.textContent ?? '').trim()))
    setSort({ col, dir: sort && sort.col === col && sort.dir === 1 ? -1 : 1, keys })
    setPage(0)
  }
  return (
    <>
      {rows.length === 0 && empty && <p className="empty">{empty}</p>}
      <div ref={ref} className="paged" onClick={onSort}>{children(sorted)}</div>
      {rows.length > SIZES[0] && <nav className="pager no-print" aria-label="Pagination">
        <span>{from}-{to} of {rows.length} {label}</span>
        <label>Per page <select value={size} onChange={e => { setSize(Number(e.target.value)); setPage(0) }}>{SIZES.map(s => <option key={s} value={s}>{s}</option>)}</select></label>
        <button type="button" className="secondary" disabled={cur === 0} onClick={() => setPage(cur - 1)}>Previous</button>
        <span>Page {cur + 1} of {pages}</span>
        <button type="button" className="secondary" disabled={cur >= pages - 1} onClick={() => setPage(cur + 1)}>Next</button>
      </nav>}
    </>
  )
}
