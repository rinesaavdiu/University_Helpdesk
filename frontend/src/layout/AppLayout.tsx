import { Link, Outlet, useNavigate } from 'react-router'
import { endSession, useSession } from '../auth/session'
import { paths } from '../routes'

export function AppLayout() {
  const session = useSession()
  const navigate = useNavigate()

  function signOut() {
    endSession('signedOut')
    navigate(paths.login, { replace: true })
  }

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
          {session && (
            <div className="app-user">
              <span className="app-user__name">
                {session.user.displayName}
                <span className="app-user__role">{session.user.role}</span>
              </span>
              <button type="button" className="app-user__sign-out" onClick={signOut}>
                Sign out
              </button>
            </div>
          )}
        </div>
      </header>
      <main className="app-main">
        <Outlet />
      </main>
    </div>
  )
}
