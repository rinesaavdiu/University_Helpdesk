import { useCallback } from 'react'
import { Link, useLocation, useParams } from 'react-router'
import { ApiError, describeError, isApiError } from '../api/errors'
import { useStudentTicketApi } from '../api/StudentTicketApiContext'
import type { Ticket } from '../api/types'
import { PageHeader } from '../components/PageHeader'
import { StatusBadge } from '../components/StatusBadge'
import { ErrorState, LoadingState } from '../components/StatusStates'
import { formatDateTime } from '../format'
import { useApiData } from '../hooks/useApiData'
import { paths } from '../routes'
import type { StudentTicketsLinkState } from './StudentTicketsPage'

function parseTicketId(value: string | undefined): number | null {
  const id = Number(value)
  return Number.isInteger(id) && id > 0 ? id : null
}

export function StudentTicketDetailPage() {
  const api = useStudentTicketApi()
  const params = useParams()
  const location = useLocation()
  const ticketId = parseTicketId(params.ticketId)
  const listState = location.state as StudentTicketsLinkState | null
  const backHref = `${paths.studentTickets}${listState?.listSearch ?? ''}`

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
            message={<p>This ticket does not exist, or it was reported by another student.</p>}
          />
        )
      }
      const { title, message } = describeError(ticket.error)
      return <ErrorState title={title} message={<p>{message}</p>} onRetry={reload} />
    }
    return <TicketDetails ticket={ticket.data} />
  }

  return (
    <section className="page">
      <Link to={backHref} className="back-link">
        ← My tickets
      </Link>
      {renderContent()}
    </section>
  )
}

function TicketDetails({ ticket }: { ticket: Ticket }) {
  return (
    <>
      <PageHeader title={`Ticket #${ticket.id}`} actions={<StatusBadge status={ticket.status} />} />
      <div className="panel">
        <dl className="details">
          <div>
            <dt>Room</dt>
            <dd>{ticket.room}</dd>
          </div>
          <div>
            <dt>Category</dt>
            <dd>{ticket.category.name}</dd>
          </div>
          <div>
            <dt>Reported</dt>
            <dd>
              <time dateTime={ticket.createdAt}>{formatDateTime(ticket.createdAt)}</time>
            </dd>
          </div>
          <div>
            <dt>Last updated</dt>
            <dd>
              <time dateTime={ticket.updatedAt}>{formatDateTime(ticket.updatedAt)}</time>
            </dd>
          </div>
          {ticket.resolvedAt && (
            <div>
              <dt>Resolved</dt>
              <dd>
                <time dateTime={ticket.resolvedAt}>{formatDateTime(ticket.resolvedAt)}</time>
              </dd>
            </div>
          )}
        </dl>
        <h2 className="panel__heading">Description</h2>
        <p className="description">{ticket.description}</p>
      </div>
    </>
  )
}
