import { useCallback, useEffect, useState, type ReactNode } from 'react'
import { AuthBrand } from './AuthBrand'
import './app-lock.css'

type Verifier = { salt: string; hash: string; failures: number; lockedUntil: number }
type PinField = 'pin' | 'newPin' | 'confirm'

const keyFor = (email: string) => 'rbos.local-pin.v1:' + email.trim().toLowerCase()
const toBase64 = (bytes: Uint8Array) => btoa(String.fromCharCode(...bytes))
const fromBase64 = (value: string) => Uint8Array.from(atob(value), c => c.charCodeAt(0))

async function derive(pin: string, salt: Uint8Array) {
  const material = await crypto.subtle.importKey('raw', new TextEncoder().encode(pin), 'PBKDF2', false, ['deriveBits'])
  return new Uint8Array(await crypto.subtle.deriveBits({ name: 'PBKDF2', hash: 'SHA-256', salt: Uint8Array.from(salt).buffer, iterations: 600_000 }, material, 256))
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
  const [activeField, setActiveField] = useState<PinField>('pin')
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
    const change = () => {
      setChangingPin(true)
      setLocked(true)
      setPin('')
      setNewPin('')
      setConfirm('')
      setActiveField('pin')
      setError('')
    }
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

  function valueFor(field: PinField) {
    return field === 'pin' ? pin : field === 'newPin' ? newPin : confirm
  }

  function setValueFor(field: PinField, value: string) {
    const digits = value.replace(/\D/g, '').slice(0, 4)
    if (field === 'pin') setPin(digits)
    else if (field === 'newPin') setNewPin(digits)
    else setConfirm(digits)
  }

  function appendDigit(digit: string) {
    if (!/^\d$/.test(digit)) return
    setValueFor(activeField, valueFor(activeField) + digit)
    setError('')
  }

  function clearActiveField() {
    setValueFor(activeField, '')
    setError('')
  }

  function backspaceActiveField() {
    setValueFor(activeField, valueFor(activeField).slice(0, -1))
    setError('')
  }

  async function setup() {
    if (!/^\d{4}$/.test(pin)) { setError('Choose exactly four digits.'); return }
    if (pin !== confirm) { setError('PINs do not match.'); setActiveField('confirm'); return }
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
    if (!/^\d{4}$/.test(pin)) { setError('Enter your current four-digit PIN.'); setActiveField('pin'); return }
    if (!await verify(pin, verifier)) {
      const failures = verifier.failures + 1
      const delay = failures >= 5 ? Math.min(15 * 60_000, 30_000 * 2 ** Math.min(failures - 5, 5)) : 0
      const updated = { ...verifier, failures, lockedUntil: delay ? Date.now() + delay : 0 }
      localStorage.setItem(storageKey, JSON.stringify(updated))
      setVerifier(updated); setPin('')
      setRemaining(delay ? Math.ceil(delay / 1000) : 0)
      setError(delay ? 'Too many attempts. Try again later.' : 'Current PIN is incorrect.')
      setActiveField('pin')
      return
    }
    if (!/^\d{4}$/.test(newPin) || newPin !== confirm) {
      setError('New PINs must match and contain exactly four digits.')
      setActiveField(!/^\d{4}$/.test(newPin) ? 'newPin' : 'confirm')
      return
    }
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

  async function signOut() {
    setError('')
    try {
      await onSignOut()
      localStorage.removeItem(storageKey)
    } catch {
      setError('You are offline. Reconnect before signing out so the server session can be revoked.')
    }
  }

  function handlePinKeyDown(e: React.KeyboardEvent<HTMLInputElement>) {
    if (e.key === 'Enter') {
      e.preventDefault()
      if (changingPin) void changePin()
      else if (verifier) void unlock()
      else void setup()
    }
  }

  const selectedValue = valueFor(activeField)
  const field = (name: PinField, label: string) => (
    <label className={`app-lock-pin-field ${activeField === name ? 'is-active' : ''}`}>
      <span>{label}</span>
      <input
        type="password"
        inputMode="numeric"
        pattern="[0-9]*"
        maxLength={4}
        autoComplete="off"
        value={valueFor(name)}
        onFocus={() => setActiveField(name)}
        onKeyDown={handlePinKeyDown}
        onChange={e => setValueFor(name, e.target.value)}
        aria-label={label}
      />
    </label>
  )

  if (loading) return <main className="access auth-access app-lock-screen"><AuthBrand /><p className="app-lock-loading">Preparing this device…</p></main>
  if (!verifier || locked) return (
    <main className="access auth-access app-lock-screen">
      <AuthBrand />
      <section className="app-lock-card">
        <div className="app-lock-hero">
          <span className="app-lock-icon" aria-hidden="true">🔒</span>
          <h1>{changingPin ? 'Change app PIN' : verifier ? 'Unlock your shop' : 'Set up your app PIN'}</h1>
          <p>{changingPin ? 'Confirm your current PIN, then choose a new one.' : verifier ? 'Enter your 4-digit device PIN to continue.' : 'Set a four-digit PIN for the local privacy lock on this browser.'}</p>
        </div>

        {verifier && !changingPin && (
          <div className="app-lock-pin-indicators" aria-label={`${pin.length} of 4 PIN digits entered`}>
            {[0, 1, 2, 3].map(i => (
              <span key={i} className={pin.length > i ? 'is-filled' : ''} aria-hidden="true" />
            ))}
          </div>
        )}

        <div className="app-lock-pin-fields">
          {changingPin ? (
            <>
              {field('pin', 'Current PIN')}
              {field('newPin', 'New four-digit PIN')}
              {field('confirm', 'Confirm new PIN')}
            </>
          ) : verifier ? field('pin', 'PIN') : (
            <>
              {field('pin', 'Create four-digit PIN')}
              {field('confirm', 'Confirm PIN')}
            </>
          )}
        </div>

        <div className="app-lock-keypad" role="group" aria-label="Numeric PIN keypad">
          {['1', '2', '3', '4', '5', '6', '7', '8', '9', 'Clear', '0', 'Backspace'].map(key => (
            <button
              key={key}
              type="button"
              className={key === 'Clear' || key === 'Backspace' ? 'app-lock-keypad-action' : ''}
              aria-label={key === 'Backspace' ? 'Backspace' : key === 'Clear' ? 'Clear PIN field' : `Digit ${key}`}
              onClick={() => key === 'Clear' ? clearActiveField() : key === 'Backspace' ? backspaceActiveField() : appendDigit(key)}
              disabled={busy}
            >
              {key === 'Backspace' ? '⌫' : key}
            </button>
          ))}
        </div>

        {error && <p role="alert" className="app-lock-error">{error}{remaining > 0 ? ` (${remaining}s)` : ''}</p>}

        <button
          type="button"
          className="app-lock-unlock auth-primary"
          disabled={busy}
          onClick={() => void (changingPin ? changePin() : verifier ? unlock() : setup())}
        >
          {busy ? 'Please wait…' : changingPin ? 'Save new PIN' : verifier ? 'Unlock' : 'Save PIN'}
        </button>

        {changingPin && (
          <button type="button" className="auth-secondary" onClick={() => { setChangingPin(false); setLocked(false); setPin(''); setNewPin(''); setConfirm(''); setError('') }}>
            Cancel
          </button>
        )}

        <p className="app-lock-note">
          This is a local privacy lock, not a second authentication factor. Your server session and API permissions remain the actual security boundary. Do not use this PIN as a password.
        </p>
        <button type="button" className="auth-signout-link" onClick={() => { void signOut() }}>Sign out</button>
      </section>
    </main>
  )

  return <>{children}</>
}
