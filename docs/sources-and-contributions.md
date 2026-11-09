# Sources and contributions

This document records what the University Helpdesk API is built from, what
outside material it uses, how AI tools were used, and who contributed what, as
section 14 of the course requirements asks. Items marked **To be completed by
the team** must be filled in and confirmed by the group members.

## AI assistance

The repository was empty when work started on 7 November 2026. From then on,
the content of every commit was prepared by **Cursor's AI agent**, working in
one Cursor session on the team's Windows computer. Cursor selected the
underlying language model; the session does not record which model produced
each part.

**What the AI agent produced:**

- The source code of the API, the Notification Worker, and the benchmark.
- The unit, HTTP, integration, and recovery tests, and the Postman collection.
- The Dockerfiles, `compose.yaml`, the nginx gateway configuration, the
  Kubernetes manifests, the GitHub Actions workflow, and the CI scripts.
- All documentation, including this file, and a proposed commit message for
  each step.

**What the AI agent helped verify:** it ran the builds and test suites, applied
migrations to dedicated local databases, started the Compose stack and a local
kind cluster, ran the Postman collection, the recovery checks, the benchmark,
and a local rehearsal of the CI job, and recorded the results in
[`verification-results.md`](verification-results.md) and
[`benchmark-results.md`](benchmark-results.md). The GitHub Actions runs listed
there ran on GitHub after the team pushed.

**What the team did:**

- Directed the work: gave the agent the instructions for each step, including
  the project topic, scope, order of steps, and constraints such as keeping
  credentials out of the repository and leaving commits to the team.
- Received each change for review, then staged, committed, and pushed it from
  their own GitHub accounts. The agent never staged, committed, or pushed.
- Provided local access when asked (for example the PostgreSQL administrator
  login on 7 November) and approved installing Docker Engine, kind, and kubectl
  in WSL.

The commits carry no AI co-author trailer, so the Git history alone does not
show AI involvement; this section is the record of it.

**To be completed by the team:**

- Who wrote the step instructions given to the agent.
- What each member reviewed, tested, or changed by hand, and anything the team
  rejected or corrected.
- Whether the course's guidelines on AI use require further disclosure.

## Group members and contributions

GitHub links the repository's commits to two accounts (repository contributors
list and `git log`, 9 November 2026):

| GitHub account | Commits | Dates | Commits made |
| -------------- | ------: | ----- | ------------ |
| [`rinesaavdiu`](https://github.com/rinesaavdiu) (owner of the repository) | 16 | 7–8 Nov 2026 | From `ba81ce0` (README and folder structure) to `06e5a1f` (benchmark, Kubernetes, deployment docs) |
| [`domenikka`](https://github.com/domenikka) | 3 | 9 Nov 2026 | `f4875fb` (CI workflow), `da59588` (installation and API docs), `4bf38b0` (verification results) |

Both accounts committed from the same computer, in the same session as the AI
agent. A commit's author shows who committed a change, not who wrote it, so
this document does not credit any code or design decision to an individual.
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

## Container images and tools

| Item | Version | Purpose |
| ---- | ------- | ------- |
| `mcr.microsoft.com/dotnet/sdk`, `aspnet`, `runtime` | `10.0-alpine` | Building and running the API and worker images |
| `postgres` | `18.6-alpine` | Database |
| `rabbitmq` | `4.1-management-alpine` | Message broker |
| `redis` | `8.2-alpine` | Category cache |
| `axllent/mailpit` | `v1.33` | Test mail inbox |
| `nginx` | `1.28-alpine` | Compose gateway |
| Docker Engine and Docker Compose, kind, kubectl | See the README's "Tools and versions" | Local deployment |
| GitHub Actions: `actions/checkout`, `actions/setup-dotnet`, `actions/upload-artifact`, `helm/kind-action` | Pinned to commit SHAs in `.github/workflows/ci.yml` | CI |
| Postman collection format v2.1 and Newman | Newman 6 | API walkthrough and checks |
| actionlint, ShellCheck, jq | 1.7.12, 0.8.0, 1.6 | Checking the CI workflow and scripts; jq is also used by the smoke test |

## Reused material and references

- **No third-party code is included** beyond the libraries and images listed
  above; the repository started empty on 7 November 2026 and every file was
  written in the session described under AI assistance. The EF Core migration
  files were generated by `dotnet-ef` from the model. AI-generated code can
  still resemble public examples; the multi-stage Dockerfiles and the ASP.NET
  Core configuration follow the patterns in the official Microsoft
  documentation.
- **Error type URIs.** API errors use Problem Details (RFC 9457) with `type`
  links to `https://httpstatuses.io/<status>`, an external reference site.
- **Demo data** (users, categories, rooms, and ticket text) is fictional and
  uses the reserved `.test` email domain.

**To be completed by the team:** any course material, lecture examples,
tutorials, or other sources the team consulted while planning or reviewing the
project.
