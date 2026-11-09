import { apiBaseUrl } from '../config'
import { createFetchRequest, type ApiRequest } from './http'
import { createStudentTicketApi } from './studentTicketApi'

export const apiRequest: ApiRequest = createFetchRequest({ baseUrl: apiBaseUrl })

export const studentTicketApi = createStudentTicketApi(apiRequest)
