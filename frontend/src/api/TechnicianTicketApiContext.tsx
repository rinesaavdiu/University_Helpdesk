import { createContext, useContext, type ReactNode } from 'react'
import type { TechnicianTicketApi } from './technicianTicketApi'

const TechnicianTicketApiContext = createContext<TechnicianTicketApi | null>(null)

type TechnicianTicketApiProviderProps = {
  api: TechnicianTicketApi
  children: ReactNode
}

export function TechnicianTicketApiProvider({ api, children }: TechnicianTicketApiProviderProps) {
  return <TechnicianTicketApiContext value={api}>{children}</TechnicianTicketApiContext>
}

export function useTechnicianTicketApi(): TechnicianTicketApi {
  const api = useContext(TechnicianTicketApiContext)
  if (!api) {
    throw new Error('useTechnicianTicketApi must be used inside a TechnicianTicketApiProvider.')
  }
  return api
}
