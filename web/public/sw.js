// Minimal service worker: makes the app installable. It never caches anything,
// so billing, stock and ledger data (and new deployments) are always fresh.
self.addEventListener('install', () => self.skipWaiting())
self.addEventListener('activate', e => e.waitUntil(self.clients.claim()))
self.addEventListener('fetch', () => {})
