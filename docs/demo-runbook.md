# Demo runbook

A step-by-step script for the live demonstration at the defense (section 12 of
the course requirements): a student reports a problem in Swagger, a technician
resolves it, the student sees the new status, and the resolution email arrives
in Mailpit. Along the way it shows one access-control check and the matching
rows in PostgreSQL. The slides are in [`presentation.md`](presentation.md).

The demo uses the Docker Compose system and takes about 8 minutes. The
[rehearsal record](#rehearsal-record) at the end lists what was actually run
before the defense and what was not.

> **React frontend.** The browser walkthrough with the React frontend and its
> screenshots will be added here once the frontend has been verified against
> the real API. Until then, the demo uses Swagger.

## Before the defense

Do these the day before, on the computer that will be used for the demo:

1. Start the system and check that every service is healthy (see "Start the
   system" below). `docker compose up -d --build` needs internet access the
   first time: the .NET base images are not stored on the demo laptop. If the
   images `university-helpdesk-api:local` and `university-helpdesk-worker:local`
   already exist and the code has not changed since they were built, use
   `docker compose up -d --no-build --wait` instead, which works offline.
2. Run the whole demo once and note the ticket number it creates.
3. Prepare two browser windows: one normal and one private (incognito). Swagger
   keeps one token per window, so the student uses one window and the
   technician the other, and nobody has to log out in the middle of the demo.
4. Have the demo passwords ready from `.env` (`DEMO_STUDENT1_PASSWORD`,
   `DEMO_STUDENT2_PASSWORD`, `DEMO_TECHNICIAN1_PASSWORD`) without opening `.env`
   on the projected screen. They are local demo passwords, but `.env` also
   holds the database, RabbitMQ, and JWT secrets.
5. Decide who presents which part. Each member explains their own confirmed
   contribution (slide "Individual contributions").

On a Windows computer where Docker runs inside WSL, keep a WSL terminal open
for the whole defense. When WSL has no open session it shuts down after a
while, and the containers stop with it. PostgreSQL, RabbitMQ, Redis, and
Mailpit do not restart on their own afterwards (a known finding in
[`verification-results.md`](verification-results.md#follow-up-findings)); run
`docker compose up -d --no-build --wait` again if that happens.

## Start the system

In a terminal in the repository folder (in WSL, `cd` to the folder under
`/mnt/c/...`):

```bash
docker compose up -d --no-build --wait
docker compose ps
```

Expected: `db-init` has exited with `0`, and `api` (two replicas), `gateway`,
`worker`, `postgres`, `rabbitmq`, `redis`, and `mailpit` are `healthy`.

Open these tabs:

| Tab | URL |
| --- | --- |
| Swagger, student (normal window) | `http://127.0.0.1:58080/swagger` |
| Swagger, technician (private window) | `http://127.0.0.1:58080/swagger` |
| Mailpit | `http://127.0.0.1:58025` |

Use `127.0.0.1` rather than `localhost`: on the demo laptop each new connection
to `localhost` waited about 2 seconds (see
[`benchmark-results.md`](benchmark-results.md)).

## The demo

Each step lists what to do, what the audience should see, and what to say. The
ticket number, timestamps, and versions in the examples come from the
rehearsal; yours will differ.

### 1. The student signs in (student window)

In **Auth → `POST /auth/login`**, select **Try it out** and send:

```json
{ "email": "student1@uni.test", "password": "<DEMO_STUDENT1_PASSWORD>" }
```

Expected: `200` with `accessToken`, `tokenType: "Bearer"`, `expiresIn: 3600`,
and `user.role: "Student"`. Copy the `accessToken`, select **Authorize** at the
top of the page, paste it, and close the dialog.

Say: passwords are stored as hashes; the token is a signed JWT that carries
the user's ID and role and expires after an hour.

### 2. The student reports a problem

Optional: **`GET /categories`** returns the six categories; Projector has
ID `3`.

In **`POST /tickets`**, send:

```json
{ "room": "B-204", "categoryId": 3, "description": "The projector shows no image from the HDMI cable." }
```

Expected: `201 Created` with a `Location` header such as `/tickets/10004`,
`status: "Open"`, and `version: 1`. Note the ticket `id`.

Say: the server sets the owner from the token, the status, and the version;
a client cannot send them.

### 3. Access-control check: the student tries to resolve the ticket

Still as the student, in **`PATCH /tickets/{id}/status`**, enter the ticket ID
and send:

```json
{ "status": "Resolved", "version": 1 }
```

Expected: `403 Forbidden`. Only technicians may change the status.

Say: roles are enforced on the server by authorization policies, not by
hiding buttons in a client. The other rule to mention: another student gets
`404` for this ticket, so its existence is not revealed (test case TC-08).

### 4. The technician moves the ticket forward (technician window)

Sign in as in step 1 with `technician1@uni.test` and authorize.

1. **`GET /tickets`** with `status` = `Open`: the new ticket is listed.
2. **`PATCH /tickets/{id}/status`** with
   `{ "status": "InProgress", "version": 1 }`: `200`, status `InProgress`,
   version `2`.
3. **`PATCH /tickets/{id}/status`** with
   `{ "status": "Resolved", "version": 2 }`: `200`, status `Resolved`,
   version `3`, and `resolvedAt` is set.

Optional, to show concurrency control: send `{ "status": "Resolved", "version": 1 }`
before the last step. Expected: `409 Conflict`, because the ticket has changed
since version 1.

Say: each change is one conditional `UPDATE` that checks the current status and
version, so two technicians cannot both apply a change. Resolving the ticket
also saves a notification event in the same database transaction.

### 5. The student sees the update (student window)

**`GET /tickets/{id}`**: `200`, status `Resolved`, version `3`, and
`resolvedAt` is set.

### 6. The email arrives in Mailpit

Switch to the Mailpit tab (it updates by itself). Expected: an email from
University Helpdesk to `student1@uni.test` with the subject
"Your helpdesk ticket #&lt;id&gt; has been resolved", containing the room, the
category, the resolution time, and the student's description. In the
rehearsal it arrived within 0.6 to 0.9 seconds of resolving.

Say: the API never sends email itself. It published the event to RabbitMQ, and
the separate Notification Worker sent the email. If the worker or the mail
server is down, the event waits and the email is sent later (recovery checks,
TC-33).

### 7. The same data in PostgreSQL

In the terminal, replace `10004` with your ticket ID:

```bash
docker compose exec postgres psql -U helpdesk -d helpdesk -c 'SELECT t."Id", u."Email", c."Name" AS "Category", t."Room", t."Status", t."Version", t."ResolvedAt" FROM "Tickets" t JOIN "Users" u ON u."Id" = t."StudentId" JOIN "Categories" c ON c."Id" = t."CategoryId" WHERE t."Id" = 10004;'
docker compose exec postgres psql -U helpdesk -d helpdesk -c 'SELECT "TicketId", "EventType", "RecipientEmail", "PublishedAt" FROM "PendingNotifications" WHERE "TicketId" = 10004;'
```

These commands use the database user and name from `.env.example`; if `.env`
changes `POSTGRES_USER` or `POSTGRES_DB`, use those values. Rehearsal output:

```text
  Id   |       Email       | Category  | Room  |  Status  | Version |          ResolvedAt
-------+-------------------+-----------+-------+----------+---------+------------------------------
 10004 | student1@uni.test | Projector | B-204 | Resolved |       3 | 2026-11-09 21:02:15.14958+00

 TicketId |   EventType    |  RecipientEmail   |          PublishedAt
----------+----------------+-------------------+-------------------------------
    10004 | TicketResolved | student1@uni.test | 2026-11-09 21:02:15.765031+00
```

Say: the row the API returned is the row in the database, with the student and
category joined through foreign keys. The `PendingNotifications` row is the
transactional outbox: it was written in the same transaction as the status
change, and `PublishedAt` was set once RabbitMQ confirmed the event.

### If something goes wrong

| Problem | What to do |
| ------- | ---------- |
| `401` in Swagger | The token is missing or expired: sign in again and select **Authorize**. Check that you pasted only the token, without `Bearer`. |
| `409` on a status change | The version is stale: `GET /tickets/{id}`, then repeat with the current `version`. |
| A container is not healthy, or the API reports unhealthy after a break | `docker compose up -d --no-build --wait`. |
| No email after a few seconds | `docker compose logs worker --tail 20`. The event is not lost: it waits in the outbox or the queue. |
| Swagger does not load at all | Run the same flow with the Postman collection: `npx newman run tests/postman/UniversityHelpdesk.postman_collection.json -e tests/postman/local.postman_environment.json` (see the README). |

## Live-change exercise: filter tickets by room

Section 12 says the teacher may ask for a small change during the defense,
such as a new field, filter, or validation. Practice this one until it takes
under 10 minutes. Make the change on a separate branch, not on `main`.

**Task.** `GET /tickets?room=B-204` returns only tickets in that room. Students
still see only their own tickets. The value is trimmed, matched exactly, and at
most 50 characters, like a ticket's room; a longer value returns `400`.

**1. Add the query parameter** in
`backend/UniversityHelpdesk.Api/Tickets/TicketContracts.cs`, in
`ListTicketsQuery` after `Status`:

```csharp
    [MaxLength(50)]
    public string? Room { get; set; }
```

**2. Apply the filter** in `backend/UniversityHelpdesk.Api/Tickets/TicketService.cs`,
in `ListAsync` after the status filter:

```csharp
        if (!string.IsNullOrWhiteSpace(query.Room))
        {
            var room = query.Room.Trim();
            tickets = tickets.Where(ticket => ticket.Room == room);
        }
```

The filter is added to `Visible(...)`, which already limits a student to their
own tickets, so the ownership rule needs no change. EF Core sends the room as
a query parameter, not as part of the SQL text.

**3. Add tests.** In `tests/UniversityHelpdesk.Api.Tests/TicketEndpointTests.cs`,
add a case to `ListTickets_WithInvalidQuery_Returns400` (no database needed):

```csharp
    [InlineData("?room=AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA", "room")]
```

In `tests/UniversityHelpdesk.Api.Tests/Integration/TicketCrudIntegrationTests.cs`:

```csharp
    [IntegrationFact]
    public async Task RoomFilter_ReturnsOnlyTicketsInThatRoom()
    {
        var owner = await fixture.Database.CreateAccountAsync("rooms", Domain.UserRole.Student);
        var student = await fixture.ClientForAsync(owner);
        var inRoom = (await student.CreateTicketAsync(room: "R-101")).Id();
        await student.CreateTicketAsync(room: "R-102");

        var body = await (await student.GetAsync("/tickets?room=%20R-101%20")).ReadJsonAsync();

        Assert.Equal(1, body.GetProperty("totalCount").GetInt32());
        Assert.Equal(new[] { inRoom }, body.GetProperty("items").EnumerateArray().Select(item => item.Id()));
    }
```

**4. Run the tests.**

```powershell
dotnet test tests/UniversityHelpdesk.Api.Tests --filter "FullyQualifiedName~ListTickets_WithInvalidQuery"
```

With `HELPDESK_TEST_DATABASE` set as in the README, add
`|FullyQualifiedName~RoomFilter` to the filter to run the database test too.

**5. Show it in Swagger.** Rebuilding the Compose images needs internet access
(see "Before the defense"). Without it, leave the Compose stack running and
start only the changed API from source, as in the README ("Run the API and
worker from source", steps 2 and 6, with the connection string and user
secrets). Do not start a second worker. Open `http://localhost:5276/swagger`:
`GET /tickets` now has a `room` parameter. Stop `dotnet run` afterwards.

Other changes worth practicing in the same way: a `category` filter on
`GET /tickets` (same pattern), or a validation rule such as rejecting a room
that contains no letters or digits (an attribute on `CreateTicketRequest`
and `UpdateTicketRequest`, plus cases in the `400` tests).

## After the demo

```bash
docker compose stop
```

This keeps the data. The demo tickets stay in the database as ordinary
tickets; `docker compose down -v` deletes all data and is not needed.

## Rehearsal record

Rehearsed on 9 November 2026 on commit `7c0b1f8`, on the laptop described in
[`verification-results.md`](verification-results.md), against the Compose
stack started with `docker compose up -d --no-build --wait` from images built
on 8 November after the last change to `backend/` (rebuilding failed: WSL
could not resolve `mcr.microsoft.com` at the time).

| Step | What ran | Result |
| ---- | -------- | ------ |
| Start | `docker compose up -d --no-build --wait` | All 8 services healthy, `db-init` exited `0`, in 35 s |
| Swagger and Mailpit pages | Opened `http://127.0.0.1:58080/swagger` and `http://127.0.0.1:58025` in a browser | Swagger listed every endpoint and the **Authorize** button; Mailpit showed the resolution email |
| Steps 1 to 6 | The same requests as above, sent by a script over HTTP to the gateway, twice | Both runs: login `200`, ticket `201` (`10003`, then `10004`), student status change `403`, student 2 reading the ticket `404`, `InProgress` `200` (version 2), stale version `409`, `Resolved` `200` (version 3), student reads `Resolved`; the email reached `student1@uni.test` in Mailpit 0.9 s and 0.6 s after resolving. Each run took under 4 s |
| Step 7 | Both `psql` commands, as written above | Ticket row `Resolved`, version 3, room `B-204`, owner `student1@uni.test`; one `TicketResolved` row with `PublishedAt` set 0.6 s after `ResolvedAt` |
| Live-change exercise | Steps 1 to 4 in a temporary copy of the repository, discarded afterwards | Built in 33 s with 0 errors. Against a local `helpdesk_test` database, 13 selected tests passed, including the two new ones; the whole ordinary suite passed (API 87 passed and 18 skipped, worker 14 passed) |
| Live change in Swagger | The changed API run from source on `http://localhost:5276`, next to the Compose stack | `GET /tickets?room=B-204` as student 1 returned 2 of their 3 tickets, both in `B-204`; a 51-character room returned `400`; Swagger listed the `room` parameter with a maximum length of 50 |

**Not rehearsed:**

- The clicks in Swagger with a real login, **Authorize**, and two browser
  windows. The requests were the same, but they were sent by a script so that
  the passwords and tokens never appeared on screen or in any log. The team
  should click through the demo once before the defense.
- Rebuilding the images with the live change (`docker compose up --build`),
  because WSL had no DNS at the time.
- Timing a full presentation with speakers; the 8 minutes above is an
  estimate.
- The React walkthrough, which will be added after the frontend is verified.
