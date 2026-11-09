import type { ListTicketsParams } from './types'

/** The `GET /tickets` path with the given filter and page, for example `/tickets?status=Open&page=2`. */
export function ticketListPath({ status, page, pageSize }: ListTicketsParams): string {
  const query = new URLSearchParams()
  if (status) query.set('status', status)
  if (page) query.set('page', String(page))
  if (pageSize) query.set('pageSize', String(pageSize))
  const search = query.toString()
  return search ? `/tickets?${search}` : '/tickets'
}
