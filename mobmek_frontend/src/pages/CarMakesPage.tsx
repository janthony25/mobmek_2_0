import { useState } from 'react'
import { createCarMake, deleteCarMake, getCarMakes, updateCarMake } from '@/api/carMakes'
import { createCarModel, deleteCarModel, getCarModels, updateCarModel } from '@/api/carModels'
import { CrudSection } from '@/components/crud/CrudSection'
import { PageHeader } from '@/components/ui/PageHeader'
import { StateMessage } from '@/components/ui/StateMessage'
import type { CarMake, CarModel, NamedLookupRequest } from '@/types'

export function CarMakesPage() {
  const [selectedMake, setSelectedMake] = useState<CarMake | null>(null)

  return (
    <div>
      <PageHeader title="Car Makes & Models" description="Manage makes and the models under each make." />

      <div className="grid gap-6 lg:grid-cols-2">
        {/* Makes */}
        <CrudSection<CarMake>
          resourceName="Make"
          title="Makes"
          variant="section"
          load={getCarMakes}
          getId={(m) => m.id}
          rowLabel={(m) => m.name}
          columns={[
            {
              header: 'Name',
              cell: (m) => (
                <button
                  type="button"
                  onClick={() => setSelectedMake(m)}
                  className={`font-medium hover:underline ${
                    selectedMake?.id === m.id ? 'text-slate-900' : 'text-slate-600'
                  }`}
                >
                  {m.name}
                </button>
              ),
            },
          ]}
          fields={[{ name: 'name', label: 'Name', type: 'text', required: true }]}
          onCreate={(v) => createCarMake(v as unknown as NamedLookupRequest).then(() => undefined)}
          onUpdate={(id, v) => updateCarMake(id, v as unknown as NamedLookupRequest).then(() => undefined)}
          onDelete={deleteCarMake}
        />

        {/* Models for the selected make */}
        {selectedMake ? (
          <CrudSection<CarModel>
            key={selectedMake.id}
            resourceName="Model"
            title={`${selectedMake.name} models`}
            variant="section"
            load={() => getCarModels(selectedMake.id)}
            getId={(m) => m.id}
            rowLabel={(m) => m.name}
            columns={[{ header: 'Name', cell: (m) => m.name, className: 'font-medium text-slate-900' }]}
            fields={[{ name: 'name', label: 'Name', type: 'text', required: true }]}
            onCreate={(v) =>
              createCarModel({ carMakeId: selectedMake.id, name: v.name as string }).then(() => undefined)
            }
            onUpdate={(id, v) =>
              updateCarModel(id, { carMakeId: selectedMake.id, name: v.name as string }).then(() => undefined)
            }
            onDelete={deleteCarModel}
          />
        ) : (
          <div className="flex items-center">
            <StateMessage title="No make selected" description="Pick a make on the left to manage its models." />
          </div>
        )}
      </div>
    </div>
  )
}
