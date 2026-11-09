import { useCallback, useRef, useState } from 'react'
import { Link, useLocation, useParams } from 'react-router'
import { ApiError, describeError, isApiError } from '../api/errors'
import { useTechnicianTicketApi } from '../api/TechnicianTicketApiContext'
import type { Ticket, TicketStatus } from '../api/types'
import { statusLabels } from '../components/StatusBadge'
import { ErrorState, LoadingState } from '../components/StatusStates'
import { TicketDetails } from '../components/TicketDetails'
import { useApiData } from '../hooks/useApiData'
import { parseTicketId, paths } from '../routes'

/** Router state for the technician detail page: the list page to return to. */
export type TechnicianTicketLocationState = {
  listSearch?: string
}

type StatusAction = {
  next: TicketStatus
  label: string
  pendingLabel: string
  done: (id: number) => string
}

/** The one status change allowed from each status; a resolved ticket has none. */
const statusActions: Partial<Record<TicketStatus, StatusAction>> = {
  Open: {
    next: 'InProgress',
    label: 'Start work',
    pendingLabel: 'Starting…',
    done: (id) => `You started work on ticket #${id}.`,
  },
  InProgress: {
    next: 'Resolved',
    label: 'Resolve ticket',
    pendingLabel: 'Resolving…',
    done: (id) => `Ticket #${id} is resolved. The student will get an email.`,
  },
}

type Feedback = { kind: 'notice' | 'conflict' | 'error'; message: string }

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
  const [ticket, reload, replaceTicket] = useApiData(load)

  const [pending, setPending] = useState(false)
  const pendingRef = useRef(false)
  const [feedback, setFeedback] = useState<Feedback | null>(null)

  async function advance(current: Ticket, action: StatusAction) {
    if (pendingRef.current) return
    pendingRef.current = true
    setPending(true)
    setFeedback(null)
    try {
      const updated = await api.changeStatus(current.id, { status: action.next, version: current.version })
      replaceTicket(updated)
      setFeedback({ kind: 'notice', message: action.done(updated.id) })
    } catch (error) {
      if (isApiError(error, 409)) {
        setFeedback({
          kind: 'conflict',
          message: `Ticket #${current.id} was changed by someone else before your update was saved, so nothing was changed. The latest version is shown below. Check it before you continue.`,
        })
        reload()
      } else if (isApiError(error, 404)) {
        setFeedback({ kind: 'error', message: 'This ticket no longer exists. The student may have deleted it.' })
        reload()
      } else {
        const { title, message } = describeError(error)
        setFeedback({ kind: 'error', message: `${title}. ${message}` })
      }
    } finally {
      pendingRef.current = false
      setPending(false)
    }
  }

  function renderActions(current: Ticket) {
    const action = statusActions[current.status]
    if (!action) {
      return <p className="field__hint">This ticket is resolved. There are no further status changes.</p>
    }
    return (
      <div className="form-actions">
        <button
          type="button"
          className="button"
          disabled={pending}
          aria-busy={pending || undefined}
          onClick={() => advance(current, action)}
        >
          {pending ? action.pendingLabel : action.label}
        </button>
        <p className="field__hint">Moves the ticket to {statusLabels[action.next]}.</p>
      </div>
    )
  }

  function renderFeedback() {
    if (!feedback) return null
    if (feedback.kind === 'notice') {
      return (
        <p className="notice" role="status">
          {feedback.message}
        </p>
      )
    }
    return (
      <div className={feedback.kind === 'conflict' ? 'alert alert--warning' : 'alert'} role="alert">
        {feedback.message}
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
            message={<p>This ticket does not exist. It may have been deleted by the student who reported it.</p>}
          />
        )
      }
      const { title, message } = describeError(ticket.error)
      return <ErrorState title={title} message={<p>{message}</p>} onRetry={reload} />
    }
    return (
      <>
        <TicketDetails ticket={ticket.data} showStudent />
        {renderActions(ticket.data)}
      </>
    )
  }

  return (
    <section className="page">
      <Link to={backHref} className="back-link">
        ← All tickets
      </Link>
      {renderFeedback()}
      {renderContent()}
    </section>
  )
}
