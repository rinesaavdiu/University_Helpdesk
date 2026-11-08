# University Helpdesk API

**Sistem për raportimin e problemeve teknike në universitet**

University Helpdesk API is a backend service for reporting and tracking
technical problems in university classrooms. Students report issues with
classroom technology, such as projectors, computers, or network access, and
technicians track those reports and mark them as resolved. When a ticket is
resolved, the Notification Worker emails the student.

The commands below are for Windows PowerShell and run from the repository root.

## Run everything with Docker Compose

Requires Docker with Docker Compose v2 (on Windows, Docker Desktop with the
WSL 2 backend). The .NET SDK is not needed for this.

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

## Monitoring

| Path            | Purpose                                                                                 |
| --------------- | --------------------------------------------------------------------------------------- |
| `/health/live`  | Liveness probe: the API process is serving requests. No dependency checks.              |
| `/health/ready` | Readiness probe: PostgreSQL is reachable. Returns `503` when it is not.                  |
| `/metrics`      | Prometheus metrics: request counts, `5xx` errors, and latency per route template, plus category cache hits and misses. |

The gateway does not expose `/metrics`, because each replica has its own
counters. Read them inside the Compose network, for example
`docker compose exec api wget -qO- http://127.0.0.1:8080/metrics`.

The API and the worker write JSON logs to the console. Each API request is
logged once with its route template, status code, duration, and trace ID.
Notification logs include the event ID. Logs never contain credentials, tokens,
or ticket descriptions. See `docs/api-contract.md` for the metric names.

## Directory guide

| Directory        | Purpose                                                       |
| ---------------- | ------------------------------------------------------------- |
| `backend/`       | Source code and Dockerfiles for the API and the Notification Worker |
| `database/init/` | Scripts PostgreSQL runs when its Compose volume is created    |
| `deploy/`        | Configuration for the Compose gateway                         |
| `tests/`         | Unit, integration, and recovery tests, and the Postman collection |
| `docs/`          | Project documentation and requirements                        |
| `scripts/`       | Helper scripts for development and maintenance                |
