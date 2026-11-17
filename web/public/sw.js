const CACHE_PREFIX = 'rbos-shell-'
const CACHE_NAME = CACHE_PREFIX + 'v1'
const APP_SHELL = ['/', '/index.html', '/manifest.webmanifest', '/logo-mark.svg']

self.addEventListener('install', event => {
  event.waitUntil((async () => {
    const cache = await caches.open(CACHE_NAME)
    await cache.addAll(APP_SHELL)
    await self.skipWaiting()
  })())
})

self.addEventListener('activate', event => {
  event.waitUntil((async () => {
    const names = await caches.keys()
    await Promise.all(names.filter(name => name.startsWith(CACHE_PREFIX) && name !== CACHE_NAME).map(name => caches.delete(name)))
    await self.clients.claim()
  })())
})

self.addEventListener('message', event => {
  if (event.data?.type === 'RBOS_SKIP_WAITING') self.skipWaiting()
})

self.addEventListener('fetch', event => {
  const request = event.request
  if (request.method !== 'GET') return
  const url = new URL(request.url)
  if (url.origin !== self.location.origin) return
  if (url.pathname === '/api' || url.pathname.startsWith('/api/')) return
  if (request.headers.has('authorization')) return

  if (request.mode === 'navigate') {
    event.respondWith((async () => {
      try {
        const response = await fetch(request)
        if (response.ok && response.headers.get('Cache-Control')?.includes('no-store') !== true) {
          const cache = await caches.open(CACHE_NAME)
          await cache.put('/index.html', response.clone())
        }
        return response
      } catch {
        return (await caches.match('/index.html')) || (await caches.match('/'))
      }
    })())
    return
  }

  if (['script', 'style', 'font', 'image'].includes(request.destination)) {
    event.respondWith((async () => {
      const cache = await caches.open(CACHE_NAME)
      const cached = await cache.match(request)
      if (cached) return cached
      try {
        const response = await fetch(request)
        if (response.ok && response.type === 'basic' && !response.headers.get('Cache-Control')?.includes('no-store')) {
          await cache.put(request, response.clone())
        }
        return response
      } catch {
        return new Response('Offline asset unavailable', { status: 503, statusText: 'Offline' })
      }
    })())
  }
})
