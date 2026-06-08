const CACHE_NAME = 'ot-tracker-v6';
const STATIC_ASSETS = [
  '/',
  '/index.html',
  '/css/style.css?v=6',
  '/js/utils.js?v=6',
  '/js/auth.js?v=6',
  '/js/stats.js?v=6',
  '/js/records.js?v=6',
  '/js/clock.js?v=6',
  '/js/admin.js?v=6',
  '/js/app.js?v=6',
  '/使用指南.html?v=6',
  '/管理员使用指南.html?v=6',
  '/manifest.json',
  '/icon-192.png',
  '/icon-512.png'
];

// Install: cache static assets
self.addEventListener('install', e => {
  e.waitUntil(
    caches.open(CACHE_NAME)
      .then(cache => cache.addAll(STATIC_ASSETS))
      .then(() => self.skipWaiting())
  );
});

// Activate: clean old caches
self.addEventListener('activate', e => {
  e.waitUntil(
    caches.keys().then(keys =>
      Promise.all(keys.filter(k => k !== CACHE_NAME).map(k => caches.delete(k)))
    ).then(() => self.clients.claim())
  );
});

// Fetch: network first, fallback to cache (for API calls always go network)
self.addEventListener('fetch', e => {
  const url = new URL(e.request.url);

  // API requests: always network
  if (url.pathname.startsWith('/api/')) {
    e.respondWith(fetch(e.request));
    return;
  }

  // Static assets: cache first, then network
  e.respondWith(
    caches.match(e.request)
      .then(cached => cached || fetch(e.request).then(resp => {
        // Cache new static assets
        if (resp.status === 200 && resp.type === 'basic') {
          const clone = resp.clone();
          caches.open(CACHE_NAME).then(cache => cache.put(e.request, clone));
        }
        return resp;
      }))
      .catch(() => {
        // Offline fallback: return cached index.html
        if (e.request.destination === 'document') {
          return caches.match('/index.html');
        }
      })
  );
});
