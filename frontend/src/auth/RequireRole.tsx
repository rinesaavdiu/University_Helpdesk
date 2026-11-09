import { Link, Navigate, Outlet, useLocation } from 'react-router'
import { ErrorState } from '../components/StatusStates'
import { homePathByRole, paths, type LoginLocationState, type Role } from '../routes'
import { useSession } from './session'

const roleNames: Record<Role, string> = {
  Student: 'students',
  Technician: 'technicians',
}

/** Shows the child routes to a signed-in user with `role`; sends everyone else to sign in. */
export function RequireRole({ role }: { role: Role }) {
  const session = useSession()
  const location = useLocation()

  if (!session) {
    const state: LoginLocationState = { from: `${location.pathname}${location.search}` }
    return <Navigate to={paths.login} replace state={state} />
  }

  if (session.user.role !== role) {
    return (
      <section className="page page--narrow">
        <ErrorState
          title="Not allowed"
          message={
            <>
              <p>This page is only for {roleNames[role]}.</p>
              <p className="state__action">
                <Link to={homePathByRole[session.user.role]} className="button">
                  Go to your tickets
                </Link>
              </p>
            </>
          }
        />
      </section>
    )
  }

  return <Outlet />
}

/** Sends `/` to the signed-in user's start page, or to sign in. */
export function HomeRedirect() {
  const session = useSession()
  return <Navigate to={session ? homePathByRole[session.user.role] : paths.login} replace />
}
