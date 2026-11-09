import { Link } from 'react-router'
import { EmptyState } from '../components/StatusStates'
import { paths } from '../routes'

export function NotFoundPage() {
  return (
    <section className="page page--narrow">
      <EmptyState
        title="Page not found"
        action={
          <Link to={paths.login} className="button">
            Go to sign in
          </Link>
        }
      >
        <p>The address does not match any page.</p>
      </EmptyState>
    </section>
  )
}
