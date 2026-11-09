import { PageHeader } from '../components/PageHeader'
import { EmptyState } from '../components/StatusStates'

export function TechnicianTicketsPage() {
  return (
    <section className="page">
      <PageHeader title="All tickets" description="Every reported problem, newest first." />
      <EmptyState title="Ticket list not built yet">
        <p>This screen is a placeholder until the technician ticket list is added.</p>
      </EmptyState>
    </section>
  )
}
