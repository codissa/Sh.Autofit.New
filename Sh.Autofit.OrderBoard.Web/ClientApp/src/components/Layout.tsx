import { NavLink, Outlet } from 'react-router-dom';

export default function Layout() {
  const linkClass = ({ isActive }: { isActive: boolean }) =>
    `px-4 py-2 rounded-lg text-sm font-medium transition-colors ${
      isActive ? 'bg-gray-900' : 'hover:bg-gray-700'
    }`;

  return (
    <div className="min-h-screen bg-gray-100">
      <nav className="bg-gray-800 text-white shadow-md">
        <div className="px-4 py-2 flex items-center gap-4">
          <h1 className="text-lg font-bold whitespace-nowrap">לוח הזמנות</h1>
          <div className="flex gap-1">
            <NavLink to="/" end className={linkClass}>
              לוח
            </NavLink>
            <NavLink to="/delivery-methods" className={linkClass}>
              שיטות משלוח
            </NavLink>
            <NavLink to="/customer-rules" className={linkClass}>
              כללי לקוחות
            </NavLink>
          </div>
        </div>
      </nav>
      <Outlet />
    </div>
  );
}
