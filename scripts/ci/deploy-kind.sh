#!/usr/bin/env bash
# Loads the locally built images into a kind cluster and deploys the three Kustomize layers in
# order: infrastructure, the db-init Job (waiting until it completes), then the apps.
# Usage: KIND_CLUSTER=<name> scripts/ci/deploy-kind.sh   (default cluster: university-helpdesk)
set -euo pipefail
cd "$(dirname "$0")/../.."

cluster=${KIND_CLUSTER:-university-helpdesk}
k() { kubectl --context "kind-$cluster" -n university-helpdesk "$@"; }

kind load docker-image university-helpdesk-api:local university-helpdesk-worker:local --name "$cluster"

kubectl --context "kind-$cluster" apply -k deploy/kubernetes/infrastructure
k rollout status statefulset/postgres --timeout=300s

# A Job cannot be changed once created, so the previous one is replaced.
k delete job db-init --ignore-not-found
kubectl --context "kind-$cluster" apply -k deploy/kubernetes/init

echo "Waiting for the db-init Job..."
for _ in $(seq 1 90); do
  if [[ $(k get job db-init -o jsonpath='{.status.succeeded}') == 1 ]]; then
    echo "db-init completed."
    break
  fi
  if [[ $(k get job db-init -o jsonpath='{.status.conditions[?(@.type=="Failed")].status}') == True ]]; then
    echo "db-init failed." >&2
    exit 1
  fi
  sleep 5
done
if [[ $(k get job db-init -o jsonpath='{.status.succeeded}') != 1 ]]; then
  echo "db-init did not complete within 7.5 minutes." >&2
  exit 1
fi

kubectl --context "kind-$cluster" apply -k deploy/kubernetes/apps
k rollout status deployment/api --timeout=300s
k rollout status deployment/notification-worker --timeout=300s
k get pods -o wide
