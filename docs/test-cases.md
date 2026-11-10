# Test cases

This document lists the main test cases with their input or precondition, the
expected result, the actual result, and the evidence. It covers section 7 of
the course requirements. The summary of every check, with dates and
environments, is in [`verification-results.md`](verification-results.md).

## How the results were produced

| Run | Date | What ran | Result |
| --- | ---- | -------- | ------ |
| Local, with a test database | 9 Nov 2026, commit `4804f47` | `dotnet test UniversityHelpdesk.slnx --logger trx` with `HELPDESK_TEST_DATABASE` pointing at a dedicated `helpdesk_test` database on a local PostgreSQL 18 instance | API tests 110 of 110 passed (including the 24 integration tests), worker tests 14 of 14 passed; the 5 recovery checks were skipped because their services were not configured |
| Local, with Compose services | 8 Nov 2026 | The recovery checks against the Compose PostgreSQL, RabbitMQ, Mailpit, and Redis; the Postman collection through the gateway | Recovery 5 of 5 passed; Newman 27 of 27 passed |
| GitHub Actions | 10 Nov 2026, commit `ef61cb1` | [Run 42722426707](https://github.com/rinesaavdiu/University_Helpdesk/actions/runs/42722426707): ordinary tests, the Kubernetes smoke test, and the frontend and gateway build | Succeeded |
| Manual HTTPS check | 9 Nov 2026 | API started with `--launch-profile https` | See TC-34 |
| Browser, through the Compose gateway | 10 Nov 2026, commit `dfa9d19` | A stack started by `scripts/verify-compose.sh` from a `git archive` of the commit; the React app in separate browser tabs for the student and two technician sessions | See TC-37 to TC-43 |

The evidence column names the test class and method in `tests/`. Where a test
runs several inputs, the count is given in brackets. "Passed" in the actual
column means the test ran and every assertion held.

## Authentication

| ID | Input or precondition | Expected | Actual | Evidence |
| -- | --------------------- | -------- | ------ | -------- |
| TC-01 | `POST /auth/login` with a seeded student's email in upper case and the correct password | `200`; `tokenType` `Bearer`, `expiresIn` 3600, the user's ID, email, and role; the token then works on `GET /tickets` | Passed | `AccessIntegrationTests.Login_WithValidCredentials_ReturnsAWorkingTokenForThatUser` |
| TC-02 | Login with a wrong password, and with an unknown email | `401` for both, with the same `detail`, so the response does not reveal whether the email exists | Passed | `AccessIntegrationTests.Login_WithWrongPasswordOrUnknownEmail_Returns401WithTheSameMessage` |
| TC-03 | Login body `{}`, an invalid email, a missing password, or an extra `role` field | `400`; the extra field is named in `errors` | Passed | `JwtAuthenticationTests.Login_WithInvalidRequest_Returns400` (3), `ProblemDetailsTests.UnknownLoginField_Returns400_NamingTheField` |
| TC-04 | Protected endpoints called without a token | `401` Problem Details with a `Bearer` challenge | Passed | `ProblemDetailsTests.MissingToken_Returns401Problem_AndKeepsBearerChallenge`, `TicketEndpointTests.TicketEndpoints_WithoutToken_Return401` (4), `CreateTicket_WithoutToken_Returns401`, `ChangeStatus_WithoutToken_Returns401`, `CategoryEndpointTests.ListCategories_WithoutToken_Returns401` |
| TC-05 | Expired token, tampered token, token signed with another key, wrong issuer or audience | `401` | Passed | `JwtAuthenticationTests.ExpiredToken_Returns401`, `TamperedToken_Returns401`, `TokenSignedWithAnotherKey_Returns401`, `TokenWithWrongIssuerOrAudience_Returns401` (2) |
| TC-06 | Token issued for a user | Expires after 60 minutes and carries the user ID and role | Passed | `JwtAuthenticationTests.IssuedToken_ExpiresAfter60Minutes_AndCarriesDocumentedClaims`, `ValidToken_Authenticates_WithUserIdAndRole` |

## Authorization and ownership

| ID | Input or precondition | Expected | Actual | Evidence |
| -- | --------------------- | -------- | ------ | -------- |
| TC-07 | A technician calls `POST /tickets`, `PATCH /tickets/{id}`, `DELETE /tickets/{id}`; a student calls `PATCH /tickets/{id}/status`; an anonymous client calls `GET /tickets/{id}` | `403`, `403`, `403`, `403`, `401`; the ticket stays `Open` with version 1 | Passed | `AccessIntegrationTests.RoleRestrictions_AreEnforced` |
| TC-08 | Student 2 reads, edits, and deletes student 1's ticket, and lists their own tickets | `404` for each action; the ticket is not in student 2's list and is unchanged | Passed | `AccessIntegrationTests.AnotherStudentsTicket_IsNotFound_AndStaysUnchanged` |
| TC-09 | A technician reads tickets created by two different students | `200` for each, and both appear in the technician's list | Passed | `AccessIntegrationTests.Technician_SeesEveryStudentsTickets` |

## Categories

| ID | Input or precondition | Expected | Actual | Evidence |
| -- | --------------------- | -------- | ------ | -------- |
| TC-10 | `GET /categories` with an inactive category in the database | Only active categories, sorted by name, including `Projector` | Passed | `AccessIntegrationTests.Categories_ListOnlyActiveCategories_OrderedByName` |
| TC-11 | Redis unreachable or disabled | The cache is skipped within the timeout and categories come from PostgreSQL | Passed | `CategoryCacheTests.UnreachableRedis_FallsBackWithinTheTimeout`, `Disabled_IsAlwaysAMiss` |

## Tickets: create, read, update, delete

| ID | Input or precondition | Expected | Actual | Evidence |
| -- | --------------------- | -------- | ------ | -------- |
| TC-12 | `POST /tickets` `{"room":"A-204","categoryId":3,"description":"The projector turns on but shows no image."}`, then `GET` the `Location` | `201`, `Location: /tickets/{id}`, owner is the caller, `Open`, version 1, category `Projector`, `resolvedAt` null; the `GET` returns the same body | Passed | `TicketCrudIntegrationTests.Ticket_CanBeCreatedReadUpdatedAndDeleted` |
| TC-13 | `PATCH /tickets/{id}` `{"room":"A-206","categoryId":2,"version":1}` | `200`, room and category changed, description unchanged, version 2 | Passed | Same test |
| TC-14 | `DELETE /tickets/{id}`, then `GET` | `204` with an empty body; then `404`, and the row is gone from the database | Passed | Same test |
| TC-15 | 5 tickets, `GET /tickets?page=1..4&pageSize=2`, then `?status=InProgress` and `?status=Open` after one ticket is started | Pages of 2, 2, 1, and 0 items, newest first, `totalCount` 5; the status filters return 1 and 4 tickets | Passed | `TicketCrudIntegrationTests.Pagination_PagesThroughOwnTickets_NewestFirst` |

## Invalid input and edge cases

| ID | Input or precondition | Expected | Actual | Evidence |
| -- | --------------------- | -------- | ------ | -------- |
| TC-16 | Create with an empty room, the description `"short"`, a server-controlled `status`, or `categoryId` `"three"` | `400` with `errors` naming the field; no ticket is saved | Passed | `TicketCrudIntegrationTests.InvalidCreateRequest_Returns400NamingTheField_AndSavesNothing` (4) |
| TC-17 | Create or edit with an inactive category or category `999999` | `400` naming `categoryId` | Passed | `TicketCrudIntegrationTests.InactiveOrUnknownCategory_IsRejectedOnCreateAndEdit` (2) |
| TC-18 | Create with missing fields, a blank room, a description that is too short after trimming, a server-controlled `status` or `studentId`, a wrong value type, or a room that is too long | `400` naming each field | Passed | `TicketEndpointTests.CreateTicket_WithMissingFields_Returns400ForEachField`, `CreateTicket_WithInvalidOrServerControlledField_Returns400` (4), `CreateTicket_WithWrongValueType_Returns400NamingTheField`, `CreateTicket_WithTooLongRoom_Returns400` |
| TC-19 | Edit without `version`, with no changes, a blank room, a short description, `version` 0, or a `status` field | `400` naming the field | Passed | `TicketEndpointTests.UpdateTicket_WithInvalidBody_Returns400` (6) |
| TC-20 | Status change without `status` or `version`, with `Closed`, lower-case `inprogress`, `version` 0, or a `resolvedAt` field | `400` naming the field | Passed | `TicketEndpointTests.ChangeStatus_WithInvalidBody_Returns400` (6) |
| TC-21 | List with `page=0`, `pageSize=0`, `pageSize=101`, `page=abc`, `status=Closed`, `status=open` | `400` naming the parameter | Passed | `TicketEndpointTests.ListTickets_WithInvalidQuery_Returns400` (6), `TicketCrudIntegrationTests.InvalidListQuery_Returns400` (4) |
| TC-22 | Malformed JSON body | `400` with "The request body must be valid JSON."; the body is not echoed back | Passed | `ProblemDetailsTests.MalformedJson_Returns400_WithoutEchoingTheBody` |
| TC-23 | Ticket ID that does not exist (`2147483000`) for read, edit, delete, and status change; non-numeric ID; unknown route | `404` for each | Passed | `TicketCrudIntegrationTests.MissingTicket_Returns404ForEveryAction`, `TicketEndpointTests.GetTicket_WithNonNumericId_Returns404`, `ProblemDetailsTests.UnmatchedRoute_Returns404Problem` |

## Status workflow and concurrency

| ID | Input or precondition | Expected | Actual | Evidence |
| -- | --------------------- | -------- | ------ | -------- |
| TC-24 | Open ticket: change to `Resolved`, then `Open`; then `InProgress`; then back to `Open`; the student edits and deletes; then `Resolved`; then any further change | `409`, `409`; `200` (version 2); `409`; `409`, `409`; `200` (version 3, `resolvedAt` set); `409`. Exactly one `TicketResolved` pending notification addressed to the student | Passed | `WorkflowIntegrationTests.Status_AdvancesOneStepAtATime_AndResolutionQueuesOneNotification` |
| TC-25 | Edit with version 1, then edit again with the stale version 1 | Second edit `409`; the ticket keeps the first change and version 2 | Passed | `TicketCrudIntegrationTests.Update_WithAStaleVersion_Returns409AndKeepsTheTicket` |
| TC-26 | 8 simultaneous edits with the same version | Exactly one `200` and seven `409`; the winning change is stored with version 2 | Passed | `WorkflowIntegrationTests.ConcurrentEdits_WithTheSameVersion_OnlyOneSucceeds` |
| TC-27 | 8 simultaneous `Resolved` requests on an `InProgress` ticket | One `200`, seven `409`, one pending notification, version 3 | Passed | `WorkflowIntegrationTests.ConcurrentResolutions_ResolveOnce_AndQueueOneNotification` |
| TC-28 | A student edit and a technician status change at the same moment | One `200` and one `409` | Passed | `WorkflowIntegrationTests.StudentEditRacingATechnician_OnlyOneSucceeds` |

## Error handling and operations

| ID | Input or precondition | Expected | Actual | Evidence |
| -- | --------------------- | -------- | ------ | -------- |
| TC-29 | A test-only route throws an unhandled exception | `500` `application/problem+json` with the generic `detail` and a `traceId`; no exception type, message, or class names | Passed | `ProblemDetailsTests.UnexpectedException_Returns500Problem_WithoutExceptionDetails` |
| TC-30 | `GET /health/live`; `GET /health/ready` with an unreachable database | Live: `200` `Healthy` with no checks. Ready: `503` within 5 s, the `database` check `Unhealthy`, and no exception text or connection details | Passed | `MonitoringTests.Liveness_IsHealthyWithoutDependencies`, `Readiness_ReportsTheUnreachableDatabaseQuicklyWithoutDetails` |
| TC-31 | Requests to `/tickets/41`, an unknown path, and a failing route, then `GET /metrics` | Metrics use route templates (`/tickets/{id:int}`), group unknown paths as `unmatched`, and count the `500` | Passed | `MonitoringTests.RequestMetrics_UseRouteTemplatesNotRawPaths`, `RequestMetrics_GroupUnknownPathsAndCountServerErrors` |
| TC-32 | `GET /swagger/v1/swagger.json` and `GET /swagger` | `200` | Passed | `SwaggerSmokeTests` (2) |

## Failure and recovery, end to end

| ID | Input or precondition | Expected | Actual | Evidence |
| -- | --------------------- | -------- | ------ | -------- |
| TC-33 | Recovery checks: worker stopped; SMTP down once; SMTP down for every attempt; RabbitMQ unreachable while a ticket is resolved; Redis connection cut | The email arrives after the worker starts; the send is retried and arrives; the event moves to the failed queue; the event waits in the outbox and is delivered after recovery; categories come from PostgreSQL and caching resumes | Passed, 5 of 5 (8 Nov 2026) | `tests/UniversityHelpdesk.RecoveryTests/RecoveryChecks.cs`; [`verification-results.md`](verification-results.md) |
| TC-34 | API started with `dotnet run --launch-profile https` | HTTPS on `https://localhost:7052`; HTTP redirected to HTTPS; the endpoints behave as over HTTP | `http://localhost:5276/tickets` returned `307` to `https://localhost:7052/tickets`. The server presented the .NET development certificate (`CN=localhost`). Clients that validate certificates rejected it because it is not trusted on this machine; with validation skipped, `/health/ready` `200`, `/swagger/v1/swagger.json` `200`, `GET /tickets` without a token `401`, login `200` with a token | Manual check, 9 Nov 2026; see the README's HTTPS section |
| TC-35 | Postman collection through the Compose gateway | All 27 requests and assertions pass, including the full workflow of one ticket | Passed, 27 of 27 (8 Nov 2026) | `tests/postman/UniversityHelpdesk.postman_collection.json` |
| TC-36 | Kubernetes smoke test: student login, create ticket `CI-101`, technician moves it to `InProgress` and `Resolved`, the student reads it | The student sees `Resolved`, and the resolution email reaches `student1@uni.test` in Mailpit | Passed in every hosted run, most recently checked [42722426707](https://github.com/rinesaavdiu/University_Helpdesk/actions/runs/42722426707) on `ef61cb10be7b75e2920ab49a3ef730703876ae3d` | `scripts/ci/smoke-test.sh` |

## Web frontend through the Compose gateway

These ran in a browser against the production build served by the gateway
(10 Nov 2026, commit `dfa9d19`). The walkthrough and screenshots are in
[`demo-runbook.md`](demo-runbook.md#the-demo-in-the-browser). TC-37 is also
checked by `scripts/verify-compose.sh` on every run.

| ID | Input or precondition | Expected | Actual | Evidence |
| -- | --------------------- | -------- | ------ | -------- |
| TC-37 | Direct requests to `/`, `/login`, `/student/tickets/1`, `/technician/tickets?status=Open`, the app script, `/api/health/live`, `/health/ready`, `/swagger`, `/metrics`, `/api/metrics`, a missing file under `/assets/` | The app page for the routes; the script as JavaScript; health `200`; Swagger UI; `404` for the metrics paths and the missing file | Passed; Swagger UI listed all 8 operations | `scripts/verify-compose.sh`, step 4; manual check on 10 Nov 2026 |
| TC-38 | Signed in, open or refresh `/student/tickets?status=Resolved`, `/student/tickets/1`, `/technician/tickets?status=Open&page=2`, `/technician/tickets/1`, and an unknown path | Each page loads with the session kept and the filter and page from the URL; the unknown path shows "Page not found"; no Content Security Policy violation | Passed; the list request kept its query (`/api/tickets?status=Open&page=2&pageSize=10`) | Manual check on 10 Nov 2026 |
| TC-39 | Student 1 reports `C-112`, Projector; a technician starts work and resolves it in the app; the student refreshes the ticket | Open, then In progress, then Resolved with version 3; the student sees Resolved without edit or delete | Passed: ticket #2, version 3 | Runbook steps B1 to B7; `flow-02`, `flow-05`, `flow-07`, `flow-08` screenshots |
| TC-40 | The student opens a technician page and sends a status change; the technician opens a student page and creates a ticket | "Not allowed" on both pages with no request to the other role's API; `403` for both API calls; the ticket stays Open | Passed | Runbook step B3; `flow-03` screenshot |
| TC-41 | Two technician sessions on the same ticket; the second selects **Start work**, and later **Resolve ticket**, with an outdated version | `409`, the conflict warning, and the page reloads with the action for the current status, or none | Passed for both actions | Runbook step B5; `flow-06` screenshot |
| TC-42 | The ticket in TC-39 is resolved | One email to `student1@uni.test` with the ticket number, room, category, and description; one `TicketResolved` row with `PublishedAt` set | Passed: delivered 0.3 s after resolving | Runbook step B8; `flow-09` screenshot |
| TC-43 | Sign in as student 1 and as technician 1 in new tabs, with the gateway's Content Security Policy enforced from page load to the signed-in page (no bypass) | Sign-in succeeds with no policy violation; a request to another origin is blocked before and after | Passed for both: `POST /api/auth/login` `200`, **My tickets** and **All tickets**, 0 violations; requests to Mailpit's origin were blocked by `connect-src` | Manual check on 10 Nov 2026 on the same `dfa9d19` stack (the app is unchanged in `ef61cb1`); runbook rehearsal record |

## Not covered by an automated test

- **The browser flow** (TC-38 to TC-43) was checked in a browser; the
  frontend has no automated UI tests.

- **SQL injection.** All queries go through EF Core, which sends values as
  parameters; the one raw query (`OutboxPublisher`) uses `FromSql` with
  interpolated values, which EF Core also turns into parameters. No test sends
  injection-style input.
- **Duplicate emails after a worker crash** between sending and acknowledging
  are possible by design (at-least-once delivery) and are not tested.
- **Logging** is visible in the console output of every run but is not
  asserted by a test, apart from the metrics in TC-31.
