#!/usr/bin/env bash
# Saves the state, events, and logs of the university-helpdesk namespace for troubleshooting.
# Secrets are never read from the cluster, and every value from secrets.env is replaced with
# *** in the collected files as a second safeguard.
# Usage: KIND_CLUSTER=<name> scripts/ci/collect-diagnostics.sh <output-directory>
set -uo pipefail
cd "$(dirname "$0")/../.." || exit 1

cluster=${KIND_CLUSTER:-university-helpdesk}
out=${1:?Usage: collect-diagnostics.sh <output-directory>}
secrets_file=deploy/kubernetes/infrastructure/secrets.env
k() { kubectl --context "kind-$cluster" -n university-helpdesk "$@"; }

mkdir -p "$out/logs"
k get deployments,statefulsets,jobs,pods,services,endpoints,persistentvolumeclaims,configmaps -o wide > "$out/resources.txt" 2>&1
k get events --sort-by=.lastTimestamp > "$out/events.txt" 2>&1
k describe pods > "$out/pods.txt" 2>&1
k describe job db-init > "$out/db-init-job.txt" 2>&1

for pod in $(k get pods -o jsonpath='{.items[*].metadata.name}' 2> /dev/null); do
  for container in $(k get pod "$pod" -o jsonpath='{.spec.initContainers[*].name} {.spec.containers[*].name}'); do
    k logs "$pod" -c "$container" > "$out/logs/$pod.$container.log" 2>&1
    if k logs "$pod" -c "$container" --previous > "$out/logs/$pod.$container.previous.log" 2> /dev/null; then
      :
    else
      rm -f "$out/logs/$pod.$container.previous.log"
    fi
  done
done

if [[ -f $secrets_file ]]; then
  while IFS='=' read -r key value; do
    value=${value%$'\r'}
    [[ $key =~ ^[A-Z0-9_]+$ && ${#value} -ge 6 ]] || continue
    escaped=$(printf '%s' "$value" | sed 's/[][\/.^$*&]/\\&/g')
    find "$out" -type f -exec sed -i "s/$escaped/***/g" {} +
  done < "$secrets_file"
fi

echo "Diagnostics written to $out ($(find "$out" -type f | wc -l) files)."
