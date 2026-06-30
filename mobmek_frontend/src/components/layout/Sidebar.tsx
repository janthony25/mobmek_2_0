import { NavLink } from 'react-router-dom'

interface NavItem {
  to: string
  label: string
  icon: string
}

const NAV_ITEMS: NavItem[] = [
  { to: '/customers', label: 'Customers', icon: '👥' },
  { to: '/jobs', label: 'Job Center', icon: '🔧' },
  { to: '/car-makes', label: 'Car Makes & Models', icon: '🚗' },
]

export function Sidebar() {
  return (
    <aside className="flex h-full w-60 shrink-0 flex-col border-r border-slate-200 bg-slate-900 text-slate-100">
      <div className="flex h-16 items-center gap-2 px-6 text-lg font-semibold tracking-tight">
        <span className="text-xl">🛠️</span>
        <span>Mobmek</span>
      </div>

      <nav className="flex-1 space-y-1 px-3 py-4">
        {NAV_ITEMS.map((item) => (
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
      </nav>

      <div className="border-t border-slate-800 px-6 py-4 text-xs text-slate-500">
        Mobmek Workshop
      </div>
    </aside>
  )
}
