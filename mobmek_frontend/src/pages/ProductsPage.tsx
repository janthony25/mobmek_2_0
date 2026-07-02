import { createProduct, deleteProduct, getProducts, updateProduct } from '@/api/products'
import { CrudSection } from '@/components/crud/CrudSection'
import type { FieldSchema } from '@/components/crud/types'
import { currency, orDash } from '@/lib/format'
import type { Product, ProductRequest } from '@/types'

const fields: FieldSchema[] = [
  { name: 'name', label: 'Name', type: 'text', required: true },
  { name: 'price', label: 'Price', type: 'number', required: true, step: '0.01', min: 0 },
  { name: 'stockQuantity', label: 'Stock quantity', type: 'number', required: true, min: 0, defaultValue: 0 },
  { name: 'description', label: 'Description', type: 'textarea' },
]

export function ProductsPage() {
  return (
    <CrudSection<Product>
      resourceName="Product"
      description="Parts and products available in the workshop."
      load={getProducts}
      pageSize={50}
      getId={(p) => p.id}
      rowLabel={(p) => p.name}
      columns={[
        { header: 'Name', cell: (p) => p.name, className: 'font-medium text-slate-900' },
        { header: 'Price', cell: (p) => currency(p.price) },
        { header: 'Stock', cell: (p) => p.stockQuantity },
        { header: 'Description', cell: (p) => orDash(p.description) },
      ]}
      fields={fields}
      onCreate={(v) => createProduct(v as unknown as ProductRequest).then(() => undefined)}
      onUpdate={(id, v) => updateProduct(id, v as unknown as ProductRequest).then(() => undefined)}
      onDelete={deleteProduct}
    />
  )
}
