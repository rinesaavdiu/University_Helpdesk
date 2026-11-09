import { endSession, getSession } from '../auth/session'
import { apiBaseUrl } from '../config'
import { createFetchRequest, type ApiRequest } from './http'
import { createStudentTicketApi } from './studentTicketApi'
import { createTechnicianTicketApi } from './technicianTicketApi'

const fetchRequest = createFetchRequest({
  baseUrl: apiBaseUrl,
  getAccessToken: () => getSession()?.accessToken ?? null,
})

/** Sends requests with the signed-in user's token. A `401` while signed in ends the session. */
export const apiRequest: ApiRequest = async (path, init) => {
  const signedIn = getSession() !== null
  const response = await fetchRequest(path, init)
  if (response.status === 401 && signedIn) {
    endSession('expired')
  }
  return response
}

export const studentTicketApi = createStudentTicketApi(apiRequest)

export const technicianTicketApi = createTechnicianTicketApi(apiRequest)
