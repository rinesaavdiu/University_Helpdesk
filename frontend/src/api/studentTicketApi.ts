import { requestJson, type ApiRequest } from './http'
import type { ListTicketsParams, Ticket, TicketPage } from './types'

/** The ticket operations the student screens use. */
export interface StudentTicketApi {
  listTickets(params: ListTicketsParams, signal?: AbortSignal): Promise<TicketPage>
  getTicket(id: number, signal?: AbortSignal): Promise<Ticket>
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
  }
}
