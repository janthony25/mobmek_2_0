import { apiDelete, apiGet, apiPost, apiPut } from './client'
import type { Note, NoteRequest } from '@/types'

export const getNotes = () => apiGet<Note[]>('/notes')
export const createNote = (body: NoteRequest) => apiPost<Note>('/notes', body)
export const updateNote = (id: string, body: NoteRequest) => apiPut<Note>(`/notes/${id}`, body)
export const deleteNote = (id: string) => apiDelete(`/notes/${id}`)
