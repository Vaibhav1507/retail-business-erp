import { useCallback, useEffect, useState } from 'react'
import { api } from './api'
import { BarcodeScanner, type ScanResult } from './BarcodeScanner'

type Row = { id: string; code: string; symbology: string | null; packQuantity: number }
const msg = (e: unknown) => (e instanceof Error ? e.message : 'Something went wrong')

/** Barcodes and serial tracking for an existing product. Each barcode is one product or pack code, never one physical unit. */
export function BarcodeManager({ productId }: { productId: string }) {
  const [rows, setRows] = useState<Row[]>([])
  const [track, setTrack] = useState(false)
  const [code, setCode] = useState('')
  const [pack, setPack] = useState('1')
  const [symbology, setSymbology] = useState('MANUAL')
  const [scanning, setScanning] = useState(false)
  const [error, setError] = useState('')

  const load = useCallback(async () => {
    const r = await api<{ trackSerials: boolean; barcodes: Row[] }>('GET', `/products/${productId}/barcodes`)
    setRows(r.barcodes); setTrack(r.trackSerials)
  }, [productId])
  useEffect(() => { load().catch(e => setError(msg(e))) }, [load])

  async function add() {
    setError('')
    try {
      await api('POST', `/products/${productId}/barcodes`, { code, symbology, packQuantity: Number(pack) || 1 })
      setCode(''); setPack('1'); setSymbology('MANUAL'); await load()
    } catch (e) { setError(msg(e)) }
  }
  async function remove(id: string) {
    setError('')
    if (!window.confirm('Remove this barcode?')) return; try { await api('DELETE', `/products/${productId}/barcodes/${id}`); await load() } catch (e) { setError(msg(e)) }
  }
  async function toggleTrack(enabled: boolean) {
    setError('')
    try { await api('PUT', `/products/${productId}/serial-tracking`, { enabled }); setTrack(enabled) } catch (e) { setError(msg(e)) }
  }
  const onScan = (r: ScanResult) => { setCode(r.code); setSymbology(r.symbology); setScanning(false) }

  return (
    <fieldset className="barcode-manager">
      <legend>Barcodes</legend>
      {rows.length === 0 && <p className="hint-line">No barcode linked yet.</p>}
      <ul>{rows.map(r => <li key={r.id}>{r.code} {r.packQuantity > 1 ? `(pack of ${r.packQuantity})` : ''} <button type="button" className="secondary" onClick={() => remove(r.id)} aria-label={`Remove barcode ${r.code}`}>Remove</button></li>)}</ul>
      <div className="scanner-manual">
        <label>Barcode <input value={code} onChange={e => { setCode(e.target.value); setSymbology('MANUAL') }} maxLength={64} autoComplete="off" /></label>
        <label>Units per pack <input type="number" min={1} value={pack} onChange={e => setPack(e.target.value)} /></label>
        <button type="button" className="secondary" onClick={() => setScanning(true)}>Scan</button>
        <button type="button" onClick={add} disabled={!code.trim()}>Add barcode</button>
      </div>
      <label className="check"><input type="checkbox" checked={track} onChange={e => toggleTrack(e.target.checked)} /> Track serial / IMEI numbers for this product</label>
      {error && <p role="alert">{error}</p>}
      {scanning && <BarcodeScanner onScan={onScan} onClose={() => setScanning(false)} />}
    </fieldset>
  )
}
