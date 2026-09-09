import { useEffect, useRef, useState } from 'react'
import { Button } from './Button'
import { Modal } from './Modal'
import { captureFrame, describeCameraError, photoFileName } from '@/lib/camera'

interface CameraCaptureModalProps {
  open: boolean
  onClose: () => void
  /** Called with the captured JPEG. The caller decides whether to keep the camera open. */
  onCapture: (file: File) => void
  title?: string
}

/**
 * Takes a photo from a live camera stream, so "Take photo" opens a camera on a laptop
 * too — not just on a phone or tablet, which is all an `<input capture>` can manage.
 * The stream is stopped whenever the dialog closes or the facing camera changes, so the
 * device's camera light never stays on behind a closed dialog.
 */
export function CameraCaptureModal({ open, onClose, onCapture, title = 'Take photo' }: CameraCaptureModalProps) {
  const videoRef = useRef<HTMLVideoElement>(null)
  const [facingMode, setFacingMode] = useState<'environment' | 'user'>('environment')
  const [hasMultipleCameras, setHasMultipleCameras] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [ready, setReady] = useState(false)
  const [busy, setBusy] = useState(false)

  useEffect(() => {
    if (!open) return

    // Held in a local rather than read off the ref in cleanup: by the time cleanup
    // runs the dialog is closing and the ref has already been detached.
    const video = videoRef.current
    let stream: MediaStream | null = null
    let cancelled = false
    setError(null)
    setReady(false)

    navigator.mediaDevices
      // "ideal" rather than "exact" everywhere: a laptop with one fixed webcam should
      // still open rather than fail an unsatisfiable rear-camera constraint.
      .getUserMedia({
        video: { facingMode, width: { ideal: 1920 }, height: { ideal: 1080 } },
        audio: false,
      })
      .then(async (opened) => {
        stream = opened
        if (cancelled) return
        if (video) video.srcObject = opened
        setReady(true)

        // Only worth offering a flip button when there's something to flip to. Device
        // enumeration is best-effort: a browser that refuses it just hides the button.
        try {
          const devices = await navigator.mediaDevices.enumerateDevices()
          if (!cancelled) {
            setHasMultipleCameras(devices.filter((d) => d.kind === 'videoinput').length > 1)
          }
        } catch {
          setHasMultipleCameras(false)
        }
      })
      .catch((err: unknown) => {
        if (!cancelled) setError(describeCameraError(err))
      })

    return () => {
      cancelled = true
      stream?.getTracks().forEach((track) => track.stop())
      if (video) video.srcObject = null
    }
  }, [open, facingMode])

  const take = async () => {
    if (!videoRef.current) return
    setBusy(true)
    try {
      onCapture(await captureFrame(videoRef.current, photoFileName()))
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err))
    } finally {
      setBusy(false)
    }
  }

  return (
    <Modal open={open} title={title} onClose={onClose} maxWidth="max-w-xl">
      <div className="space-y-3">
        {error && <p className="rounded-md bg-red-50 px-3 py-2 text-sm text-red-700">{error}</p>}

        {/* Kept mounted even while starting or errored so the ref is there when the
            stream arrives. `playsInline` stops iOS Safari going fullscreen. */}
        <video
          ref={videoRef}
          autoPlay
          playsInline
          muted
          className={`w-full rounded-md bg-slate-900 ${error ? 'hidden' : ''}`}
        />

        {!ready && !error && <p className="text-sm text-slate-500">Starting the camera…</p>}

        <div className="flex justify-end gap-2 border-t border-slate-100 pt-3">
          {hasMultipleCameras && !error && (
            <Button
              variant="secondary"
              disabled={busy}
              onClick={() => setFacingMode((m) => (m === 'environment' ? 'user' : 'environment'))}
            >
              🔄 Flip camera
            </Button>
          )}
          <Button variant="secondary" onClick={onClose} disabled={busy}>
            Cancel
          </Button>
          <Button onClick={() => void take()} disabled={!ready || busy || Boolean(error)}>
            {busy ? 'Capturing…' : '📸 Capture'}
          </Button>
        </div>
      </div>
    </Modal>
  )
}
