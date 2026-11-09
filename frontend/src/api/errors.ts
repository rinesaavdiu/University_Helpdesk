export type ProblemDetails = {
  type?: string
  title?: string
  status?: number
  detail?: string
  traceId?: string
  errors?: Record<string, string[]>
}

export class ApiError extends Error {
  readonly status: number
  readonly problem: ProblemDetails | null

  constructor(status: number, problem: ProblemDetails | null) {
    super(problem?.detail ?? problem?.title ?? `The request failed with status ${status}.`)
    this.name = 'ApiError'
    this.status = status
    this.problem = problem
  }
}

export function isApiError(error: unknown, status?: number): error is ApiError {
  return error instanceof ApiError && (status === undefined || error.status === status)
}

export type ErrorDescription = {
  title: string
  message: string
}

export function describeError(error: unknown): ErrorDescription {
  if (error instanceof ApiError) {
    if (error.status === 401) {
      return {
        title: 'Sign in required',
        message: 'You are not signed in, or your session has expired. Sign in and try again.',
      }
    }
    if (error.status === 403) {
      return { title: 'Not allowed', message: 'Your account is not allowed to do this.' }
    }
    if (error.status === 404) {
      return { title: 'Not found', message: 'The item does not exist or is not available to you.' }
    }
    if (error.status >= 500) {
      return {
        title: 'Server error',
        message: 'The server could not complete the request. Try again in a moment.',
      }
    }
    return { title: 'Request failed', message: error.message }
  }
  if (error instanceof TypeError) {
    return {
      title: 'Cannot reach the server',
      message: 'Check your connection and that the API is running, then try again.',
    }
  }
  return { title: 'Something went wrong', message: 'An unexpected error occurred. Try again.' }
}
