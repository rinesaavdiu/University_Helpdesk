---
marp: true
paginate: true
title: University Helpdesk API
style: |
  section { font-size: 21px; padding: 40px 60px; }
  h2 { font-size: 34px; }
  table { font-size: 19px; }
  pre { font-size: 17px; }
---

<!-- Slides in Marp format: each "---" starts a new slide. The Marp extension
for VS Code or `npx @marp-team/marp-cli docs/presentation.md --pdf` exports
them. Every number comes from docs/verification-results.md,
docs/benchmark-results.md, docs/test-cases.md, or docs/demo-runbook.md. -->

# University Helpdesk API

**Sistem për raportimin e problemeve teknike në universitet**

Ueb Shërbimet dhe Ueb API-të, project defense

_Group members: to be completed by the team_

---

## The problem

- Students find broken classroom technology: projectors, computers, network,
  audio, smart boards.
- **University Helpdesk:** students report problems as tickets, technicians
  move them through `Open → InProgress → Resolved`, and the student gets an
  email when the ticket is resolved.

| Role | Can do |
| ---- | ------ |
| Student | Report problems; view, edit, and delete their own tickets while `Open` |
| Technician | View every ticket; move a ticket one status forward |

---

## Architecture

```text
 Swagger / Postman / (React, planned)
              │ HTTP + JWT
              ▼
   nginx gateway ──► Ticket API ×2 ──► PostgreSQL (data + outbox)
                         │      └────► Redis (category cache)
                         │ TicketResolved event
                         ▼
                     RabbitMQ ──► Notification Worker ×1 ──► SMTP (Mailpit)
```

- **Ticket API:** ASP.NET Core 10, controllers → services → EF Core.
- **Notification Worker:** .NET 10 worker service, no HTTP and no database.
- Runs with Docker Compose and on Kubernetes (local kind cluster).

---

## Why two services, and why RabbitMQ

**Separate API and worker**

- Email is slow and depends on an outside server; ticket requests must stay
  fast. A slow or failed mail server never blocks or fails a request.
- A crash in one service does not stop the other.
- They scale differently: 2 API replicas, 1 worker.
- The API itself stays one layered service: users, categories, and tickets
  change together in the same transactions.

**RabbitMQ**

- Durable queue: the event waits while the worker is stopped.
- Publisher confirms and late acknowledgements: no side loses an event.
- Failed deliveries go to a failed queue for inspection, not into the void.

---

## Why PostgreSQL

The data is relational, and the rules are enforced by the database itself:

- Foreign keys: ticket → student, ticket → category, event → ticket.
- Unique: user email, category name, one resolution event per ticket.
- Checks: valid role and status, `ResolvedAt` set exactly when `Resolved`.
- **Transactional outbox:** the status change and its event are saved in one
  transaction, then published.
- Conditional `UPDATE … WHERE Version = @v`: no lost updates (`409`).
- `FOR UPDATE SKIP LOCKED`: the two API replicas never publish the same event.

---

## Security

- Sign-in returns a signed JWT (1 hour) with the user's ID and role;
  passwords are stored as hashes.
- Every endpoint except login needs the token. Anonymous by design: login,
  `/health/*`, Swagger (Development only), and `/metrics`, which is blocked at
  the gateway.
- Roles are checked on the server: a student changing a status gets `403`;
  another student's ticket is `404`.
- Server-side validation (`400` with problem details); errors never leak
  exception details; queries are parameterized by EF Core.
- Secrets live in `.env`, Kubernetes Secrets, or user secrets, never in Git.

**HTTPS.** Verified locally with `dotnet run --launch-profile https`: HTTP is
redirected to HTTPS (`307`), using the .NET development certificate, which is
**not trusted on the demo laptop**. Compose and kind use plain HTTP on the
local machine only. TLS for a published deployment is designed, **not
implemented or tested**.

---

## Live demo

_Swagger, on the Docker Compose system. Steps and fallbacks in
`docs/demo-runbook.md`._

1. Student signs in and reports: room `B-204`, Projector → `201`, `Open`.
2. **Access check:** the student tries to resolve it → `403`.
3. Technician: `InProgress`, then `Resolved` (versions 2 and 3).
4. Student reads the ticket → `Resolved`.
5. Mailpit: "Your helpdesk ticket #… has been resolved".
6. PostgreSQL: the same ticket row, and its outbox row with `PublishedAt`.

Rehearsed twice on 9 Nov: every step returned the expected status; the email
arrived 0.6 to 0.9 s after resolving.

<!-- React walkthrough: to be added once the frontend is verified against the
real API, with screenshots of the same flow. -->

> **React walkthrough:** placeholder until the frontend is verified against
> the real API.

---

## Tests

| Suite | Result |
| ----- | ------ |
| API tests, including 24 HTTP integration tests on real PostgreSQL | **110 / 110** passed (9 Nov) |
| Notification Worker tests | **14 / 14** passed (9 Nov) |
| Recovery checks: worker, SMTP, RabbitMQ, and Redis outages | **5 / 5** passed (8 Nov) |
| Postman collection through the gateway (Newman) | **27 / 27** passed (8 Nov) |

- 36 documented test cases with input, expected result, actual result, and
  evidence (`docs/test-cases.md`).
- Covered: CRUD, invalid input (`400`), roles (`401`/`403`), ownership
  (`404`), the status workflow, concurrent edits (one `200`, seven `409`), and
  a `500` without details.
- Not covered by a test: duplicate emails after a worker crash (possible by
  design), SQL injection-style input.

---

## CI and deployment

**GitHub Actions** on every push to `main`, every pull request, and manually:

1. Build and run the ordinary test suite.
2. Deploy to a fresh kind cluster and run a smoke test: student reports,
   technician resolves, email in Mailpit.

**5 of 5 runs succeeded**, each in about 3 minutes; the latest is
run 42697737387 on `7c0b1f8`. Integration and recovery tests need real
services and run locally, not in CI.

**Deployment checks (8 Nov)**

- Compose: 2 API replicas behind nginx both served requests; data survived
  `down` and `up`.
- Kubernetes: ordered start-up through a `db-init` Job; 60 requests split
  21 / 39 across the two pods; data survived replacing both API pods and
  re-creating the PostgreSQL pod.

---

## Performance

Target (NFR-P1): `GET /tickets` p95 under **300 ms** with 20 concurrent users
and 1,000 tickets, on the Compose system.

| | Result |
| - | ------ |
| `GET /tickets` p95, 7 runs | **76.5 to 133.0 ms**, 0 failures |
| `POST /tickets` p95, 3 runs | 60.6 to 118.8 ms |

- **Finding:** logins looked slow (2.7 s median). The cause was the client:
  `localhost` tried IPv6 first and each new connection waited about 2 s.
  With `127.0.0.1`: 0.55 to 0.65 s.
- **Honest result:** the Redis cache works (counters show every hit and miss),
  but this workload did not show it to be faster.
- One 4-core laptop; results vary about 30% between runs.

---

## Limitations and next steps

- Development configuration with demo accounts; HTTPS only verified locally
  with an untrusted development certificate.
- Integration and recovery tests are not run in CI.
- Compose does not restart PostgreSQL, RabbitMQ, Redis, and Mailpit after the
  host restarts (fix found, not applied yet).
- Not implemented: API versioning (`/v1`), multi-factor authentication, a
  web application firewall, a Prometheus server.
- Next: the React frontend for students and technicians, then the final
  submission package, verified from a clean copy.

---

## Individual contributions

_Each member presents their own confirmed contribution. To be completed by
the team, matching `docs/sources-and-contributions.md`._

| Member | Contribution | Evidence |
| ------ | ------------ | -------- |
| _Name_ | _To be completed_ | _To be completed_ |
| _Name_ | _To be completed_ | _To be completed_ |

---

## Sources and AI use

- Libraries, images, and references with versions and licenses:
  `docs/sources-and-contributions.md`.
- **AI use:** our team organized the project into milestones, directed
  changes as issues came up, and managed the Git history. We used ChatGPT
  throughout the project to help plan the work, break down the assignment
  requirements, and troubleshoot. We also used AI coding assistance to
  generate and revise code, tests, configuration, and documentation, and to
  run verification checks. Individual contributions are listed separately.

---

# Questions

Repository: `github.com/rinesaavdiu/University_Helpdesk`

Documentation: `README.md` and `docs/`
