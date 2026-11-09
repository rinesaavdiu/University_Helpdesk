# Frontend handoff: routes, sign-in, and the API client

The student screens in `frontend/` are finished and were checked in the
browser against the real API (see the student walkthrough in
[`demo-runbook.md`](demo-runbook.md#student-screens-in-the-browser)). They do
not depend on sign-in directly: they get every ticket and category through one
small interface, and the code that sends requests is in one file. Sign-in,
the session, the role guards, and the technician ticket queue use the same
file.

Still to build: technician status changes and serving the frontend from
Docker Compose.

## Student routes

The paths are defined once in `frontend/src/routes.ts` (`paths`, plus
`studentTicketPath(id)` and `studentEditTicketPath(id)` for links). Use those
helpers instead of writing paths by hand.

| Path | Page | What it does |
| ---- | ---- | ------------ |
| `/student/tickets` | `StudentTicketsPage` | The student's own tickets, newest first, 10 per page. `?status=Open\|InProgress\|Resolved` and `?page=N` are kept in the URL. |
| `/student/tickets/new` | `ReportIssuePage` | Report form (room, category, description). After `201` it opens the new ticket with a notice. |
| `/student/tickets/:ticketId` | `StudentTicketDetailPage` | One ticket. **Edit** and **Delete** (with a confirmation) are shown only while the ticket is `Open`. |
| `/student/tickets/:ticketId/edit` | `EditTicketPage` | Edits an open ticket. Sends only the changed fields plus `version`; on `409` it reloads the ticket and lets the student reapply or discard their changes. |

`frontend/src/studentRoutes.tsx` exports these as `studentRoutes`
(a `RouteObject[]`), and `frontend/src/router.tsx` wraps them in a guard
route instead of protecting each page:

```tsx
{ element: <RequireRole role="Student" />, children: studentRoutes },
```

`RequireRole` (`frontend/src/auth/RequireRole.tsx`) renders the child routes
for a signed-in user with that role. A signed-out visitor goes to `/login`,
and the page they asked for is passed along so sign-in can return to it. A
signed-in user with the other role sees "Not allowed" with a link to their own
tickets. The technician routes use the same guard with `role="Technician"`.
`homePathByRole` in `routes.ts` gives each role's start page, which `/` and
sign-in open.

## Technician routes

| Path | Page | What it does |
| ---- | ---- | ------------ |
| `/technician/tickets` | `TechnicianTicketsPage` | Every student's tickets, newest first, 10 per page, with the reporting student's ID on each card. `?status=` and `?page=` are kept in the URL. |
| `/technician/tickets/:ticketId` | `TechnicianTicketDetailPage` | One ticket, including the reporting student. The back link returns to the same filter and page. |

`frontend/src/technicianRoutes.tsx` exports them as `technicianRoutes`, which
`router.tsx` wraps in `<RequireRole role="Technician" />`; links use
`technicianTicketPath(id)`. The API returns only the student's ID with a
ticket, not their name or email, so the screens show "Student #1".

Both roles share these pieces: `TicketList` (with `showStudent` for
technicians), `TicketDetails` (the ticket panel, also with `showStudent`),
`StatusFilter`, `Pagination`, the loading, empty, and error states, and the
URL helpers in `frontend/src/ticketListSearch.ts`.

## Session

`frontend/src/auth/session.ts` holds the session: the access token, its
expiry time, and the user from `POST /auth/login`. Components read it with
`useSession()`; code outside React uses `getSession()`. `startSession` and
`endSession('signedOut' | 'expired')` change it.

- It is stored in the tab's `sessionStorage`, so a refresh keeps the user
  signed in, a new tab signs in separately, and closing the tab signs out.
- The API has no refresh tokens, so a session lasts as long as the token, 60
  minutes. A timer ends it at the expiry time, and a `401` on any request
  made while signed in also ends it. The guard then sends the user to
  `/login`, which says "Your session has expired" and returns them to the
  page afterwards.
- A stored session that has already expired when the page loads is dropped
  the same way.
- `sessionStorage` can be read by scripts on the page, like any
  browser-side token store; the app renders all API text as plain text and
  loads no third-party scripts.

## `StudentTicketApi`

`frontend/src/api/studentTicketApi.ts`:

```ts
interface StudentTicketApi {
  listTickets(params: ListTicketsParams, signal?: AbortSignal): Promise<TicketPage>
  getTicket(id: number, signal?: AbortSignal): Promise<Ticket>
  listCategories(signal?: AbortSignal): Promise<Category[]>
  createTicket(input: CreateTicketInput): Promise<Ticket>
  updateTicket(id: number, input: UpdateTicketInput): Promise<Ticket>
  deleteTicket(id: number): Promise<void>
}
```

The types are in `frontend/src/api/types.ts` and match
[`api-contract.md`](api-contract.md). Pages get the implementation from
`useStudentTicketApi()`; `StudentTicketApiProvider` in `src/main.tsx` supplies
it. Failed requests throw `ApiError` (`src/api/errors.ts`) with the HTTP
status and the problem details; `describeError` turns it into a title and
message, and a `401` becomes "Sign in required".

## `TechnicianTicketApi`

`frontend/src/api/technicianTicketApi.ts`, supplied by
`TechnicianTicketApiProvider` and read with `useTechnicianTicketApi()`:

```ts
interface TechnicianTicketApi {
  listTickets(params: ListTicketsParams, signal?: AbortSignal): Promise<TicketPage>
  getTicket(id: number, signal?: AbortSignal): Promise<Ticket>
}
```

Both ticket interfaces build the `GET /tickets` query with `ticketListPath`
(`frontend/src/api/ticketQuery.ts`).

## The integration seam: `ApiRequest`

All requests go through one function type in `frontend/src/api/http.ts`:

```ts
type ApiRequest = (path: string, init?: RequestInit) => Promise<Response>
```

`frontend/src/api/appApi.ts` builds the app's one authenticated client. It
adds `Authorization: Bearer <token>` from the session through
`getAccessToken`, and ends the session on a `401`:

```ts
const fetchRequest = createFetchRequest({
  baseUrl: apiBaseUrl,
  getAccessToken: () => getSession()?.accessToken ?? null,
})
export const apiRequest: ApiRequest = async (path, init) => { /* fetchRequest, then endSession('expired') on 401 */ }
export const studentTicketApi = createStudentTicketApi(apiRequest)
export const technicianTicketApi = createTechnicianTicketApi(apiRequest)
```

The sign-in page calls `login(apiRequest, email, password)` from
`frontend/src/api/authApi.ts`; with no session it sends no token.

## API address

The app calls `/api/...`. `npm run dev` and `npm run preview` forward `/api`
to `HELPDESK_API_URL` (default `http://127.0.0.1:58080`, the Compose gateway)
and remove the `/api` prefix (`frontend/vite.config.ts`). For Compose, either
serve the built `frontend/dist/` behind a proxy that does the same, or build
with `VITE_API_BASE_URL` set to the API's address (the API would then need
CORS for that origin).

## Development harness (removed)

Before sign-in existed, `frontend/dev-harness/` showed the student screens
with a pasted token. It was removed when the React sign-in replaced it; sign
in at `/login` instead.

Never commit a token or password, and hide them before taking screenshots.
