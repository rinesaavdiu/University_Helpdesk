# API contract

**University Helpdesk API** — *Sistem për raportimin e problemeve teknike në universitet*

This document defines the planned HTTP contract. The requirements are in
[`requirements.md`](requirements.md) and the design is in
[`design.md`](design.md).

## Conventions

- Request and response bodies are JSON (`application/json`). Error bodies use
  `application/problem+json`.
- Every endpoint except `POST /auth/login` requires the header
  `Authorization: Bearer <token>`.
- Field names use camelCase. Timestamps are UTC in ISO 8601 format, for example
  `2026-11-07T09:15:00Z`.
- Roles are `Student` and `Technician`. Ticket statuses are `Open`,
  `InProgress`, and `Resolved`.
- Unknown fields and server-controlled fields (`id`, `studentId`, `status` on
  create or edit, `version` on create, timestamps) in a request body are
  rejected with `400`.

## Endpoints

| Method   | Path                    | Permitted caller                     | Behavior                                                                                                    | Success                            |
| -------- | ----------------------- | ------------------------------------ | ----------------------------------------------------------------------------------------------------------- | ---------------------------------- |
| `POST`   | `/auth/login`           | Public                               | Checks email and password and returns a JWT.                                                                | `200`                              |
| `GET`    | `/categories`           | `Student` or `Technician`            | Lists active categories, ordered by name.                                                                   | `200`                              |
| `POST`   | `/tickets`              | `Student`                            | Creates a ticket owned by the caller with status `Open`.                                                    | `201` with `Location: /tickets/{id}` |
| `GET`    | `/tickets`              | `Student` or `Technician`            | Students get their own tickets; technicians get all. Optional `status` filter and pagination.              | `200`                              |
| `GET`    | `/tickets/{id}`         | Student owner or `Technician`        | Returns one ticket.                                                                                         | `200`                              |
| `PATCH`  | `/tickets/{id}`         | Student owner                        | Edits `room`, `categoryId`, and/or `description` while the ticket is `Open`.                                | `200`                              |
| `DELETE` | `/tickets/{id}`         | Student owner                        | Deletes the ticket while it is `Open`.                                                                      | `204`, no body                     |
| `PATCH`  | `/tickets/{id}/status`  | `Technician`                         | Advances the ticket to the next allowed status (`Open → InProgress → Resolved`).                            | `200`                              |

## Authentication

### `POST /auth/login`

Request:

```json
{
  "email": "student1@uni.test",
  "password": "example-password"
}
```

Response `200`:

```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.example.signature",
  "tokenType": "Bearer",
  "expiresIn": 3600,
  "user": {
    "id": 1,
    "email": "student1@uni.test",
    "displayName": "Demo Student One",
    "role": "Student"
  }
}
```

| Field      | Rules                              |
| ---------- | ---------------------------------- |
| `email`    | Required, valid email, max 254 characters. |
| `password` | Required, max 128 characters.      |

Tokens expire after 60 minutes (`expiresIn` is in seconds). The token contains
the user ID (`sub`), email, and role. A missing or malformed field returns
`400`. A wrong email or password returns `401` with the same message in both
cases.

## Categories

### `GET /categories`

Response `200`:

```json
[
  { "id": 1, "name": "Audio" },
  { "id": 2, "name": "Computer" },
  { "id": 3, "name": "Projector" }
]
```

## Tickets

### Ticket representation

Every endpoint that returns a ticket uses this shape:

```json
{
  "id": 42,
  "studentId": 1,
  "room": "A-204",
  "category": { "id": 3, "name": "Projector" },
  "description": "The projector turns on but shows no image from the HDMI cable.",
  "status": "Open",
  "version": 1,
  "createdAt": "2026-11-07T09:15:00Z",
  "updatedAt": "2026-11-07T09:15:00Z",
  "resolvedAt": null
}
```

### Field rules

| Field         | Rules                                                                     |
| ------------- | ------------------------------------------------------------------------- |
| `room`        | Required on create. 1–50 characters after trimming whitespace.            |
| `categoryId`  | Required on create. Must reference an active category.                    |
| `description` | Required on create. 10–2000 characters after trimming whitespace.         |
| `version`     | Required on edit and status change. Must match the ticket's current version. |
| `status`      | Required on status change. One of `Open`, `InProgress`, `Resolved`.       |

### `POST /tickets`

Request:

```json
{
  "room": "A-204",
  "categoryId": 3,
  "description": "The projector turns on but shows no image from the HDMI cable."
}
```

Response `201` with header `Location: /tickets/42` and the created ticket in
the body.

### `GET /tickets`

Query parameters:

| Parameter  | Default | Rules                                                |
| ---------- | ------- | ---------------------------------------------------- |
| `status`   | none    | Optional. One of `Open`, `InProgress`, `Resolved`.   |
| `page`     | `1`     | Integer, minimum `1`.                                |
| `pageSize` | `20`    | Integer, `1`–`100`.                                  |

Out-of-range or invalid values return `400`; they are not silently corrected.
Results are ordered by `createdAt`, newest first. A page beyond the last
returns an empty `items` array.

Response `200`:

```json
{
  "items": [
    {
      "id": 42,
      "studentId": 1,
      "room": "A-204",
      "category": { "id": 3, "name": "Projector" },
      "description": "The projector turns on but shows no image from the HDMI cable.",
      "status": "Open",
      "version": 1,
      "createdAt": "2026-11-07T09:15:00Z",
      "updatedAt": "2026-11-07T09:15:00Z",
      "resolvedAt": null
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 1
}
```

### `GET /tickets/{id}`

Response `200` with the ticket.

### `PATCH /tickets/{id}`

Send `version` and at least one of `room`, `categoryId`, or `description`.
Fields that are left out keep their current value.

Request:

```json
{
  "room": "A-206",
  "description": "The projector in A-206 turns on but shows no image from the HDMI cable.",
  "version": 1
}
```

Response `200` with the updated ticket. `version` is incremented and
`updatedAt` is set by the server.

### `DELETE /tickets/{id}`

Response `204` with no body.

### `PATCH /tickets/{id}/status`

`status` must be the next status in the workflow. Moving backwards, skipping
`InProgress`, or changing a `Resolved` ticket returns `409`.

Request:

```json
{
  "status": "InProgress",
  "version": 1
}
```

Response `200` with the updated ticket. When the new status is `Resolved`, the
server sets `resolvedAt` and queues a confirmation email to the ticket's
student.

## Errors

All errors use the Problem Details format:

```json
{
  "type": "https://httpstatuses.io/409",
  "title": "Conflict",
  "status": 409,
  "detail": "The ticket was changed by another request. Reload it and try again.",
  "traceId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01"
}
```

Validation errors (`400`) add an `errors` object keyed by field name:

```json
{
  "type": "https://httpstatuses.io/400",
  "title": "Bad Request",
  "status": 400,
  "detail": "One or more fields are invalid.",
  "traceId": "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01",
  "errors": {
    "description": ["Description must be between 10 and 2000 characters."]
  }
}
```

| Status | When                                                                                                                                                         |
| ------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `400`  | Invalid JSON, missing or invalid fields, unknown or server-controlled fields, invalid query parameters, an inactive or unknown `categoryId`.                |
| `401`  | Missing, invalid, or expired token; wrong email or password at login.                                                                                        |
| `403`  | Authenticated, but the role is not permitted for the endpoint (for example, a technician calling `POST /tickets` or a student calling `PATCH /tickets/{id}/status`). |
| `404`  | The ticket does not exist, or a student requests another student's ticket.                                                                                   |
| `409`  | The ticket is not in a state that allows the action (a student editing or deleting a non-`Open` ticket, a status change that is not the next step), or `version` does not match. |
| `500`  | Unexpected server error. The body contains only a generic `detail` and the `traceId`; no stack traces or internal details.                                  |

Checks are applied in this order: authentication (`401`), role (`403`), request
validation (`400`), ticket existence and visibility (`404`), then ticket state
and version (`409`).
