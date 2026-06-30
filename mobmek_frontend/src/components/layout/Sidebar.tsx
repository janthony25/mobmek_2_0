import { NavLink } from 'react-router-dom'

interface NavItem {
  to: string
  label: string
  icon: string
}

interface NavGroup {
  heading: string
  items: NavItem[]
}

const NAV_GROUPS: NavGroup[] = [
  {
    heading: 'Workshop',
    items: [
      { to: '/customers', label: 'Customers', icon: '👥' },
      { to: '/jobs', label: 'Job Center', icon: '🔧' },
    ],
  },
  {
    heading: 'Catalog',
    items: [
      { to: '/products', label: 'Products', icon: '📦' },
      { to: '/services', label: 'Services', icon: '🧾' },
      { to: '/car-makes', label: 'Car Makes & Models', icon: '🚗' },
    ],
  },
  {
    heading: 'Staff',
    items: [
      { to: '/employees', label: 'Employees', icon: '🧑‍🔧' },
      { to: '/employee-titles', label: 'Titles', icon: '🏷️' },
      { to: '/employment-types', label: 'Employment Types', icon: '📋' },
    ],
  },
  {
    heading: 'Settings',
    items: [{ to: '/tax', label: 'Tax (GST)', icon: '💰' }],
  },
]

export function Sidebar() {
  return (
    <aside className="flex h-full w-60 shrink-0 flex-col overflow-y-auto border-r border-slate-200 bg-slate-900 text-slate-100">
      <div className="flex h-16 shrink-0 items-center gap-2 px-6 text-lg font-semibold tracking-tight">
        <span className="text-xl">🛠️</span>
        <span>Mobmek</span>
      </div>

      <nav className="flex-1 px-3 py-4">
        {NAV_GROUPS.map((group) => (
          <div key={group.heading} className="mb-4">
            <p className="px-3 pb-1 text-xs font-semibold uppercase tracking-wider text-slate-500">
              {group.heading}
            </p>
            <div className="space-y-1">
              {group.items.map((item) => (
                <NavLink
                  key={item.to}
                  to={item.to}
                  className={({ isActive }) =>
                    [
                      'flex items-center gap-3 rounded-lg px-3 py-2 text-sm font-medium transition-colors',
                      isActive
                        ? 'bg-slate-700 text-white'
                        : 'text-slate-300 hover:bg-slate-800 hover:text-white',
                    ].join(' ')
                  }
                >
                  <span aria-hidden>{item.icon}</span>
                  <span>{item.label}</span>
                </NavLink>
              ))}
            </div>
          </div>
        ))}
      </nav>

      <div className="shrink-0 border-t border-slate-800 px-6 py-4 text-xs text-slate-500">
        Mobmek Workshop
      </div>
    </aside>
  )
}
