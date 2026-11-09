import { useCallback, useEffect, useState } from 'react'

export type ApiData<T> =
  | { status: 'loading' }
  | { status: 'error'; error: unknown }
  | { status: 'success'; data: T }

/**
 * Runs `load` when it changes and returns its state and a `reload` function. Wrap `load` in
 * `useCallback`, or it runs on every render.
 */
export function useApiData<T>(load: (signal: AbortSignal) => Promise<T>) {
  const [state, setState] = useState<ApiData<T>>({ status: 'loading' })
  const [attempt, setAttempt] = useState(0)

  useEffect(() => {
    const controller = new AbortController()
    setState({ status: 'loading' })
    load(controller.signal).then(
      (data) => {
        if (!controller.signal.aborted) setState({ status: 'success', data })
      },
      (error: unknown) => {
        if (!controller.signal.aborted) setState({ status: 'error', error })
      },
    )
    return () => controller.abort()
  }, [load, attempt])

  const reload = useCallback(() => setAttempt((value) => value + 1), [])

  return [state, reload] as const
}
