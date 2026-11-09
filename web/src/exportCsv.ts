import { api } from './api'
/** D9: controlled client-side CSV export. Quotes fields and neutralises spreadsheet formula injection. */
const safe = (v: unknown): string => {
  let s = v == null ? '' : String(v)
  if (/^[=+\-@\t\r]/.test(s) && !/^-?\d+(\.\d+)?$/.test(s)) s = "'" + s
  return /[",\n\r]/.test(s) ? '"' + s.replace(/"/g, '""') + '"' : s
}

export function toCsv(headers: string[], rows: unknown[][]): string {
  return [headers, ...rows].map(r => r.map(safe).join(',')).join('\r\n')
}

export function downloadCsv(filename: string, headers: string[], rows: unknown[][]) {
  const blob = new Blob(['\ufeff' + toCsv(headers, rows)], { type: 'text/csv;charset=utf-8' })
  const a = document.createElement('a')
  a.href = URL.createObjectURL(blob)
  a.download = filename
  a.click()
  URL.revokeObjectURL(a.href)
  void api('POST', '/exports/audit', { report: filename.replace(/\.csv$/i, '').slice(0, 60), rows: rows.length }).catch(() => {})
}
