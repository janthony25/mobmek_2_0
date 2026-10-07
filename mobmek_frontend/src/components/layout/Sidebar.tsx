import { useState } from 'react'
import type { LucideIcon } from 'lucide-react'
import {
  AlarmClock,
  Building2,
  Calculator,
  Calendar,
  CalendarClock,
  Car,
  ChevronLeft,
  ChevronRight,
  ClipboardList,
  DollarSign,
  FileText,
  Handshake,
  Hammer,
  KeyRound,
  Landmark,
  ListChecks,
  LogOut,
  Mail,
  Package,
  Percent,
  Receipt,
  Repeat,
  Shield,
  SlidersHorizontal,
  Tag,
  Tags,
  TrendingUp,
  User,
  UserCog,
  Users,
  Wrench,
  X,
} from 'lucide-react'
import { NavLink } from 'react-router-dom'
import { useAuth } from '@/contexts/AuthContext'
import { PERMISSIONS } from '@/types'

interface NavItem {
  to: string
  label: string
  icon: LucideIcon
  /** Temporarily hidden from the sidebar (page still exists, just not linked here yet). */
  hidden?: boolean
  /** Only shown if the signed-in user holds this permission — mirrors the route's
   * RequirePermission guard / the API's [Authorize(Policy = Permissions.X)]. */
  permission?: string
}

interface NavGroup {
  heading: string
  items: NavItem[]
  /** Set when one or more items in this group are hidden — adds a light "(Hidden)" badge next to the heading with this as the hover tooltip. */
  hiddenNote?: string
}

const NAV_GROUPS: NavGroup[] = [
  {
    heading: 'Workshop',
    items: [
      { to: '/customers', label: 'Customers', icon: Users },
      { to: '/appointments', label: 'Appointments', icon: Calendar },
      { to: '/jobs', label: 'Job Center', icon: Hammer },
      { to: '/invoices', label: 'Invoices', icon: Receipt },
      { to: '/quotations', label: 'Quotations', icon: FileText },
    ],
  },
  {
    heading: 'Templates',
    hiddenNote: 'Products page is hidden, enable to show',
    items: [
      { to: '/products', label: 'Products', icon: Package, hidden: true },
      { to: '/services', label: 'Services', icon: ListChecks },
      { to: '/car-makes', label: 'Car Makes & Models', icon: Car },
      { to: '/reminder-templates', label: 'Reminder Templates', icon: AlarmClock },
    ],
  },
  {
    heading: 'Finance',
    hiddenNote: 'Enable to see pages',
    items: [
      { to: '/cash-flow', label: 'Cash Flow', icon: DollarSign, hidden: true, permission: PERMISSIONS.AccessCashFlow },
      { to: '/recurring-planned', label: 'Recurring & Planned', icon: Repeat, hidden: true, permission: PERMISSIONS.AccessCashFlow },
      { to: '/forecast', label: 'Forecast', icon: TrendingUp, hidden: true, permission: PERMISSIONS.AccessCashFlow },
      { to: '/gst-report', label: 'GST Report', icon: Calculator, hidden: true, permission: PERMISSIONS.AccessCashFlow },
      { to: '/cash-accounts', label: 'Cash Accounts', icon: Landmark, hidden: true, permission: PERMISSIONS.AccessCashFlow },
      { to: '/transaction-categories', label: 'Categories', icon: Tags, hidden: true, permission: PERMISSIONS.AccessCashFlow },
      { to: '/payees', label: 'Payees', icon: Handshake, hidden: true, permission: PERMISSIONS.AccessCashFlow },
      { to: '/categorization-rules', label: 'Rules', icon: SlidersHorizontal, hidden: true, permission: PERMISSIONS.AccessCashFlow },
    ],
  },
  {
    heading: 'Staff',
    items: [
      { to: '/accounts', label: 'Accounts & Roles', icon: KeyRound, permission: PERMISSIONS.ManageAccounts },
      { to: '/roles', label: 'Roles & Permissions', icon: Shield, permission: PERMISSIONS.ManageAccounts },
      { to: '/employees', label: 'Employees', icon: UserCog, permission: PERMISSIONS.ManageEmployees },
      { to: '/employee-titles', label: 'Titles', icon: Tag, permission: PERMISSIONS.ManageEmployees },
      { to: '/employment-types', label: 'Employment Types', icon: ClipboardList, permission: PERMISSIONS.ManageEmployees },
    ],
  },
  {
    heading: 'Settings',
    items: [
      { to: '/tax', label: 'Tax (GST)', icon: Percent, permission: PERMISSIONS.AccessCashFlow },
      { to: '/business-details', label: 'Business Details', icon: Building2, permission: PERMISSIONS.ManageBusinessSettings },
      { to: '/email-settings', label: 'Email', icon: Mail, permission: PERMISSIONS.ManageBusinessSettings },
      { to: '/calendar-sync', label: 'Calendar Sync', icon: CalendarClock, permission: PERMISSIONS.ManageCalendarSync },
    ],
  },
]

const STORAGE_KEY = 'mobmek:sidebar-collapsed'

// `collapsed` is a desktop-only icon-rail preference (toggle is `lg:flex`-only, below
// that it's unreachable) — the mobile/tablet off-canvas drawer always has room for full
// labels, so every class/render that depends on it must stay scoped to `lg:` and up.
const navItemClass = (collapsed: boolean, isActive: boolean) =>
  [
    'flex items-center gap-3 rounded-xl py-2.5 text-sm font-medium transition-colors px-3',
    collapsed ? 'lg:justify-center lg:px-2' : '',
    isActive
      ? 'bg-indigo-50 text-indigo-600'
      : 'text-slate-600 hover:bg-slate-50 hover:text-slate-900',
  ].join(' ')

interface SidebarProps {
  /** Whether the off-canvas drawer is open on screens below `lg`. Ignored at `lg` and up, where the sidebar is always in-flow. */
  mobileOpen: boolean
  onCloseMobile: () => void
}

export function Sidebar({ mobileOpen, onCloseMobile }: SidebarProps) {
  const [collapsed, setCollapsed] = useState<boolean>(
    () => localStorage.getItem(STORAGE_KEY) === 'true',
  )
  const { user, isAdmin, hasPermission, logout } = useAuth()

  const toggle = () => {
    setCollapsed((prev) => {
      const next = !prev
      localStorage.setItem(STORAGE_KEY, String(next))
      return next
    })
  }

  return (
    <>
      {mobileOpen && (
        <div
          className="fixed inset-0 z-30 bg-slate-900/40 lg:hidden"
          onClick={onCloseMobile}
          aria-hidden
        />
      )}
      <aside
        className={[
          'fixed inset-y-0 left-0 z-40 flex h-full w-64 shrink-0 flex-col overflow-y-auto bg-white transition-transform duration-200',
          'border-r border-slate-200 lg:static lg:translate-x-0 lg:transition-[width]',
          collapsed ? 'lg:w-16' : 'lg:w-60',
          mobileOpen ? 'translate-x-0' : '-translate-x-full',
        ].join(' ')}
      >
        <div
          className={[
            'flex h-16 shrink-0 items-center text-lg font-semibold tracking-tight text-slate-900',
            collapsed ? 'lg:justify-center lg:px-2' : 'gap-2',
            'justify-between px-4 lg:px-6',
          ].join(' ')}
        >
          <span className="flex items-center gap-2">
            <Wrench className="h-5 w-5 text-indigo-600" strokeWidth={2} />
            <span className={collapsed ? 'lg:hidden' : ''}>Mobmek</span>
          </span>
          <button
            type="button"
            onClick={onCloseMobile}
            aria-label="Close menu"
            className="flex h-8 w-8 items-center justify-center rounded-lg text-slate-400 hover:bg-slate-50 hover:text-slate-600 lg:hidden"
          >
            <X className="h-5 w-5" strokeWidth={1.75} />
          </button>
          <button
            type="button"
            onClick={toggle}
            aria-label={collapsed ? 'Expand sidebar' : 'Collapse sidebar'}
            title={collapsed ? 'Expand' : 'Collapse'}
            className="hidden h-7 w-7 shrink-0 items-center justify-center rounded-lg text-slate-400 transition-colors hover:bg-slate-50 hover:text-slate-600 lg:flex"
          >
            {collapsed ? (
              <ChevronRight className="h-4 w-4" strokeWidth={1.75} />
            ) : (
              <ChevronLeft className="h-4 w-4" strokeWidth={1.75} />
            )}
          </button>
        </div>

        <nav className={['flex-1 py-2', collapsed ? 'px-2' : 'px-3'].join(' ')}>
          {NAV_GROUPS.map((group, groupIndex) => {
            const visibleItems = group.items.filter((item) => !item.hidden && (!item.permission || hasPermission(item.permission)))
            return (
              <div
                key={group.heading}
                className={[
                  'mb-1 pb-1',
                  groupIndex > 0 ? 'mt-4 border-t border-slate-100 pt-4' : 'mt-2',
                ].join(' ')}
              >
                <p className={['px-3 pb-2 text-xs font-semibold uppercase tracking-wider text-slate-400', collapsed ? 'lg:hidden' : ''].join(' ')}>
                  {group.heading}
                  {group.hiddenNote && (
                    <span
                      className="ml-1 cursor-default font-medium normal-case tracking-normal text-slate-400"
                      title={group.hiddenNote}
                    >
                      (Hidden)
                    </span>
                  )}
                </p>
                {visibleItems.length > 0 && (
                  <div className="space-y-0.5">
                    {visibleItems.map((item) => (
                      <NavLink
                        key={item.to}
                        to={item.to}
                        title={collapsed ? item.label : undefined}
                        onClick={onCloseMobile}
                        className={({ isActive }) => navItemClass(collapsed, isActive)}
                      >
                        <item.icon className="h-[18px] w-[18px] shrink-0" strokeWidth={1.75} aria-hidden />
                        <span className={collapsed ? 'lg:hidden' : ''}>{item.label}</span>
                      </NavLink>
                    ))}
                  </div>
                )}
              </div>
            )
          })}
        </nav>

        <div
          className={[
            'shrink-0 border-t border-slate-100 py-3 px-3',
            collapsed ? 'lg:flex lg:flex-col lg:items-center lg:gap-1 lg:px-2' : '',
          ].join(' ')}
        >
          {user && (
            <p
              className={['truncate px-3 pb-2 text-xs text-slate-400', collapsed ? 'lg:hidden' : ''].join(' ')}
              title={user.email}
            >
              {user.firstName} {user.lastName}
              {isAdmin && <span className="ml-1 text-slate-400">· Admin</span>}
            </p>
          )}
          <NavLink
            to="/profile"
            title={collapsed ? 'Profile' : undefined}
            onClick={onCloseMobile}
            className={({ isActive }) => navItemClass(collapsed, isActive)}
          >
            <User className="h-[18px] w-[18px] shrink-0" strokeWidth={1.75} aria-hidden />
            <span className={collapsed ? 'lg:hidden' : ''}>Profile</span>
          </NavLink>
          <button
            type="button"
            onClick={() => void logout()}
            title="Sign out"
            className={[
              'flex w-full items-center gap-3 rounded-xl px-3 py-2.5 text-sm font-medium text-slate-600 transition-colors hover:bg-slate-50 hover:text-slate-900',
              collapsed ? 'lg:w-auto lg:justify-center lg:px-2' : '',
            ].join(' ')}
          >
            <LogOut className="h-[18px] w-[18px] shrink-0" strokeWidth={1.75} aria-hidden />
            <span className={collapsed ? 'lg:hidden' : ''}>Sign out</span>
          </button>
        </div>
      </aside>
    </>
  )
}
