// Tiny cross-tree event bus so the always-mounted right-hand board panel can
// refresh when a routed page (e.g. a customer/car detail page) creates, edits or
// deletes a reminder. Pages call notifyBoardChanged(); the panel subscribes.

const EVENT = 'mobmek:board-changed'

export function notifyBoardChanged(): void {
  window.dispatchEvent(new Event(EVENT))
}

export function onBoardChanged(callback: () => void): () => void {
  window.addEventListener(EVENT, callback)
  return () => window.removeEventListener(EVENT, callback)
}
