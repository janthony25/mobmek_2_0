import type { ReactNode } from 'react'

export interface SelectOption {
  value: string
  label: string
}

export type FieldType = 'text' | 'textarea' | 'number' | 'select' | 'checkbox'

/** Declarative description of a single form field, consumed by ResourceForm. */
export interface FieldSchema {
  name: string
  label: string
  type: FieldType
  required?: boolean
  placeholder?: string
  help?: string
  /** Options for `select`. */
  options?: SelectOption[]
  /** Default value used when creating (no existing record). */
  defaultValue?: string | number | boolean
  /** number input attributes */
  step?: string
  min?: number
  max?: number
  /** Serialize the value to a number (use for numeric selects such as enums). */
  numeric?: boolean
}

/** A column in a CrudSection table. */
export interface Column<T> {
  header: string
  cell: (row: T) => ReactNode
  className?: string
}
