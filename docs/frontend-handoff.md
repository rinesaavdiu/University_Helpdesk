# Frontend handoff: student screens

The student screens in `frontend/` are finished and were checked in the
browser against the real API (see the student walkthrough in
[`demo-runbook.md`](demo-runbook.md#student-screens-in-the-browser)). They do
not depend on sign-in: they get every ticket and category through one small
interface, and the code that sends requests is in one file. This page lists
what the sign-in, technician, and Compose work needs to connect to.

Still to build (Domenika): the sign-in screen and session, role guards, the
technician dashboard, and serving the frontend from Docker Compose.

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
(a `RouteObject[]`), and `frontend/src/router.tsx` spreads them into the app
router under `AppLayout`. To protect them, wrap the array in a guard route
instead of editing each page:

```tsx
{ element: <RequireRole role="Student" />, children: studentRoutes },
```

`RequireRole` is yours to write: render `<Outlet />` for a signed-in student,
otherwise redirect to `paths.login`. `homePathByRole` in `routes.ts` gives the
page to open after sign-in for each role.

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

## The integration seam: `ApiRequest`

All requests go through one function type in `frontend/src/api/http.ts`:

```ts
type ApiRequest = (path: string, init?: RequestInit) => Promise<Response>
```

`frontend/src/api/appApi.ts` builds the app's instance. Today it sends no
token, which is why the app shows "Sign in required":

```ts
export const apiRequest: ApiRequest = createFetchRequest({ baseUrl: apiBaseUrl })
export const studentTicketApi = createStudentTicketApi(apiRequest)
```

To connect sign-in, replace `apiRequest` with your authenticated client.
`createFetchRequest` already accepts `getAccessToken`, which adds
`Authorization: Bearer <token>`:

```ts
export const apiRequest = createFetchRequest({ baseUrl: apiBaseUrl, getAccessToken: () => session.accessToken })
```

If the client should also react to an expired token (for example, sign the
user out and go to `/login` on `401`), wrap the request function; the student
pages need no change. The technician dashboard can reuse `apiRequest` and
`requestJson` with its own interface in the same style.

## API address

The app calls `/api/...`. `npm run dev` and `npm run preview` forward `/api`
to `HELPDESK_API_URL` (default `http://127.0.0.1:58080`, the Compose gateway)
and remove the `/api` prefix (`frontend/vite.config.ts`). For Compose, either
serve the built `frontend/dist/` behind a proxy that does the same, or build
with `VITE_API_BASE_URL` set to the API's address (the API would then need
CORS for that origin).

## Development harness

`frontend/dev-harness/` serves the student routes with a pasted student token
at `http://localhost:5173/dev-harness/` (`npm run dev` only; it is not in
`npm run build`, and `frontend/dist/` contains none of it). It keeps the token
in that tab's session storage, uses a hash router, and supplies its own
`StudentTicketApi` built from `createFetchRequest` with `getAccessToken`, the
same way the real client will. Once sign-in works in the app, the harness can
be deleted together with the `dev-harness` entry in
`frontend/tsconfig.app.json` and the harness paragraph in the README.

Never commit a token or password, and hide them before taking screenshots.
