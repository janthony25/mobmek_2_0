import { useState } from 'react'
import { NavLink } from 'react-router-dom'
import { useAuth } from '@/contexts/AuthContext'

interface NavItem {
  to: string
  label: string
  icon: string
  /** Temporarily hidden from the sidebar (page still exists, just not linked here yet). */
  hidden?: boolean
  /** Only shown to Admins — mirrors the route's RequireAdmin guard / the API's [Authorize(Roles = "Admin")]. */
  adminOnly?: boolean
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
      { to: '/customers', label: 'Customers', icon: '👥' },
      { to: '/appointments', label: 'Appointments', icon: '📅' },
      { to: '/jobs', label: 'Job Center', icon: '🔧' },
      { to: '/invoices', label: 'Invoices', icon: '🧾' },
      { to: '/quotations', label: 'Quotations', icon: '📄' },
    ],
  },
  {
    heading: 'Catalog',
    hiddenNote: 'Products page is hidden, enable to show',
    items: [
      { to: '/products', label: 'Products', icon: '📦', hidden: true },
      { to: '/services', label: 'Services', icon: '🧾' },
      { to: '/car-makes', label: 'Car Makes & Models', icon: '🚗' },
    ],
  },
  {
    heading: 'Finance',
    hiddenNote: 'Enable to see pages',
    items: [
      { to: '/cash-flow', label: 'Cash Flow', icon: '💵', hidden: true, adminOnly: true },
      { to: '/recurring-planned', label: 'Recurring & Planned', icon: '🔁', hidden: true, adminOnly: true },
      { to: '/forecast', label: 'Forecast', icon: '📈', hidden: true, adminOnly: true },
      { to: '/gst-report', label: 'GST Report', icon: '🧮', hidden: true, adminOnly: true },
      { to: '/cash-accounts', label: 'Cash Accounts', icon: '🏦', hidden: true, adminOnly: true },
      { to: '/transaction-categories', label: 'Categories', icon: '🗂️', hidden: true, adminOnly: true },
      { to: '/payees', label: 'Payees', icon: '🤝', hidden: true, adminOnly: true },
      { to: '/categorization-rules', label: 'Rules', icon: '⚙️', hidden: true, adminOnly: true },
    ],
  },
  {
    heading: 'Staff',
    items: [
      { to: '/employees', label: 'Employees', icon: '🧑‍🔧', adminOnly: true },
      { to: '/employee-titles', label: 'Titles', icon: '🏷️', adminOnly: true },
      { to: '/employment-types', label: 'Employment Types', icon: '📋', adminOnly: true },
    ],
  },
  {
    heading: 'Settings',
    items: [
      { to: '/tax', label: 'Tax (GST)', icon: '💰', adminOnly: true },
      { to: '/business-details', label: 'Business Details', icon: '🏢', adminOnly: true },
      { to: '/email-settings', label: 'Email', icon: '✉️', adminOnly: true },
      { to: '/reminder-templates', label: 'Reminder Templates', icon: '⏰' },
    ],
  },
]

const STORAGE_KEY = 'mobmek:sidebar-collapsed'

export function Sidebar() {
  const [collapsed, setCollapsed] = useState<boolean>(
    () => localStorage.getItem(STORAGE_KEY) === 'true',
  )
  const { user, isAdmin, logout } = useAuth()

  const toggle = () => {
    setCollapsed((prev) => {
      const next = !prev
      localStorage.setItem(STORAGE_KEY, String(next))
      return next
    })
  }

  return (
    <aside
      className={[
        'flex h-full shrink-0 flex-col overflow-y-auto border-r border-slate-200 bg-slate-900 text-slate-100 transition-[width] duration-200',
        collapsed ? 'w-16' : 'w-60',
      ].join(' ')}
    >
      <div
        className={[
          'flex h-16 shrink-0 items-center text-lg font-semibold tracking-tight',
          collapsed ? 'justify-center px-2' : 'gap-2 px-6',
        ].join(' ')}
      >
        {collapsed ? (
          <button
            type="button"
            onClick={toggle}
            aria-label="Expand sidebar"
            title="Expand"
            className="group flex h-10 w-10 items-center justify-center rounded-lg transition-colors hover:bg-slate-800"
          >
            <span className="text-xl group-hover:hidden">🛠️</span>
            <span className="hidden text-base text-slate-300 group-hover:inline">»</span>
          </button>
        ) : (
          <>
            <span className="text-xl">🛠️</span>
            <span className="flex-1">Mobmek</span>
            <button
              type="button"
              onClick={toggle}
              aria-label="Collapse sidebar"
              title="Collapse"
              className="flex h-7 w-7 items-center justify-center rounded-md text-sm text-slate-400 transition-colors hover:bg-slate-800 hover:text-white"
            >
              «
            </button>
          </>
        )}
      </div>

      <nav className={['flex-1 py-4', collapsed ? 'px-2' : 'px-3'].join(' ')}>
        {NAV_GROUPS.map((group) => {
          const visibleItems = group.items.filter((item) => !item.hidden && (!item.adminOnly || isAdmin))
          return (
            <div key={group.heading} className="mb-4">
              {!collapsed && (
                <p className="px-3 pb-1 text-xs font-semibold uppercase tracking-wider text-slate-500">
                  {group.heading}
                  {group.hiddenNote && (
                    <span
                      className="ml-1 cursor-default font-medium normal-case tracking-normal text-slate-600"
                      title={group.hiddenNote}
                    >
                      (Hidden)
                    </span>
                  )}
                </p>
              )}
              {visibleItems.length > 0 && (
                <div className="space-y-1">
                  {visibleItems.map((item) => (
                    <NavLink
                      key={item.to}
                      to={item.to}
                      title={collapsed ? item.label : undefined}
                      className={({ isActive }) =>
                        [
                          'flex items-center gap-3 rounded-lg py-2 text-sm font-medium transition-colors',
                          collapsed ? 'justify-center px-2' : 'px-3',
                          isActive
                            ? 'bg-slate-700 text-white'
                            : 'text-slate-300 hover:bg-slate-800 hover:text-white',
                        ].join(' ')
                      }
                    >
                      <span aria-hidden>{item.icon}</span>
                      {!collapsed && <span>{item.label}</span>}
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
          'shrink-0 border-t border-slate-800 py-3',
          collapsed ? 'flex flex-col items-center gap-2 px-2' : 'px-4',
        ].join(' ')}
      >
        {!collapsed && user && (
          <p className="truncate px-2 pb-2 text-xs text-slate-400" title={user.email}>
            {user.firstName} {user.lastName}
            {isAdmin && <span className="ml-1 text-slate-600">· Admin</span>}
          </p>
        )}
        <NavLink
          to="/profile"
          title={collapsed ? 'Profile' : undefined}
          className={({ isActive }) =>
            [
              'flex items-center gap-2 rounded-lg py-2 text-sm font-medium transition-colors',
              collapsed ? 'justify-center px-2' : 'w-full px-3',
              isActive ? 'bg-slate-800 text-white' : 'text-slate-300 hover:bg-slate-800 hover:text-white',
            ].join(' ')
          }
        >
          <span aria-hidden>👤</span>
          {!collapsed && <span>Profile</span>}
        </NavLink>
        <button
          type="button"
          onClick={() => void logout()}
          title="Sign out"
          className={[
            'flex items-center gap-2 rounded-lg py-2 text-sm font-medium text-slate-300 transition-colors hover:bg-slate-800 hover:text-white',
            collapsed ? 'justify-center px-2' : 'w-full px-3',
          ].join(' ')}
        >
          <span aria-hidden>🚪</span>
          {!collapsed && <span>Sign out</span>}
        </button>
      </div>
    </aside>
  )
}
