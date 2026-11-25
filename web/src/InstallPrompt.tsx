import { useEffect, useState } from 'react'

type InstallEvent = Event & { prompt: () => Promise<void>; userChoice: Promise<{ outcome: string }> }

const DISMISS_KEY = 'rbos.install.dismissed'

function isStandalone() {
  return window.matchMedia('(display-mode: standalone)').matches || (navigator as { standalone?: boolean }).standalone === true
}

function isIos() {
  return /iphone|ipad|ipod/i.test(navigator.userAgent)
}

export function InstallPrompt() {
  const [event, setEvent] = useState<InstallEvent | null>(null)
  const [hidden, setHidden] = useState(() => isStandalone() || localStorage.getItem(DISMISS_KEY) === '1')

  useEffect(() => {
    const onPrompt = (e: Event) => { e.preventDefault(); setEvent(e as InstallEvent) }
    const onInstalled = () => setHidden(true)
    window.addEventListener('beforeinstallprompt', onPrompt)
    window.addEventListener('appinstalled', onInstalled)
    return () => { window.removeEventListener('beforeinstallprompt', onPrompt); window.removeEventListener('appinstalled', onInstalled) }
  }, [])

  function dismiss() { localStorage.setItem(DISMISS_KEY, '1'); setHidden(true) }

  async function install() {
    if (!event) return
    await event.prompt()
    await event.userChoice
    setEvent(null)
    setHidden(true)
  }

  if (hidden || (!event && !isIos())) return null

  return (
    <div className="install-prompt" role="region" aria-label="Install app">
      <p>
        {event
          ? 'Add Dhandha Mitra to your home screen to open it like an app.'
          : 'To add this app to your home screen: tap the Share button, then "Add to Home Screen".'}
      </p>
      <div className="install-actions">
        {event && <button type="button" onClick={install}>Install app</button>}
        <button type="button" className="secondary" onClick={dismiss}>Not now</button>
      </div>
    </div>
  )
}
