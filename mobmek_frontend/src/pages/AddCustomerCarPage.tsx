import { useState } from 'react'
import { useAuth } from '@/contexts/AuthContext'
import { createCustomerWithCar } from '@/api/customers'
import { CustomerForm } from '@/components/forms/CustomerForm'
import { Button } from '@/components/ui/Button'
import type { CreateCustomerWithCarRequest } from '@/types'

/** Standalone, nav-free page for a dedicated kiosk/tablet account (see PERMISSIONS.AddCustomerAndCar) —
 * no sidebar, no other pages reachable, so a customer can safely be handed the tablet to fill in
 * their own details. Reuses CustomerForm as-is (already collects a first car when creating). */
export function AddCustomerCarPage() {
  const { logout } = useAuth()
  const [formKey, setFormKey] = useState(0)
  const [addedFirstName, setAddedFirstName] = useState<string | null>(null)

  const handleSubmit = async (values: Record<string, unknown>) => {
    const result = await createCustomerWithCar(values as unknown as CreateCustomerWithCarRequest)
    setAddedFirstName(result.customer.firstName)
  }

  const handleReset = () => {
    setAddedFirstName(null)
    setFormKey((key) => key + 1)
  }

  return (
    <div className="flex min-h-screen items-center justify-center bg-slate-50 px-4 py-10">
      <div className="w-full max-w-xl rounded-lg border border-slate-200 bg-white p-8 shadow-sm">
        <div className="mb-6 text-center">
          <span className="text-3xl" aria-hidden>
            🛠️
          </span>
          <h1 className="mt-2 text-lg font-semibold text-slate-900">Mobmek</h1>
          <p className="text-sm text-slate-500">Add your details and vehicle</p>
        </div>

        {addedFirstName ? (
          <div className="space-y-6 text-center">
            <p className="text-base text-slate-700">
              Thanks, {addedFirstName}! Your details and vehicle have been added.
            </p>
            <Button onClick={handleReset} className="w-full justify-center">
              Add another
            </Button>
          </div>
        ) : (
          <CustomerForm key={formKey} initial={null} onSubmit={handleSubmit} onCancel={handleReset} />
        )}

        <button
          type="button"
          onClick={() => void logout()}
          className="mt-6 block w-full text-center text-xs font-medium text-slate-400 hover:text-slate-600"
        >
          Sign out
        </button>
      </div>
    </div>
  )
}
