import { createContext, useContext, type ReactNode } from 'react'
import type { StudentTicketApi } from './studentTicketApi'

const StudentTicketApiContext = createContext<StudentTicketApi | null>(null)

type StudentTicketApiProviderProps = {
  api: StudentTicketApi
  children: ReactNode
}

export function StudentTicketApiProvider({ api, children }: StudentTicketApiProviderProps) {
  return <StudentTicketApiContext value={api}>{children}</StudentTicketApiContext>
}

export function useStudentTicketApi(): StudentTicketApi {
  const api = useContext(StudentTicketApiContext)
  if (!api) {
    throw new Error('useStudentTicketApi must be used inside a StudentTicketApiProvider.')
  }
  return api
}
