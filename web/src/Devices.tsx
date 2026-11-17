import { useCallback, useEffect, useState } from 'react'
import { api, ApiError } from './api'
import { getLanguage, t } from './i18n'
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
      <p className="devices-note">{t('devices.note')}</p>
    </section>
  )
}
