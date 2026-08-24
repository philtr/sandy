import { Controller } from "@hotwired/stimulus"

export default class extends Controller {
  connect() {
    this.tick = this.tick.bind(this)
    this.timer = setInterval(this.tick, 1000)
    this.connectCable()
    this.tick()
  }

  disconnect() {
    clearInterval(this.timer)
    if (this.socket) this.socket.close()
  }

  connectCable() {
    const scheme = location.protocol === "https:" ? "wss" : "ws"
    this.socket = new WebSocket(`${scheme}://${location.host}/cable`)
    this.socket.addEventListener("open", () => this.socket.send(JSON.stringify({
      command: "subscribe",
      identifier: JSON.stringify({ channel: "FamilyChannel" })
    })))
    this.socket.addEventListener("message", event => this.receive(JSON.parse(event.data)))
    this.socket.addEventListener("close", () => {
      if (this.element.isConnected) this.reconnectTimer = setTimeout(() => this.connectCable(), 3000)
    })
  }

  receive(frame) {
    const message = frame.message
    if (!message?.device_id) return
    const card = this.element.querySelector(`[data-device-id="${message.device_id}"]`)
    if (!card) return

    if (message.type === "heartbeat") card.dataset.lastHeartbeatAt = new Date().toISOString()
    if (message.timer_state) {
      card.dataset.expiresAt = message.timer_state.expires_at || ""
      card.dataset.lastHeartbeatAt = new Date().toISOString()
    }
    this.renderCard(card)
  }

  tick() {
    this.element.querySelectorAll("[data-device-id]").forEach(card => this.renderCard(card))
  }

  renderCard(card) {
    if (card.dataset.revoked === "true") {
      card.dataset.status = "revoked"
      card.querySelector(".status").textContent = "revoked"
      return
    }

    const now = Date.now()
    const heartbeat = Date.parse(card.dataset.lastHeartbeatAt || "")
    const expires = Date.parse(card.dataset.expiresAt || "")
    const online = Number.isFinite(heartbeat) && now - heartbeat <= 75000
    const active = Number.isFinite(expires) && expires > now
    const status = online ? (active ? "active" : "expired") : "offline"
    card.dataset.status = status
    card.querySelector(".status").textContent = status

    if (!online) return
    const remaining = card.querySelector("[data-role='remaining']")
    if (!active) return remaining.textContent = "Time’s up"
    const seconds = Math.max(0, Math.ceil((expires - now) / 1000))
    const hours = Math.floor(seconds / 3600)
    const minutes = Math.floor((seconds % 3600) / 60)
    const secs = seconds % 60
    remaining.textContent = hours > 0 ? `${hours}h ${minutes}m ${secs}s` : `${minutes}m ${secs}s`
  }
}
