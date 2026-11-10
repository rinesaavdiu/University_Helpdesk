# Sources and contributions

This document records what the University Helpdesk API is built from, what
outside material it uses, how AI tools were used, and who contributed what, as
section 14 of the course requirements asks. Items marked **To be completed by
the team** must be filled in and confirmed by the group members.

## AI assistance

Rinesa Avdiu and Domenika Zeqiri organized the project into milestones,
directed the work, and managed the Git history. We used ChatGPT to plan the
project, clarify assignment requirements, and troubleshoot. We also used AI
tools during implementation, documentation, and verification. The individual
contribution records describe each member's role.

## Group members and contributions

GitHub links the repository's commits to two accounts (`git log` up to
`27bb22d`, 10 November 2026; the commit that adds this record is not counted):

| Member | GitHub account | Commits | Dates |
| ------ | -------------- | ------: | ----- |
| Rinesa Avdiu | [`rinesaavdiu`](https://github.com/rinesaavdiu) (owner of the repository) | 21 | 7–9 Nov 2026 |
| Domenika Zeqiri | [`domenikka`](https://github.com/domenikka) | 14 | 9–10 Nov 2026 |

**How this record was built.** The attributions below come from three kinds of
evidence:

- **Git history** (verifiable in the repository): who committed each change,
  and the files and diffs it contains. A few commit messages do not describe
  their diff; for those, the diff was used and the difference is noted.
- **The team's working record** (not part of the repository): every change was
  prepared as a separate checkpoint in one working session on one computer.
  Each checkpoint was assigned to one member, and that member reviewed,
  staged, committed, and pushed it under their own GitHub account.
- **The documents in `docs/`**: the frontend handoff, the demo runbook's
  rehearsal records, the test cases, and the verification results, which
  record what was checked and when.

"Directed" below means the member set the scope of a change and reviewed and
committed the result. The team confirmed this division of work on 10 November
2026; each member explains their own record at the defense, as section 12 of
the course requirements asks.

### Rinesa Avdiu (`rinesaavdiu`)

Rinesa directed the backend from the first commit to the deployment
configuration, and then the student half of the React frontend.

| Area | Contribution | Evidence |
| ---- | ------------ | -------- |
| Planning and specification | Directed and committed the repository structure, the requirements, the design, and the API contract | `ba81ce0`, `64531b6`; `docs/requirements.md`, `docs/design.md`, `docs/api-contract.md` |
| Backend: API and database | Directed the API scaffold and Swagger UI, the PostgreSQL and EF Core setup, the schema and seeded categories, JWT sign-in with the Student and Technician roles, Problem Details and validation, ticket creation, listing, reading, editing, and deleting, the category list, and the status workflow | `b788cc2`, `dd095c2`, `613180b`, `a7fb074`, `9bbfb97`, `238b902`, `cedf36a` (its message, "student crud", covers only `POST /tickets` and the ticket service), `af1d000`, `48c7b1c` |
| Backend: messaging and reliability | Directed the outbox table for pending notifications, publishing to RabbitMQ, the Notification Worker, the Redis category cache, and the health checks and metrics | `c044941`, `cc74f37`, `419539c` |
| Deployment and testing | Directed the Dockerfiles, Docker Compose with the nginx gateway and database init script, the HTTP integration tests, the recovery checks, the Postman collection, the benchmark, and the Kubernetes manifests with their deployment documentation | `b982f31` (its message begins "git combuild"; the diff packages the services and adds the integration and recovery tests), `06e5a1f`; `deploy/kubernetes/`, `benchmarks/`, `tests/` |
| Frontend: student screens | Directed the React and TypeScript scaffold with the shared layout, the student ticket list and detail pages, the report form, and editing and deleting open tickets | `e0941b7`, `8d15526`, `d9a33d3`, `ec709fe`; `frontend/src/pages/Student*.tsx`, `ReportIssuePage.tsx`, `EditTicketPage.tsx`, `components/TicketForm.tsx` |
| Testing and documentation | Directed the student browser rehearsal (report, edit, ownership check with a second student, delete, and the resolved view) and wrote the frontend handoff for the sign-in and technician work | `4f8e37e`; `docs/frontend-handoff.md`, the student section of `docs/demo-runbook.md`, `docs/screenshots/student-01` to `student-08` |

### Domenika Zeqiri (`domenikka`)

Domenika directed the CI pipeline, the verification and defense documents,
and the technician half of the React frontend through to the verified browser
demo.

| Area | Contribution | Evidence |
| ---- | ------------ | -------- |
| CI and deployment checks | Directed the GitHub Actions workflow: build and tests, image builds, and a Kubernetes smoke test on a temporary kind cluster | `f4875fb`; `.github/workflows/ci.yml`, `scripts/ci/` |
| Documentation and verification | Directed the installation and API documentation, the recorded test and deployment results, this sources document, the HTTPS section, and the per-case test table | `da59588`, `4bf38b0`, `4804f47`, `7c0b1f8`, `b29c149`; `docs/verification-results.md`, `docs/test-cases.md` |
| Presentation and submission | Directed the slides, the demo runbook, the packaging and verification scripts, and the submission checklist, and their final update for the React demo | `37d8ee2`, `7ef281e`, `27bb22d`; `docs/presentation.md`, `docs/demo-runbook.md`, `scripts/package-submission.ps1`, `scripts/verify-package.ps1` |
| Frontend: sign-in and roles | Directed the React sign-in page, the session handling (tab-scoped storage, expiry, sign-out on `401`), and the role guards, replacing the development harness | `b53ab9d` (its message says "verify student workflows in the browser"; the diff adds `frontend/src/auth/session.ts`, `auth/RequireRole.tsx`, `api/authApi.ts`, and the sign-in in `pages/LoginPage.tsx`) |
| Frontend: technician screens | Directed the technician ticket queue with filter and pagination, and the Start work and Resolve ticket actions, including the `409` reload | `675a677`, `1e02c3c`; `frontend/src/pages/Technician*.tsx` |
| Frontend integration | Directed serving the React app through Docker Compose: the gateway image, the `/api` routing, the Content Security Policy, the gateway checks, and the CI frontend job | `dfa9d19`; `frontend/Dockerfile`, `deploy/gateway.conf`, `scripts/verify-compose.sh` |
| Browser verification | Directed the full browser rehearsal from student report to technician resolution and the Mailpit email, the stale-update and wrong-role checks, and the sign-in with the Content Security Policy enforced | `ef61cb1`, `7ef281e`; TC-37 to TC-43 in `docs/test-cases.md`, `docs/screenshots/flow-01` to `flow-09` |

### To be completed by the team

- **Work not visible in Git:** planning with ChatGPT, reviewing each other's
  changes, and manual checks such as clicking through the demo by hand. Add
  these to the record of whoever did them; they are not credited above.
- **Rehearsal:** the timed, spoken run of the presentation and demo is
  pending. Record who presents which part once it has been done.

## Libraries

All packages come from NuGet. Versions are those referenced by the project
files; licenses are from each package's metadata.

| Package | Version | Used by | License |
| ------- | ------- | ------- | ------- |
| Microsoft.AspNetCore.Authentication.JwtBearer | 10.0.14 | API: JWT sign-in | MIT |
| Microsoft.EntityFrameworkCore.Design | 10.0.4 | API: migrations at design time | MIT |
| Npgsql.EntityFrameworkCore.PostgreSQL | 10.0.3 | API: PostgreSQL access through EF Core | PostgreSQL |
| prometheus-net.AspNetCore | 8.2.1 | API: `/metrics` | MIT |
| RabbitMQ.Client | 7.2.2 | API and worker: messaging | Apache-2.0 or MPL-2.0 |
| StackExchange.Redis | 3.4.31 | API: category cache; benchmark | MIT |
| Swashbuckle.AspNetCore | 10.3.1 | API: OpenAPI document and Swagger UI | MIT |
| MailKit | 4.18.2 | Worker: SMTP | MIT |
| Microsoft.Extensions.Hosting | 10.0.14 | Worker: host | MIT |
| Microsoft.AspNetCore.Mvc.Testing | 10.0.14 | Tests: in-memory API host | MIT |
| Microsoft.NET.Test.Sdk | 17.14.1 | Tests | MIT |
| xunit | 2.9.3 | Tests | Apache-2.0 |
| xunit.runner.visualstudio | 3.1.4 | Tests | Apache-2.0 |
| coverlet.collector | 6.0.4 | Tests: coverage collection | MIT |
| dotnet-ef (local tool) | 10.0.4 | Creating migrations | MIT |

The projects also use the .NET 10 SDK and the ASP.NET Core shared framework.
The password hasher is ASP.NET Core Identity's `PasswordHasher`, part of that
framework.

## Frontend packages

All packages come from npm and are locked in `frontend/package-lock.json`
(`npm ci` installs exactly these versions). Versions are the locked ones;
licenses are from each installed package's `package.json`.

| Package | Version | Used for | License |
| ------- | ------- | -------- | ------- |
| react | 19.3.1 | UI components (in the browser bundle) | MIT |
| react-dom | 19.3.1 | Rendering in the browser (in the bundle) | MIT |
| react-router | 7.18.5 | Routes, links, and URL search parameters (in the bundle) | MIT |
| vite | 8.3.7 | Development server and production build | MIT |
| @vitejs/plugin-react | 6.3.0 | JSX and Fast Refresh for Vite | MIT |
| typescript | 7.0.2 | Type checking (`npm run build`) | Apache-2.0 |
| oxlint | 1.93.0 | Linting (`npm run lint`) | MIT |
| @types/react, @types/react-dom | 19.3.0 | Type definitions | MIT |
| @types/node | 24.19.2 | Type definitions for `vite.config.ts` | MIT |

With their dependencies, `npm ci` installed 31 packages on Windows: 24 MIT,
3 Apache-2.0, 2 MPL-2.0 (`lightningcss` and its platform binary, used by Vite
at build time), 1 ISC, and 1 BSD-3-Clause. Only React, React DOM, React Router,
and their MIT-licensed dependencies are included in the files served to the
browser; the other packages are build tools.

## Container images and tools

| Item | Version | Purpose |
| ---- | ------- | ------- |
| `mcr.microsoft.com/dotnet/sdk`, `aspnet`, `runtime` | `10.0-alpine` | Building and running the API and worker images |
| `postgres` | `18.6-alpine` | Database |
| `rabbitmq` | `4.1-management-alpine` | Message broker |
| `redis` | `8.2-alpine` | Category cache |
| `axllent/mailpit` | `v1.33` | Test mail inbox |
| `nginx` | `1.28-alpine` | Compose gateway, serving the frontend |
| `node` | `22.22-alpine` | Building the frontend inside the gateway image |
| Docker Engine and Docker Compose, kind, kubectl | See the README's "Tools and versions" | Local deployment |
| GitHub Actions: `actions/checkout`, `actions/setup-dotnet`, `actions/upload-artifact`, `helm/kind-action` | Pinned to commit SHAs in `.github/workflows/ci.yml` | CI |
| Marp CLI (`@marp-team/marp-cli`) | 4.x, run with `npx` | Exporting `docs/presentation.pdf` from `docs/presentation.md` |
| Postman collection format v2.1 and Newman | Newman 6 | API walkthrough and checks |
| actionlint, ShellCheck, jq | 1.7.12, 0.8.0, 1.6 | Checking the CI workflow and scripts; jq is also used by the smoke test |

## Reused material and references

- **No third-party code is included** beyond the libraries and images listed
  above; the repository started empty on 7 November 2026 and every file was
  written in the session described under AI assistance. The EF Core migration
  files were generated by `dotnet-ef` from the model. The multi-stage
  Dockerfiles and the ASP.NET Core configuration follow the patterns in the
  official Microsoft
  documentation, and the frontend's TypeScript and Vite configuration
  (`tsconfig.app.json`, `tsconfig.node.json`, `vite.config.ts`) has the same
  layout as Vite's React and TypeScript template.
- **Error type URIs.** API errors use Problem Details (RFC 9457) with `type`
  links to `https://httpstatuses.io/<status>`, an external reference site.
- **Demo data** (users, categories, rooms, and ticket text) is fictional and
  uses the reserved `.test` email domain.

**To be completed by the team:** any course material, lecture examples,
tutorials, or other sources the team consulted while planning or reviewing the
project.
