import { useCallback, useEffect, useState } from 'react'
import { api, ApiError } from './api'
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
  return new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value))
}

export function DevicesPage({ onCurrentDeviceRevoked }: Props) {
  const [devices, setDevices] = useState<Device[]>([])
  const [loading, setLoading] = useState(true)
  const [busyId, setBusyId] = useState<string | null>(null)
  const [error, setError] = useState('')

  const load = useCallback(async () => {
    setError('')
    setLoading(true)
    try {
      setDevices(await api<Device[]>('GET', '/auth/devices'))
    } catch (reason) {
      setError(reason instanceof ApiError ? reason.message : 'Could not load signed-in devices. Check your connection and retry.')
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => { void load() }, [load])

  async function revoke(device: Device) {
    const message = device.isCurrentDevice
      ? 'Revoke this device? You will be signed out on this browser.'
      : 'Revoke this device? Its next request to RBOS will require sign-in again.'
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
      setError(reason instanceof ApiError ? reason.message : 'Could not revoke this device. Please retry.')
    } finally {
      setBusyId(null)
    }
  }

  return (
    <section className="devices-page" aria-labelledby="devices-title">
      <header className="devices-heading">
        <div>
          <h1 id="devices-title">Signed-in devices</h1>
          <p>Review the browsers used to access this account. Revoking a device blocks its saved sessions the next time they contact RBOS.</p>
        </div>
        <button type="button" onClick={() => { void load() }} disabled={loading}>Refresh</button>
      </header>

      {error && <p role="alert" className="devices-error">{error}</p>}
      {loading && <p role="status">Loading devices…</p>}
      {!loading && devices.length === 0 && <p>No devices have been registered for this account yet.</p>}

      <div className="device-list">
        {devices.map(device => {
          const expired = new Date(device.expiresAt).getTime() <= Date.now()
          const revoked = !!device.revokedAt
          return (
            <article className="device-card" key={device.id}>
              <div className="device-card-main">
                <div className="device-card-title">
                  <h2>{device.label}</h2>
                  {device.isCurrentDevice && <span className="device-current">This device</span>}
                  {revoked && <span className="device-status">Revoked</span>}
                  {!revoked && expired && <span className="device-status">Expired</span>}
                </div>
                <p>{device.browser} · {device.platform}</p>
                <dl>
                  <div><dt>Last activity</dt><dd>{dateTime(device.lastSeenAt)}</dd></div>
                  <div><dt>Last sign-in</dt><dd>{dateTime(device.lastAuthenticatedAt)}</dd></div>
                  <div><dt>Registered</dt><dd>{dateTime(device.createdAt)}</dd></div>
                </dl>
              </div>
              <div className="device-card-action">
                <button type="button" className="device-revoke" disabled={revoked || expired || busyId === device.id} onClick={() => { void revoke(device) }}>
                  {busyId === device.id ? 'Revoking…' : revoked ? 'Revoked' : expired ? 'Expired' : 'Revoke device'}
                </button>
              </div>
            </article>
          )
        })}
      </div>
      <p className="devices-note">A device label describes a browser profile; it does not prove the physical identity of a phone or computer. A device that is offline cannot receive revocation until it reconnects.</p>
    </section>
  )
}
