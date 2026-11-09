import { useCallback, useState } from 'react'
import { Link, useLocation, useNavigate, useParams } from 'react-router'
import { ApiError, describeError, isApiError } from '../api/errors'
import { useStudentTicketApi } from '../api/StudentTicketApiContext'
import type { Ticket } from '../api/types'
import { PageHeader } from '../components/PageHeader'
import { StatusBadge } from '../components/StatusBadge'
import { ErrorState, LoadingState } from '../components/StatusStates'
import { formatDateTime } from '../format'
import { useApiData } from '../hooks/useApiData'
import { parseTicketId, paths, studentEditTicketPath } from '../routes'

/** Router state for the detail and list pages: the list page to return to, and a message to show once. */
export type StudentTicketLocationState = {
  listSearch?: string
  notice?: string
}

export function StudentTicketDetailPage() {
  const api = useStudentTicketApi()
  const params = useParams()
  const location = useLocation()
  const navigate = useNavigate()
  const ticketId = parseTicketId(params.ticketId)
  const locationState = location.state as StudentTicketLocationState | null
  const backHref = `${paths.studentTickets}${locationState?.listSearch ?? ''}`

  const load = useCallback(
    (signal: AbortSignal) =>
      ticketId === null ? Promise.reject(new ApiError(404, null)) : api.getTicket(ticketId, signal),
    [api, ticketId],
  )
  const [ticket, reload] = useApiData(load)

  const [confirmingDelete, setConfirmingDelete] = useState(false)
  const [deleting, setDeleting] = useState(false)
  const [actionError, setActionError] = useState<string | null>(null)

  async function deleteTicket(id: number) {
    setDeleting(true)
    setActionError(null)
    try {
      await api.deleteTicket(id)
      const state: StudentTicketLocationState = { notice: `Ticket #${id} was deleted.` }
      navigate(paths.studentTickets, { replace: true, state })
    } catch (error) {
      setDeleting(false)
      setConfirmingDelete(false)
      if (isApiError(error, 409)) {
        setActionError('This ticket is no longer open, so it cannot be deleted. The latest version is shown below.')
        reload()
      } else if (isApiError(error, 404)) {
        const state: StudentTicketLocationState = { notice: `Ticket #${id} had already been deleted.` }
        navigate(paths.studentTickets, { replace: true, state })
      } else {
        const { title, message } = describeError(error)
        setActionError(`${title}. ${message}`)
      }
    }
  }

  function renderActions(current: Ticket) {
    if (current.status !== 'Open') {
      return (
        <p className="field__hint">Only open tickets can be edited or deleted. This ticket is being handled.</p>
      )
    }
    if (confirmingDelete) {
      return (
        <div className="confirm" role="group" aria-label="Confirm deletion">
          <p>
            <strong>Delete ticket #{current.id}?</strong> This cannot be undone.
          </p>
          <div className="form-actions">
            <button type="button" className="button button--danger" disabled={deleting} onClick={() => deleteTicket(current.id)}>
              {deleting ? 'Deleting…' : 'Delete ticket'}
            </button>
            <button
              type="button"
              className="button button--secondary"
              disabled={deleting}
              onClick={() => setConfirmingDelete(false)}
            >
              Keep ticket
            </button>
          </div>
        </div>
      )
    }
    return (
      <div className="form-actions">
        <Link to={studentEditTicketPath(current.id)} className="button">
          Edit ticket
        </Link>
        <button type="button" className="button button--secondary" onClick={() => setConfirmingDelete(true)}>
          Delete ticket
        </button>
      </div>
    )
  }

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
    return (
      <>
        {locationState?.notice && (
          <p className="notice" role="status">
            {locationState.notice}
          </p>
        )}
        {actionError && (
          <div className="alert" role="alert">
            {actionError}
          </div>
        )}
        <TicketDetails ticket={ticket.data} />
        {renderActions(ticket.data)}
      </>
    )
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
