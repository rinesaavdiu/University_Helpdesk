import type { Ticket } from '../api/types'
import { formatDateTime } from '../format'
import { PageHeader } from './PageHeader'
import { StatusBadge } from './StatusBadge'

type TicketDetailsProps = {
  ticket: Ticket
  /** Show who reported the ticket; technicians need it, students already know. */
  showStudent?: boolean
}

export function TicketDetails({ ticket, showStudent = false }: TicketDetailsProps) {
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
          {showStudent && (
            <div>
              <dt>Student</dt>
              <dd>Student #{ticket.studentId}</dd>
            </div>
          )}
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
