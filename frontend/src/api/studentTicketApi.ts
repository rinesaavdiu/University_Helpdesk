import { requestJson, type ApiRequest } from './http'
import type { Category, CreateTicketInput, ListTicketsParams, Ticket, TicketPage, UpdateTicketInput } from './types'

/** The ticket and category operations the student screens use. */
export interface StudentTicketApi {
  listTickets(params: ListTicketsParams, signal?: AbortSignal): Promise<TicketPage>
  getTicket(id: number, signal?: AbortSignal): Promise<Ticket>
  listCategories(signal?: AbortSignal): Promise<Category[]>
  createTicket(input: CreateTicketInput): Promise<Ticket>
  updateTicket(id: number, input: UpdateTicketInput): Promise<Ticket>
  deleteTicket(id: number): Promise<void>
}

function jsonBody(method: string, value: unknown): RequestInit {
  return {
    method,
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(value),
  }
}

export function createStudentTicketApi(request: ApiRequest): StudentTicketApi {
  return {
    listTickets({ status, page, pageSize }, signal) {
      const query = new URLSearchParams()
      if (status) query.set('status', status)
      if (page) query.set('page', String(page))
      if (pageSize) query.set('pageSize', String(pageSize))
      const search = query.toString()
      return requestJson<TicketPage>(request, search ? `/tickets?${search}` : '/tickets', { signal })
    },

    getTicket(id, signal) {
      return requestJson<Ticket>(request, `/tickets/${id}`, { signal })
    },

    listCategories(signal) {
      return requestJson<Category[]>(request, '/categories', { signal })
    },

    createTicket(input) {
      return requestJson<Ticket>(request, '/tickets', jsonBody('POST', input))
    },

    updateTicket(id, input) {
      return requestJson<Ticket>(request, `/tickets/${id}`, jsonBody('PATCH', input))
    },

    deleteTicket(id) {
      return requestJson<void>(request, `/tickets/${id}`, { method: 'DELETE' })
    },
  }
}
