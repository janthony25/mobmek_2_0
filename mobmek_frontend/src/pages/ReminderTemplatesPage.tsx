import {
  createReminderTemplate,
  deleteReminderTemplate,
  getReminderTemplates,
  updateReminderTemplate,
} from '@/api/reminderTemplates'
import { CrudSection } from '@/components/crud/CrudSection'
import type { FieldSchema } from '@/components/crud/types'
import { useAuth } from '@/contexts/AuthContext'
import { orDash } from '@/lib/format'
import type { ReminderTemplate, ReminderTemplateRequest } from '@/types'

const fields: FieldSchema[] = [
  { name: 'name', label: 'Name', type: 'text', required: true, placeholder: 'e.g. Next WOF' },
  {
    name: 'defaultIntervalMonths',
    label: 'Default interval (months)',
    type: 'number',
    min: 1,
    max: 120,
    help: 'Pre-fills a reminder’s due date to this many months from today (e.g. 12 for WOF).',
  },
  { name: 'description', label: 'Description', type: 'textarea' },
]

export function ReminderTemplatesPage() {
  // Everyone can read these (picked when adding a reminder on a job/car); only Admin can
  // change the presets themselves — mirrors the API's method-level [Authorize(Roles = "Admin")]
  // on create/update/delete.
  const { isAdmin } = useAuth()

  return (
    <CrudSection<ReminderTemplate>
      resourceName="Reminder Template"
      description={
        isAdmin
          ? 'Reusable reminder presets (e.g. Next WOF, Next Service) you can pick when adding a reminder.'
          : 'Reusable reminder presets you can pick when adding a reminder. Only an Admin can add, edit, or remove presets.'
      }
      load={getReminderTemplates}
      getId={(t) => t.id}
      rowLabel={(t) => t.name}
      columns={[
        { header: 'Name', cell: (t) => t.name, className: 'font-medium text-slate-900' },
        {
          header: 'Default interval',
          cell: (t) => (t.defaultIntervalMonths ? `${t.defaultIntervalMonths} mo` : '—'),
        },
        { header: 'Description', cell: (t) => orDash(t.description) },
      ]}
      fields={fields}
      onCreate={
        isAdmin
          ? (v) => createReminderTemplate(v as unknown as ReminderTemplateRequest).then(() => undefined)
          : undefined
      }
      onUpdate={
        isAdmin
          ? (id, v) => updateReminderTemplate(id, v as unknown as ReminderTemplateRequest).then(() => undefined)
          : undefined
      }
      onDelete={isAdmin ? deleteReminderTemplate : undefined}
    />
  )
}
