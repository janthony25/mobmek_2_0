// Live in-browser camera capture.
//
// An <input type="file" capture="environment"> only opens the camera on phones and
// tablets — desktop browsers ignore `capture` and show the file picker instead. These
// helpers drive a real camera stream, which works on laptops as well.

/**
 * True when the browser can open a live camera stream. `mediaDevices` is only exposed in
 * a secure context, so this is false over plain HTTP on anything but localhost — callers
 * must keep a file-picker fallback for that case.
 */
export const cameraSupported = (): boolean => Boolean(navigator.mediaDevices?.getUserMedia)

/** "photo-20260827-142530.jpg" — sortable, and unique enough for one-per-second capture. */
export function photoFileName(now: Date = new Date()): string {
  const pad = (n: number) => String(n).padStart(2, '0')
  const date = `${now.getFullYear()}${pad(now.getMonth() + 1)}${pad(now.getDate())}`
  const time = `${pad(now.getHours())}${pad(now.getMinutes())}${pad(now.getSeconds())}`
  return `photo-${date}-${time}.jpg`
}

/** Grabs the frame currently on screen as a JPEG File, at the camera's native resolution. */
export function captureFrame(video: HTMLVideoElement, fileName: string): Promise<File> {
  const canvas = document.createElement('canvas')
  canvas.width = video.videoWidth
  canvas.height = video.videoHeight

  const context = canvas.getContext('2d')
  if (!context || canvas.width === 0 || canvas.height === 0) {
    return Promise.reject(new Error('The camera image was not ready yet — try again.'))
  }
  context.drawImage(video, 0, 0)

  return new Promise((resolve, reject) => {
    canvas.toBlob(
      (blob) =>
        blob
          ? resolve(new File([blob], fileName, { type: 'image/jpeg' }))
          : reject(new Error('Could not read the camera image.')),
      'image/jpeg',
      0.9,
    )
  })
}

/** Turns a getUserMedia rejection into something worth showing a mechanic. */
export function describeCameraError(err: unknown): string {
  const name = err instanceof DOMException ? err.name : ''
  switch (name) {
    case 'NotAllowedError':
    case 'SecurityError':
      return 'Camera access was blocked. Allow camera access for this site in your browser, then try again.'
    case 'NotFoundError':
    case 'OverconstrainedError':
      return 'No camera was found on this device.'
    case 'NotReadableError':
      return 'The camera is already in use by another app.'
    default:
      return err instanceof Error ? err.message : 'The camera could not be started.'
  }
}
