import { useCallback, useEffect, useRef, useState } from 'react'
import { BrowserMultiFormatReader } from '@zxing/browser'
import { BarcodeFormat, DecodeHintType } from '@zxing/library'

export type ScanResult = { code: string; symbology: string }

const FORMATS: Record<number, string> = {
  [BarcodeFormat.EAN_13]: 'EAN_13', [BarcodeFormat.EAN_8]: 'EAN_8', [BarcodeFormat.UPC_A]: 'UPC_A', [BarcodeFormat.UPC_E]: 'UPC_E',
  [BarcodeFormat.CODE_128]: 'CODE_128', [BarcodeFormat.QR_CODE]: 'QR_CODE', [BarcodeFormat.DATA_MATRIX]: 'DATA_MATRIX',
}

export function cameraErrorMessage(e: unknown): string {
  if (!window.isSecureContext) return 'Camera needs a secure (HTTPS) connection. Type the code instead.'
  if (!navigator.mediaDevices?.getUserMedia) return 'This browser cannot use the camera. Type the code instead.'
  const name = (e as { name?: string })?.name
  if (name === 'NotAllowedError' || name === 'SecurityError') return 'Camera permission was denied. Allow camera access in the browser settings, or type the code.'
  if (name === 'NotFoundError' || name === 'OverconstrainedError') return 'No camera was found. Type the code instead.'
  if (name === 'NotReadableError') return 'The camera is busy in another app. Close it and try again, or type the code.'
  return 'Could not start the camera. Type the code instead.'
}

export function BarcodeScanner({ onScan, onClose, title = 'Scan barcode' }: { onScan: (r: ScanResult) => void; onClose: () => void; title?: string }) {
  const video = useRef<HTMLVideoElement>(null)
  const last = useRef({ code: '', at: 0 })
  const onScanRef = useRef(onScan)
  onScanRef.current = onScan
  const [error, setError] = useState('')
  const [torch, setTorch] = useState<boolean | null>(null)
  const [manual, setManual] = useState('')

  const emit = useCallback((code: string, symbology: string) => {
	const now = Date.now()
	if (code === last.current.code && now - last.current.at < 1500) return
	last.current = { code, at: now }
	onScanRef.current({ code, symbology })
  }, [])

  useEffect(() => {
	let cancelled = false
	let stopScan: (() => void) | null = null
	const el = video.current
	const hints = new Map()
	hints.set(DecodeHintType.POSSIBLE_FORMATS, Object.keys(FORMATS).map(Number))
	hints.set(DecodeHintType.TRY_HARDER, true)
	const reader = new BrowserMultiFormatReader(hints)
	;(async () => {
	  try {
		if (!window.isSecureContext || !navigator.mediaDevices?.getUserMedia) throw new Error('insecure')
		const controls = await reader.decodeFromConstraints(
		  { video: { facingMode: { ideal: 'environment' }, width: { ideal: 1920 }, height: { ideal: 1080 }, advanced: [{ focusMode: 'continuous' } as MediaTrackConstraintSet] } as MediaTrackConstraints },
		  el ?? undefined,
		  result => { if (result) emit(result.getText(), FORMATS[result.getBarcodeFormat()] ?? 'UNKNOWN') })
		if (cancelled) { controls.stop(); return }
		stopScan = () => controls.stop()
		const track = (el?.srcObject as MediaStream | null)?.getVideoTracks()[0]
		const caps = track?.getCapabilities?.() as { torch?: boolean } | undefined
		setTorch(caps?.torch ? false : null)
	  } catch (e) { if (!cancelled) setError(cameraErrorMessage(e)) }
	})()
	return () => {
	  cancelled = true
	  stopScan?.()
	  const stream = el?.srcObject as MediaStream | null
	  stream?.getTracks().forEach(t => t.stop())
	  if (el) el.srcObject = null
	}
  }, [emit])

  async function toggleTorch() {
	const track = (video.current?.srcObject as MediaStream | null)?.getVideoTracks()[0]
	if (!track || torch === null) return
	try {
	  await track.applyConstraints({ advanced: [{ torch: !torch } as MediaTrackConstraintSet] })
	  setTorch(!torch)
	} catch { setError('Flashlight is not available on this camera.') }
  }

  function submitManual(e: React.FormEvent) {
	e.preventDefault()
	const code = manual.trim()
	if (!code) return
	setManual('')
	onScan({ code, symbology: 'MANUAL' })
  }

  return (
	<div className="scanner" role="dialog" aria-modal="true" aria-label={title}>
	  <div className="scanner-head">
		<strong>{title}</strong>
		<button type="button" className="secondary" onClick={onClose}>Close</button>
	  </div>
	  {!error && <div className="scanner-view"><video ref={video} muted playsInline aria-label="Camera preview" /><div className="scanner-guide" aria-hidden="true" /></div>}
	  {error && <p role="alert">{error}</p>}
	  {torch !== null && <button type="button" className="secondary" onClick={toggleTorch} aria-pressed={torch}>{torch ? 'Flashlight off' : 'Flashlight on'}</button>}
	  <form onSubmit={submitManual} className="scanner-manual">
		<label>Type the code instead<input value={manual} onChange={e => setManual(e.target.value)} inputMode="numeric" autoComplete="off" /></label>
		<button type="submit">Use code</button>
	  </form>
	</div>
  )
}
