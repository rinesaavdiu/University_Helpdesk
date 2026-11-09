import { Link } from 'react-router'
import { EmptyState } from '../components/StatusStates'

export function NotFoundPage() {
  return (
    <section className="page page--narrow">
      <EmptyState
        title="Page not found"
        action={
          <Link to="/" className="button">
            Go to the start page
          </Link>
        }
      >
        <p>The address does not match any page.</p>
      </EmptyState>
    </section>
  )
}
