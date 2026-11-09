import type { TicketStatus } from '../api/types'

export const statusLabels: Record<TicketStatus, string> = {
  Open: 'Open',
  InProgress: 'In progress',
  Resolved: 'Resolved',
}

export function StatusBadge({ status }: { status: TicketStatus }) {
  return <span className={`badge badge--${status}`}>{statusLabels[status]}</span>
}
