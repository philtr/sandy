const CACHE = "sandy-shell-v1"

self.addEventListener("install", event => {
  event.waitUntil(caches.open(CACHE).then(cache => cache.addAll(["/icon.png", "/icon.svg"])))
})

self.addEventListener("activate", event => event.waitUntil(self.clients.claim()))

self.addEventListener("fetch", event => {
  if (event.request.method !== "GET" || event.request.mode === "navigate") return
  event.respondWith(caches.match(event.request).then(cached => cached || fetch(event.request)))
})
