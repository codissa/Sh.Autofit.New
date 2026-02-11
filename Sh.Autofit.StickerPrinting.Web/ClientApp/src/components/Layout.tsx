import { NavLink, Outlet } from 'react-router-dom';

export default function Layout() {
  return (
    <div className="min-h-screen bg-gray-50">
      <nav className="bg-blue-700 text-white shadow-md">
        <div className="max-w-5xl mx-auto px-4 py-3 flex items-center gap-6">
          <h1 className="text-lg font-bold whitespace-nowrap">S.H. Label Printer</h1>
          <div className="flex gap-1">
            <NavLink
              to="/"
              end
              className={({ isActive }) =>
                `px-4 py-2 rounded-lg text-sm font-medium transition-colors ${isActive ? 'bg-blue-900' : 'hover:bg-blue-600'}`
              }
            >
              Print on Demand
            </NavLink>
            <NavLink
              to="/stock"
              className={({ isActive }) =>
                `px-4 py-2 rounded-lg text-sm font-medium transition-colors ${isActive ? 'bg-blue-900' : 'hover:bg-blue-600'}`
              }
            >
              Stock Move
            </NavLink>
          </div>
        </div>
      </nav>
      <main className="max-w-5xl mx-auto px-4 py-6">
        <Outlet />
      </main>
    </div>
  );
}
