import { useState } from 'react'

/**
 * Country dialling codes offered in the phone picker. NZ first — it's the workshop's
 * home country and the default for numbers stored before this field existed.
 */
const PHONE_COUNTRIES = [
  { code: '+64', label: 'NZ' },
  { code: '+61', label: 'AU' },
] as const

const DEFAULT_CODE = PHONE_COUNTRIES[0].code

/** True when the stored value already carries one of the dialling codes we offer. */
const explicitCode = (value: string) =>
  PHONE_COUNTRIES.find((c) => value.trim().startsWith(c.code))?.code

/**
 * Splits a stored number into its dialling code and the national part. Numbers saved
 * before the picker existed have no code: they're read as the default country with the
 * trunk "0" dropped, which is what dialling them internationally does (021… → +64 21…).
 */
function splitPhone(value: string): { code: string; national: string } {
  const trimmed = (value ?? '').trim()
  const code = explicitCode(trimmed)
  return code
    ? { code, national: trimmed.slice(code.length).trim() }
    : { code: DEFAULT_CODE, national: trimmed.replace(/^0/, '') }
}

/** Recombines a code and national number into the single string the API stores. */
function joinPhone(code: string, national: string): string {
  const trimmed = national.trim()
  return trimmed === '' ? '' : `${code} ${trimmed}`
}

// Keeps the national box to things that can appear in a phone number, and tolerates a
// pasted full international number by dropping the code it starts with.
function sanitize(input: string): string {
  const withoutCode = input.trim().replace(/^\+\s*(64|61)\s*/, '')
  return withoutCode.replace(/[^\d ()-]/g, '')
}

interface PhoneInputProps {
  /** The combined number as stored, e.g. "+64 211234567". Empty when unset. */
  value: string
  onChange: (value: string) => void
  invalid?: boolean
  disabled?: boolean
}

/**
 * Phone entry as a country dropdown plus the national number, emitting one combined
 * string so nothing on the API side has to change. While the number is empty there is
 * no code in the value to read back, so the picked country is held locally until the
 * first digit is typed.
 */
export function PhoneInput({ value, onChange, invalid, disabled }: PhoneInputProps) {
  const parsed = splitPhone(value)
  const [pendingCode, setPendingCode] = useState(parsed.code)
  const code = explicitCode(value) ?? pendingCode

  return (
    <div
      className={`mt-1 flex w-full overflow-hidden rounded-md border shadow-sm focus-within:ring-1 ${
        invalid
          ? 'border-red-500 bg-red-50 focus-within:border-red-500 focus-within:ring-red-500'
          : 'border-slate-300 focus-within:border-slate-500 focus-within:ring-slate-500'
      }`}
    >
      <select
        value={code}
        disabled={disabled}
        aria-label="Country code"
        onChange={(e) => {
          setPendingCode(e.target.value)
          onChange(joinPhone(e.target.value, parsed.national))
        }}
        className="border-r border-slate-300 bg-slate-50 px-2 py-2 text-sm text-slate-700 focus:outline-none disabled:text-slate-400"
      >
        {PHONE_COUNTRIES.map((c) => (
          <option key={c.code} value={c.code}>
            {c.label} {c.code}
          </option>
        ))}
      </select>
      <input
        type="tel"
        inputMode="tel"
        value={parsed.national}
        disabled={disabled}
        maxLength={20}
        placeholder="211234567"
        onChange={(e) => onChange(joinPhone(code, sanitize(e.target.value)))}
        className="w-full px-3 py-2 text-sm focus:outline-none disabled:text-slate-400"
      />
    </div>
  )
}
