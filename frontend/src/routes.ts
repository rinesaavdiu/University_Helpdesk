export type Role = 'Student' | 'Technician'

export const paths = {
  login: '/login',
  studentTickets: '/student/tickets',
  studentNewTicket: '/student/tickets/new',
  studentTicket: '/student/tickets/:ticketId',
  studentEditTicket: '/student/tickets/:ticketId/edit',
  technicianTickets: '/technician/tickets',
  technicianTicket: '/technician/tickets/:ticketId',
} as const

export function studentTicketPath(id: number): string {
  return `/student/tickets/${id}`
}

export function studentEditTicketPath(id: number): string {
  return `/student/tickets/${id}/edit`
}

export function technicianTicketPath(id: number): string {
  return `/technician/tickets/${id}`
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

const areaByRole: Record<Role, string> = {
  Student: '/student/',
  Technician: '/technician/',
}

/** Whether a signed-in user with this role may open the path; used to return after sign-in. */
export function canVisit(role: Role, path: string): boolean {
  return path.startsWith(areaByRole[role])
}

/** Router state for the sign-in page: the page the user tried to open. */
export type LoginLocationState = {
  from?: string
}
