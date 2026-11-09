import { Link } from 'react-router'
import type { Ticket } from '../api/types'
import { formatDateTime } from '../format'
import { StatusBadge } from './StatusBadge'

type TicketListProps = {
  tickets: Ticket[]
  ticketHref: (ticket: Ticket) => string
  /** Router state passed to the detail page, for example to return to the same list page. */
  linkState?: unknown
  /** Show who reported each ticket; technicians need it, students already know. */
  showStudent?: boolean
}

export function TicketList({ tickets, ticketHref, linkState, showStudent = false }: TicketListProps) {
  return (
    <ul className="ticket-list">
      {tickets.map((ticket) => (
        <li key={ticket.id}>
          <Link to={ticketHref(ticket)} state={linkState} className="ticket-card">
            <div className="ticket-card__header">
              <span className="ticket-card__title">
                #{ticket.id} · Room {ticket.room}
              </span>
              <StatusBadge status={ticket.status} />
            </div>
            <p className="ticket-card__meta">
              {ticket.category.name}
              {showStudent && ` · Student #${ticket.studentId}`} · Reported{' '}
              <time dateTime={ticket.createdAt}>{formatDateTime(ticket.createdAt)}</time>
            </p>
            <p className="ticket-card__description">{ticket.description}</p>
          </Link>
        </li>
      ))}
    </ul>
  )
}
