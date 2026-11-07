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

5. Store the JWT signing key and the demo account passwords in .NET user
   secrets. They are saved in your Windows profile, not in the repository. The
   signing key must be at least 32 bytes; the API refuses to start without it.
   Choose your own values:

   ```powershell
   $project = "backend/UniversityHelpdesk.Api"
   dotnet user-secrets set "Jwt:SigningKey" "<at least 32 random characters>" --project $project
   dotnet user-secrets set "DemoAccounts:Student1Password" "<password>" --project $project
   dotnet user-secrets set "DemoAccounts:Student2Password" "<password>" --project $project
   dotnet user-secrets set "DemoAccounts:Technician1Password" "<password>" --project $project
   ```

   Run `dotnet user-secrets list --project $project` to see the values later.

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

To stop PostgreSQL and keep its data, run `docker compose stop postgres`.

## Directory guide

| Directory   | Purpose                                         |
| ----------- | ----------------------------------------------- |
| `backend/`  | Source code for the API                         |
| `database/` | Database schema, migrations, and seed data      |
| `tests/`    | Automated tests                                 |
| `docs/`     | Project documentation and requirements          |
| `scripts/`  | Helper scripts for development and maintenance  |
