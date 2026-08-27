import { Controller } from "@hotwired/stimulus"

export default class extends Controller {
  static targets = ["button", "source"]

  async copy() {
    if (!this.hasSourceTarget) return

    const text = this.sourceTarget.textContent.trim()
    if (!text) return

    try {
      await navigator.clipboard.writeText(text)
    } catch (_error) {
      this.copyWithTextarea(text)
    }

    this.buttonTarget.textContent = "Copied"
    clearTimeout(this.resetTimer)
    this.resetTimer = setTimeout(() => { this.buttonTarget.textContent = "Copy diagnostics" }, 2000)
  }

  disconnect() {
    clearTimeout(this.resetTimer)
  }

  copyWithTextarea(text) {
    const textarea = document.createElement("textarea")
    textarea.value = text
    textarea.setAttribute("readonly", "")
    textarea.style.position = "fixed"
    textarea.style.opacity = "0"
    document.body.appendChild(textarea)
    textarea.select()
    document.execCommand("copy")
    textarea.remove()
  }
}
