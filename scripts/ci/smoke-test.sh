#!/usr/bin/env bash
# End-to-end check of a deployed cluster: a student logs in and creates a ticket, a technician
# moves it to InProgress and Resolved, and the resolution email must reach the student in Mailpit.
# Tokens and passwords are never printed.
# Usage: KIND_CLUSTER=<name> scripts/ci/smoke-test.sh   (default cluster: university-helpdesk)
set -euo pipefail
cd "$(dirname "$0")/../.."

cluster=${KIND_CLUSTER:-university-helpdesk}
secrets_file=deploy/kubernetes/infrastructure/secrets.env
api_port=${API_PORT:-58090}
mailpit_port=${MAILPIT_PORT:-58125}
api=http://127.0.0.1:$api_port
mailpit=http://127.0.0.1:$mailpit_port

secret() { grep -E "^$1=" "$secrets_file" | head -n 1 | cut -d= -f2- | tr -d '\r'; }
fail() { echo "FAILED: $*" >&2; exit 1; }

forwards=()
stop_forwards() { for pid in "${forwards[@]}"; do kill "$pid" 2>/dev/null || true; done; }
trap stop_forwards EXIT
for target in "service/api $api_port:8080" "service/mailpit $mailpit_port:8025"; do
  # shellcheck disable=SC2086 # target holds two separate arguments
  kubectl --context "kind-$cluster" -n university-helpdesk port-forward $target > /dev/null 2>&1 &
  forwards+=("$!")
done

wait_for() {
  for _ in $(seq 1 30); do
    curl -fsS -o /dev/null "$1" 2> /dev/null && return 0
    sleep 2
  done
  fail "$1 did not respond"
}
wait_for "$api/health/ready"
wait_for "$mailpit/readyz"
echo "API and Mailpit are reachable."

# call METHOD PATH TOKEN [JSON_BODY]
call() {
  local args=(-sS --fail-with-body -X "$1" -H "Authorization: Bearer $3")
  if [[ $# -ge 4 ]]; then
    args+=(-H 'Content-Type: application/json' --data "$4")
  fi
  curl "${args[@]}" "$api$2"
}

login() {
  local token
  token=$(jq -n --arg email "$1" --arg password "$(secret "$2")" '{email: $email, password: $password}' |
    curl -sS --fail-with-body -H 'Content-Type: application/json' --data @- "$api/auth/login" |
    jq -r '.accessToken // empty') || fail "login as $1"
  [[ -n $token ]] || fail "login as $1 returned no token"
  printf '%s' "$token"
}

student=$(login student1@uni.test DEMO_STUDENT1_PASSWORD)
technician=$(login technician1@uni.test DEMO_TECHNICIAN1_PASSWORD)
echo "Logged in as student1@uni.test and technician1@uni.test."

category=$(call GET /categories "$student" | jq -r '.[0].id // empty')
[[ -n $category ]] || fail "no active category"

body=$(jq -n --argjson category "$category" \
  '{room: "CI-101", categoryId: $category, description: "Smoke test: the projector does not start."}')
ticket=$(call POST /tickets "$student" "$body") || fail "create ticket"
id=$(jq -r .id <<< "$ticket")
version=$(jq -r .version <<< "$ticket")
echo "Student created ticket #$id."

for status in InProgress Resolved; do
  body=$(jq -n --arg status "$status" --argjson version "$version" '{status: $status, version: $version}')
  ticket=$(call PATCH "/tickets/$id/status" "$technician" "$body") || fail "change ticket #$id to $status"
  [[ $(jq -r .status <<< "$ticket") == "$status" ]] || fail "ticket #$id is not $status"
  version=$(jq -r .version <<< "$ticket")
  echo "Technician moved ticket #$id to $status."
done

[[ $(call GET "/tickets/$id" "$student" | jq -r .status) == Resolved ]] || fail "student does not see ticket #$id as Resolved"

subject="Your helpdesk ticket #$id has been resolved"
for _ in $(seq 1 60); do
  recipient=$(curl -fsS "$mailpit/api/v1/messages" |
    jq -r --arg subject "$subject" '[.messages[] | select(.Subject == $subject)][0].To[0].Address // empty')
  if [[ -n $recipient ]]; then
    [[ $recipient == student1@uni.test ]] || fail "resolution email went to $recipient"
    echo "Resolution email for ticket #$id was delivered to $recipient."
    exit 0
  fi
  sleep 2
done
fail "no resolution email for ticket #$id within 2 minutes"
