import { Link, Outlet } from 'react-router'

export function AppLayout() {
  return (
    <div className="app">
      <header className="app-header">
        <div className="app-header__inner">
          <Link to="/" className="app-brand">
            <span className="app-brand__name">University Helpdesk</span>
            <span className="app-brand__tagline">
              Sistem për raportimin e problemeve teknike në universitet
            </span>
          </Link>
        </div>
      </header>
      <main className="app-main">
        <Outlet />
      </main>
    </div>
  )
}
