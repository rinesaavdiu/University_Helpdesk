#!/usr/bin/env bash
# Writes deploy/kubernetes/infrastructure/secrets.env with random values for every change-me
# entry in secrets.env.example. Values are never printed; on GitHub Actions each one is
# registered as a mask first, so it is hidden even if a later step echoes it.
# Usage: scripts/ci/new-kind-secrets.sh
set -euo pipefail
cd "$(dirname "$0")/../.."

example=deploy/kubernetes/infrastructure/secrets.env.example
output=deploy/kubernetes/infrastructure/secrets.env

if [[ -e $output ]]; then
  echo "$output already exists; remove it first to generate new values." >&2
  exit 1
fi

umask 077
: > "$output"
while IFS= read -r line || [[ -n $line ]]; do
  line=${line%$'\r'}
  if [[ $line =~ ^([A-Z0-9_]+)=change-me$ ]]; then
    key=${BASH_REMATCH[1]}
    length=24
    [[ $key == JWT_SIGNING_KEY ]] && length=48
    # Letters and digits only: the values are inserted into connection strings.
    value=$(openssl rand -base64 64 | tr -dc 'A-Za-z0-9' | cut -c "1-$length")
    if [[ ${GITHUB_ACTIONS:-} == true ]]; then
      echo "::add-mask::$value"
    fi
    printf '%s=%s\n' "$key" "$value" >> "$output"
  elif [[ $line =~ ^[A-Z0-9_]+= ]]; then
    printf '%s\n' "$line" >> "$output"
  fi
done < "$example"

echo "Wrote $output with $(grep -c '=' "$output") settings."
