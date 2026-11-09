import { Link, useNavigate } from 'react-router'
import { useStudentTicketApi } from '../api/StudentTicketApiContext'
import type { CreateTicketInput } from '../api/types'
import { PageHeader } from '../components/PageHeader'
import { emptyTicketForm, TicketForm } from '../components/TicketForm'
import { paths, studentTicketPath } from '../routes'
import type { StudentTicketLocationState } from './StudentTicketDetailPage'

export function ReportIssuePage() {
  const api = useStudentTicketApi()
  const navigate = useNavigate()

  async function report(input: CreateTicketInput) {
    const ticket = await api.createTicket(input)
    const state: StudentTicketLocationState = { notice: `Your issue was reported as ticket #${ticket.id}.` }
    navigate(studentTicketPath(ticket.id), { state })
  }

  return (
    <section className="page page--form">
      <Link to={paths.studentTickets} className="back-link">
        ← My tickets
      </Link>
      <PageHeader title="Report an issue" description="Tell the technicians which room has a problem and what is wrong." />
      <TicketForm
        initialValues={emptyTicketForm}
        submitLabel="Report issue"
        submittingLabel="Reporting…"
        cancelHref={paths.studentTickets}
        onSubmit={report}
      />
    </section>
  )
}
