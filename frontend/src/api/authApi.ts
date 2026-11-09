import type { SessionUser } from '../auth/session'
import { requestJson, type ApiRequest } from './http'

export type LoginResponse = {
  accessToken: string
  tokenType: string
  /** Seconds until the token expires. */
  expiresIn: number
  user: SessionUser
}

export function login(request: ApiRequest, email: string, password: string): Promise<LoginResponse> {
  return requestJson<LoginResponse>(request, '/auth/login', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email, password }),
  })
}
