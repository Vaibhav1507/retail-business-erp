import { useCallback, useEffect, useState, type ReactNode } from 'react'
import './app-lock.css'

type Verifier = { salt: string; hash: string; failures: number; lockedUntil: number }
const keyFor = (email: string) => 'rbos.local-pin.v1:' + email.trim().toLowerCase()
const toBase64 = (bytes: Uint8Array) => btoa(String.fromCharCode(...bytes))
const fromBase64 = (value: string) => Uint8Array.from(atob(value), c => c.charCodeAt(0))
async function derive(pin: string, salt: Uint8Array) {
  const material = await crypto.subtle.importKey('raw', new TextEncoder().encode(pin), 'PBKDF2', false, ['deriveBits'])
  return new Uint8Array(await crypto.subtle.deriveBits({ name: 'PBKDF2', hash: 'SHA-256', salt, iterations: 600_000 }, material, 256))
}
async function makeVerifier(pin: string): Promise<Verifier> {
  const salt = crypto.getRandomValues(new Uint8Array(16))
  return { salt: toBase64(salt), hash: toBase64(await derive(pin, salt)), failures: 0, lockedUntil: 0 }
}
async function verify(pin: string, value: Verifier) {
  const actual = await derive(pin, fromBase64(value.salt))
  const expected = fromBase64(value.hash)
  return actual.length === expected.length && actual.every((b, i) => b === expected[i])
}

export function AppLockGate({ email, children, onSignOut }: { email: string; children: ReactNode; onSignOut: () => Promise<void> }) {
  const storageKey = keyFor(email)
  const [verifier, setVerifier] = useState<Verifier | null>(null)
  const [loading, setLoading] = useState(true)
  const [locked, setLocked] = useState(true)
  const [pin, setPin] = useState('')
  const [confirm, setConfirm] = useState('')
  const [newPin, setNewPin] = useState('')
  const [changingPin, setChangingPin] = useState(false)
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  const [remaining, setRemaining] = useState(0)

  const reload = useCallback(() => {
    try { setVerifier(JSON.parse(localStorage.getItem(storageKey) ?? 'null') as Verifier | null) }
    catch { setVerifier(null) }
    setLoading(false)
    setLocked(true)
  }, [storageKey])

  useEffect(() => { reload() }, [reload])
  useEffect(() => {
    const lock = () => setLocked(true)
    const change = () => { setChangingPin(true); setLocked(true); setPin(''); setNewPin(''); setConfirm(''); setError('') }
    window.addEventListener('rbos:lock', lock)
    window.addEventListener('rbos:change-pin', change)
    return () => { window.removeEventListener('rbos:lock', lock); window.removeEventListener('rbos:change-pin', change) }
  }, [])
  useEffect(() => {
    if (locked || loading) return
    let timer = window.setTimeout(() => setLocked(true), 5 * 60 * 1000)
    const reset = () => { window.clearTimeout(timer); timer = window.setTimeout(() => setLocked(true), 5 * 60 * 1000) }
    const events = ['pointerdown', 'keydown', 'touchstart']
    events.forEach(name => window.addEventListener(name, reset, { passive: true }))
    return () => { window.clearTimeout(timer); events.forEach(name => window.removeEventListener(name, reset)) }
  }, [locked, loading])

  async function setup() {
    if (!/^\d{4}$/.test(pin)) { setError('Choose exactly four digits.'); return }
    if (pin !== confirm) { setError('PINs do not match.'); return }
    setBusy(true); setError('')
    try {
      const next = await makeVerifier(pin)
      localStorage.setItem(storageKey, JSON.stringify(next))
      setVerifier(next); setPin(''); setConfirm(''); setLocked(false)
    } catch { setError('This browser cannot create a local PIN. Use sign out and try a supported browser.') }
    finally { setBusy(false) }
  }

  async function changePin() {
    if (!verifier) return
    if (!/^\d{4}$/.test(pin)) { setError('Enter your current four-digit PIN.'); return }
    if (!await verify(pin, verifier)) {
      const failures = verifier.failures + 1
      const delay = failures >= 5 ? Math.min(15 * 60_000, 30_000 * 2 ** Math.min(failures - 5, 5)) : 0
      const updated = { ...verifier, failures, lockedUntil: delay ? Date.now() + delay : 0 }
      localStorage.setItem(storageKey, JSON.stringify(updated))
      setVerifier(updated); setPin('')
      setRemaining(delay ? Math.ceil(delay / 1000) : 0)
      setError(delay ? 'Too many attempts. Try again later.' : 'Current PIN is incorrect.')
      return
    }
    if (!/^\d{4}$/.test(newPin) || newPin !== confirm) { setError('New PINs must match and contain exactly four digits.'); return }
    setBusy(true); setError('')
    try {
      const next = await makeVerifier(newPin)
      localStorage.setItem(storageKey, JSON.stringify(next))
      setVerifier(next); setPin(''); setNewPin(''); setConfirm(''); setChangingPin(false); setLocked(false)
    } catch { setError('Could not update the local PIN.') }
    finally { setBusy(false) }
  }

  async function unlock() {
    if (!/^\d{4}$/.test(pin) || !verifier) { setError('Enter your four-digit PIN.'); return }
    const wait = Math.max(0, verifier.lockedUntil - Date.now())
    if (wait > 0) { setRemaining(Math.ceil(wait / 1000)); setError('Too many attempts. Try again later.'); return }
    setBusy(true); setError('')
    try {
      if (await verify(pin, verifier)) {
        const next = { ...verifier, failures: 0, lockedUntil: 0 }
        localStorage.setItem(storageKey, JSON.stringify(next))
        setVerifier(next); setPin(''); setLocked(false)
      } else {
        const failures = verifier.failures + 1
        const delay = failures >= 5 ? Math.min(15 * 60_000, 30_000 * 2 ** Math.min(failures - 5, 5)) : 0
        const next = { ...verifier, failures, lockedUntil: delay ? Date.now() + delay : 0 }
        localStorage.setItem(storageKey, JSON.stringify(next))
        setVerifier(next); setPin('')
        setError(delay ? 'Too many attempts. Try again later.' : 'Incorrect PIN.')
        setRemaining(delay ? Math.ceil(delay / 1000) : 0)
      }
    } catch { setError('Could not verify the local PIN. Sign out and sign in again.') }
    finally { setBusy(false) }
  }

  if (loading) return <main className="access"><p>Preparing this device…</p></main>
  if (!verifier || locked) return (
    <main className="access app-lock-screen">
      <section className="app-lock-card">
        <h1>{changingPin ? 'Change app PIN' : verifier ? 'Unlock this device' : 'Set up your app PIN'}</h1>
        <p>{changingPin ? 'Confirm your current PIN, then choose a new one.' : verifier ? 'Enter your four-digit PIN to reopen the shop workspace.' : 'Set a four-digit PIN for the local privacy lock on this browser.'}</p>
        <label>{changingPin ? 'Current PIN' : verifier ? 'PIN' : 'Create four-digit PIN'}
          <input autoFocus inputMode="numeric" type="password" maxLength={4} autoComplete="off" value={pin} onChange={e => setPin(e.target.value.replace(/\D/g, '').slice(0, 4))} />
        </label>
        {changingPin ? <>
          <label>New four-digit PIN
            <input inputMode="numeric" type="password" maxLength={4} autoComplete="off" value={newPin} onChange={e => setNewPin(e.target.value.replace(/\D/g, '').slice(0, 4))} />
          </label>
          <label>Confirm new PIN
            <input inputMode="numeric" type="password" maxLength={4} autoComplete="off" value={confirm} onChange={e => setConfirm(e.target.value.replace(/\D/g, '').slice(0, 4))} />
          </label>
        </> : !verifier && <label>Confirm PIN
          <input inputMode="numeric" type="password" maxLength={4} autoComplete="off" value={confirm} onChange={e => setConfirm(e.target.value.replace(/\D/g, '').slice(0, 4))} />
        </label>}
        {error && <p role="alert">{error}{remaining > 0 ? ` (${remaining}s)` : ''}</p>}
        <button type="button" disabled={busy} onClick={() => void (changingPin ? changePin() : verifier ? unlock() : setup())}>{busy ? 'Please wait…' : changingPin ? 'Save new PIN' : verifier ? 'Unlock' : 'Save PIN'}</button>
        {changingPin && <button type="button" className="secondary" onClick={() => { setChangingPin(false); setLocked(false); setPin(''); setNewPin(''); setConfirm(''); setError('') }}>Cancel</button>}
        <button type="button" className="secondary" onClick={() => { void onSignOut().then(() => localStorage.removeItem(storageKey)).catch(() => setError('You are offline. Reconnect before signing out so the server session can be revoked.')) }}>Sign out</button>
        <p className="app-lock-note">This is a local privacy lock, not a second authentication factor. Your server session and API permissions remain the actual security boundary. Do not use this PIN as a password.</p>
      </section>
    </main>
  )

  return <>{children}</>
}
