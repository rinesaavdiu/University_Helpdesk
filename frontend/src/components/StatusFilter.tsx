import { useId } from 'react'
import { ticketStatuses, type TicketStatus } from '../api/types'
import { parseStatus } from '../ticketListSearch'
import { statusLabels } from './StatusBadge'

type StatusFilterProps = {
  status: TicketStatus | undefined
  onChange: (status: TicketStatus | undefined) => void
}

export function StatusFilter({ status, onChange }: StatusFilterProps) {
  const id = useId()
  return (
    <div className="toolbar">
      <label htmlFor={id}>Status</label>
      <select
        id={id}
        className="select"
        value={status ?? ''}
        onChange={(event) => onChange(parseStatus(event.target.value))}
      >
        <option value="">All statuses</option>
        {ticketStatuses.map((value) => (
          <option key={value} value={value}>
            {statusLabels[value]}
          </option>
        ))}
      </select>
    </div>
  )
}
