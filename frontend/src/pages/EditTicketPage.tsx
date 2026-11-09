import { useCallback, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router'
import { ApiError, describeError, isApiError } from '../api/errors'
import { useStudentTicketApi } from '../api/StudentTicketApiContext'
import type { CreateTicketInput, Ticket, UpdateTicketInput } from '../api/types'
import { PageHeader } from '../components/PageHeader'
import { statusLabels } from '../components/StatusBadge'
import { EmptyState, ErrorState, LoadingState } from '../components/StatusStates'
import { TicketForm, type TicketFormValues } from '../components/TicketForm'
import { useApiData } from '../hooks/useApiData'
import { parseTicketId, paths, studentTicketPath } from '../routes'
import type { StudentTicketLocationState } from './StudentTicketDetailPage'

function formValues(ticket: Ticket): TicketFormValues {
  return { room: ticket.room, categoryId: String(ticket.category.id), description: ticket.description }
}

/** The fields the student changed compared with `ticket`, or null when nothing changed. */
function changesFrom(ticket: Ticket, input: CreateTicketInput): UpdateTicketInput | null {
  const changes: UpdateTicketInput = { version: ticket.version }
  if (input.room !== ticket.room) changes.room = input.room
  if (input.categoryId !== ticket.category.id) changes.categoryId = input.categoryId
  if (input.description !== ticket.description) changes.description = input.description
  return Object.keys(changes).length > 1 ? changes : null
}

/** The latest ticket with the student's own changes applied, so fields they did not touch stay current. */
function applyChanges(ticket: Ticket, changes: UpdateTicketInput): TicketFormValues {
  const values = formValues(ticket)
  if (changes.room !== undefined) values.room = changes.room
  if (changes.categoryId !== undefined) values.categoryId = String(changes.categoryId)
  if (changes.description !== undefined) values.description = changes.description
  return values
}

type Conflict = {
  changes: UpdateTicketInput
  categoryName?: string
}

export function EditTicketPage() {
  const api = useStudentTicketApi()
  const navigate = useNavigate()
  const ticketId = parseTicketId(useParams().ticketId)

  const load = useCallback(
    (signal: AbortSignal) =>
      ticketId === null ? Promise.reject(new ApiError(404, null)) : api.getTicket(ticketId, signal),
    [api, ticketId],
  )
  const [loaded, reload] = useApiData(load)

  const [latest, setLatest] = useState<Ticket | null>(null)
  const [draft, setDraft] = useState<TicketFormValues | null>(null)
  const [conflict, setConflict] = useState<Conflict | null>(null)
  const [unchanged, setUnchanged] = useState(false)
  const [formKey, setFormKey] = useState(0)

  const backHref = ticketId === null ? paths.studentTickets : studentTicketPath(ticketId)
  const header = (
    <>
      <Link to={backHref} className="back-link">
        ← Back to the ticket
      </Link>
      <PageHeader title={ticketId === null ? 'Edit ticket' : `Edit ticket #${ticketId}`} />
    </>
  )

  if (loaded.status === 'loading') {
    return (
      <section className="page page--form">
        {header}
        <LoadingState label="Loading the ticket…" />
      </section>
    )
  }
  if (loaded.status === 'error') {
    const { title, message } = isApiError(loaded.error, 404)
      ? { title: 'Ticket not found', message: 'This ticket does not exist, or it was reported by another student.' }
      : describeError(loaded.error)
    return (
      <section className="page page--form">
        {header}
        <ErrorState title={title} message={<p>{message}</p>} onRetry={isApiError(loaded.error, 404) ? undefined : reload} />
      </section>
    )
  }

  const ticket = latest ?? loaded.data

  async function save(input: CreateTicketInput, categoryName?: string) {
    setUnchanged(false)
    const changes = changesFrom(ticket, input)
    if (!changes) {
      setUnchanged(true)
      return
    }
    try {
      await api.updateTicket(ticket.id, changes)
    } catch (error) {
      if (isApiError(error, 409)) {
        const fresh = await api.getTicket(ticket.id)
        setLatest(fresh)
        setDraft(applyChanges(fresh, changes))
        setConflict({ changes, categoryName })
        setFormKey((key) => key + 1)
        return
      }
      throw error
    }
    const state: StudentTicketLocationState = { notice: 'Your changes were saved.' }
    navigate(studentTicketPath(ticket.id), { replace: true, state })
  }

  function discardDraft() {
    setDraft(null)
    setConflict(null)
    setUnchanged(false)
    setFormKey((key) => key + 1)
  }

  if (ticket.status !== 'Open') {
    return (
      <section className="page page--form">
        {header}
        <EmptyState
          title="This ticket can no longer be edited"
          action={
            <Link to={studentTicketPath(ticket.id)} className="button button--secondary">
              View the ticket
            </Link>
          }
        >
          <p>
            It is now {statusLabels[ticket.status].toLowerCase()}. Only open tickets can be changed
            {conflict ? ', so your changes were not saved.' : '.'}
          </p>
        </EmptyState>
      </section>
    )
  }

  const banner = conflict ? (
    <div className="alert alert--warning" role="alert">
      <p>
        <strong>Someone else changed this ticket while you were editing it.</strong> Your changes were not saved. The
        form now shows the latest version with your changes applied on top.
      </p>
      <p>Your changes:</p>
      <ul className="change-list">
        {conflict.changes.room !== undefined && <li>Room: {conflict.changes.room}</li>}
        {conflict.changes.categoryId !== undefined && (
          <li>Category: {conflict.categoryName ?? `category ${conflict.changes.categoryId}`}</li>
        )}
        {conflict.changes.description !== undefined && <li>Description: {conflict.changes.description}</li>}
      </ul>
      <p>The latest saved version:</p>
      <dl className="details details--compact">
        <div>
          <dt>Room</dt>
          <dd>{ticket.room}</dd>
        </div>
        <div>
          <dt>Category</dt>
          <dd>{ticket.category.name}</dd>
        </div>
        <div>
          <dt>Description</dt>
          <dd className="description">{ticket.description}</dd>
        </div>
      </dl>
      <p>Select Save changes to apply your changes to the latest version, or discard them.</p>
      <div className="form-actions">
        <button type="button" className="button button--secondary" onClick={discardDraft}>
          Discard my changes
        </button>
      </div>
    </div>
  ) : unchanged ? (
    <p className="notice" role="status">
      You have not changed anything yet.
    </p>
  ) : null

  return (
    <section className="page page--form">
      {header}
      <TicketForm
        key={formKey}
        initialValues={draft ?? formValues(ticket)}
        submitLabel="Save changes"
        submittingLabel="Saving…"
        cancelHref={studentTicketPath(ticket.id)}
        onSubmit={save}
        banner={banner}
      />
    </section>
  )
}
