import { isRouteErrorResponse, useRouteError } from 'react-router'
import { ErrorState } from '../components/StatusStates'

export function RouteErrorPage() {
  const error = useRouteError()
  const message = isRouteErrorResponse(error)
    ? `${error.status} ${error.statusText}`
    : 'The page could not be displayed. Reload it to try again.'

  return (
    <section className="page page--narrow">
      <ErrorState message={<p>{message}</p>} onRetry={() => window.location.reload()} />
    </section>
  )
}
