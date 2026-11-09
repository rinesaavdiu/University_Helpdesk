import { useCallback, useId, useRef, useState, type FormEvent } from 'react'
import { Link, useNavigate } from 'react-router'
import { describeError, isApiError } from '../api/errors'
import { useStudentTicketApi } from '../api/StudentTicketApiContext'
import { PageHeader } from '../components/PageHeader'
import { useApiData } from '../hooks/useApiData'
import { paths, studentTicketPath } from '../routes'
import type { StudentTicketLocationState } from './StudentTicketDetailPage'

const roomMaxLength = 50
const descriptionMinLength = 10
const descriptionMaxLength = 2000

type Field = 'room' | 'categoryId' | 'description'
type FieldErrors = Partial<Record<Field, string>>
const fields: Field[] = ['room', 'categoryId', 'description']

type FormValues = {
  room: string
  categoryId: string
  description: string
}

function validate(values: FormValues): FieldErrors {
  const errors: FieldErrors = {}
  const room = values.room.trim()
  const description = values.description.trim()
  if (room.length === 0) {
    errors.room = 'Enter the room, for example A-204.'
  } else if (room.length > roomMaxLength) {
    errors.room = `The room can have at most ${roomMaxLength} characters.`
  }
  if (!values.categoryId) {
    errors.categoryId = 'Choose a category.'
  }
  if (description.length < descriptionMinLength) {
    errors.description = `Describe the problem in at least ${descriptionMinLength} characters.`
  } else if (description.length > descriptionMaxLength) {
    errors.description = `The description can have at most ${descriptionMaxLength} characters.`
  }
  return errors
}

/** Splits a 400 response into messages for the form fields and messages for the whole form. */
function readServerErrors(errors: Record<string, string[]>): { fieldErrors: FieldErrors; other: string[] } {
  const fieldErrors: FieldErrors = {}
  const other: string[] = []
  for (const [key, messages] of Object.entries(errors)) {
    const field = fields.find((name) => name.toLowerCase() === key.toLowerCase())
    if (field) {
      fieldErrors[field] = messages.join(' ')
    } else {
      other.push(...messages)
    }
  }
  return { fieldErrors, other }
}

export function ReportIssuePage() {
  const api = useStudentTicketApi()
  const navigate = useNavigate()
  const id = useId()
  const formRef = useRef<HTMLFormElement>(null)

  const loadCategories = useCallback((signal: AbortSignal) => api.listCategories(signal), [api])
  const [categories, reloadCategories] = useApiData(loadCategories)

  const [values, setValues] = useState<FormValues>({ room: '', categoryId: '', description: '' })
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({})
  const [formError, setFormError] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)

  function update(field: keyof FormValues, value: string) {
    setValues((current) => ({ ...current, [field]: value }))
    setFieldErrors((current) => ({ ...current, [field]: undefined }))
  }

  function focusFirstError(errors: FieldErrors) {
    const first = fields.find((field) => errors[field])
    if (first) {
      formRef.current?.querySelector<HTMLElement>(`[name="${first}"]`)?.focus()
    }
  }

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setFormError(null)
    const errors = validate(values)
    setFieldErrors(errors)
    if (Object.keys(errors).length > 0) {
      focusFirstError(errors)
      return
    }

    setSubmitting(true)
    try {
      const ticket = await api.createTicket({
        room: values.room.trim(),
        categoryId: Number(values.categoryId),
        description: values.description.trim(),
      })
      const state: StudentTicketLocationState = { notice: `Your issue was reported as ticket #${ticket.id}.` }
      navigate(studentTicketPath(ticket.id), { state })
    } catch (error) {
      setSubmitting(false)
      if (isApiError(error, 400) && error.problem?.errors) {
        const server = readServerErrors(error.problem.errors)
        setFieldErrors(server.fieldErrors)
        setFormError(server.other.length > 0 ? server.other.join(' ') : 'Check the highlighted fields.')
        focusFirstError(server.fieldErrors)
        return
      }
      const { title, message } = describeError(error)
      setFormError(`${title}. ${message}`)
    }
  }

  const descriptionLength = values.description.trim().length
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
    <section className="page page--form">
      <Link to={paths.studentTickets} className="back-link">
        ← My tickets
      </Link>
      <PageHeader title="Report an issue" description="Tell the technicians which room has a problem and what is wrong." />

      <form ref={formRef} className="panel form" onSubmit={submit} noValidate>
        {formError && (
          <div className="alert" role="alert">
            {formError}
          </div>
        )}

        <div className="field">
          <label htmlFor={`${id}-room`}>Room</label>
          <input
            {...fieldProps('room')}
            className="input"
            value={values.room}
            maxLength={roomMaxLength}
            autoComplete="off"
            placeholder="For example A-204"
            onChange={(event) => update('room', event.target.value)}
          />
          {fieldError('room')}
        </div>

        <div className="field">
          <label htmlFor={`${id}-categoryId`}>Category</label>
          <select
            {...fieldProps('categoryId')}
            className="select"
            value={values.categoryId}
            disabled={categories.status !== 'success'}
            onChange={(event) => update('categoryId', event.target.value)}
          >
            <option value="">
              {categories.status === 'loading' ? 'Loading categories…' : 'Choose a category'}
            </option>
            {categories.status === 'success' &&
              categories.data.map((category) => (
                <option key={category.id} value={category.id}>
                  {category.name}
                </option>
              ))}
          </select>
          {categories.status === 'error' && (
            <p className="field__error">
              {describeError(categories.error).message}{' '}
              <button type="button" className="link-button" onClick={reloadCategories}>
                Load categories again
              </button>
            </p>
          )}
          {fieldError('categoryId')}
        </div>

        <div className="field">
          <label htmlFor={`${id}-description`}>Description</label>
          <textarea
            {...fieldProps('description')}
            className="input textarea"
            rows={6}
            value={values.description}
            maxLength={descriptionMaxLength}
            onChange={(event) => update('description', event.target.value)}
          />
          <p className="field__hint">
            What is wrong, and since when? {descriptionLength}/{descriptionMaxLength} characters, at least{' '}
            {descriptionMinLength}.
          </p>
          {fieldError('description')}
        </div>

        <div className="form-actions">
          <button type="submit" className="button" disabled={submitting}>
            {submitting ? 'Reporting…' : 'Report issue'}
          </button>
          <Link to={paths.studentTickets} className="button button--secondary">
            Cancel
          </Link>
        </div>
      </form>
    </section>
  )
}
