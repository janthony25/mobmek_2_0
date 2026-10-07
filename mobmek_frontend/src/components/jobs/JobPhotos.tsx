import { useState } from 'react'
import { addJobPhoto, deleteJobPhoto, getJobPhotos, jobPhotoUrl } from '@/api/jobPhotos'
import { Button } from '@/components/ui/Button'
import { ConfirmDialog } from '@/components/ui/ConfirmDialog'
import { useToast } from '@/components/ui/toast'
import { CameraCaptureModal } from '@/components/ui/CameraCaptureModal'
import { useAsync } from '@/hooks/useAsync'
import { cameraSupported } from '@/lib/camera'
import type { PhotoDraft } from '@/lib/jobLineDrafts'
import type { JobPhoto } from '@/types'

/** Photos accepted for upload — kept in step with the API's image-only check. */
const ACCEPT = 'image/*'
const MAX_BYTES = 25 * 1024 * 1024

/**
 * "Upload photo" always opens the file picker. "Take photo" opens a live camera via
 * `getUserMedia`, which is the only thing that works on a laptop — an
 * `<input capture="environment">` only opens the camera on phones and tablets, and is
 * silently ignored everywhere else. That input is still the fallback for browsers with
 * no camera API, which includes any page served over plain HTTP (not a secure context).
 */
function AddPhotoButtons({
  onFiles,
  disabled,
}: {
  onFiles: (files: File[]) => void
  disabled?: boolean
}) {
  const [cameraOpen, setCameraOpen] = useState(false)
  const liveCamera = cameraSupported()

  const buttonClass = `inline-flex cursor-pointer items-center justify-center gap-1.5 rounded-md border border-slate-300 bg-white px-3.5 py-2 text-sm font-medium text-slate-700 transition-colors hover:bg-slate-50 ${
    disabled ? 'cursor-not-allowed opacity-60' : ''
  }`

  const filePicker = (label: string, capture: boolean) => (
    <label className={buttonClass}>
      {label}
      <input
        type="file"
        accept={ACCEPT}
        multiple={!capture}
        {...(capture ? { capture: 'environment' as const } : {})}
        className="hidden"
        disabled={disabled}
        onChange={(e) => {
          onFiles(Array.from(e.target.files ?? []))
          e.target.value = ''
        }}
      />
    </label>
  )

  return (
    <div className="flex flex-wrap gap-2">
      {filePicker('⬆ Upload photo', false)}
      {liveCamera ? (
        <button type="button" className={buttonClass} disabled={disabled} onClick={() => setCameraOpen(true)}>
          📷 Take photo
        </button>
      ) : (
        filePicker('📷 Take photo', true)
      )}
      <CameraCaptureModal
        open={cameraOpen}
        onClose={() => setCameraOpen(false)}
        onCapture={(file) => {
          setCameraOpen(false)
          onFiles([file])
        }}
      />
    </div>
  )
}

function Thumbnail({
  src,
  label,
  onRemove,
  disabled,
  href,
}: {
  src: string
  label: string
  onRemove: () => void
  disabled?: boolean
  href?: string
}) {
  const image = (
    <img src={src} alt={label} className="h-28 w-full rounded-md border border-slate-200 object-cover" />
  )

  return (
    <li className="space-y-1">
      {href ? (
        <a href={href} target="_blank" rel="noreferrer" title={label}>
          {image}
        </a>
      ) : (
        image
      )}
      <div className="flex items-center justify-between gap-1">
        <span className="truncate text-xs text-slate-500" title={label}>
          {label}
        </span>
        <Button variant="ghost" size="sm" className="shrink-0 text-red-600" disabled={disabled} onClick={onRemove}>
          Remove
        </Button>
      </div>
    </li>
  )
}

/**
 * Photo picker for a job that doesn't exist yet (the New Job page). Files are held as
 * drafts and uploaded by the caller once the job has an id.
 */
export function JobPhotoDraftPicker({
  photos,
  onAdd,
  onRemove,
  disabled,
}: {
  photos: PhotoDraft[]
  onAdd: (files: File[]) => void
  onRemove: (key: string) => void
  disabled?: boolean
}) {
  const toast = useToast()

  const add = (files: File[]) => {
    const tooBig = files.filter((f) => f.size > MAX_BYTES)
    if (tooBig.length > 0) {
      toast.error(`${tooBig.map((f) => f.name).join(', ')}: each photo must be 25 MB or smaller.`)
    }
    const accepted = files.filter((f) => f.size <= MAX_BYTES && f.size > 0)
    if (accepted.length > 0) onAdd(accepted)
  }

  return (
    <div className="space-y-3">
      <AddPhotoButtons onFiles={add} disabled={disabled} />
      {photos.length === 0 ? (
        <p className="text-sm text-slate-500">No photos yet. They're uploaded when the job is created.</p>
      ) : (
        <ul className="grid grid-cols-2 gap-3 sm:grid-cols-3">
          {photos.map((p) => (
            <Thumbnail
              key={p.key}
              src={p.previewUrl}
              label={p.file.name}
              disabled={disabled}
              onRemove={() => onRemove(p.key)}
            />
          ))}
        </ul>
      )}
    </div>
  )
}

/** Photos on a saved job: loads what's stored, and uploads/removes against the API. */
export function JobPhotosSection({ jobId }: { jobId: string }) {
  const toast = useToast()
  const [busy, setBusy] = useState(false)
  const [deleting, setDeleting] = useState<JobPhoto | null>(null)
  const { data, loading, error, reload } = useAsync(() => getJobPhotos(jobId), [jobId])

  const upload = async (files: File[]) => {
    if (files.length === 0) return
    setBusy(true)
    try {
      for (const file of files) {
        if (file.size === 0 || file.size > MAX_BYTES) {
          toast.error(`${file.name}: each photo must be between 1 byte and 25 MB.`)
          continue
        }
        await addJobPhoto(jobId, file)
      }
      reload()
      toast.success(files.length === 1 ? 'Photo added' : 'Photos added')
    } catch (err) {
      toast.error(err instanceof Error ? err.message : String(err))
    } finally {
      setBusy(false)
    }
  }

  const handleDelete = async () => {
    if (!deleting) return
    setBusy(true)
    try {
      await deleteJobPhoto(jobId, deleting.id)
      reload()
      toast.success('Photo removed')
    } catch (err) {
      toast.error(err instanceof Error ? err.message : String(err))
    } finally {
      setBusy(false)
      setDeleting(null)
    }
  }

  return (
    <section className="space-y-3 rounded-lg border border-slate-200 bg-white p-5">
      <h2 className="text-sm font-semibold uppercase tracking-wide text-slate-500">Photos</h2>
      <AddPhotoButtons onFiles={(files) => void upload(files)} disabled={busy} />
      {loading && !data && <p className="text-sm text-slate-500">Loading photos…</p>}
      {error && <p className="text-sm text-red-600">Could not load this job's photos.</p>}
      {data && data.length === 0 && <p className="text-sm text-slate-500">No photos on this job.</p>}
      {data && data.length > 0 && (
        <ul className="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-4">
          {data.map((p) => (
            <Thumbnail
              key={p.id}
              src={jobPhotoUrl(jobId, p.id)}
              href={jobPhotoUrl(jobId, p.id)}
              label={p.fileName}
              disabled={busy}
              onRemove={() => setDeleting(p)}
            />
          ))}
        </ul>
      )}
      <ConfirmDialog
        open={deleting !== null}
        title="Remove photo"
        message={deleting ? `Remove "${deleting.fileName}"? This can't be undone.` : ''}
        confirmLabel="Remove"
        onConfirm={handleDelete}
        onCancel={() => setDeleting(null)}
      />
    </section>
  )
}
