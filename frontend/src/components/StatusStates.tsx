import type { ReactNode } from 'react'

type LoadingStateProps = {
  label?: string
}

export function LoadingState({ label = 'Loading…' }: LoadingStateProps) {
  return (
    <div className="state state--loading" role="status" aria-live="polite">
      <span className="spinner" aria-hidden="true" />
      <p>{label}</p>
    </div>
  )
}

type EmptyStateProps = {
  title: string
  children?: ReactNode
  action?: ReactNode
}

export function EmptyState({ title, children, action }: EmptyStateProps) {
  return (
    <div className="state state--empty">
      <h2>{title}</h2>
      {children && <div className="state__body">{children}</div>}
      {action && <div className="state__action">{action}</div>}
    </div>
  )
}

type ErrorStateProps = {
  title?: string
  message: ReactNode
  onRetry?: () => void
}

export function ErrorState({ title = 'Something went wrong', message, onRetry }: ErrorStateProps) {
  return (
    <div className="state state--error" role="alert">
      <h2>{title}</h2>
      <div className="state__body">{message}</div>
      {onRetry && (
        <div className="state__action">
          <button type="button" className="button" onClick={onRetry}>
            Try again
          </button>
        </div>
      )}
    </div>
  )
}
