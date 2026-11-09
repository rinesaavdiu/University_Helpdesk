# Sources and contributions

This document records what the University Helpdesk API is built from, what
outside material it uses, how AI tools were used, and who contributed what, as
section 14 of the course requirements asks. Items marked **To be completed by
the team** must be filled in and confirmed by the group members.

## AI assistance

Our team organized the project into milestones, directed changes as issues
came up, and managed the Git history. We used ChatGPT throughout the project
to help plan the work, break down the assignment requirements, and
troubleshoot. We also used AI coding assistance to generate and revise code,
tests, configuration, and documentation, and to run verification checks. This
includes the React frontend, its Docker build, and the browser checks: the AI
assistant drove a browser through the demo flow, and the screenshots in
`docs/screenshots/` were captured during those runs. Individual contributions
are listed separately.

## Group members and contributions

GitHub links the repository's commits to two accounts (`git log` up to
`ef61cb1`, 10 November 2026; the commit that adds this table is not counted):

| GitHub account | Commits | Dates | Commits made |
| -------------- | ------: | ----- | ------------ |
| [`rinesaavdiu`](https://github.com/rinesaavdiu) (owner of the repository) | 21 | 7–9 Nov 2026 | From `ba81ce0` (README and folder structure) to `06e5a1f` (benchmark, Kubernetes, deployment docs); then the React frontend's first five commits, from `e0941b7` (scaffold and shared layout) to `4f8e37e` (student screens checked in the browser) |
| [`domenikka`](https://github.com/domenikka) | 12 | 9–10 Nov 2026 | `f4875fb` (CI workflow) to `37d8ee2` (presentation and demo runbook): CI, documentation, verification results, sources, and the presentation; then `b53ab9d` to `ef61cb1`: frontend sign-in, the technician queue and status actions, serving the frontend through Compose, and the browser verification |

Both accounts committed from the same computer, in the same session as the AI
coding assistant. A commit's author shows who committed a change, not who
wrote it, so this document does not credit any code or design decision to an
individual.
Section 12 of the course requirements asks each member to explain their
individual contribution at the defense.

**To be completed by the team** (one row per group member):

| Name | GitHub account | Contributions (for example planning, implementation, review, testing, presentation) | How it can be verified |
| ---- | -------------- | ----------------------------------------------------------------------------------- | ---------------------- |
| _To be completed_ | _To be completed_ | _To be completed_ | _To be completed_ |

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
  files were generated by `dotnet-ef` from the model. AI-generated code can
  still resemble public examples; the multi-stage Dockerfiles and the ASP.NET
  Core configuration follow the patterns in the official Microsoft
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
