import { useState } from 'react'
import { getCarMakes } from '@/api/carMakes'
import { getCarModels } from '@/api/carModels'
import { PageHeader } from '@/components/ui/PageHeader'
import { StateMessage } from '@/components/ui/StateMessage'
import { useAsync } from '@/hooks/useAsync'
import type { CarMake } from '@/types'

export function CarMakesPage() {
  const { data: makes, loading, error } = useAsync(getCarMakes, [])
  const [selectedMake, setSelectedMake] = useState<CarMake | null>(null)

  return (
    <div>
      <PageHeader
        title="Car Makes & Models"
        description="Pick a make to see its models."
      />

      <div className="grid gap-6 md:grid-cols-[18rem_1fr]">
        {/* Makes list */}
        <div className="rounded-lg border border-slate-200 bg-white">
          <div className="border-b border-slate-100 px-4 py-3 text-xs font-semibold uppercase tracking-wide text-slate-500">
            Makes
          </div>

          {loading && <p className="px-4 py-6 text-sm text-slate-500">Loading makes…</p>}
          {error && <p className="px-4 py-6 text-sm text-red-600">{error.message}</p>}
          {makes && makes.length === 0 && (
            <p className="px-4 py-6 text-sm text-slate-500">No car makes yet.</p>
          )}

          {makes && makes.length > 0 && (
            <ul className="max-h-[28rem] overflow-y-auto py-1">
              {makes.map((make) => {
                const isActive = selectedMake?.id === make.id
                return (
                  <li key={make.id}>
                    <button
                      type="button"
                      onClick={() => setSelectedMake(make)}
                      className={[
                        'flex w-full items-center justify-between px-4 py-2 text-left text-sm transition-colors',
                        isActive
                          ? 'bg-slate-900 font-medium text-white'
                          : 'text-slate-700 hover:bg-slate-50',
                      ].join(' ')}
                    >
                      <span>{make.name}</span>
                      <span aria-hidden className={isActive ? 'text-white' : 'text-slate-300'}>
                        ›
                      </span>
                    </button>
                  </li>
                )
              })}
            </ul>
          )}
        </div>

        {/* Models for the selected make */}
        <div className="rounded-lg border border-slate-200 bg-white">
          <div className="border-b border-slate-100 px-4 py-3 text-xs font-semibold uppercase tracking-wide text-slate-500">
            {selectedMake ? `${selectedMake.name} models` : 'Models'}
          </div>
          {selectedMake ? (
            <CarModelsList makeId={selectedMake.id} />
          ) : (
            <p className="px-4 py-10 text-center text-sm text-slate-500">
              Select a make on the left to view its models.
            </p>
          )}
        </div>
      </div>
    </div>
  )
}

function CarModelsList({ makeId }: { makeId: string }) {
  const { data: models, loading, error } = useAsync(() => getCarModels(makeId), [makeId])

  if (loading) return <p className="px-4 py-6 text-sm text-slate-500">Loading models…</p>
  if (error) return <p className="px-4 py-6 text-sm text-red-600">{error.message}</p>
  if (!models || models.length === 0) {
    return (
      <div className="px-4 py-6">
        <StateMessage title="No models" description="This make has no models yet." />
      </div>
    )
  }

  return (
    <ul className="divide-y divide-slate-100">
      {models.map((model) => (
        <li key={model.id} className="px-4 py-3 text-sm text-slate-700">
          {model.name}
        </li>
      ))}
    </ul>
  )
}
