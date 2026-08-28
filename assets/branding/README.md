# Sandy branding assets

`sandy-app-icon-master.png` is the canonical 1024×1024 raster master for the
Sandy timer mark. Keep the `Sandy` wordmark as live text in the platform's
default UI font rather than baking it into image assets.

Derived application assets:

- `server/public/icon-512.png` and `icon-192.png` — PWA install icons
- `server/public/apple-touch-icon.png` — 180×180 Apple touch icon
- `server/public/favicon-32.png` — browser favicon
- `server/public/icon.png` — general-purpose 512×512 compatibility icon
- `agent/src/Sandy.Agent/Resources/Sandy.png` — in-app WPF image resource
- `agent/src/Sandy.Agent/Resources/Sandy.ico` — Windows executable icon with
  16, 24, 32, 48, 64, 128, and 256 px frames

When replacing the master, regenerate every derived asset together and bump
the icon URL revision in the Rails layout, PWA manifest, service worker, and
metadata test so existing installations do not retain stale artwork.
