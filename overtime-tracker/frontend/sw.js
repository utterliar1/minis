const CACHE_NAME = 'ot-tracker-v18';
const STATIC_ASSETS = [
  '/',
  '/index.html',
  '/css/style.css?v=18',
  '/js/utils.js?v=18',
  '/js/auth.js?v=18',
  '/js/stats.js?v=18',
  '/js/records.js?v=18',
  '/js/clock.js?v=18',
  '/js/admin.js?v=18',
  '/js/app.js?v=18',
  '/使用指南.html?v=18',
  '/管理员使用指南.html?v=18',
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
