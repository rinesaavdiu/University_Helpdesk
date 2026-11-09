# University Helpdesk API

**Sistem për raportimin e problemeve teknike në universitet**

University Helpdesk API is a backend service for reporting and tracking
technical problems in university classrooms. Students report issues with
classroom technology, such as projectors, computers, or network access, and
technicians track those reports and mark them as resolved. When a ticket is
resolved, the Notification Worker emails the student.

This repository contains the backend: the Ticket API and the Notification
Worker. A web frontend for students and technicians is planned but not built
yet; until then, use the Swagger UI or the Postman collection described below.

The commands below are for Windows PowerShell and run from the repository root.

## Tools and versions

What you need depends on how you run the project:

| Task | Tools |
| ---- | ----- |
| Run everything in containers | Docker Engine with Docker Compose v2 |
| Run the API and worker from source | .NET 10 SDK, plus Docker for the backing services |
| Deploy to a local Kubernetes cluster | Docker, kind, and kubectl |
| Run the Postman collection from the command line | Node.js (for `npx newman`) |

These are the versions the project was built and verified with:

| Tool | Version |
| ---- | ------- |
| .NET SDK | 10.0.403 (the images use the `10.0-alpine` .NET images) |
| EF Core command-line tools | `dotnet-ef` 10.0.4, pinned in `dotnet-tools.json` |
| Docker Engine and Docker Compose | 29.9.3 and v5.7.0, inside WSL 2 (Ubuntu 22.04) on Windows 10 |
| kind and kubectl | kind v0.33.0 (Kubernetes v1.37.0 node) and kubectl v1.37.2 |
| Node.js and Newman | Node.js 22.20.0 and Newman 6 |

The services run as these images: `postgres:18.6-alpine`,
`rabbitmq:4.1-management-alpine`, `redis:8.2-alpine`, `axllent/mailpit:v1.33`,
and `nginx:1.28-alpine` (the Compose gateway). The main libraries are
ASP.NET Core 10, EF Core 10.0.4 with Npgsql 10.0.3, RabbitMQ.Client 7.2.2,
StackExchange.Redis 3.4.31, MailKit 4.18.2, prometheus-net 8.2.1, and xUnit
2.9.3.

## Run everything with Docker Compose

Requires Docker with Docker Compose v2 (on Windows, Docker Desktop with the
WSL 2 backend, or Docker Engine installed inside a WSL 2 distribution, where
you run the `docker` commands from the repository folder under `/mnt/c/...`).
The .NET SDK is not needed for this.

1. Copy the example settings, then open `.env` and replace every `change-me`.
   Use only letters and digits in passwords, and at least 32 characters for
   `JWT_SIGNING_KEY`. If a host port is already in use, change the matching
   `*_HOST_PORT` value.

   ```powershell
   Copy-Item .env.example .env
   notepad .env
   ```

   To generate a random value, run
   `-join ((48..57) + (65..90) + (97..122) | Get-Random -Count 40 | ForEach-Object { [char]$_ })`.

2. Build the images and start the system. The first build downloads the .NET
   images and takes a few minutes.

   ```powershell
   docker compose up -d --build --wait
   ```

   This starts PostgreSQL, RabbitMQ, Redis, and Mailpit, then runs `db-init`,
   which applies the database migrations and creates the demo accounts
   `student1@uni.test`, `student2@uni.test`, and `technician1@uni.test` with
   the passwords from `.env`. `db-init` is safe to repeat and runs on every
   `docker compose up`. Only after it succeeds do the two API replicas start,
   behind a `gateway` that spreads requests between them. The Notification
   Worker uses the same RabbitMQ exchange and queue settings as the API.

3. Open the services:

   | URL                              | What                                                     |
   | -------------------------------- | -------------------------------------------------------- |
   | `http://localhost:58080/swagger` | API (Swagger UI). Log in with `POST /auth/login`, then select **Authorize** and paste the `accessToken`. |
   | `http://localhost:58025`         | Mailpit inbox with the resolution emails.                 |
   | `http://localhost:55673`         | RabbitMQ management UI (`RABBITMQ_USER` / `RABBITMQ_PASSWORD`). |

4. Check the state and the logs:

   ```powershell
   docker compose ps
   docker compose logs -f api worker
   ```

   Every service has a health check. The API replicas are healthy when
   `/health/ready` succeeds; the worker is healthy while it is consuming from
   RabbitMQ.

To stop the system and keep the data, run `docker compose stop` or
`docker compose down`. `docker compose down -v` also deletes the database and
the RabbitMQ messages.

## Deploy to a local Kubernetes cluster

`deploy/kubernetes/` holds three Kustomize layers, applied in this order:

| Layer            | Contents |
| ---------------- | -------- |
| `infrastructure` | Namespace `university-helpdesk`, the `helpdesk-secrets` Secret, PostgreSQL and RabbitMQ as StatefulSets with 1 GiB volumes, Redis, and Mailpit. |
| `init`           | The `db-init` Job: waits for PostgreSQL, applies the migrations, and creates the demo accounts. |
| `apps`           | The API Deployment (two replicas) behind the internal `api` Service, and the Notification Worker Deployment. |

The API pods use `/health/ready` for readiness and `/health/live` for liveness,
so a database outage takes them out of the Service without restarting them. The
worker's probes check its health file. The `api` Service is `ClusterIP` only:
`/metrics` stays reachable for monitoring inside the cluster (the pods carry
`prometheus.io/scrape` annotations), but nothing is exposed outside it.

Requires Docker, [kind](https://kind.sigs.k8s.io/), and `kubectl`. The
commands are the same in PowerShell and in a WSL shell.

1. Create the cluster, build the images, and copy them into the cluster:

   ```powershell
   kind create cluster --config deploy/kubernetes/kind-cluster.yaml
   docker build --file backend/UniversityHelpdesk.Api/Dockerfile --tag university-helpdesk-api:local .
   docker build --file backend/UniversityHelpdesk.NotificationWorker/Dockerfile --tag university-helpdesk-worker:local .
   kind load docker-image university-helpdesk-api:local university-helpdesk-worker:local --name university-helpdesk
   ```

2. Create the local secrets file. It uses the same keys as `.env` and is
   ignored by Git:

   ```powershell
   Copy-Item deploy/kubernetes/infrastructure/secrets.env.example deploy/kubernetes/infrastructure/secrets.env
   notepad deploy/kubernetes/infrastructure/secrets.env
   ```

3. Deploy the layers. The apps are applied only after the Job has completed,
   so the API replicas never race to change the schema. A Job cannot be
   changed once created, so the previous one is deleted first:

   ```powershell
   kubectl apply -k deploy/kubernetes/infrastructure
   kubectl -n university-helpdesk delete job db-init --ignore-not-found
   kubectl apply -k deploy/kubernetes/init
   kubectl -n university-helpdesk wait --for=condition=complete job/db-init --timeout=300s
   kubectl apply -k deploy/kubernetes/apps
   kubectl -n university-helpdesk rollout status deployment/api
   kubectl -n university-helpdesk get pods
   ```

   In a Linux or WSL shell, `bash scripts/ci/deploy-kind.sh` runs the
   `kind load` command and these steps in one go.

4. Open the API and Mailpit from your machine. Each command keeps running, so
   use a separate terminal for each:

   ```powershell
   kubectl -n university-helpdesk port-forward service/api 58090:8080
   kubectl -n university-helpdesk port-forward service/mailpit 58125:8025
   ```

   Swagger is then at `http://localhost:58090/swagger` and Mailpit at
   `http://localhost:58125`. A port-forward sends every request to a single
   pod; requests from inside the cluster to `http://api:8080` are spread across
   both replicas.

After changing the code, repeat the build and `kind load` commands, run the
`db-init` Job again if there are new migrations, and then run
`kubectl -n university-helpdesk rollout restart deployment/api deployment/notification-worker`.
`kind delete cluster --name university-helpdesk` removes the cluster and its
data.

## Run the API and worker from source

Use this while changing the code. Docker provides only the backing services;
the API and the worker run with `dotnet run`. Requires the .NET 10 SDK as well.

1. Create `.env` as in step 1 above, then start only the backing services:

   ```powershell
   docker compose up -d --wait postgres rabbitmq redis mailpit
   ```

   Do not also start the `api` and `worker` containers: a second worker would
   take messages from the same queue.

2. Docker Compose reads `.env`, but `dotnet run` does not. Pass the connection
   string to the API through an environment variable, using the same values as
   in `.env`:

   ```powershell
   $env:ConnectionStrings__HelpdeskDatabase = "Host=127.0.0.1;Port=55432;Database=helpdesk;Username=helpdesk;Password=<POSTGRES_PASSWORD>"
   ```

3. Create the schema and seed the demo categories. Migrations are never
   applied when the API starts; run this after pulling new migrations:

   ```powershell
   dotnet run --project backend/UniversityHelpdesk.Api --launch-profile http -- migrate-database
   ```

   `dotnet tool restore` followed by
   `dotnet ef database update --project backend/UniversityHelpdesk.Api` does the
   same with the EF Core tools.

4. Store the JWT signing key, the demo account passwords, and the RabbitMQ
   password in .NET user secrets. They are saved in your Windows profile, not in
   the repository. The signing key must be at least 32 characters; the API
   refuses to start without it. `RabbitMq:Password` must match
   `RABBITMQ_PASSWORD` in `.env`:

   ```powershell
   $project = "backend/UniversityHelpdesk.Api"
   dotnet user-secrets set "Jwt:SigningKey" "<at least 32 random characters>" --project $project
   dotnet user-secrets set "DemoAccounts:Student1Password" "<password>" --project $project
   dotnet user-secrets set "DemoAccounts:Student2Password" "<password>" --project $project
   dotnet user-secrets set "DemoAccounts:Technician1Password" "<password>" --project $project
   dotnet user-secrets set "RabbitMq:Password" "<RABBITMQ_PASSWORD from .env>" --project $project
   dotnet user-secrets set "RabbitMq:Password" "<RABBITMQ_PASSWORD from .env>" --project backend/UniversityHelpdesk.NotificationWorker
   ```

   Run `dotnet user-secrets list --project $project` to see the values later.
   The other settings in both `appsettings.json` files match the default ports
   in `.env.example`. If you changed a host port, override the matching setting
   with an environment variable before starting the API or the worker:
   `$env:RabbitMq__Port`, `$env:Redis__Configuration` (for example
   `127.0.0.1:56379`), or `$env:Smtp__Port`.

5. Create the demo accounts. The command only runs in Development. Existing
   accounts are left unchanged, so it is safe to run again:

   ```powershell
   dotnet run --project backend/UniversityHelpdesk.Api --launch-profile http -- seed-demo-users
   ```

6. Start the API, and the Notification Worker in a second terminal:

   ```powershell
   dotnet run --project backend/UniversityHelpdesk.Api --launch-profile http
   dotnet run --project backend/UniversityHelpdesk.NotificationWorker
   ```

   Swagger is at `http://localhost:5276/swagger`, and Mailpit at
   `http://localhost:58025`.

## HTTPS

**Local HTTPS with `dotnet run`.** The `https` launch profile serves the API on
`https://localhost:7052` and `http://localhost:5276`, and redirects HTTP
requests to HTTPS. It uses the .NET development certificate for `localhost`.
Create and trust it once (Windows asks for confirmation), then start the API
as in the steps above, with the `https` profile:

```powershell
dotnet dev-certs https --trust
dotnet run --project backend/UniversityHelpdesk.Api --launch-profile https
```

Check it from a second terminal:

```powershell
curl.exe -s -o NUL -w "%{http_code}`n" https://localhost:7052/health/ready
curl.exe -s -o NUL -w "%{http_code} %{redirect_url}`n" http://localhost:5276/tickets
```

The first command prints `200` once the certificate is trusted. The second
prints `307 https://localhost:7052/tickets`. Swagger is at
`https://localhost:7052/swagger`. On 9 November 2026 this was checked with an
untrusted development certificate: the redirect and the HTTPS endpoints
worked, and clients that validate certificates rejected the connection until
the certificate is trusted (see TC-34 in `docs/test-cases.md`).

**Docker Compose and kind use plain HTTP.** These setups are for local
demonstrations: the Compose ports are bound to `127.0.0.1` only, and the kind
API is reached through `kubectl port-forward`. Tokens and passwords therefore
travel unencrypted, but only over the loopback interface of your own machine.
The containers listen on HTTP only, so the API does not redirect there.

**Published deployment (not implemented or tested).** A deployment reachable
by other machines must use HTTPS. The intended approach is to terminate TLS in
front of the API, at the gateway or a Kubernetes Ingress, with a certificate
from a certificate authority (for example Let's Encrypt through cert-manager),
to redirect HTTP to HTTPS and send HSTS headers there, and to have the API
trust the `X-Forwarded-Proto` header from that proxy only. None of this is part
of the repository yet.

## Configuration

Both programs read standard .NET configuration: `appsettings.json`, then user
secrets (in Development), then environment variables. In an environment
variable, `__` replaces `:`, so `RabbitMq:Password` becomes
`RabbitMq__Password`. Docker Compose sets the variables from `.env`, and
Kubernetes from the `helpdesk-secrets` Secret and the `rabbitmq-settings`
ConfigMap.

| Setting | Used by | Default in `appsettings.json` | Notes |
| ------- | ------- | ----------------------------- | ----- |
| `ConnectionStrings:HelpdeskDatabase` | API | none | Required for every request that uses the database. Without it the API still starts, but those requests fail with an error naming the missing setting. Secret. |
| `Jwt:SigningKey` | API | none | Required; at least 32 bytes. Secret. |
| `Jwt:Issuer`, `Jwt:Audience` | API | `UniversityHelpdesk.Api`, `UniversityHelpdesk.Clients` | |
| `DemoAccounts:Student1Password`, `Student2Password`, `Technician1Password` | API (`seed-demo-users`) | none | Needed only to create the demo accounts. Secret. |
| `RabbitMq:HostName`, `Port`, `VirtualHost`, `UserName` | API and worker | `127.0.0.1`, `55672`, `helpdesk`, `helpdesk` | Both must point at the same broker. |
| `RabbitMq:Password` | API and worker | none | Secret. |
| `RabbitMq:Exchange`, `TicketResolvedQueue` | API and worker | `helpdesk.events`, `helpdesk.ticket-resolved` | Must be identical in both. |
| `RabbitMq:FailedQueue` | Worker | `helpdesk.ticket-resolved.failed` | |
| `Redis:Configuration` | API | `127.0.0.1:56379` | Empty disables the category cache. |
| `Redis:CategoryCacheDuration`, `Redis:Timeout` | API | `00:05:00`, `00:00:01` | |
| `OutboxPublisher:MaxAttempts`, `FirstRetryDelay`, `MaxRetryDelay` | API | `10`, 5 seconds, 5 minutes | Publishing retries for resolution events. |
| `Smtp:Host`, `Smtp:Port` | Worker | `127.0.0.1`, `51025` | Mailpit in local development. |
| `Smtp:FromAddress`, `Smtp:FromName` | Worker | `helpdesk@uni.test`, `University Helpdesk` | |
| `Delivery:MaxAttempts`, `FirstRetryDelay`, `MaxRetryDelay` | Worker | `5`, 2 seconds, 30 seconds | Email retries before a message goes to the failed queue. |
| `Health:FilePath` | Worker | empty (off) | Health file for container probes; Compose and Kubernetes set `/tmp/notification-worker.alive`. |

Secrets are never stored in `appsettings.json` or committed: use `.env` for
Compose, `deploy/kubernetes/infrastructure/secrets.env` for Kubernetes, and
user secrets or environment variables for `dotnet run`. All three files are
ignored by Git.

## Database initialization

The API never changes the schema when it starts. Two one-off commands of the
API program prepare the database instead, and both are safe to run again:

| Command | What it does |
| ------- | ------------ |
| `migrate-database` | Applies any pending EF Core migrations. The migrations also insert the six demo categories (Audio, Computer, Projector, Network, Smart Board, Other). |
| `seed-demo-users` | Creates the missing demo accounts `student1@uni.test`, `student2@uni.test`, and `technician1@uni.test` with the passwords from `DemoAccounts:*`, and leaves existing accounts unchanged. It runs only in the Development environment. |

Each setup runs them before the API starts:

| Setup | How the commands run |
| ----- | -------------------- |
| Docker Compose | The `db-init` service runs both on every `docker compose up`; the API replicas wait until it succeeds. |
| Kubernetes | The `db-init` Job in `deploy/kubernetes/init` runs both; apply the apps only after it has completed. |
| `dotnet run` | Run `-- migrate-database` and `-- seed-demo-users` yourself, as in the steps above. |

Docker Compose also creates an empty `helpdesk_test` database when the
PostgreSQL volume is first created, for the integration tests.

## API overview and permissions

Every API endpoint except login needs `Authorization: Bearer <accessToken>`
from `POST /auth/login`. The full contract, with request and response bodies and
error codes, is in `docs/api-contract.md`.

These routes need no token:

| Route | Purpose | Exposure |
| ----- | ------- | -------- |
| `POST /auth/login` | Sign-in | Public |
| `GET /health/live`, `GET /health/ready` | Probes | Reachable through the Compose gateway; inside the cluster in Kubernetes |
| `GET /metrics` | Prometheus metrics | Blocked at the Compose gateway (`404`); the Kubernetes Service is not exposed outside the cluster |
| `/swagger`, `/swagger/v1/swagger.json` | API documentation | Only in the Development environment, which Compose, kind, and `dotnet run` use |

| Endpoint | Student | Technician |
| -------- | ------- | ---------- |
| `POST /auth/login` | Public | Public |
| `GET /categories` | Yes | Yes |
| `POST /tickets` | Yes, owned by the caller | No (`403`) |
| `GET /tickets`, `GET /tickets/{id}` | Own tickets only; another student's ticket is `404` | All tickets |
| `PATCH /tickets/{id}`, `DELETE /tickets/{id}` | Own tickets, only while `Open` | No (`403`) |
| `PATCH /tickets/{id}/status` | No (`403`) | One step forward: `Open → InProgress → Resolved` |

Edits and status changes must send the ticket's current `version`; a stale
version returns `409`. The health and metrics endpoints are described under
[Monitoring](#monitoring).

## How notifications and caching behave

When a technician resolves a ticket, the API saves a pending notification in
the same transaction, and a background publisher sends it to the
`helpdesk.events` exchange in RabbitMQ. Before publishing, the API declares the
durable `helpdesk.ticket-resolved` queue and binds it with routing key
`TicketResolved`, so messages are kept even if the Notification Worker has not
started yet. If RabbitMQ is not running, or returns a message as unroutable, the
API keeps working and the event waits in the `PendingNotifications` table; it is
retried with increasing delays and published once RabbitMQ accepts it. An event
that fails 10 times is left unpublished for inspection; set its `Attempts` back
to `0` to retry it.

The worker acknowledges a message only after the SMTP server accepts the email.
Failed sends are retried 5 times with increasing delays; after that, the
message is moved to the `helpdesk.ticket-resolved.failed` queue, where it stays
for inspection in the RabbitMQ management UI. If the worker is stopped,
messages wait in the queue and are delivered when it starts again.

Delivery is at least once in both steps. An event is marked published only
after RabbitMQ confirms it, so a crash between that confirmation and the
database commit publishes the same event again. If the worker crashes after the
SMTP server accepts an email but before RabbitMQ receives the acknowledgement,
the message is delivered again and the student gets a duplicate email.

`GET /categories` is cached in Redis for 5 minutes. If Redis is not running,
the API logs a warning and reads the categories from PostgreSQL instead.

## Tests

### Unit and HTTP tests

```powershell
dotnet test
```

These need no database, broker, or other service. The database-backed
integration tests and the recovery checks below are reported as skipped until
their environment variables are set.

### HTTP integration tests

These call the real API over HTTP with a real PostgreSQL database: login, ticket
create, read, edit, and delete, ownership, role restrictions, invalid input,
pagination, the status workflow, and concurrent updates. They need a database
used only for tests, whose name must end in `_test`. Docker Compose creates
`helpdesk_test` when the PostgreSQL volume is first created; for an older
volume, run `docker compose exec postgres createdb -U helpdesk helpdesk_test`.

```powershell
$env:HELPDESK_TEST_DATABASE = "Host=127.0.0.1;Port=55432;Database=helpdesk_test;Username=helpdesk;Password=<POSTGRES_PASSWORD>"
dotnet test tests/UniversityHelpdesk.Api.Tests --filter "FullyQualifiedName~Integration"
```

The tests apply the migrations to that database, create their own accounts
tagged with a run ID, and delete those accounts and their tickets at the end.
Other data is left alone, so the tests can be repeated, or run while another
run is in progress.

### Recovery checks

These run the real API and Notification Worker against PostgreSQL, RabbitMQ,
Mailpit, and Redis, and simulate outages with a small TCP proxy between a
component and the service, so the shared services keep running:

| Check | Expected result |
| ----- | --------------- |
| Worker stopped | The event waits in the queue, no email is sent, and the email arrives once the worker starts. |
| Temporary SMTP failure | The worker retries, and the email arrives once SMTP accepts connections again. |
| SMTP down for every attempt | After the last attempt, the event is in the failed queue with the reason and attempt count. |
| RabbitMQ outage | Resolving still succeeds, the event waits in the outbox, and the publisher and worker both reconnect and deliver it. |
| Redis outage | Categories are served from PostgreSQL within the timeout, readiness stays healthy, and caching resumes afterwards. |

With the Compose services running, set the variables below (with the
passwords from `.env`) and run:

```powershell
$env:HELPDESK_TEST_DATABASE = "Host=127.0.0.1;Port=55432;Database=helpdesk_test;Username=helpdesk;Password=<POSTGRES_PASSWORD>"
$env:HELPDESK_TEST_RABBITMQ = "amqp://helpdesk:<RABBITMQ_PASSWORD>@127.0.0.1:55672/helpdesk"
$env:HELPDESK_TEST_SMTP = "127.0.0.1:51025"
$env:HELPDESK_TEST_MAILPIT = "http://127.0.0.1:58025"
$env:HELPDESK_TEST_REDIS = "127.0.0.1:56379"
dotnet test tests/UniversityHelpdesk.RecoveryTests
```

Each run uses its own exchange and queues (`*.test-<run ID>`), its own accounts,
and Redis database 15, and deletes them and its emails at the end. It does not
touch the `api` and `worker` containers or the `helpdesk.ticket-resolved`
queue, so it can run while the Compose system is up.

### Postman collection

`tests/postman/UniversityHelpdesk.postman_collection.json` walks through the
same areas with the demo accounts. Copy the example environment, then replace
the password placeholders with the demo passwords. Files named
`*.postman_environment.json` are ignored by Git.

```powershell
Copy-Item tests/postman/helpdesk.postman_environment.example.json tests/postman/local.postman_environment.json
```

Import both files into Postman and run the collection in order, or run it with
Newman (requires Node.js):

```powershell
npx newman run tests/postman/UniversityHelpdesk.postman_collection.json -e tests/postman/local.postman_environment.json
```

`baseUrl` points at the Compose gateway; use `http://localhost:5276` for
`dotnet run`. Each run creates new tickets, so it can be repeated.

### Benchmark

`benchmarks/UniversityHelpdesk.Benchmark` measures the documented workload
against a running system: 20 concurrent users create 1,000 tickets, then list
tickets as a technician (all tickets) and as students (their own), and read the
categories cold (the Redis key is deleted before each request) and warm. For
each scenario it reports the request count, failures, median, p95, and maximum
latency, and throughput.

The benchmark creates its own accounts directly in the API's database, with
emails ending in `@benchmark.test` and a run ID, so it needs that database's
connection string. At the end it deletes those accounts and their tickets and
nothing else. With the Compose system running:

```powershell
$env:HELPDESK_BENCH_DATABASE = "Host=127.0.0.1;Port=55432;Database=helpdesk;Username=helpdesk;Password=<POSTGRES_PASSWORD>"
dotnet run --project benchmarks/UniversityHelpdesk.Benchmark -c Release -- --base-url http://127.0.0.1:58080 --redis 127.0.0.1:56379 --environment "<describe the machine and deployment>" --output benchmark.json
Remove-Item Env:HELPDESK_BENCH_DATABASE
```

Use `127.0.0.1` rather than `localhost`: on the Windows host used for the
recorded results, `localhost` tried IPv6 first, and each new connection to the
gateway took about 2 s longer, which inflated the login timings.

Run it with `--help` to see the options for the user, ticket, and request counts.
It exits with `1` if any request fails. Recorded results are in
`docs/benchmark-results.md`, and every test, recovery, deployment, and CI result
is in `docs/verification-results.md`.

### Continuous integration

`.github/workflows/ci.yml` runs on pushes to `main`, on pull requests to any
branch, and manually from the **Actions** tab (**CI → Run workflow**, or
`gh workflow run ci.yml --ref main`). Pushes to other branches do not trigger
it. It has two jobs:

1. **Build and test** restores and builds the solution in Release and runs
   `dotnet test`. The test results (`.trx` files) are uploaded as the
   `test-results` artifact.
2. **Kubernetes smoke test** builds both images, generates random credentials
   for the run (masked in the log), creates a kind cluster, and deploys the
   manifests in the order described above. It then runs
   `scripts/ci/smoke-test.sh`: a student logs in and creates a ticket, a
   technician moves it to `InProgress` and `Resolved`, and the resolution email
   must reach the student in Mailpit. The namespace's resources, events, and
   container logs are uploaded as the `kubernetes-diagnostics` artifact, with
   every credential value replaced by `***`.

Which suites run where:

| Suite | Hosted CI | Locally |
| ----- | --------- | ------- |
| Unit and HTTP tests (no services) | Yes | `dotnet test` |
| Integration tests (24, need PostgreSQL) | No: skipped, because `HELPDESK_TEST_DATABASE` is not set | With `HELPDESK_TEST_DATABASE`, as above |
| Recovery checks (5, need every service) | No: skipped | With the `HELPDESK_TEST_*` variables, as above |
| Postman collection | No | `npx newman run`, as above |
| Kubernetes deployment and smoke test | Yes | The scripts below |

A CI run therefore fails on any failing unit or HTTP test, or a failed
deployment or smoke test, but not on the integration tests or recovery checks;
run those before pushing. Per-case results are in `docs/test-cases.md`.

The same scripts run locally in a Linux or WSL shell with Docker, kind,
kubectl, `curl`, `jq`, and `openssl`. `new-kind-secrets.sh` refuses to
overwrite an existing `secrets.env`, so move yours aside first:

```bash
docker build --file backend/UniversityHelpdesk.Api/Dockerfile --tag university-helpdesk-api:local .
docker build --file backend/UniversityHelpdesk.NotificationWorker/Dockerfile --tag university-helpdesk-worker:local .
kind create cluster --config deploy/kubernetes/kind-cluster.yaml --name helpdesk-ci
bash scripts/ci/new-kind-secrets.sh
export KIND_CLUSTER=helpdesk-ci
bash scripts/ci/deploy-kind.sh
bash scripts/ci/smoke-test.sh
bash scripts/ci/collect-diagnostics.sh diagnostics
kind delete cluster --name helpdesk-ci
```

## Monitoring

| Path            | Purpose                                                                                 |
| --------------- | --------------------------------------------------------------------------------------- |
| `/health/live`  | Liveness probe: the API process is serving requests. No dependency checks.              |
| `/health/ready` | Readiness probe: PostgreSQL is reachable. Returns `503` when it is not.                  |
| `/metrics`      | Prometheus metrics: request counts, `5xx` errors, and latency per route template, plus category cache hits and misses. |

The gateway does not expose `/metrics`, because each replica has its own
counters. Read them inside the Compose network, for example
`docker compose exec api wget -qO- http://127.0.0.1:8080/metrics`. In
Kubernetes, the API pods carry `prometheus.io/scrape` annotations, and
`/metrics` is reachable only from inside the cluster.

The API and the worker write JSON logs to the console. Each API request is
logged once with its route template, status code, duration, and trace ID.
Notification logs include the event ID. Logs never contain credentials, tokens,
or ticket descriptions. See `docs/api-contract.md` for the metric names.

## Presentation and demo

`docs/presentation.md` holds the defense slides (Marp format; export with
`npx @marp-team/marp-cli docs/presentation.md --pdf`), and
`docs/demo-runbook.md` the step-by-step live demo, a practice exercise for a
small live code change, and a record of the rehearsal.

## Submission package

`scripts/package-submission.ps1` builds the submission ZIP from the committed
files and checks that it contains the required items and no local secrets or
build output; `scripts/verify-package.ps1` extracts it into a clean folder,
builds and tests it, and runs `scripts/verify-compose.sh`, which starts Docker
Compose with a fresh `.env` and new volumes and checks the core flow. The
steps and the hand-in checklist are in `docs/submission-checklist.md`.

```powershell
powershell -ExecutionPolicy Bypass -File scripts/package-submission.ps1 -IncludeSlidesPdf
powershell -ExecutionPolicy Bypass -File scripts/verify-package.ps1 -ZipPath submission/University_Helpdesk-<commit>.zip
```

## Sources and contributions

`docs/sources-and-contributions.md` lists the libraries, images, and tools the
project uses, the references it draws on, how AI tools were used, and each
group member's contributions.

## Directory guide

| Directory        | Purpose                                                       |
| ---------------- | ------------------------------------------------------------- |
| `.github/`       | GitHub Actions workflow                                       |
| `backend/`       | Source code and Dockerfiles for the API and the Notification Worker |
| `benchmarks/`    | Load benchmark for the documented performance target          |
| `database/init/` | Scripts PostgreSQL runs when its Compose volume is created    |
| `deploy/`        | Configuration for the Compose gateway, and Kubernetes manifests in `deploy/kubernetes/` |
| `tests/`         | Unit, integration, and recovery tests, and the Postman collection |
| `docs/`          | Project documentation and requirements                        |
| `scripts/`       | Packaging and clean-copy verification of the submission ZIP    |
| `scripts/ci/`    | Scripts the CI workflow uses to deploy to kind, run the smoke test, and collect diagnostics |
