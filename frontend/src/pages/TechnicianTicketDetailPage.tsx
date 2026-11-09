import { useCallback } from 'react'
import { Link, useLocation, useParams } from 'react-router'
import { ApiError, describeError, isApiError } from '../api/errors'
import { useTechnicianTicketApi } from '../api/TechnicianTicketApiContext'
import { ErrorState, LoadingState } from '../components/StatusStates'
import { TicketDetails } from '../components/TicketDetails'
import { useApiData } from '../hooks/useApiData'
import { parseTicketId, paths } from '../routes'

/** Router state for the technician detail page: the list page to return to. */
export type TechnicianTicketLocationState = {
  listSearch?: string
}

export function TechnicianTicketDetailPage() {
  const api = useTechnicianTicketApi()
  const params = useParams()
  const locationState = useLocation().state as TechnicianTicketLocationState | null
  const ticketId = parseTicketId(params.ticketId)
  const backHref = `${paths.technicianTickets}${locationState?.listSearch ?? ''}`

  const load = useCallback(
    (signal: AbortSignal) =>
      ticketId === null ? Promise.reject(new ApiError(404, null)) : api.getTicket(ticketId, signal),
    [api, ticketId],
  )
  const [ticket, reload] = useApiData(load)

  function renderContent() {
    if (ticket.status === 'loading') {
      return <LoadingState label="Loading the ticket…" />
    }
    if (ticket.status === 'error') {
      if (isApiError(ticket.error, 404)) {
        return (
          <ErrorState
            title="Ticket not found"
            message={<p>This ticket does not exist. It may have been deleted by the student who reported it.</p>}
          />
        )
      }
      const { title, message } = describeError(ticket.error)
      return <ErrorState title={title} message={<p>{message}</p>} onRetry={reload} />
    }
    return <TicketDetails ticket={ticket.data} showStudent />
  }

  return (
    <section className="page">
      <Link to={backHref} className="back-link">
        ← All tickets
      </Link>
      {renderContent()}
    </section>
  )
}
