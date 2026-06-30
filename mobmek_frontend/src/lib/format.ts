const currencyFmt = new Intl.NumberFormat(undefined, { style: 'currency', currency: 'USD' })

export const currency = (value: number | null | undefined): string =>
  value == null ? '—' : currencyFmt.format(value)

export const orDash = (value: string | number | null | undefined): string =>
  value === null || value === undefined || value === '' ? '—' : String(value)

const dateFmt = new Intl.DateTimeFormat(undefined, { dateStyle: 'medium' })

/** Formats an ISO timestamp as a short local date, or a dash when absent. */
export const date = (value: string | null | undefined): string =>
  value == null ? '—' : dateFmt.format(new Date(value))

/** Formats a fraction (0.15) as a percentage string ("15%"). */
export const percent = (fraction: number): string =>
  `${(fraction * 100).toLocaleString(undefined, { maximumFractionDigits: 2 })}%`
