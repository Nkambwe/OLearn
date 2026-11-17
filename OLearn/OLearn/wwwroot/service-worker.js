// service-worker.js - loads the application shell offline (NFR-01). Resource files are NOT cached here;
// they are kept encrypted in IndexedDB under a lease.
const CACHE = 'olearn-shell-v1';
const SHELL = ['/', '/Dashboard', '/Courses', '/Resources', '/Progress', '/Updates', '/Settings', '/Help',
  '/css/site.css', '/js/app.js', '/js/db.js', '/js/clock.js', '/js/leasecrypto.js', '/js/audit.js', '/js/offline.js',
  '/js/sync.js', '/js/resources.js', '/js/pages.js', '/js/ui.js', '/js/diagnostics.js', '/js/lease-public-key.js',
  '/manifest.json', '/offline.html', '/icons/icon-192.png', '/icons/icon-512.png'];

self.addEventListener('install', (event) => {
  event.waitUntil((async () => {
    const cache = await caches.open(CACHE);
    for (const url of SHELL) {
      try {
        const res = await fetch(url, { credentials: 'same-origin' });
        if (res.ok && !res.redirected) await cache.put(url, res); // never cache a redirect to the sign-in page
      } catch { /* cached on first visit instead */ }
    }
    self.skipWaiting();
  })());
});

self.addEventListener('activate', (event) => {
  event.waitUntil((async () => {
    for (const k of await caches.keys()) if (k !== CACHE) await caches.delete(k);
    await self.clients.claim();
  })());
});

self.addEventListener('fetch', (event) => {
  const req = event.request;
  const url = new URL(req.url);
  if (req.method !== 'GET' || url.origin !== location.origin) return;
  if (url.pathname.startsWith('/api/') || url.pathname.startsWith('/Account') || url.pathname.startsWith('/Admin')) return;
  event.respondWith((async () => {
    const cache = await caches.open(CACHE);
    try {
      const res = await Promise.race([fetch(req), new Promise((_, rej) => setTimeout(() => rej(new Error('slow')), 4000))]);
      if (res.ok && !res.redirected) cache.put(req, res.clone());
      return res;
    } catch {
      const hit = await cache.match(req, { ignoreSearch: true });
      if (hit) return hit;
      if (req.mode === 'navigate') return (await cache.match('/offline.html')) || new Response('Offline', { status: 503 });
      return new Response('', { status: 504 });
    }
  })());
});
