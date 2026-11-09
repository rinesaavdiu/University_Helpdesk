import { ticketStatuses, type TicketStatus } from './api/types'

/** Reads the `status` search parameter; anything that is not a ticket status means "all". */
export function parseStatus(value: string | null): TicketStatus | undefined {
  return ticketStatuses.find((status) => status === value)
}

/** Reads the `page` search parameter; anything that is not a positive integer means page 1. */
export function parsePage(value: string | null): number {
  const page = Number(value)
  return Number.isInteger(page) && page >= 1 ? page : 1
}

/** The search string for a ticket list, for example `?status=Open&page=2`, or `''` for the default. */
export function listSearch(status: TicketStatus | undefined, page: number): string {
  const params = new URLSearchParams()
  if (status) params.set('status', status)
  if (page > 1) params.set('page', String(page))
  const search = params.toString()
  return search ? `?${search}` : ''
}
