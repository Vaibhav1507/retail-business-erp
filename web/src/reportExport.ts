import { downloadCsv } from './exportCsv'

type Line = Record<string, unknown>

/** D9: turns a loaded report into flat CSV rows. Returns false when the report has nothing to export. */
export function exportReport(rep: string, data: any, label: string): boolean {
  if (!data) return false
  const rows: unknown[][] = []
  let headers: string[] = ['Section', 'Code', 'Name', 'Amount']
  const add = (section: string, lines: Line[] = []) => lines.forEach(l => rows.push([section, l.code ?? '', l.name ?? l.source ?? '', l.amount ?? l.value ?? '']))
  if (rep === 'tb') { headers = ['Code', 'Name', 'Debit', 'Credit']; (data.lines ?? []).forEach((l: Line) => rows.push([l.code, l.name, l.debit, l.credit])) }
  else if (rep === 'gl') { headers = ['Date', 'Source', 'Debit', 'Credit', 'Balance']; (data.lines ?? []).forEach((l: Line) => rows.push([l.date, l.source, l.debit, l.credit, l.balance])) }
  else if (rep === 'pl') { add('Income', data.income); add('Expense', data.expense); rows.push(['Total', '', 'Net profit', data.netProfit]) }
  else if (rep === 'bs') { add('Assets', data.assets); add('Liabilities', data.liabilities); add('Equity', data.equity) }
  else if (rep === 'cf') { add('Operating', data.operating); add('Financing', data.financing); rows.push(['Total', '', 'Closing cash', data.closingCash]) }
  else if (rep === 'ar' || rep === 'ap') { headers = ['Party', 'Current', '31-60', '61-90', '90+', 'Total']; (data.parties ?? []).forEach((p: Line) => rows.push([p.name, p.current, p.d31to60, p.d61to90, p.over90, p.total])) }
  else if (rep === 'sv') { headers = ['Product', 'Quantity', 'WAC', 'Value']; (data.items ?? []).forEach((i: Line) => rows.push([i.name, i.quantity, i.wac, i.value])) }
  else return false
  if (rows.length === 0) return false
  downloadCsv(`${label}_${new Date().toISOString().slice(0, 10)}.csv`, headers, rows)
  return true
}