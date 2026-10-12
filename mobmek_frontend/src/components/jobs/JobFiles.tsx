import { useState } from 'react'
import { addJobFile, deleteJobFile, getJobFiles, jobFileUrl } from '@/api/jobFiles'
import { Button } from '@/components/ui/Button'
import { ConfirmDialog } from '@/components/ui/ConfirmDialog'
import { useToast } from '@/components/ui/toast'
import { useAsync } from '@/hooks/useAsync'
import type { FileDraft } from '@/lib/jobLineDrafts'
import type { JobFile } from '@/types'

/** 25 MB, same cap as job photos (see JobFilesController). */
const MAX_BYTES = 25 * 1024 * 1024

const formatSize = (bytes: number): string => {
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`
}

function FilePicker({ onFiles, disabled }: { onFiles: (files: File[]) => void; disabled?: boolean }) {
  const buttonClass = `inline-flex cursor-pointer items-center justify-center gap-1.5 rounded-md border border-slate-300 bg-white px-3.5 py-2 text-sm font-medium text-slate-700 transition-colors hover:bg-slate-50 ${
    disabled ? 'cursor-not-allowed opacity-60' : ''
  }`

  return (
    <label className={buttonClass}>
      ⬆ Upload file
      <input
        type="file"
        multiple
        className="hidden"
        disabled={disabled}
        onChange={(e) => {
          onFiles(Array.from(e.target.files ?? []))
          e.target.value = ''
        }}
      />
    </label>
  )
}

function FileRow({ fileName, sizeBytes, href, onRemove, disabled }: {
  fileName: string
  sizeBytes: number
  href?: string
  onRemove: () => void
  disabled?: boolean
}) {
  return (
    <li className="flex items-center justify-between gap-3 rounded-md border border-slate-300 px-3 py-2">
      <div className="flex min-w-0 items-center gap-2">
        <span aria-hidden className="text-lg">📄</span>
        {href ? (
          <a href={href} target="_blank" rel="noreferrer" className="truncate text-sm text-blue-600 hover:underline" title={fileName}>
            {fileName}
          </a>
        ) : (
          <span className="truncate text-sm text-slate-700" title={fileName}>
            {fileName}
          </span>
        )}
        <span className="shrink-0 text-xs text-slate-400">{formatSize(sizeBytes)}</span>
      </div>
      <Button variant="ghost" size="sm" className="shrink-0 text-red-600" disabled={disabled} onClick={onRemove}>
        Remove
      </Button>
    </li>
  )
}

/**
 * File picker for a job that doesn't exist yet (the New Job page). Files are held as
 * drafts and uploaded by the caller once the job has an id.
 */
export function JobFileDraftPicker({
  files,
  onAdd,
  onRemove,
  disabled,
}: {
  files: FileDraft[]
  onAdd: (files: File[]) => void
  onRemove: (key: string) => void
  disabled?: boolean
}) {
  const toast = useToast()

  const add = (picked: File[]) => {
    const tooBig = picked.filter((f) => f.size > MAX_BYTES)
    if (tooBig.length > 0) {
      toast.error(`${tooBig.map((f) => f.name).join(', ')}: each file must be 25 MB or smaller.`)
    }
    const accepted = picked.filter((f) => f.size <= MAX_BYTES && f.size > 0)
    if (accepted.length > 0) onAdd(accepted)
  }

  return (
    <div className="space-y-3">
      <FilePicker onFiles={add} disabled={disabled} />
      {files.length === 0 ? (
        <p className="text-sm text-slate-500">No files yet. They're uploaded when the job is created.</p>
      ) : (
        <ul className="space-y-2">
          {files.map((f) => (
            <FileRow
              key={f.key}
              fileName={f.file.name}
              sizeBytes={f.file.size}
              disabled={disabled}
              onRemove={() => onRemove(f.key)}
            />
          ))}
        </ul>
      )}
    </div>
  )
}

/** Files on a saved job: loads what's stored, and uploads/removes against the API. */
export function JobFilesSection({ jobId }: { jobId: string }) {
  const toast = useToast()
  const [busy, setBusy] = useState(false)
  const [deleting, setDeleting] = useState<JobFile | null>(null)
  const { data, loading, error, reload } = useAsync(() => getJobFiles(jobId), [jobId])

  const upload = async (files: File[]) => {
    if (files.length === 0) return
    setBusy(true)
    try {
      for (const file of files) {
        if (file.size === 0 || file.size > MAX_BYTES) {
          toast.error(`${file.name}: each file must be between 1 byte and 25 MB.`)
          continue
        }
        await addJobFile(jobId, file)
      }
      reload()
      toast.success(files.length === 1 ? 'File added' : 'Files added')
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
      await deleteJobFile(jobId, deleting.id)
      reload()
      toast.success('File removed')
    } catch (err) {
      toast.error(err instanceof Error ? err.message : String(err))
    } finally {
      setBusy(false)
      setDeleting(null)
    }
  }

  return (
    <section className="space-y-3 rounded-lg border border-slate-200 bg-white p-5">
      <h2 className="text-sm font-semibold uppercase tracking-wide text-slate-500">Files</h2>
      <FilePicker onFiles={(files) => void upload(files)} disabled={busy} />
      {loading && !data && <p className="text-sm text-slate-500">Loading files…</p>}
      {error && <p className="text-sm text-red-600">Could not load this job's files.</p>}
      {data && data.length === 0 && <p className="text-sm text-slate-500">No files on this job.</p>}
      {data && data.length > 0 && (
        <ul className="space-y-2">
          {data.map((f) => (
            <FileRow
              key={f.id}
              fileName={f.fileName}
              sizeBytes={f.sizeBytes}
              href={jobFileUrl(jobId, f.id)}
              disabled={busy}
              onRemove={() => setDeleting(f)}
            />
          ))}
        </ul>
      )}
      <ConfirmDialog
        open={deleting !== null}
        title="Remove file"
        message={deleting ? `Remove "${deleting.fileName}"? This can't be undone.` : ''}
        confirmLabel="Remove"
        onConfirm={handleDelete}
        onCancel={() => setDeleting(null)}
      />
    </section>
  )
}
