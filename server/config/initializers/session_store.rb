# Parent sessions must survive browser and installed-PWA restarts. Rails' default
# cookie session can be discarded by mobile browsers when they close.
Rails.application.config.session_store :cookie_store,
  key: "_server_session",
  expire_after: 30.days,
  secure: Rails.env.production?,
  httponly: true,
  same_site: :lax
