import { StrictMode, useMemo, useState, type FormEvent } from 'react'
import { createRoot } from 'react-dom/client'
import { createHashRouter, Navigate } from 'react-router'
import { RouterProvider } from 'react-router/dom'
import { createFetchRequest } from '../src/api/http'
import { createStudentTicketApi } from '../src/api/studentTicketApi'
import { StudentTicketApiProvider } from '../src/api/StudentTicketApiContext'
import { apiBaseUrl } from '../src/config'
import { AppLayout } from '../src/layout/AppLayout'
import { NotFoundPage } from '../src/pages/NotFoundPage'
import { RouteErrorPage } from '../src/pages/RouteErrorPage'
import { paths } from '../src/routes'
import { studentRoutes } from '../src/studentRoutes'
import '../src/styles.css'
import './harness.css'

const tokenStorageKey = 'helpdesk-dev-harness-token'

const router = createHashRouter([
  {
    element: <AppLayout />,
    children: [
      {
        errorElement: <RouteErrorPage />,
        children: [
          { index: true, element: <Navigate to={paths.studentTickets} replace /> },
          ...studentRoutes,
          { path: '*', element: <NotFoundPage /> },
        ],
      },
    ],
  },
])

type TokenClaims = {
  email?: string
  role?: string
  exp?: number
}

function readClaims(token: string): TokenClaims | null {
  try {
    const payload = token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/')
    return JSON.parse(atob(payload)) as TokenClaims
  } catch {
    return null
  }
}

function HarnessApp() {
  const [token, setToken] = useState(() => sessionStorage.getItem(tokenStorageKey))
  const api = useMemo(
    () => createStudentTicketApi(createFetchRequest({ baseUrl: apiBaseUrl, getAccessToken: () => token })),
    [token],
  )

  function changeToken(value: string | null) {
    if (value) {
      sessionStorage.setItem(tokenStorageKey, value)
    } else {
      sessionStorage.removeItem(tokenStorageKey)
    }
    setToken(value)
  }

  return (
    <>
      <TokenBar token={token} onChange={changeToken} />
      {token ? (
        <StudentTicketApiProvider api={api}>
          <RouterProvider router={router} />
        </StudentTicketApiProvider>
      ) : (
        <p className="harness-hint">Paste a student access token above to open the student screens.</p>
      )}
    </>
  )
}

function TokenBar({ token, onChange }: { token: string | null; onChange: (token: string | null) => void }) {
  const claims = token ? readClaims(token) : null
  const expires = claims?.exp ? new Date(claims.exp * 1000) : null

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const value = new FormData(event.currentTarget).get('token')?.toString().trim().replace(/^Bearer\s+/i, '')
    if (value) onChange(value)
  }

  return (
    <div className="harness-bar">
      <strong>Development harness</strong>
      <span>Not part of the app. The token stays in this browser tab only.</span>
      {token ? (
        <span className="harness-bar__session">
          {claims?.email ?? 'Unknown account'} ({claims?.role ?? 'unknown role'})
          {expires && `, token expires ${expires.toLocaleTimeString()}`}
          {claims?.role !== 'Student' && ' – not a student token'}
          <button type="button" className="harness-bar__button" onClick={() => onChange(null)}>
            Forget token
          </button>
        </span>
      ) : (
        <form className="harness-bar__form" onSubmit={submit}>
          <label htmlFor="harness-token">Student access token</label>
          <input id="harness-token" name="token" type="password" autoComplete="off" spellCheck={false} />
          <button type="submit" className="harness-bar__button">
            Use token
          </button>
        </form>
      )}
    </div>
  )
}

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <HarnessApp />
  </StrictMode>,
)
