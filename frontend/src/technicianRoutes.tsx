import type { RouteObject } from 'react-router'
import { TechnicianTicketDetailPage } from './pages/TechnicianTicketDetailPage'
import { TechnicianTicketsPage } from './pages/TechnicianTicketsPage'
import { paths } from './routes'

/** Technician screens. They read tickets through `useTechnicianTicketApi`. */
export const technicianRoutes: RouteObject[] = [
  { path: paths.technicianTickets, element: <TechnicianTicketsPage /> },
  { path: paths.technicianTicket, element: <TechnicianTicketDetailPage /> },
]
