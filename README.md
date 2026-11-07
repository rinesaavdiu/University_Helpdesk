# University Helpdesk API

**Sistem për raportimin e problemeve teknike në universitet**

University Helpdesk API is a planned backend service for reporting and tracking
technical problems in university classrooms. Students will be able to report
issues with classroom technology, such as projectors, computers, or network
access, and technicians will be able to track those reports and mark them as
resolved.

The project is in its early stages. Installation and test instructions will be
added once they can be verified.

## Local database (Windows PowerShell)

Requires Docker with Docker Compose and the .NET 10 SDK. Run these commands from
the repository root.

1. Copy the example settings and change `POSTGRES_PASSWORD` in `.env`. If port
   `55432` is already in use, change `POSTGRES_HOST_PORT` as well.

   ```powershell
   Copy-Item .env.example .env
   ```

2. Start PostgreSQL and wait until it is healthy:

   ```powershell
   docker compose up -d --wait postgres
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

5. Start the API:

   ```powershell
   dotnet run --project backend/UniversityHelpdesk.Api --launch-profile http
   ```

To stop PostgreSQL and keep its data, run `docker compose stop postgres`.

## Directory guide

| Directory   | Purpose                                         |
| ----------- | ----------------------------------------------- |
| `backend/`  | Source code for the API                         |
| `database/` | Database schema, migrations, and seed data      |
| `tests/`    | Automated tests                                 |
| `docs/`     | Project documentation and requirements          |
| `scripts/`  | Helper scripts for development and maintenance  |
