# Submission checklist

How to build the submission ZIP, check it from a clean copy, and hand it in.
Section 11 of the course requirements asks for one ZIP on Moodle with the
source code, configuration examples and run scripts, the database schema and
initialization scripts, the tests and their results, the README, and the
presentation materials. One member submits it by **8 January 2027**; every
member attends the defense.

## What the package contains

`scripts/package-submission.ps1` builds the ZIP with `git archive` from a
commit, so it contains only committed files, in one folder named
`University_Helpdesk/`. It adds two generated files and then checks the
result.

| Course item (section 11) | In the package |
| ------------------------ | -------------- |
| 1. Source code | `backend/` (API and Notification Worker), `frontend/` (React app with `package.json` and `package-lock.json`), `benchmarks/`, `UniversityHelpdesk.slnx` |
| 2. Configuration and run scripts | `.env.example`, `compose.yaml`, `deploy/` (gateway and Kubernetes, with `secrets.env.example`), `appsettings.json` files, Dockerfiles (including `frontend/Dockerfile` for the gateway), `.github/workflows/ci.yml`, `scripts/` |
| 3. Database | EF Core migrations in `backend/UniversityHelpdesk.Api/Persistence/Migrations/` (schema and the six categories), **`database/schema.sql`** (generated from the migrations when the package is built), `database/init/` (test database), and the demo-account seeding described in the README, "Database initialization" |
| 4. Tests and results | `tests/` (unit, HTTP, integration, recovery, Postman), `docs/test-cases.md`, `docs/verification-results.md`, `docs/benchmark-results.md` |
| 5. README | `README.md`, plus `docs/requirements.md`, `docs/design.md`, `docs/api-contract.md` |
| 6. Presentation | `docs/presentation.md`, **`docs/presentation.pdf`** (exported when the package is built with `-IncludeSlidesPdf`), `docs/demo-runbook.md`, this checklist |
| Section 14: sources and AI use | `docs/sources-and-contributions.md` |

The script fails if any of these is missing, if the README has no
`migrate-database`, `seed-demo-users`, or `docker compose up` instructions, or
if the AI disclosure section is missing. It also fails if the package contains
any of the following:

- `.git`, a local `.env` or `.env.*` file (other than `.env.example`),
  `secrets.env`, a Postman environment file, `secrets.json`, or a local
  `appsettings` file.
- Keys and certificates (`.pem`, `.key`, `.pfx`, `.p12`).
- Dependencies (`node_modules/`, `packages/`), build output (`bin/`, `obj/`,
  `TestResults/`, `.trx`, `.dll`, `.exe`, `frontend/dist/`), editor folders,
  or nested archives.
- In any text file: a private key, a JSON Web Token, a GitHub token, or any
  password or key value from the local `.env` and `secrets.env` files. The
  values are compared without being printed.

Once a `frontend/` folder exists, the script also requires
`frontend/package.json` and rejects `frontend/node_modules/` and
`frontend/dist/`.

## Build the package

On Windows, from the repository root, with everything committed:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/package-submission.ps1 -IncludeSlidesPdf
```

The ZIP is written to `submission/University_Helpdesk-<commit>.zip`; the
`submission/` folder and all ZIP files are ignored by Git. The script prints
the file count, size, and SHA-256 hash, and warns if there are uncommitted
changes, which are not included. It needs the .NET 10 SDK, and for
`-IncludeSlidesPdf`, Node.js and Chrome or Edge.

## Check it from a clean copy

```powershell
powershell -ExecutionPolicy Bypass -File scripts/verify-package.ps1 -ZipPath submission/University_Helpdesk-<commit>.zip
```

The script extracts the ZIP into a new folder in the temporary directory,
outside the repository, and follows the extracted README:

1. `dotnet build` and `dotnet test` (the ordinary suite; tests that need
   services are skipped).
2. `scripts/verify-compose.sh`, run in WSL when `docker` is not available in
   PowerShell. It creates `.env` from `.env.example` with new random values,
   moves the host ports to `4xxxx` so that they do not clash with a running
   demo system, and starts Compose under the project name `helpdesk-verify`,
   so its containers and volumes are new. It then checks:
   - `db-init` exited with `0`, the migrations and categories are in the
     database, the three demo accounts exist, and `helpdesk_test` was created.
   - The gateway serves the built frontend on direct links to student and
     technician routes, its script under `/assets/`, the API under `/api`
     and at its own paths, the health checks and Swagger, and hides
     `/metrics`.
   - Logins as student and technician, `401` without a token, `403` when the
     student changes a status, ticket creation, `InProgress` and `Resolved`,
     the student sees `Resolved`, and the email reaches the student in
     Mailpit.
   - At the end it removes the project's containers, volumes, and `.env`.

Options: `-UseCachedImages` starts Compose with the existing
`university-helpdesk-*:local` images instead of building them (this does
**not** prove that the images build from the package), `-SkipCompose` runs
only the build and tests, and `-KeepRunning` leaves the system running.

To check the copy downloaded back from Moodle without building anything:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/package-submission.ps1 -CheckZip <path to the downloaded ZIP>
```

## Rehearsal (9 November 2026)

The rehearsal package was built from the files prepared for the packaging
commit (Git tree `570e2b7`), because `docs/presentation.md` and
`docs/demo-runbook.md` were not yet committed. Results are in
[`verification-results.md`](verification-results.md#submission-package).

| Check | Result |
| ----- | ------ |
| Package contents and exclusions | Passed: 150 files, including `database/schema.sql` and `docs/presentation.pdf` |
| Negative test with planted problems | All five reported; exit code `1` |
| Build and ordinary tests from the extracted copy | Passed |
| Compose with images built from the package | **Open:** blocked, WSL could not resolve `mcr.microsoft.com` |
| Compose with cached images, fresh `.env` and new volumes | Passed: database initialized, core flow, access check, email delivered |
| Timed rehearsal with speakers | Not done |
| Upload to Moodle | Not done |

**DNS blocker.** Inside WSL, name lookups through `172.28.128.1` timed out for
`mcr.microsoft.com` and `registry-1.docker.io`, so `docker pull` and
`docker compose up --build` failed; Windows resolved the same names. Common
fixes are restarting WSL (`wsl --shutdown`, which also stops the containers and
the kind cluster), or setting `networkingMode=mirrored` or `dnsTunneling=true`
in `%UserProfile%\.wslconfig`. None was applied. Once fixed, run
`scripts/verify-package.ps1` again without `-UseCachedImages`.

## Hand-in checklist

Before the final package:

- [ ] Commit `docs/presentation.md` and `docs/demo-runbook.md`. They are not
      in commit `b29c149`, although the Step 29 commit message names them.
- [ ] Group member names on the title slide of `docs/presentation.md`.
- [ ] Each member's name, GitHub account, confirmed contributions, and
      evidence in `docs/sources-and-contributions.md` and on the
      "Individual contributions" slide. Credit only what each member
      confirms.
- [ ] Check that the AI disclosure in `docs/sources-and-contributions.md` and
      on the "Sources and AI use" slide meets the course guidelines on AI use.
- [ ] Every member can explain the code they present (section 14).
- [ ] React frontend: verified against the real API, its walkthrough and
      screenshots added to `docs/demo-runbook.md` and the "Live demo" slide,
      and its setup added to the README (see "After the React frontend"
      below).

Rehearsal:

- [ ] Click through `docs/demo-runbook.md` in Swagger with two browser windows
      (the 9 November rehearsal sent the same requests from a script).
- [ ] Timed run of the whole presentation and demo with every speaker.
      Record the time: ____ minutes, on ____ (date).
- [ ] Practice the live-change exercise (filter by room) on a separate branch,
      in under 10 minutes.

Package:

- [ ] `git status` is clean, and the final commit is pushed and its GitHub
      Actions run succeeded.
- [ ] `scripts/package-submission.ps1 -IncludeSlidesPdf` passes.
- [ ] `scripts/verify-package.ps1` passes **with a fresh image build**
      (without `-UseCachedImages`).
- [ ] Open `docs/presentation.pdf` from the extracted copy and check every
      slide.

Moodle:

- [ ] One member uploads `University_Helpdesk-<commit>.zip` to the course's
      Moodle assignment by 8 January 2027 (or in the format the teacher sets).
- [ ] Download the submitted file again and run
      `scripts/package-submission.ps1 -CheckZip` on it; compare the SHA-256
      hash with the one printed when the package was built.
- [ ] Record the submitted commit and hash here: commit ____, SHA-256 ____.
- [ ] Every member attends the defense, with the demo laptop prepared as in
      `docs/demo-runbook.md` ("Before the defense").

## After the React frontend

When the frontend is in `frontend/` and verified against the real API:

1. Update the README (frontend setup and start), `docs/demo-runbook.md` (the
   browser walkthrough and screenshots), `docs/presentation.md` (the "Live
   demo" slide and any new results), and `docs/test-cases.md` and
   `docs/verification-results.md` for any frontend checks.
2. Done: Compose builds the frontend into the gateway image, and
   `scripts/verify-compose.sh` requests the app and the API through it.
3. Commit and push, and wait for GitHub Actions to pass.
4. Build the package again with `-IncludeSlidesPdf`, and run
   `scripts/verify-package.ps1` on the new ZIP, without `-UseCachedImages`.
5. Update the rehearsal section above with what ran, and repeat the
   "Package" and "Moodle" items.
