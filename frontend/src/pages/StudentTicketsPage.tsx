import { PageHeader } from '../components/PageHeader'
import { EmptyState } from '../components/StatusStates'

export function StudentTicketsPage() {
  return (
    <section className="page">
      <PageHeader title="My tickets" description="Problems you have reported and their current status." />
      <EmptyState title="Ticket list not built yet">
        <p>This screen is a placeholder until the student ticket list is added.</p>
      </EmptyState>
    </section>
  )
}
