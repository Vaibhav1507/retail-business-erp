import { useCallback, useEffect, useState } from 'react'
import { api, ApiError } from './api'
import { t } from './i18n'

type Money = { id: string; name: string; kind: string; balance: number }
type Closing = { id: string; date: string; account: string; expected: number; counted: number; difference: number; note: string }

const today = () => new Date().toISOString().slice(0, 10)

export function CashClosing() {
  const [accounts, setAccounts] = useState<Money[]>([])
  const [accountId, setAccountId] = useState('')
  const [date, setDate] = useState(today)
  const [expected, setExpected] = useState<number | null>(null)
  const [counted, setCounted] = useState('')
  const [note, setNote] = useState('')
  const [rows, setRows] = useState<Closing[]>([])
  const [msg, setMsg] = useState<{ ok: boolean; text: string } | null>(null)

  const load = useCallback(async () => {
    const [a, r] = await Promise.all([api<Money[]>('GET', '/money-accounts'), api<Closing[]>('GET', '/cash-closings')])
    setAccounts(a); setRows(r)
    setAccountId(prev => prev || a[0]?.id || '')
  }, [])

  useEffect(() => { load().catch(e => setMsg({ ok: false, text: e instanceof ApiError ? e.message : 'Could not load.' })) }, [load])

  useEffect(() => {
    if (!accountId) { setExpected(null); return }
    api<{ expected: number }>('GET', `/cash-closings/preview?moneyAccountId=${accountId}&date=${date}`)
      .then(r => setExpected(r.expected)).catch(() => setExpected(null))
  }, [accountId, date, rows])

  const diff = expected === null || counted === '' ? null : Number(counted) - expected

  const close = async () => {
    setMsg(null)
    try {
      const r = await api<{ difference: number }>('POST', '/cash-closings', { moneyAccountId: accountId, date, counted: Number(counted), note: note || null })
      setMsg({ ok: true, text: r.difference === 0 ? 'Day closed. Cash matches the books.' : `Day closed. Difference recorded: ${r.difference.toFixed(2)}` })
      setCounted(''); setNote(''); await load()
    } catch (e) {
      setMsg({ ok: false, text: e instanceof ApiError ? e.message : 'Could not close the day.' })
    }
  }

  return (
    <section aria-label="Day closing" className="billing-screen">
      <h3>{t('cc.title')}</h3>
      <label>{t('cc.account')} <select value={accountId} onChange={e => setAccountId(e.target.value)}>{accounts.map(a => <option key={a.id} value={a.id}>{a.name}</option>)}</select></label>
      <label>{t('cc.date')} <input type="date" value={date} max={today()} onChange={e => setDate(e.target.value)} /></label>
      {expected !== null && <p data-testid="closing-expected">Expected in books: {expected.toFixed(2)} INR</p>}
      <label>{t('cc.counted')} <input inputMode="decimal" value={counted} onChange={e => setCounted(e.target.value)} /></label>
      {diff !== null && <p data-testid="closing-diff">Difference: {diff.toFixed(2)} INR{diff < 0 ? ' (short)' : diff > 0 ? ' (extra)' : ''}</p>}
      <label>{t('cc.note')} <input maxLength={200} value={note} onChange={e => setNote(e.target.value)} /></label>
      <button type="button" onClick={close} disabled={!accountId || counted === '' || !(Number(counted) >= 0)}>{t('cc.close')}</button>
      {msg && <p role={msg.ok ? 'status' : 'alert'}>{msg.text}</p>}
      <h3>{t('cc.history')}</h3>
      {rows.length === 0 ? <p>{t('cc.none')}</p> : (
        <table aria-label="Closing history"><thead><tr><th>Date</th><th>Account</th><th>Expected</th><th>Counted</th><th>Difference</th><th>Note</th></tr></thead>
          <tbody>{rows.map(r => <tr key={r.id}><td>{r.date}</td><td>{r.account}</td><td>{r.expected.toFixed(2)}</td><td>{r.counted.toFixed(2)}</td><td>{r.difference.toFixed(2)}</td><td>{r.note}</td></tr>)}</tbody></table>
      )}
    </section>
  )
}
