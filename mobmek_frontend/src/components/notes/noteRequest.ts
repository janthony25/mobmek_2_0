import type { Note, NoteRequest } from '@/types'

/** The request payload that round-trips a note unchanged (for partial patches). */
export function toNoteRequest(note: Note): NoteRequest {
  return {
    title: note.title,
    body: note.body,
    dueDate: note.dueDate,
    color: note.color,
    isPinned: note.isPinned,
    isDone: note.isDone,
    customerId: note.customerId,
  }
}
