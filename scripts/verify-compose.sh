#!/usr/bin/env bash
# Checks a fresh Docker Compose start of this copy of the project, as the README describes it:
# creates .env from .env.example with random values, starts the system under its own project
# name (so its containers and volumes are new and separate from any other copy), checks the
# database initialization, checks that the gateway serves the React frontend and forwards API
# requests, and runs the core flow: login, an access check, ticket creation and resolution, and the
# resolution email in Mailpit. Removes the containers and volumes at the end.
# Passwords and tokens are never printed.
#
# Usage: scripts/verify-compose.sh
#   COMPOSE_PROJECT=helpdesk-verify  project name for the containers and volumes
#   PORT_PREFIX=4                    host ports become 4xxxx (45432, 48080, ...), apart from the defaults
#   USE_CACHED_IMAGES=1              start with existing university-helpdesk-*:local images instead of building
#   KEEP_RUNNING=1                   leave the system running and keep .env
# Requires Docker with Compose v2, curl, jq, and openssl.
set -euo pipefail
cd "$(dirname "$0")/.."

project=${COMPOSE_PROJECT:-helpdesk-verify}
prefix=${PORT_PREFIX:-4}
compose=(docker compose --project-name "$project")
fail() { echo "FAILED: $*" >&2; exit 1; }

[[ -e .env ]] && fail ".env already exists; this check needs a fresh configuration."
if [[ -n $("${compose[@]}" ps -a -q 2> /dev/null) ]] || docker volume ls -q | grep -q "^${project}_"; then
  fail "Compose project '$project' already has containers or volumes; choose another COMPOSE_PROJECT."
fi

# 1. Fresh configuration, as in README step 1: every change-me becomes a random value.
umask 077
while IFS= read -r line || [[ -n $line ]]; do
  line=${line%$'\r'}
  if [[ $line =~ ^([A-Z0-9_]+)=change-me$ ]]; then
    key=${BASH_REMATCH[1]}
    length=24
    [[ $key == JWT_SIGNING_KEY ]] && length=48
    printf '%s=%s\n' "$key" "$(openssl rand -base64 64 | tr -dc 'A-Za-z0-9' | cut -c "1-$length")"
  elif [[ $line =~ ^([A-Z0-9_]+_HOST_PORT)=5([0-9]{4})$ ]]; then
    printf '%s=%s%s\n' "${BASH_REMATCH[1]}" "$prefix" "${BASH_REMATCH[2]}"
  elif [[ $line =~ ^[A-Z0-9_]+= ]]; then
    printf '%s\n' "$line"
  fi
done < .env.example > .env
setting() { grep -E "^$1=" .env | head -n 1 | cut -d= -f2-; }
echo "Created .env with new random values; host ports use the prefix $prefix."

cleanup() {
  status=$?
  if [[ ${KEEP_RUNNING:-} == 1 ]]; then
    echo "Left project '$project' running (KEEP_RUNNING=1)."
  else
    "${compose[@]}" down --volumes --remove-orphans > /dev/null 2>&1 || true
    rm -f .env
    echo "Removed project '$project', its volumes, and .env."
  fi
  exit "$status"
}
trap cleanup EXIT

# 2. Start, as in README step 2.
start=$(date +%s)
if [[ ${USE_CACHED_IMAGES:-} == 1 ]]; then
  echo "Starting with the cached images (no build)."
  "${compose[@]}" up -d --no-build --wait > /dev/null || fail "docker compose up --no-build"
else
  echo "Building the images and starting."
  "${compose[@]}" up -d --build --wait > /dev/null || fail "docker compose up --build"
fi
echo "All services healthy after $(( $(date +%s) - start )) s:"
"${compose[@]}" ps -a --format '  {{.Service}}: {{.State}} {{.Status}}' | sort

# 3. Database initialization.
[[ $("${compose[@]}" ps -a --format '{{.Service}} {{.ExitCode}}' db-init) == "db-init 0" ]] || fail "db-init did not exit with 0"
sql() {
  "${compose[@]}" exec -T postgres sh -c 'psql -U "$POSTGRES_USER" -d "${1:-$POSTGRES_DB}" -tA -c "$0"' "$1" "${2:-}"
}
migrations=$(sql 'SELECT count(*) FROM "__EFMigrationsHistory"')
categories=$(sql 'SELECT count(*) FROM "Categories" WHERE "IsActive"')
accounts=$(sql "SELECT count(*) FROM \"Users\" WHERE \"Email\" IN ('student1@uni.test', 'student2@uni.test', 'technician1@uni.test')")
test_database=$(sql "SELECT count(*) FROM pg_database WHERE datname = 'helpdesk_test'" postgres)
echo "Database: $migrations migrations applied, $categories active categories, $accounts of 3 demo accounts, helpdesk_test database: $test_database."
[[ $migrations -ge 1 && $categories -ge 1 && $accounts == 3 && $test_database == 1 ]] || fail "database initialization is incomplete"

api=http://127.0.0.1:$(setting API_HOST_PORT)
mailpit=http://127.0.0.1:$(setting MAILPIT_UI_HOST_PORT)
body_file=$(mktemp)

# 4. The gateway serves the frontend and forwards API requests.
# get PATH: prints the HTTP status code and content type; the body goes to $body_file.
get() { curl -sS -o "$body_file" -w '%{http_code} %{content_type}' "$api$1"; }
for path in / /login /student/tickets/1 '/technician/tickets?status=Open'; do
  result=$(get "$path")
  [[ $result == "200 text/html"* ]] && grep -q '<div id="root">' "$body_file" || fail "GET $path: expected the app page, got $result"
done
get / > /dev/null
asset=$(grep -o '/assets/[^"]*\.js' "$body_file" | head -n 1)
[[ -n $asset && $(get "$asset") == "200 application/javascript"* ]] || fail "GET the app script $asset"
[[ $(get /api/health/live) == "200 application/json"* ]] && grep -q '"Healthy"' "$body_file" || fail "GET /api/health/live"
[[ $(get /health/ready) == "200 application/json"* ]] || fail "GET /health/ready"
[[ $(get /swagger/index.html) == "200 text/html"* ]] || fail "GET /swagger/index.html"
[[ $(get /swagger/v1/swagger.json) == "200 application/json"* ]] || fail "GET /swagger/v1/swagger.json"
[[ $(get /metrics) == 404* && $(get /api/metrics) == 404* ]] || fail "/metrics must not be reachable through the gateway"
echo "Gateway: the app page for /, /login, and direct links to student and technician pages; the app script;"
echo "  /api/health/live, /health/ready, and Swagger forwarded to the API; /metrics and /api/metrics blocked."

# 5. Core flow through the gateway.

login() {
  local token
  token=$(jq -n --arg email "$1" --arg password "$(setting "$2")" '{email: $email, password: $password}' |
    curl -sS --fail-with-body -H 'Content-Type: application/json' --data @- "$api/auth/login" |
    jq -r '.accessToken // empty') || fail "login as $1"
  [[ -n $token ]] || fail "login as $1 returned no token"
  printf '%s' "$token"
}
# status METHOD PATH TOKEN [JSON_BODY]: prints the HTTP status code; the body goes to $body_file.
status() {
  local args=(-sS -o "$body_file" -w '%{http_code}' -X "$1" -H "Authorization: Bearer $3")
  [[ $# -ge 4 ]] && args+=(-H 'Content-Type: application/json' --data "$4")
  curl "${args[@]}" "$api$2"
}
expect() { [[ $1 == "$2" ]] || fail "$3: expected $2, got $1"; }

student=$(login student1@uni.test DEMO_STUDENT1_PASSWORD)
technician=$(login technician1@uni.test DEMO_TECHNICIAN1_PASSWORD)
echo "Login: student1@uni.test and technician1@uni.test signed in."

expect "$(status GET /tickets "")" 401 "GET /tickets without a token"
expect "$(status GET /categories "$student")" 200 "GET /categories"
category=$(jq -r '.[0].id' "$body_file")

expect "$(status POST /tickets "$student" "{\"room\":\"V-101\",\"categoryId\":$category,\"description\":\"Package check: the projector does not start.\"}")" 201 "create ticket"
id=$(jq -r .id "$body_file")
echo "Ticket: student created ticket #$id (201)."

expect "$(status PATCH "/tickets/$id/status" "$student" '{"status":"Resolved","version":1}')" 403 "student changes the status"
echo "Access check: a student changing the status gets 403; a request without a token gets 401."

version=1
for target in InProgress Resolved; do
  expect "$(status PATCH "/tickets/$id/status" "$technician" "{\"status\":\"$target\",\"version\":$version}")" 200 "move to $target"
  version=$(jq -r .version "$body_file")
done
expect "$(status GET "/api/tickets/$id" "$student")" 200 "student reads the ticket through /api, as the frontend does"
[[ $(jq -r .status "$body_file") == Resolved ]] || fail "the student does not see ticket #$id as Resolved"
resolved=$(date +%s)
echo "Resolution: technician moved #$id to InProgress and Resolved; the student sees Resolved through /api."

subject="Your helpdesk ticket #$id has been resolved"
for _ in $(seq 1 60); do
  recipient=$(curl -fsS "$mailpit/api/v1/messages" |
    jq -r --arg subject "$subject" '[.messages[] | select(.Subject == $subject)][0].To[0].Address // empty')
  if [[ -n $recipient ]]; then
    [[ $recipient == student1@uni.test ]] || fail "the email went to $recipient"
    echo "Email: delivered to $recipient in Mailpit within $(( $(date +%s) - resolved )) s."
    echo "Compose check passed."
    exit 0
  fi
  sleep 1
done
fail "no resolution email for ticket #$id within 60 s"
