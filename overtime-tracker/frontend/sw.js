const CACHE_NAME = 'ot-tracker-v1.0';
const STATIC_ASSETS = [
  '/css/style.css?v=1.0',
  '/js/utils.js?v=1.0',
  '/js/auth.js?v=1.0',
  '/js/stats.js?v=1.0',
  '/js/records.js?v=1.0',
  '/js/clock.js?v=1.0',
  '/js/admin.js?v=1.0',
  '/js/app.js?v=1.0',
  '/使用指南.html?v=1.0',
  '/管理员使用指南.html?v=1.0',
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

// Fetch: API and document requests always try network first.
self.addEventListener('fetch', e => {
  const url = new URL(e.request.url);

  // API requests: always network
  if (url.pathname.startsWith('/api/')) {
    e.respondWith(fetch(e.request));
    return;
  }

  if (e.request.mode === 'navigate' || e.request.destination === 'document') {
    e.respondWith(
      fetch(e.request)
        .then(resp => {
          if (resp.status === 200 && resp.type === 'basic') {
            const clone = resp.clone();
            caches.open(CACHE_NAME).then(cache => cache.put('/index.html', clone));
          }
          return resp;
        })
        .catch(() => caches.match('/index.html'))
    );
    return;
  }

  // Static assets: cache first, then network.
  e.respondWith(
    caches.match(e.request)
      .then(cached => cached || fetch(e.request).then(resp => {
        if (resp.status === 200 && resp.type === 'basic') {
          const clone = resp.clone();
          caches.open(CACHE_NAME).then(cache => cache.put(e.request, clone));
        }
        return resp;
      }))
      .catch(() => {
        return caches.match(e.request);
      })
  );
});
