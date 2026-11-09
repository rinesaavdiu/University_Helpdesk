export type Role = 'Student' | 'Technician'

export const paths = {
  login: '/login',
  studentTickets: '/student/tickets',
  technicianTickets: '/technician/tickets',
} as const

export const homePathByRole: Record<Role, string> = {
  Student: paths.studentTickets,
  Technician: paths.technicianTickets,
}
