import { requestJson, type ApiRequest } from './http'
import { ticketListPath } from './ticketQuery'
import type { ListTicketsParams, Ticket, TicketPage } from './types'

/** The ticket operations the technician screens use. */
export interface TechnicianTicketApi {
  listTickets(params: ListTicketsParams, signal?: AbortSignal): Promise<TicketPage>
  getTicket(id: number, signal?: AbortSignal): Promise<Ticket>
}

export function createTechnicianTicketApi(request: ApiRequest): TechnicianTicketApi {
  return {
    listTickets(params, signal) {
      return requestJson<TicketPage>(request, ticketListPath(params), { signal })
    },

    getTicket(id, signal) {
      return requestJson<Ticket>(request, `/tickets/${id}`, { signal })
    },
  }
}
