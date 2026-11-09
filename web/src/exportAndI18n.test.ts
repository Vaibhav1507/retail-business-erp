import { afterEach, describe, expect, it, vi } from 'vitest'
import { getLanguage, setLanguage, t } from './i18n'
import type { MessageKey } from './i18n'
import { toCsv } from './exportCsv'
import * as csvModule from './exportCsv'
import { exportReport } from './reportExport'

afterEach(() => { setLanguage('en'); vi.restoreAllMocks() })

describe('i18n catalogue parity (G4)', () => {
  const keys: MessageKey[] = [
    'gst.show', 'gst.csv', 'gst.none', 'gst.rate', 'gst.total', 'gst.inter', 'gst.intra',
    'ca.intro', 'ca.download',
    'ta.email', 'ta.password', 'ta.add', 'ta.active', 'ta.disable',
    'theme.dark', 'theme.light',
  ]

  it('has a non-empty English and Hindi string for every new key', () => {
    for (const k of keys) {
      setLanguage('en'); const en = t(k)
      setLanguage('hi'); const hi = t(k)
      expect(en.length).toBeGreaterThan(0)
      expect(hi.length).toBeGreaterThan(0)
      expect(hi).not.toBe(en)
    }
  })

  it('substitutes the count in reminder messages in both languages', () => {
    for (const lang of ['en', 'hi'] as const) {
      setLanguage(lang)
      expect(t('rem.orders', { n: 3 })).toContain('3')
      expect(t('rem.dayClosing', { n: 2 })).toContain('2')
      expect(t('rem.orders', { n: 3 })).not.toContain('{n}')
    }
  })

  it('tracks the current language', () => {
    setLanguage('hi')
    expect(getLanguage()).toBe('hi')
  })
})

describe('toCsv (D9)', () => {
  it('quotes commas, quotes and newlines', () => {
    expect(toCsv(['a', 'b'], [['x, y', 'say "hi"'], ['line\nbreak', 'ok']])).toBe('a,b\r\n"x, y","say ""hi"""\r\n"line\nbreak",ok')
  })

  it('neutralises spreadsheet formulas but keeps plain negative numbers', () => {
    const out = toCsv(['v'], [['=SUM(A1)'], ['+1+1'], ['@cmd'], ['-5'], ['-2.50'], ['-abc']])
    const lines = out.split('\r\n')
    expect(lines[1]).toBe("'=SUM(A1)")
    expect(lines[2]).toBe("'+1+1")
    expect(lines[3]).toBe("'@cmd")
    expect(lines[4]).toBe('-5')
    expect(lines[5]).toBe('-2.50')
    expect(lines[6]).toBe("'-abc")
  })

  it('renders null and undefined as empty cells', () => {
    expect(toCsv(['a', 'b'], [[null, undefined]])).toBe('a,b\r\n,')
  })
})

describe('exportReport (D9, G6)', () => {
  it('returns false and downloads nothing when there is no data', () => {
    const spy = vi.spyOn(csvModule, 'downloadCsv').mockImplementation(() => {})
    expect(exportReport('tb', null, 'tb')).toBe(false)
    expect(exportReport('tb', { lines: [] }, 'tb')).toBe(false)
    expect(exportReport('unknown', { lines: [{}] }, 'x')).toBe(false)
    expect(spy).not.toHaveBeenCalled()
  })

  it('exports a trial balance with debit and credit columns', () => {
    const spy = vi.spyOn(csvModule, 'downloadCsv').mockImplementation(() => {})
    const ok = exportReport('tb', { lines: [{ code: '1000', name: 'Cash', debit: 50, credit: 0 }] }, 'trial')
    expect(ok).toBe(true)
    expect(spy).toHaveBeenCalledTimes(1)
    const [filename, headers, rows] = spy.mock.calls[0]
    expect(filename).toMatch(/^trial_\d{4}-\d{2}-\d{2}\.csv$/)
    expect(headers).toEqual(['Code', 'Name', 'Debit', 'Credit'])
    expect(rows).toEqual([['1000', 'Cash', 50, 0]])
  })

  it('exports profit and loss with a net profit row', () => {
    const spy = vi.spyOn(csvModule, 'downloadCsv').mockImplementation(() => {})
    const ok = exportReport('pl', { income: [{ code: '4000', name: 'Sales', amount: 500 }], expense: [{ code: '5000', name: 'Rent', amount: 200 }], netProfit: 300 }, 'pl')
    expect(ok).toBe(true)
    const rows = spy.mock.calls[0][2] as unknown[][]
    expect(rows).toContainEqual(['Income', '4000', 'Sales', 500])
    expect(rows).toContainEqual(['Expense', '5000', 'Rent', 200])
    expect(rows[rows.length - 1]).toEqual(['Total', '', 'Net profit', 300])
  })

  it('exports ageing and stock valuation with their own headers', () => {
    const spy = vi.spyOn(csvModule, 'downloadCsv').mockImplementation(() => {})
    expect(exportReport('ar', { parties: [{ name: 'Ravi', current: 1, d31to60: 2, d61to90: 3, over90: 4, total: 10 }] }, 'ar')).toBe(true)
    expect(spy.mock.calls[0][1]).toEqual(['Party', 'Current', '31-60', '61-90', '90+', 'Total'])
    expect(exportReport('sv', { items: [{ name: 'Cable', quantity: 2, wac: 5, value: 10 }] }, 'sv')).toBe(true)
    expect(spy.mock.calls[1][1]).toEqual(['Product', 'Quantity', 'WAC', 'Value'])
  })
})
