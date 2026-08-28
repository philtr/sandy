const CACHE = "sandy-shell-v4"
const APP_ASSETS = [
  "/apple-touch-icon.png?v=2",
  "/favicon-32.png?v=2",
  "/icon-192.png?v=2",
  "/icon-512.png?v=2"
]

self.addEventListener("install", event => {
  event.waitUntil(caches.open(CACHE).then(cache => cache.addAll(APP_ASSETS)))
})

self.addEventListener("activate", event => {
  event.waitUntil(
    caches.keys()
      .then(keys => Promise.all(keys.filter(key => key.startsWith("sandy-shell-") && key !== CACHE).map(key => caches.delete(key))))
      .then(() => self.clients.claim())
  )
})

self.addEventListener("fetch", event => {
  if (event.request.method !== "GET" || event.request.mode === "navigate") return
  event.respondWith(caches.match(event.request).then(cached => cached || fetch(event.request)))
})
