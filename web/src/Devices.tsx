import { useCallback, useEffect, useState, type FormEvent } from 'react'
import { api, ApiError } from './api'
import { getLanguage, t } from './i18n'
import { listPasskeys, passkeyEnabled, registerPasskey, removePasskey, type PasskeySummary } from './passkeys'
import './devices.css'

type Device = {
  id: string
  label: string
  browser: string
  platform: string
  createdAt: string
  lastSeenAt: string
  lastAuthenticatedAt: string
  expiresAt: string
  revokedAt: string | null
  isCurrentDevice: boolean
}

type Props = { onCurrentDeviceRevoked: () => Promise<void> }

function dateTime(value: string) {
  const locale = getLanguage() === 'hi' ? 'hi-IN' : 'en-IN'
  return new Intl.DateTimeFormat(locale, { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value))
}

export function DevicesPage({ onCurrentDeviceRevoked }: Props) {
  const [devices, setDevices] = useState<Device[]>([])
  const [loading, setLoading] = useState(true)
  const [busyId, setBusyId] = useState<string | null>(null)
  const [error, setError] = useState('')
  const [passkeys, setPasskeys] = useState<PasskeySummary[]>([])
  const [passkeysEnabled, setPasskeysEnabled] = useState(false)
  const [passkeyBusy, setPasskeyBusy] = useState(false)
  const [passkeyAction, setPasskeyAction] = useState<{ kind: 'register'; name: string } | { kind: 'remove'; credentialId: string } | null>(null)
  const [passkeyPassword, setPasskeyPassword] = useState('')

  const load = useCallback(async () => {
    setError('')
    setLoading(true)
    try {
      setDevices(await api<Device[]>('GET', '/auth/devices'))
    } catch (reason) {
      setError(reason instanceof ApiError ? reason.message : t('devices.loadError'))
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => { void load() }, [load])
  const loadPasskeys = useCallback(async () => {
    if (!passkeysEnabled) return
    try { setPasskeys(await listPasskeys()) }
    catch (reason) { setError(reason instanceof ApiError ? reason.message : 'Could not load passkeys.') }
  }, [passkeysEnabled])
  useEffect(() => { void passkeyEnabled().then(enabled => setPasskeysEnabled(enabled)) }, [])
  useEffect(() => { if (passkeysEnabled) void loadPasskeys() }, [passkeysEnabled, loadPasskeys])

  async function addPasskey() {
    const name = window.prompt('Name this passkey', 'My phone')
    if (name === null) return
    setPasskeyAction({ kind: 'register', name: name.trim() || 'This device' })
    setPasskeyPassword('')
  }

  async function deletePasskey(passkey: PasskeySummary) {
    if (!window.confirm('Remove this passkey? You can still sign in with your password.')) return
    setPasskeyAction({ kind: 'remove', credentialId: passkey.credentialId })
    setPasskeyPassword('')
  }

  async function confirmPasskeyAction(event: FormEvent) {
    event.preventDefault()
    if (!passkeyAction || !passkeyPassword) return
    setPasskeyBusy(true); setError('')
    try {
      if (passkeyAction.kind === 'register') await registerPasskey(passkeyAction.name, passkeyPassword)
      else await removePasskey(passkeyAction.credentialId, passkeyPassword)
      setPasskeyAction(null); setPasskeyPassword('')
      await loadPasskeys()
    } catch (reason) {
      setError(reason instanceof ApiError ? reason.message : reason instanceof Error ? reason.message : 'Passkey operation failed.')
    } finally { setPasskeyBusy(false) }
  }

  async function revoke(device: Device) {
    const message = device.isCurrentDevice
      ? t('devices.confirmCurrent')
      : t('devices.confirmOther')
    if (!window.confirm(message)) return

    setBusyId(device.id)
    setError('')
    try {
      await api('POST', `/auth/devices/${device.id}/revoke`)
      if (device.isCurrentDevice) {
        await onCurrentDeviceRevoked()
        return
      }
      await load()
    } catch (reason) {
      setError(reason instanceof ApiError ? reason.message : t('devices.revokeError'))
    } finally {
      setBusyId(null)
    }
  }

  return (
    <section className="devices-page" aria-labelledby="devices-title">
      <header className="devices-heading">
        <div>
          <h1 id="devices-title">{t('devices.title')}</h1>
          <p>{t('devices.intro')}</p>
        </div>
        <button type="button" onClick={() => { void load() }} disabled={loading}>{t('devices.refresh')}</button>
      </header>

      {error && <p role="alert" className="devices-error">{error}</p>}
      {loading && <p role="status">{t('devices.loading')}</p>}
      {!loading && devices.length === 0 && <p>{t('devices.empty')}</p>}

      <div className="device-list">
        {devices.map(device => {
          const expired = new Date(device.expiresAt).getTime() <= Date.now()
          const revoked = !!device.revokedAt
          return (
            <article className="device-card" key={device.id}>
              <div className="device-card-main">
                <div className="device-card-title">
                  <h2>{device.label}</h2>
                  {device.isCurrentDevice && <span className="device-current">{t('devices.this')}</span>}
                  {revoked && <span className="device-status">{t('devices.revoked')}</span>}
                  {!revoked && expired && <span className="device-status">{t('devices.expired')}</span>}
                </div>
                <p>{device.browser} · {device.platform}</p>
                <dl>
                  <div><dt>{t('devices.lastActivity')}</dt><dd>{dateTime(device.lastSeenAt)}</dd></div>
                  <div><dt>{t('devices.lastSignin')}</dt><dd>{dateTime(device.lastAuthenticatedAt)}</dd></div>
                  <div><dt>{t('devices.registered')}</dt><dd>{dateTime(device.createdAt)}</dd></div>
                </dl>
              </div>
              <div className="device-card-action">
                <button type="button" className="device-revoke" disabled={revoked || expired || busyId === device.id} onClick={() => { void revoke(device) }}>
                  {busyId === device.id ? t('devices.revoking') : revoked ? t('devices.revoked') : expired ? t('devices.expired') : t('devices.revoke')}
                </button>
              </div>
            </article>
          )
        })}
      </div>
      <section className="passkey-panel" aria-labelledby="passkeys-title">
        <h2 id="passkeys-title">Passkeys and biometric sign-in</h2>
        {!passkeysEnabled
          ? <p>Passkeys are not currently enabled by this app's server configuration, or this browser does not support WebAuthn in its current context. Setting your app PIN does not register a passkey. Once enabled on the production HTTPS hostname, return here and choose Add a passkey to register this phone's fingerprint, face unlock or device PIN.</p>
          : <p>Use your phone or computer's built-in authenticator (fingerprint, face unlock or device PIN) to sign in. Your password remains available as a recovery method.</p>}
        <button type="button" disabled={!passkeysEnabled || passkeyBusy} onClick={() => { void addPasskey() }}>{passkeyBusy ? 'Please wait…' : 'Add a passkey on this device'}</button>
        {passkeysEnabled && passkeyAction && <form className="passkey-confirm" onSubmit={confirmPasskeyAction}>
          <label>Confirm account password
            <input type="password" autoComplete="current-password" required value={passkeyPassword} onChange={e => setPasskeyPassword(e.target.value)} />
          </label>
          <button type="submit" disabled={passkeyBusy || !passkeyPassword}>{passkeyBusy ? 'Please wait…' : 'Confirm'}</button>
          <button type="button" className="secondary" disabled={passkeyBusy} onClick={() => { setPasskeyAction(null); setPasskeyPassword('') }}>Cancel</button>
        </form>}
        {passkeysEnabled && (passkeys.length === 0 ? <p>No passkeys registered.</p> : <ul>{passkeys.map(passkey => <li key={passkey.credentialId}>
          <strong>{passkey.name || 'Passkey'}</strong> · Added {dateTime(passkey.createdAt)}{passkey.isBackedUp ? ' · Synced backup' : ''}
          <button type="button" className="device-revoke" disabled={passkeyBusy} onClick={() => { void deletePasskey(passkey) }}>Remove</button>
        </li>)}</ul>)}
      </section>
      <p className="devices-note">{t('devices.note')}</p>
    </section>
  )
}
