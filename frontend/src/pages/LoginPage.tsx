import { useId, useRef, useState, type FormEvent } from 'react'
import { Navigate, useLocation, useNavigate } from 'react-router'
import { apiRequest } from '../api/appApi'
import { login } from '../api/authApi'
import { describeError, isApiError } from '../api/errors'
import { startSession, useSessionState } from '../auth/session'
import { PageHeader } from '../components/PageHeader'
import { canVisit, homePathByRole, type LoginLocationState } from '../routes'

const emailMaxLength = 254
const passwordMaxLength = 128

type Field = 'email' | 'password'
type FieldErrors = Partial<Record<Field, string>>

function validate(email: string, password: string): FieldErrors {
  const errors: FieldErrors = {}
  if (email.length === 0) {
    errors.email = 'Enter your university email.'
  } else if (!/^[^\s@]+@[^\s@]+$/.test(email)) {
    errors.email = 'Enter an email address such as name@uni.test.'
  }
  if (password.length === 0) {
    errors.password = 'Enter your password.'
  }
  return errors
}

const endMessages = {
  expired: 'Your session has expired. Sign in again to continue.',
  signedOut: 'You have signed out.',
}

export function LoginPage() {
  const { session, ended } = useSessionState()
  const navigate = useNavigate()
  const from = (useLocation().state as LoginLocationState | null)?.from
  const id = useId()
  const formRef = useRef<HTMLFormElement>(null)

  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({})
  const [formError, setFormError] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)

  if (session && !submitting) {
    return <Navigate to={homePathByRole[session.user.role]} replace />
  }

  function focusFirstError(errors: FieldErrors) {
    const first = (['email', 'password'] as const).find((field) => errors[field])
    if (first) {
      formRef.current?.querySelector<HTMLElement>(`[name="${first}"]`)?.focus()
    }
  }

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setFormError(null)
    const trimmedEmail = email.trim()
    const errors = validate(trimmedEmail, password)
    setFieldErrors(errors)
    if (Object.keys(errors).length > 0) {
      focusFirstError(errors)
      return
    }

    setSubmitting(true)
    try {
      const result = await login(apiRequest, trimmedEmail, password)
      startSession({
        accessToken: result.accessToken,
        expiresAt: Date.now() + result.expiresIn * 1000,
        user: result.user,
      })
      const role = result.user.role
      navigate(from && canVisit(role, from) ? from : homePathByRole[role], { replace: true })
    } catch (error) {
      setPassword('')
      setSubmitting(false)
      if (isApiError(error, 401)) {
        setFormError('The email or password is incorrect.')
      } else if (isApiError(error, 400) && error.problem?.errors) {
        setFormError(Object.values(error.problem.errors).flat().join(' '))
      } else {
        const { title, message } = describeError(error)
        setFormError(`${title}. ${message}`)
      }
    }
  }

  const errorId = (field: Field) => `${id}-${field}-error`
  const fieldProps = (field: Field) => ({
    id: `${id}-${field}`,
    name: field,
    'aria-invalid': fieldErrors[field] ? true : undefined,
    'aria-describedby': fieldErrors[field] ? errorId(field) : undefined,
  })
  const fieldError = (field: Field) =>
    fieldErrors[field] && (
      <p id={errorId(field)} className="field__error">
        {fieldErrors[field]}
      </p>
    )

  return (
    <section className="page page--narrow">
      <PageHeader title="Sign in" description="Students and technicians sign in with their university email." />
      {ended && !formError && (
        <p className={ended === 'expired' ? 'alert alert--warning' : 'notice'} role="status">
          {endMessages[ended]}
        </p>
      )}
      <form ref={formRef} className="panel form" onSubmit={submit} noValidate>
        {formError && (
          <div className="alert" role="alert">
            {formError}
          </div>
        )}
        <div className="field">
          <label htmlFor={`${id}-email`}>Email</label>
          <input
            {...fieldProps('email')}
            className="input"
            type="email"
            autoComplete="username"
            maxLength={emailMaxLength}
            value={email}
            onChange={(event) => {
              setEmail(event.target.value)
              setFieldErrors((current) => ({ ...current, email: undefined }))
            }}
          />
          {fieldError('email')}
        </div>
        <div className="field">
          <label htmlFor={`${id}-password`}>Password</label>
          <input
            {...fieldProps('password')}
            className="input"
            type="password"
            autoComplete="current-password"
            maxLength={passwordMaxLength}
            value={password}
            onChange={(event) => {
              setPassword(event.target.value)
              setFieldErrors((current) => ({ ...current, password: undefined }))
            }}
          />
          {fieldError('password')}
        </div>
        <div className="form-actions">
          <button type="submit" className="button" disabled={submitting}>
            {submitting ? 'Signing in…' : 'Sign in'}
          </button>
        </div>
      </form>
    </section>
  )
}
