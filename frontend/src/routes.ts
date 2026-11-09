export type Role = 'Student' | 'Technician'

export const paths = {
  login: '/login',
  studentTickets: '/student/tickets',
  studentTicket: '/student/tickets/:ticketId',
  technicianTickets: '/technician/tickets',
} as const

export function studentTicketPath(id: number): string {
  return `/student/tickets/${id}`
}

export const homePathByRole: Record<Role, string> = {
  Student: paths.studentTickets,
  Technician: paths.technicianTickets,
}
