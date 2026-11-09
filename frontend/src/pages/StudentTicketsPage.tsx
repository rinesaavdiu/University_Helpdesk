import { useCallback, useId } from 'react'
import { Link, useLocation, useSearchParams } from 'react-router'
import { describeError } from '../api/errors'
import { useStudentTicketApi } from '../api/StudentTicketApiContext'
import { ticketStatuses, type TicketStatus } from '../api/types'
import { PageHeader } from '../components/PageHeader'
import { Pagination } from '../components/Pagination'
import { statusLabels } from '../components/StatusBadge'
import { EmptyState, ErrorState, LoadingState } from '../components/StatusStates'
import { TicketList } from '../components/TicketList'
import { useApiData } from '../hooks/useApiData'
import { paths, studentTicketPath } from '../routes'
import type { StudentTicketLocationState } from './StudentTicketDetailPage'

const pageSize = 10

function parseStatus(value: string | null): TicketStatus | undefined {
  return ticketStatuses.find((status) => status === value)
}

function parsePage(value: string | null): number {
  const page = Number(value)
  return Number.isInteger(page) && page >= 1 ? page : 1
}

function listSearch(status: TicketStatus | undefined, page: number): string {
  const params = new URLSearchParams()
  if (status) params.set('status', status)
  if (page > 1) params.set('page', String(page))
  const search = params.toString()
  return search ? `?${search}` : ''
}

export function StudentTicketsPage() {
  const api = useStudentTicketApi()
  const [searchParams, setSearchParams] = useSearchParams()
  const notice = (useLocation().state as StudentTicketLocationState | null)?.notice
  const status = parseStatus(searchParams.get('status'))
  const page = parsePage(searchParams.get('page'))
  const statusFilterId = useId()

  const load = useCallback(
    (signal: AbortSignal) => api.listTickets({ status, page, pageSize }, signal),
    [api, status, page],
  )
  const [tickets, reload] = useApiData(load)

  const linkState: StudentTicketLocationState = { listSearch: listSearch(status, page) }
  const reportLink = (
    <Link to={paths.studentNewTicket} className="button">
      Report an issue
    </Link>
  )

  function renderContent() {
    if (tickets.status === 'loading') {
      return <LoadingState label="Loading your tickets…" />
    }
    if (tickets.status === 'error') {
      const { title, message } = describeError(tickets.error)
      return <ErrorState title={title} message={<p>{message}</p>} onRetry={reload} />
    }

    const { items, totalCount } = tickets.data
    if (totalCount === 0) {
      return status ? (
        <EmptyState
          title={`No ${statusLabels[status].toLowerCase()} tickets`}
          action={
            <Link to={paths.studentTickets} className="button button--secondary">
              Show all tickets
            </Link>
          }
        >
          <p>None of your tickets have this status.</p>
        </EmptyState>
      ) : (
        <EmptyState title="No tickets yet" action={reportLink}>
          <p>Problems you report will appear here.</p>
        </EmptyState>
      )
    }
    if (items.length === 0) {
      return (
        <EmptyState
          title="This page is empty"
          action={
            <Link to={listSearch(status, 1) || paths.studentTickets} className="button button--secondary">
              Go to the first page
            </Link>
          }
        >
          <p>There are fewer tickets than this page number needs.</p>
        </EmptyState>
      )
    }

    return (
      <>
        <p className="list-summary">
          {totalCount} {totalCount === 1 ? 'ticket' : 'tickets'}
        </p>
        <TicketList tickets={items} ticketHref={(ticket) => studentTicketPath(ticket.id)} linkState={linkState} />
        <Pagination
          page={page}
          pageSize={pageSize}
          totalCount={totalCount}
          pageHref={(target) => listSearch(status, target) || paths.studentTickets}
        />
      </>
    )
  }

  return (
    <section className="page">
      <PageHeader
        title="My tickets"
        description="Problems you have reported and their current status."
        actions={reportLink}
      />
      {notice && (
        <p className="notice" role="status">
          {notice}
        </p>
      )}
      <div className="toolbar">
        <label htmlFor={statusFilterId}>Status</label>
        <select
          id={statusFilterId}
          className="select"
          value={status ?? ''}
          onChange={(event) => {
            const next = parseStatus(event.target.value)
            setSearchParams(next ? { status: next } : {})
          }}
        >
          <option value="">All statuses</option>
          {ticketStatuses.map((value) => (
            <option key={value} value={value}>
              {statusLabels[value]}
            </option>
          ))}
        </select>
      </div>
      {renderContent()}
    </section>
  )
}
