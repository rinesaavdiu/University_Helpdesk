export type Role = 'Student' | 'Technician'

export const paths = {
  login: '/login',
  studentTickets: '/student/tickets',
  studentNewTicket: '/student/tickets/new',
  studentTicket: '/student/tickets/:ticketId',
  studentEditTicket: '/student/tickets/:ticketId/edit',
  technicianTickets: '/technician/tickets',
} as const

export function studentTicketPath(id: number): string {
  return `/student/tickets/${id}`
}

export function studentEditTicketPath(id: number): string {
  return `/student/tickets/${id}/edit`
}

/** Reads the `:ticketId` route parameter; returns null when it is not a positive integer. */
export function parseTicketId(value: string | undefined): number | null {
  const id = Number(value)
  return Number.isInteger(id) && id > 0 ? id : null
}

export const homePathByRole: Record<Role, string> = {
  Student: paths.studentTickets,
  Technician: paths.technicianTickets,
}
