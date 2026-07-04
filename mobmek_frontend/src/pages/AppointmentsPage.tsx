import { useEffect, useMemo, useState } from 'react'
import { useLocation, useNavigate } from 'react-router-dom'
import { getAppointments, createAppointment } from '@/api/appointments'
import { getEmployees } from '@/api/employees'
import { useAsync } from '@/hooks/useAsync'
import { PageHeader } from '@/components/ui/PageHeader'
import { Button } from '@/components/ui/Button'
import { Badge } from '@/components/ui/Badge'
import { Modal } from '@/components/ui/Modal'
import { StateMessage } from '@/components/ui/StateMessage'
import { useToast } from '@/components/ui/toast'
import { AppointmentForm } from '@/components/forms/AppointmentForm'
import { AppointmentDetailModal } from '@/components/appointments/AppointmentDetailModal'
import { APPOINTMENT_STATUS_TONES } from '@/components/appointments/statusTones'
import { controlClass } from '@/components/forms/controls'
import { date as formatDate, time, orDash } from '@/lib/format'
import { APPOINTMENT_STATUS_LABELS, AppointmentStatus } from '@/types'
import type { Appointment, CreateAppointmentRequest, Job } from '@/types'

// Visible day window of the time grid (local time).
const DAY_START_HOUR = 7
const DAY_END_HOUR = 18
const HOUR_PX = 56
const GRID_HEIGHT = (DAY_END_HOUR - DAY_START_HOUR) * HOUR_PX

// --- Local-time date math (no date library by design) --------------------------

const startOfDay = (d: Date) => new Date(d.getFullYear(), d.getMonth(), d.getDate())
const addDays = (d: Date, n: number) => new Date(d.getFullYear(), d.getMonth(), d.getDate() + n)
/** Monday-based start of week. */
const startOfWeek = (d: Date) => addDays(startOfDay(d), -((d.getDay() + 6) % 7))
const startOfMonth = (d: Date) => new Date(d.getFullYear(), d.getMonth(), 1)
const addMonths = (d: Date, n: number) => new Date(d.getFullYear(), d.getMonth() + n, 1)
const isSameDay = (a: Date, b: Date) =>
  a.getFullYear() === b.getFullYear() && a.getMonth() === b.getMonth() && a.getDate() === b.getDate()

/** "yyyy-mm-dd" in local time, for the jump-to-date input. */
const toDateInput = (d: Date) =>
  `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`

const WEEKDAY_FMT = new Intl.DateTimeFormat(undefined, { weekday: 'short' })
const DAY_FMT = new Intl.DateTimeFormat(undefined, { month: 'short', day: 'numeric' })
const MONTH_FMT = new Intl.DateTimeFormat(undefined, { month: 'long', year: 'numeric' })
const FULL_DAY_FMT = new Intl.DateTimeFormat(undefined, {
  weekday: 'long',
  month: 'long',
  day: 'numeric',
  year: 'numeric',
})

// --- Time-grid block layout -----------------------------------------------------

interface PositionedAppointment {
  appointment: Appointment
  top: number
  height: number
  /** Column index / count within its overlap cluster, for side-by-side rendering. */
  col: number
  cols: number
}

/** Minutes into the visible day window, clamped so out-of-hours bookings stay visible. */
const minutesIntoDay = (iso: string, day: Date) => {
  const d = new Date(iso)
  const raw = (d.getTime() - day.getTime()) / 60000 - DAY_START_HOUR * 60
  return Math.max(0, Math.min(raw, (DAY_END_HOUR - DAY_START_HOUR) * 60))
}

/** Assigns overlapping appointments to side-by-side columns (greedy, by start time). */
function layoutDay(appointments: Appointment[], day: Date): PositionedAppointment[] {
  const sorted = [...appointments].sort(
    (a, b) => new Date(a.startUtc).getTime() - new Date(b.startUtc).getTime(),
  )

  const result: PositionedAppointment[] = []
  let cluster: { start: number; end: number; col: number; appointment: Appointment }[] = []
  let colEnds: number[] = []
  let clusterEnd = -1

  const flush = () => {
    for (const item of cluster) {
      const startPx = (item.start / 60) * HOUR_PX
      const endPx = (item.end / 60) * HOUR_PX
      result.push({
        appointment: item.appointment,
        top: startPx,
        height: Math.max(endPx - startPx, 28),
        col: item.col,
        cols: colEnds.length,
      })
    }
    cluster = []
    colEnds = []
  }

  for (const appointment of sorted) {
    const start = minutesIntoDay(appointment.startUtc, day)
    const end = Math.max(minutesIntoDay(appointment.endUtc, day), start + 30)

    if (cluster.length > 0 && start >= clusterEnd) flush()

    let col = colEnds.findIndex((e) => e <= start)
    if (col === -1) {
      col = colEnds.length
      colEnds.push(end)
    } else {
      colEnds[col] = end
    }
    cluster.push({ start, end, col, appointment })
    clusterEnd = Math.max(clusterEnd, end)
  }
  flush()

  return result
}

const STATUS_BLOCK_CLASSES: Record<AppointmentStatus, string> = {
  [AppointmentStatus.Scheduled]: 'border-blue-300 bg-blue-100 text-blue-900 hover:bg-blue-200',
  [AppointmentStatus.Confirmed]: 'border-green-300 bg-green-100 text-green-900 hover:bg-green-200',
  [AppointmentStatus.Arrived]: 'border-amber-300 bg-amber-100 text-amber-900 hover:bg-amber-200',
  [AppointmentStatus.Completed]: 'border-slate-300 bg-slate-100 text-slate-600 hover:bg-slate-200',
  [AppointmentStatus.NoShow]: 'border-red-300 bg-red-100 text-red-800 hover:bg-red-200',
  [AppointmentStatus.Cancelled]: 'border-slate-200 bg-slate-50 text-slate-400 line-through hover:bg-slate-100',
}

const STATUS_OPTIONS = Object.entries(APPOINTMENT_STATUS_LABELS)

// controlClass is built for forms (w-full, mt-1); toolbar controls need intrinsic
// widths or they each claim a whole row once the toolbar wraps.
const toolbarControlClass = controlClass.replace('mt-1 w-full ', '')

/** Who the block is for: the linked customer, or the phone contact until converted. */
const personLabel = (a: Appointment) => a.customerName ?? a.contactName ?? ''

type CalendarView = 'day' | 'week' | 'month'

export function AppointmentsPage() {
  const toast = useToast()
  const location = useLocation()
  const navigate = useNavigate()

  const [view, setView] = useState<CalendarView>('week')
  const [anchor, setAnchor] = useState(() => startOfDay(new Date()))
  const [statusFilter, setStatusFilter] = useState('')
  const [mechanicFilter, setMechanicFilter] = useState('')

  // Debounced so we don't hit the API per keystroke. A non-empty term switches the
  // calendar to a cross-time search-results list (rego / customer name lookup).
  const [searchInput, setSearchInput] = useState('')
  const [search, setSearch] = useState('')
  useEffect(() => {
    const t = setTimeout(() => setSearch(searchInput.trim()), 300)
    return () => clearTimeout(t)
  }, [searchInput])
  const searching = search !== ''

  const [creatingSlot, setCreatingSlot] = useState<{ start: Date; end: Date } | null>(null)
  const [prefillJob, setPrefillJob] = useState<Job | null>(null)
  const [selected, setSelected] = useState<Appointment | null>(null)

  const closeCreating = () => {
    setCreatingSlot(null)
    setPrefillJob(null)
  }

  // Arriving from a job page's "Create appointment" button: open the create modal
  // pre-scoped to that job via the "Existing job" tab.
  useEffect(() => {
    const job = (location.state as { job?: Job } | null)?.job
    if (!job) return
    const start = new Date()
    start.setHours(9, 0, 0, 0)
    setCreatingSlot({ start, end: new Date(start.getTime() + 60 * 60000) })
    setPrefillJob(job)
    navigate(location.pathname, { replace: true, state: null })
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [location.state])

  // Day view shows exactly one day, week 7 days, month always 6 whole weeks.
  const range = useMemo(() => {
    if (view === 'day') {
      const from = startOfDay(anchor)
      return { from, to: addDays(from, 1) }
    }
    if (view === 'week') {
      const from = startOfWeek(anchor)
      return { from, to: addDays(from, 7) }
    }
    const from = startOfWeek(startOfMonth(anchor))
    return { from, to: addDays(from, 42) }
  }, [view, anchor])

  const { data: employees } = useAsync(getEmployees, [])
  const { data: appointments, loading, error, reload } = useAsync(
    () =>
      getAppointments({
        // A search spans all time; the calendar queries only its visible range.
        ...(searching ? { search } : { from: range.from.toISOString(), to: range.to.toISOString() }),
        status: statusFilter === '' ? undefined : (Number(statusFilter) as AppointmentStatus),
        mechanicId: mechanicFilter || undefined,
      }),
    [searching, search, range, statusFilter, mechanicFilter],
  )

  const goToday = () => setAnchor(startOfDay(new Date()))
  const step = (dir: 1 | -1) => {
    if (view === 'day') setAnchor(addDays(anchor, dir))
    else if (view === 'week') setAnchor(addDays(startOfWeek(anchor), dir * 7))
    else setAnchor(addMonths(anchor, dir))
  }

  const rangeLabel =
    view === 'day'
      ? FULL_DAY_FMT.format(anchor)
      : view === 'week'
        ? `${DAY_FMT.format(range.from)} – ${DAY_FMT.format(addDays(range.from, 6))}, ${range.from.getFullYear()}`
        : MONTH_FMT.format(anchor)

  const handleCreate = async (values: CreateAppointmentRequest) => {
    await createAppointment(values)
    toast.success('Appointment booked.')
    closeCreating()
    reload()
  }

  const openDay = (day: Date) => {
    setAnchor(startOfDay(day))
    setView('day')
    setSearchInput('')
  }

  return (
    <div>
      <PageHeader
        title="Appointments"
        description="Workshop calendar — bookings link to customers and jobs, or hold a caller's details until check-in."
      />

      <div className="mb-4 flex flex-wrap items-center gap-2">
        <div className="flex rounded-lg bg-slate-100 p-1">
          {(['day', 'week', 'month'] as const).map((v) => (
            <button
              key={v}
              type="button"
              onClick={() => setView(v)}
              className={`rounded-md px-3 py-1 text-sm font-medium capitalize transition-colors ${
                view === v && !searching
                  ? 'bg-white text-slate-900 shadow-sm'
                  : 'text-slate-500 hover:text-slate-700'
              }`}
            >
              {v}
            </button>
          ))}
        </div>

        <div className="flex items-center gap-1">
          <Button variant="secondary" size="sm" onClick={() => step(-1)} aria-label="Previous">
            ←
          </Button>
          <Button variant="secondary" size="sm" onClick={goToday}>
            Today
          </Button>
          <Button variant="secondary" size="sm" onClick={() => step(1)} aria-label="Next">
            →
          </Button>
        </div>

        {/* Jump straight to a specific day's appointments. */}
        <input
          type="date"
          value={toDateInput(anchor)}
          onChange={(e) => {
            if (!e.target.value) return
            const [y, m, d] = e.target.value.split('-').map(Number)
            openDay(new Date(y, m - 1, d))
          }}
          className={toolbarControlClass}
          aria-label="Jump to date"
        />

        <span className="text-sm font-semibold text-slate-700">
          {searching ? `Search: “${search}”` : rangeLabel}
        </span>

        <div className="ml-auto flex flex-wrap items-center gap-2">
          <input
            type="search"
            value={searchInput}
            onChange={(e) => setSearchInput(e.target.value)}
            placeholder="Search rego or customer…"
            className={`${toolbarControlClass} w-48`}
            aria-label="Search appointments"
          />
          <select
            value={statusFilter}
            onChange={(e) => setStatusFilter(e.target.value)}
            className={toolbarControlClass}
          >
            <option value="">All statuses</option>
            {STATUS_OPTIONS.map(([value, label]) => (
              <option key={value} value={value}>
                {label}
              </option>
            ))}
          </select>
          <select
            value={mechanicFilter}
            onChange={(e) => setMechanicFilter(e.target.value)}
            className={toolbarControlClass}
          >
            <option value="">All mechanics</option>
            {(employees ?? []).map((emp) => (
              <option key={emp.id} value={emp.id}>
                {emp.firstName} {emp.lastName}
              </option>
            ))}
          </select>
          <Button
            onClick={() => {
              const start = new Date(anchor)
              start.setHours(9, 0, 0, 0)
              setCreatingSlot({ start, end: new Date(start.getTime() + 60 * 60000) })
            }}
          >
            + New appointment
          </Button>
        </div>
      </div>

      {error ? (
        <StateMessage title="Couldn't load appointments" description={error.message} />
      ) : loading && !appointments ? (
        <StateMessage title="Loading…" />
      ) : searching ? (
        <SearchResults appointments={appointments ?? []} term={search} onAppointmentClick={setSelected} />
      ) : view === 'month' ? (
        <MonthGrid
          gridStart={range.from}
          month={anchor.getMonth()}
          appointments={appointments ?? []}
          onDayOpen={openDay}
          onAppointmentClick={setSelected}
        />
      ) : (
        <TimeGrid
          days={
            view === 'day' ? [range.from] : Array.from({ length: 7 }, (_, i) => addDays(range.from, i))
          }
          appointments={appointments ?? []}
          onSlotClick={(start) => setCreatingSlot({ start, end: new Date(start.getTime() + 60 * 60000) })}
          onAppointmentClick={setSelected}
          onDayOpen={view === 'day' ? undefined : openDay}
        />
      )}

      <Modal
        open={creatingSlot !== null}
        title="New appointment"
        onClose={closeCreating}
        maxWidth="max-w-2xl"
      >
        {creatingSlot && (
          <AppointmentForm
            initial={null}
            initialSlot={creatingSlot}
            initialJob={prefillJob ?? undefined}
            onSubmit={handleCreate}
            onCancel={closeCreating}
          />
        )}
      </Modal>

      {selected && (
        <AppointmentDetailModal
          appointment={selected}
          onClose={() => setSelected(null)}
          onChanged={(updated) => {
            setSelected(updated)
            reload()
          }}
          onDeleted={() => {
            setSelected(null)
            reload()
          }}
        />
      )}
    </div>
  )
}

interface SearchResultsProps {
  appointments: Appointment[]
  term: string
  onAppointmentClick: (a: Appointment) => void
}

/** Cross-time lookup results: upcoming bookings first, then the visit history. */
function SearchResults({ appointments, term, onAppointmentClick }: SearchResultsProps) {
  if (appointments.length === 0) {
    return (
      <StateMessage
        title={`No appointments match “${term}”`}
        description="Search checks customer names, contact names, phone numbers, regos and vehicle descriptions."
      />
    )
  }

  const now = new Date()
  const upcoming = appointments.filter((a) => new Date(a.endUtc) >= now)
  const past = appointments.filter((a) => new Date(a.endUtc) < now).reverse()

  const section = (heading: string, items: Appointment[]) =>
    items.length > 0 && (
      <section>
        <h2 className="mb-2 text-xs font-medium uppercase tracking-wide text-slate-500">
          {heading} ({items.length})
        </h2>
        <ul className="divide-y divide-slate-100 overflow-hidden rounded-lg border border-slate-200 bg-white">
          {items.map((a) => (
            <li key={a.id}>
              <button
                type="button"
                onClick={() => onAppointmentClick(a)}
                className="flex w-full flex-wrap items-center gap-x-4 gap-y-1 px-4 py-3 text-left text-sm hover:bg-slate-50"
              >
                <span className="w-40 shrink-0 text-slate-600">
                  {formatDate(a.startUtc)} · {time(a.startUtc)}
                </span>
                <span className="min-w-40 flex-1">
                  <span className="block font-medium text-slate-900">{a.title}</span>
                  <span className="block text-slate-500">
                    {orDash(personLabel(a))} · {a.carDescription ?? orDash(a.vehicleDescription)}
                  </span>
                </span>
                <Badge tone={APPOINTMENT_STATUS_TONES[a.status]}>
                  {APPOINTMENT_STATUS_LABELS[a.status]}
                </Badge>
              </button>
            </li>
          ))}
        </ul>
      </section>
    )

  return (
    <div className="space-y-6">
      {section('Upcoming', upcoming)}
      {section('Past', past)}
    </div>
  )
}

interface TimeGridProps {
  days: Date[]
  appointments: Appointment[]
  onSlotClick: (start: Date) => void
  onAppointmentClick: (a: Appointment) => void
  /** When set, clicking a day header zooms to that day. */
  onDayOpen?: (day: Date) => void
}

/** Google-Calendar-style time grid: hour rows, one column per day, positioned blocks. */
function TimeGrid({ days, appointments, onSlotClick, onAppointmentClick, onDayOpen }: TimeGridProps) {
  const today = new Date()
  const hours = Array.from({ length: DAY_END_HOUR - DAY_START_HOUR }, (_, i) => DAY_START_HOUR + i)
  const columns = { gridTemplateColumns: `3.5rem repeat(${days.length}, minmax(0, 1fr))` }

  const byDay = days.map((day) => {
    const next = addDays(day, 1)
    return layoutDay(
      appointments.filter((a) => new Date(a.startUtc) < next && new Date(a.endUtc) > day),
      day,
    )
  })

  return (
    <div className="overflow-x-auto rounded-lg border border-slate-200 bg-white">
      <div className={days.length > 1 ? 'min-w-[840px]' : ''}>
        {/* Day headers */}
        <div className="grid border-b border-slate-200" style={columns}>
          <div />
          {days.map((day) => (
            <button
              key={day.toISOString()}
              type="button"
              disabled={!onDayOpen}
              onClick={() => onDayOpen?.(day)}
              className={`border-l border-slate-100 px-2 py-2 text-center text-sm ${
                isSameDay(day, today) ? 'bg-slate-50 font-semibold text-slate-900' : 'text-slate-600'
              } ${onDayOpen ? 'hover:bg-slate-100' : ''}`}
            >
              <span className="block text-xs uppercase tracking-wide text-slate-400">
                {WEEKDAY_FMT.format(day)}
              </span>
              {days.length > 1 ? day.getDate() : DAY_FMT.format(day)}
            </button>
          ))}
        </div>

        {/* Time grid */}
        <div className="grid" style={columns}>
          {/* Hour gutter */}
          <div className="relative" style={{ height: GRID_HEIGHT }}>
            {hours.map((h, i) => (
              <span
                key={h}
                className="absolute right-2 -translate-y-1/2 text-xs text-slate-400"
                style={{ top: i * HOUR_PX }}
              >
                {i > 0 && `${String(h).padStart(2, '0')}:00`}
              </span>
            ))}
          </div>

          {days.map((day, dayIndex) => (
            <div
              key={day.toISOString()}
              className={`relative border-l border-slate-100 ${isSameDay(day, today) ? 'bg-slate-50/60' : ''}`}
              style={{ height: GRID_HEIGHT }}
            >
              {/* Clickable hour slots with gridlines */}
              {hours.map((h) => (
                <button
                  key={h}
                  type="button"
                  aria-label={`Book ${DAY_FMT.format(day)} ${h}:00`}
                  className="block h-14 w-full border-b border-slate-100 hover:bg-slate-100/70"
                  onClick={() => {
                    const start = new Date(day)
                    start.setHours(h, 0, 0, 0)
                    onSlotClick(start)
                  }}
                />
              ))}

              {/* Appointment blocks, side by side when overlapping */}
              {byDay[dayIndex].map(({ appointment, top, height, col, cols }) => (
                <button
                  key={appointment.id}
                  type="button"
                  onClick={() => onAppointmentClick(appointment)}
                  className={`absolute overflow-hidden rounded-md border px-1.5 py-1 text-left text-xs shadow-sm transition-colors ${STATUS_BLOCK_CLASSES[appointment.status]}`}
                  style={{
                    top,
                    height,
                    left: `calc(${(col / cols) * 100}% + 2px)`,
                    width: `calc(${(1 / cols) * 100}% - 4px)`,
                  }}
                >
                  <span className="block truncate font-semibold">
                    {time(appointment.startUtc)} {appointment.title}
                  </span>
                  <span className="block truncate">{personLabel(appointment)}</span>
                </button>
              ))}
            </div>
          ))}
        </div>
      </div>
    </div>
  )
}

interface MonthGridProps {
  gridStart: Date
  month: number
  appointments: Appointment[]
  onDayOpen: (day: Date) => void
  onAppointmentClick: (a: Appointment) => void
}

const MAX_MONTH_CHIPS = 3

function MonthGrid({ gridStart, month, appointments, onDayOpen, onAppointmentClick }: MonthGridProps) {
  const today = new Date()
  const days = Array.from({ length: 42 }, (_, i) => addDays(gridStart, i))

  return (
    <div className="overflow-hidden rounded-lg border border-slate-200 bg-white">
      <div className="grid grid-cols-7 border-b border-slate-200 text-center text-xs font-medium uppercase tracking-wide text-slate-400">
        {days.slice(0, 7).map((day) => (
          <div key={day.toISOString()} className="px-2 py-2">
            {WEEKDAY_FMT.format(day)}
          </div>
        ))}
      </div>
      <div className="grid grid-cols-7">
        {days.map((day) => {
          const dayAppointments = appointments
            .filter((a) => isSameDay(new Date(a.startUtc), day))
            .sort((a, b) => new Date(a.startUtc).getTime() - new Date(b.startUtc).getTime())
          const overflow = dayAppointments.length - MAX_MONTH_CHIPS
          const inMonth = day.getMonth() === month

          return (
            <div
              key={day.toISOString()}
              className={`min-h-28 border-t border-l border-slate-100 p-1.5 first:border-l-0 ${
                inMonth ? '' : 'bg-slate-50/70'
              }`}
            >
              <button
                type="button"
                onClick={() => onDayOpen(day)}
                className={`mb-1 flex h-6 w-6 items-center justify-center rounded-full text-xs font-medium hover:bg-slate-200 ${
                  isSameDay(day, today)
                    ? 'bg-slate-900 text-white hover:bg-slate-700'
                    : inMonth
                      ? 'text-slate-700'
                      : 'text-slate-400'
                }`}
              >
                {day.getDate()}
              </button>

              <div className="space-y-1">
                {dayAppointments.slice(0, MAX_MONTH_CHIPS).map((a) => (
                  <button
                    key={a.id}
                    type="button"
                    onClick={() => onAppointmentClick(a)}
                    className={`block w-full truncate rounded border px-1.5 py-0.5 text-left text-xs ${STATUS_BLOCK_CLASSES[a.status]}`}
                  >
                    {time(a.startUtc)} {a.title}
                  </button>
                ))}
                {overflow > 0 && (
                  <button
                    type="button"
                    onClick={() => onDayOpen(day)}
                    className="block w-full rounded px-1.5 py-0.5 text-left text-xs text-slate-500 hover:bg-slate-100"
                  >
                    +{overflow} more
                  </button>
                )}
              </div>
            </div>
          )
        })}
      </div>
    </div>
  )
}
