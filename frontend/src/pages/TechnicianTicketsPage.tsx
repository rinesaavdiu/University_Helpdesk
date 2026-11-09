import { useCallback } from 'react'
import { Link, useSearchParams } from 'react-router'
import { describeError } from '../api/errors'
import { useTechnicianTicketApi } from '../api/TechnicianTicketApiContext'
import { PageHeader } from '../components/PageHeader'
import { Pagination } from '../components/Pagination'
import { statusLabels } from '../components/StatusBadge'
import { StatusFilter } from '../components/StatusFilter'
import { EmptyState, ErrorState, LoadingState } from '../components/StatusStates'
import { TicketList } from '../components/TicketList'
import { useApiData } from '../hooks/useApiData'
import { paths, technicianTicketPath } from '../routes'
import { listSearch, parsePage, parseStatus } from '../ticketListSearch'
import type { TechnicianTicketLocationState } from './TechnicianTicketDetailPage'

const pageSize = 10

export function TechnicianTicketsPage() {
  const api = useTechnicianTicketApi()
  const [searchParams, setSearchParams] = useSearchParams()
  const status = parseStatus(searchParams.get('status'))
  const page = parsePage(searchParams.get('page'))

  const load = useCallback(
    (signal: AbortSignal) => api.listTickets({ status, page, pageSize }, signal),
    [api, status, page],
  )
  const [tickets, reload] = useApiData(load)

  const linkState: TechnicianTicketLocationState = { listSearch: listSearch(status, page) }

  function renderContent() {
    if (tickets.status === 'loading') {
      return <LoadingState label="Loading tickets…" />
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
            <Link to={paths.technicianTickets} className="button button--secondary">
              Show all tickets
            </Link>
          }
        >
          <p>No ticket has this status right now.</p>
        </EmptyState>
      ) : (
        <EmptyState title="No tickets yet">
          <p>Problems that students report will appear here.</p>
        </EmptyState>
      )
    }
    if (items.length === 0) {
      return (
        <EmptyState
          title="This page is empty"
          action={
            <Link to={listSearch(status, 1) || paths.technicianTickets} className="button button--secondary">
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
        <TicketList
          tickets={items}
          ticketHref={(ticket) => technicianTicketPath(ticket.id)}
          linkState={linkState}
          showStudent
        />
        <Pagination
          page={page}
          pageSize={pageSize}
          totalCount={totalCount}
          pageHref={(target) => listSearch(status, target) || paths.technicianTickets}
        />
      </>
    )
  }

  return (
    <section className="page">
      <PageHeader title="All tickets" description="Every reported problem, newest first." />
      <StatusFilter status={status} onChange={(next) => setSearchParams(next ? { status: next } : {})} />
      {renderContent()}
    </section>
  )
}
