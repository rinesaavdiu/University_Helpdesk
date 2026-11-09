import { ApiError, type ProblemDetails } from './errors'

/**
 * Sends one request to the API. `path` starts with `/`, for example `/tickets?page=2`.
 * Implementations add the base URL and any credentials.
 */
export type ApiRequest = (path: string, init?: RequestInit) => Promise<Response>

type FetchRequestOptions = {
  baseUrl: string
  getAccessToken?: () => string | null
}

export function createFetchRequest({ baseUrl, getAccessToken }: FetchRequestOptions): ApiRequest {
  return (path, init = {}) => {
    const headers = new Headers(init.headers)
    headers.set('Accept', 'application/json')
    const token = getAccessToken?.()
    if (token) {
      headers.set('Authorization', `Bearer ${token}`)
    }
    return fetch(`${baseUrl}${path}`, { ...init, headers })
  }
}

export async function requestJson<T>(request: ApiRequest, path: string, init?: RequestInit): Promise<T> {
  const response = await request(path, init)
  if (!response.ok) {
    throw new ApiError(response.status, await readProblem(response))
  }
  if (response.status === 204) {
    return undefined as T
  }
  return (await response.json()) as T
}

async function readProblem(response: Response): Promise<ProblemDetails | null> {
  if (!response.headers.get('Content-Type')?.includes('json')) {
    return null
  }
  try {
    return (await response.json()) as ProblemDetails
  } catch {
    return null
  }
}
