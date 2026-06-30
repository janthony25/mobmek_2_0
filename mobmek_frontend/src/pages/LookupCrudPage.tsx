import { CrudSection } from '@/components/crud/CrudSection'
import type { FieldSchema } from '@/components/crud/types'
import type { NamedLookupRequest } from '@/types'

interface NamedEntity {
  id: string
  name: string
}

interface LookupCrudPageProps<T extends NamedEntity> {
  resourceName: string
  title: string
  description?: string
  load: () => Promise<T[]>
  create: (body: NamedLookupRequest) => Promise<T>
  update: (id: string, body: NamedLookupRequest) => Promise<T>
  remove: (id: string) => Promise<void>
}

const fields: FieldSchema[] = [{ name: 'name', label: 'Name', type: 'text', required: true }]

/** Generic CRUD page for the many "name only" lookup entities. */
export function LookupCrudPage<T extends NamedEntity>({
  resourceName,
  title,
  description,
  load,
  create,
  update,
  remove,
}: LookupCrudPageProps<T>) {
  return (
    <CrudSection<T>
      resourceName={resourceName}
      title={title}
      description={description}
      load={load}
      getId={(r) => r.id}
      rowLabel={(r) => r.name}
      columns={[{ header: 'Name', cell: (r) => r.name, className: 'font-medium text-slate-900' }]}
      fields={fields}
      onCreate={(v) => create(v as unknown as NamedLookupRequest).then(() => undefined)}
      onUpdate={(id, v) => update(id, v as unknown as NamedLookupRequest).then(() => undefined)}
      onDelete={remove}
    />
  )
}
