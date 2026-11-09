export type TicketStatus = 'Open' | 'InProgress' | 'Resolved'

export const ticketStatuses: readonly TicketStatus[] = ['Open', 'InProgress', 'Resolved']

export type Category = {
  id: number
  name: string
}

export type Ticket = {
  id: number
  studentId: number
  room: string
  category: Category
  description: string
  status: TicketStatus
  version: number
  createdAt: string
  updatedAt: string
  resolvedAt: string | null
}

export type TicketPage = {
  items: Ticket[]
  page: number
  pageSize: number
  totalCount: number
}

export type CreateTicketInput = {
  room: string
  categoryId: number
  description: string
}

/** Fields that are left out keep their value. `version` must be the ticket's current version. */
export type UpdateTicketInput = Partial<CreateTicketInput> & {
  version: number
}

export type ListTicketsParams = {
  status?: TicketStatus
  page?: number
  pageSize?: number
}
