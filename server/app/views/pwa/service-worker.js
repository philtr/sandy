const CACHE = "sandy-shell-v2"
const APP_ASSETS = [
  "/apple-touch-icon.png",
  "/favicon-32.png",
  "/icon.svg",
  "/icon-192.png",
  "/icon-512.png"
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
