// Sticky-note palette. Classes are written out in full (not interpolated) so
// Tailwind's JIT compiler keeps them. `key` is what we persist in Note.color.

export interface NoteColor {
  key: string
  label: string
  /** Card background + border. */
  card: string
  /** Solid swatch for the colour picker. */
  swatch: string
}

export const NOTE_COLORS: NoteColor[] = [
  { key: 'yellow', label: 'Yellow', card: 'bg-yellow-100 border-yellow-200', swatch: 'bg-yellow-300' },
  { key: 'pink', label: 'Pink', card: 'bg-pink-100 border-pink-200', swatch: 'bg-pink-300' },
  { key: 'blue', label: 'Blue', card: 'bg-blue-100 border-blue-200', swatch: 'bg-blue-300' },
  { key: 'green', label: 'Green', card: 'bg-green-100 border-green-200', swatch: 'bg-green-300' },
  { key: 'purple', label: 'Purple', card: 'bg-purple-100 border-purple-200', swatch: 'bg-purple-300' },
]

export const DEFAULT_NOTE_COLOR = NOTE_COLORS[0]

export function noteCardClass(color: string | null): string {
  return (NOTE_COLORS.find((c) => c.key === color) ?? DEFAULT_NOTE_COLOR).card
}
