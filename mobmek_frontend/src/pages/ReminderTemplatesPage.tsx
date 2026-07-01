import {
  createReminderTemplate,
  deleteReminderTemplate,
  getReminderTemplates,
  updateReminderTemplate,
} from '@/api/reminderTemplates'
import { CrudSection } from '@/components/crud/CrudSection'
import type { FieldSchema } from '@/components/crud/types'
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
  return (
    <CrudSection<ReminderTemplate>
      resourceName="Reminder Template"
      description="Reusable reminder presets (e.g. Next WOF, Next Service) you can pick when adding a reminder."
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
      onCreate={(v) => createReminderTemplate(v as unknown as ReminderTemplateRequest).then(() => undefined)}
      onUpdate={(id, v) =>
        updateReminderTemplate(id, v as unknown as ReminderTemplateRequest).then(() => undefined)
      }
      onDelete={deleteReminderTemplate}
    />
  )
}
