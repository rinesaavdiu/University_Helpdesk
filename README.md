# University Helpdesk API

**Sistem për raportimin e problemeve teknike në universitet**

University Helpdesk API is a planned backend service for reporting and tracking
technical problems in university classrooms. Students will be able to report
issues with classroom technology, such as projectors, computers, or network
access, and technicians will be able to track those reports and mark them as
resolved.

The project is in its early stages. Installation and test instructions will be
added once they can be verified.

## Local setup (Windows PowerShell)

Requires Docker with Docker Compose and the .NET 10 SDK. Run these commands from
the repository root.

1. Copy the example settings and change `POSTGRES_PASSWORD` and
   `RABBITMQ_PASSWORD` in `.env`. If a port is already in use, change the
   matching `*_HOST_PORT` value as well.

   ```powershell
   Copy-Item .env.example .env
   ```

2. Start PostgreSQL, RabbitMQ, Redis, and Mailpit and wait until they are
   healthy:

   ```powershell
   docker compose up -d --wait
   ```

3. Docker Compose reads `.env`, but `dotnet run` does not. Pass the connection
   string to the API through an environment variable, using the same values as
   in `.env`:

   ```powershell
   $env:ConnectionStrings__HelpdeskDatabase = "Host=127.0.0.1;Port=55432;Database=helpdesk;Username=helpdesk;Password=<your password>"
   ```

4. Create the schema and seed the demo categories. Migrations are never
   applied automatically; run this after pulling new migrations:

   ```powershell
   dotnet tool restore
   dotnet ef database update --project backend/UniversityHelpdesk.Api
   ```

5. Store the JWT signing key, the demo account passwords, and the RabbitMQ
   password in .NET user secrets. They are saved in your Windows profile, not in
   the repository. The signing key must be at least 32 bytes; the API refuses to
   start without it. Choose your own values, except `RabbitMq:Password`, which
   must match `RABBITMQ_PASSWORD` in `.env`:

   ```powershell
   $project = "backend/UniversityHelpdesk.Api"
   dotnet user-secrets set "Jwt:SigningKey" "<at least 32 random characters>" --project $project
   dotnet user-secrets set "DemoAccounts:Student1Password" "<password>" --project $project
   dotnet user-secrets set "DemoAccounts:Student2Password" "<password>" --project $project
   dotnet user-secrets set "DemoAccounts:Technician1Password" "<password>" --project $project
   dotnet user-secrets set "RabbitMq:Password" "<RABBITMQ_PASSWORD from .env>" --project $project
   ```

   The Notification Worker has its own user secrets and needs the same RabbitMQ
   password:

   ```powershell
   dotnet user-secrets set "RabbitMq:Password" "<RABBITMQ_PASSWORD from .env>" --project backend/UniversityHelpdesk.NotificationWorker
   ```

   Run `dotnet user-secrets list --project $project` to see the values later.
   The other settings in both `appsettings.json` files match Compose. If you
   changed a host port in `.env`, override the matching setting with an
   environment variable before starting the API or the worker:
   `$env:RabbitMq__Port`, `$env:Redis__Configuration` (for example
   `127.0.0.1:56379`), or `$env:Smtp__Port`.

6. Create the demo accounts `student1@uni.test`, `student2@uni.test`
   (students), and `technician1@uni.test` (technician). The command only runs
   in Development. Existing accounts are left unchanged, so it is safe to run
   again:

   ```powershell
   dotnet run --project backend/UniversityHelpdesk.Api --launch-profile http -- seed-demo-users
   ```

7. Start the API:

   ```powershell
   dotnet run --project backend/UniversityHelpdesk.Api --launch-profile http
   ```

   Open `http://localhost:5276/swagger`, call `POST /auth/login` with a demo
   account, then select **Authorize** and paste the returned `accessToken`.

   When a technician resolves a ticket, the API saves a pending notification
   and a background publisher sends it to the `helpdesk.events` exchange in
   RabbitMQ. Before publishing, the API declares the durable
   `helpdesk.ticket-resolved` queue and binds it with routing key
   `TicketResolved`, so messages are kept even if the Notification Worker has
   not started yet. If RabbitMQ is not running, or returns a message as
   unroutable, the API keeps working and the event waits in the
   `PendingNotifications` table; it is retried with increasing delays and
   published once RabbitMQ accepts it. An event that fails 10 times is left
   unpublished for inspection; set its `Attempts` back to `0` to retry it.

   Delivery is at least once: an event is marked published only after RabbitMQ
   confirms it, so a crash between that confirmation and the database commit
   publishes the same event again on the next attempt.
   The RabbitMQ management UI is at `http://localhost:55673`.

   `GET /categories` is cached in Redis for 5 minutes. If Redis is not running,
   the API logs a warning and reads the categories from PostgreSQL instead.

8. In a second terminal, start the Notification Worker. It sends a
   confirmation email for each resolved ticket to Mailpit, which shows every
   message at `http://localhost:58025`:

   ```powershell
   dotnet run --project backend/UniversityHelpdesk.NotificationWorker
   ```

   The worker acknowledges a message only after the SMTP server accepts the
   email. Failed sends are retried 5 times with increasing delays; after that,
   the message is moved to the `helpdesk.ticket-resolved.failed` queue, where
   it stays for inspection in the RabbitMQ management UI. If the worker is
   stopped, messages wait in the queue and are delivered when it starts again.

   Emails are delivered at least once. If the worker crashes after the SMTP
   server accepts an email but before RabbitMQ receives the acknowledgement,
   the message is delivered again and the student gets a duplicate email.

## Monitoring

| Path            | Purpose                                                                                 |
| --------------- | --------------------------------------------------------------------------------------- |
| `/health/live`  | Liveness probe: the API process is serving requests. No dependency checks.              |
| `/health/ready` | Readiness probe: PostgreSQL is reachable. Returns `503` when it is not.                  |
| `/metrics`      | Prometheus metrics: request counts, `5xx` errors, and latency per route template, plus category cache hits and misses. |

The API and the worker write JSON logs to the console. Each API request is
logged once with its route template, status code, duration, and trace ID.
Notification logs include the event ID. Logs never contain credentials, tokens,
or ticket descriptions. See `docs/api-contract.md` for the metric names.

To stop the services and keep their data, run `docker compose stop`.

## Directory guide

| Directory   | Purpose                                         |
| ----------- | ----------------------------------------------- |
| `backend/`  | Source code for the API and the Notification Worker |
| `database/` | Database schema, migrations, and seed data      |
| `tests/`    | Automated tests                                 |
| `docs/`     | Project documentation and requirements          |
| `scripts/`  | Helper scripts for development and maintenance  |
