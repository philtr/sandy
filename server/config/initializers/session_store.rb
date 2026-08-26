# Parent sessions need to survive a browser or installed-PWA restart. Rails' default
# cookie session is a session cookie, which mobile browsers can discard on close.
Rails.application.config.session_store :cookie_store,
  key: "_server_session",
  expire_after: 30.days,
  secure: Rails.env.production?,
  httponly: true,
  same_site: :lax
