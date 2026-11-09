import type { RouteObject } from 'react-router'
import { EditTicketPage } from './pages/EditTicketPage'
import { ReportIssuePage } from './pages/ReportIssuePage'
import { StudentTicketDetailPage } from './pages/StudentTicketDetailPage'
import { StudentTicketsPage } from './pages/StudentTicketsPage'
import { paths } from './routes'

/** Student screens. They read tickets through `useStudentTicketApi`, not from the login state. */
export const studentRoutes: RouteObject[] = [
  { path: paths.studentTickets, element: <StudentTicketsPage /> },
  { path: paths.studentNewTicket, element: <ReportIssuePage /> },
  { path: paths.studentTicket, element: <StudentTicketDetailPage /> },
  { path: paths.studentEditTicket, element: <EditTicketPage /> },
]
