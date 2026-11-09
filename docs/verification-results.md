# Verification results

This document records the checks that were actually run against the University
Helpdesk API, what each one was expected to show, and what it showed. The
requirements are in [`requirements.md`](requirements.md); the full benchmark
tables are in [`benchmark-results.md`](benchmark-results.md), and the individual
test cases with their inputs and results are in [`test-cases.md`](test-cases.md).

All local checks ran on one laptop: Intel Core i7-7700HQ (4 cores, 8 threads),
15.8 GB RAM, Windows 10 22H2 (build 19045), with Docker Engine 29.9.3, Docker
Compose v5.7.0, kind v0.33.0 (Kubernetes v1.37.0), and kubectl v1.37.2 inside
WSL 2 (Ubuntu 22.04). Clients on Windows used .NET SDK 10 (runtime 10.0.14).

## Summary

| Area | Expected | Actual | Date |
| ---- | -------- | ------ | ---- |
| Ordinary tests (no external services) | All pass; service-backed tests skip | 100 passed, 22 skipped, 0 failed | 9 Nov 2026 |
| HTTP integration tests (a dedicated `helpdesk_test` database) | All pass | 24 of 24 passed, against Compose PostgreSQL and again against a local PostgreSQL 18 instance | 8 and 9 Nov 2026 |
| Recovery checks (Compose services) | All pass | 5 of 5 passed | 8 Nov 2026 |
| Postman collection (Newman, through the Compose gateway) | All requests and assertions pass | 27 of 27 passed | 8 Nov 2026 |
| Compose deployment | Healthy services, email delivered, data persists | Met; see below | 8 Nov 2026 |
| Kubernetes deployment (local kind cluster) | Ordered start-up, end-to-end flow, data persists | Met; see below | 8 Nov 2026 |
| Local HTTPS (`--launch-profile https`) | HTTPS served, HTTP redirected | Met with an untrusted development certificate; see TC-34 | 9 Nov 2026 |
| GitHub Actions | Both jobs pass on push to `main` | 5 of 5 runs succeeded | 9 Nov 2026 |
| Clean, extracted submission package starts and passes its checks (NFR-M3) | Health checks pass from the extracted copy | Not yet run | Open |
| NFR-P1: `GET /tickets` p95 under 300 ms | Under 300 ms | 76.5 to 133.0 ms over 7 runs | 8 and 9 Nov 2026 |
| Category cache speedup | Warm reads faster than cold reads | Not demonstrated for this workload | 8 Nov 2026 |

## Automated tests

**Ordinary suite.** `dotnet test UniversityHelpdesk.slnx` with no
`HELPDESK_TEST_*` variables set, on commit `da59588`:

| Project | Passed | Skipped | Failed |
| ------- | -----: | ------: | -----: |
| `UniversityHelpdesk.Api.Tests` | 86 | 17 | 0 |
| `UniversityHelpdesk.NotificationWorker.Tests` | 14 | 0 | 0 |
| `UniversityHelpdesk.RecoveryTests` | 0 | 5 | 0 |

The skipped tests are the ones that need PostgreSQL or the other services. The
build has 0 warnings. This suite includes the JWT checks for missing, expired,
and tampered tokens (NFR-S1) and the `500` response forced through a test-only
route, which contains no exception details (NFR-S4).

**Integration tests.** With `HELPDESK_TEST_DATABASE` pointing at the
`helpdesk_test` database that the Compose init script creates, the 24 tests
selected by `--filter "FullyQualifiedName~Integration"` passed on 8 November.
On 9 November, the whole suite ran again on commit `4804f47` against a
`helpdesk_test` database on a local PostgreSQL 18 instance: 110 of 110 API
tests passed, including the same 24. Each case is listed in
[`test-cases.md`](test-cases.md). They cover login
against stored hashes (NFR-S3), role restrictions and another student's ticket
(NFR-S2), the ticket lifecycle and status workflow, and concurrent edits and
resolutions where only one request succeeds and the other receives `409`
(NFR-R3).

**Recovery checks.** With the `HELPDESK_TEST_*` variables pointing at the
Compose PostgreSQL, RabbitMQ, Mailpit, and Redis, all five passed:

| Check | Requirement | Expected and actual result |
| ----- | ----------- | -------------------------- |
| Stopped worker | FR-7 | The event waits in the queue and is emailed once the worker starts |
| Temporary SMTP failure | FR-7 | The send is retried and the email arrives when SMTP returns |
| SMTP down for every attempt | FR-7 | The event moves to the failed queue |
| Broker outage | NFR-R2 | The event waits in the outbox; the publisher and the worker recover and the email arrives |
| Redis outage | NFR-R1, NFR-P2 | Categories are served from PostgreSQL during the outage; the second request after recovery is a cache hit |

**Postman collection.** `npx newman run` against `http://localhost:58080`
passed all 27 requests and assertions, including the full status workflow for
ticket #1.

## Deployment checks

**Docker Compose** (`docker compose up -d`, 8 November 2026):

- The images built, `db-init` exited successfully, and both API replicas, the
  gateway, and the worker became healthy. The init script created
  `helpdesk_test`.
- Through the gateway, `/metrics` returns `404`.
- After the Newman run, both API replicas had served requests through the
  gateway: 21 and 32 non-health requests, counted from each replica's own
  metrics.
- The resolution email reached the Mailpit inbox.
- Ticket #1 was still `Resolved` after a full `docker compose down` and `up`
  (volumes kept).

**Kubernetes** (local kind cluster `university-helpdesk`, 8 November 2026):

- The `db-init` Job applied the 3 migrations and created the demo accounts
  before the API started. Both API pods and the worker became ready with 0
  restarts.
- Login, ticket creation, and resolution worked through a port-forward, and the
  resolution email reached Mailpit within about a second.
- 60 requests sent from inside the cluster to the `api` Service were split 21
  and 39 between the two API pods.
- Ticket #1 survived replacing both API pods (`kubectl rollout restart`) and
  re-creating `postgres-0`, and read back correctly through the new pods.

**CI rehearsal.** Before the workflow was committed, its Kubernetes job ran end
to end in WSL against a fresh copy of the repository without local secrets and
a separate temporary cluster. It passed in 279 seconds. The 13 diagnostic files
contained 0 of the generated credentials, and a password planted in them was
replaced with `***`.

## GitHub Actions

Every run of [`.github/workflows/ci.yml`](../.github/workflows/ci.yml) so far
was triggered by a push to `main`, ran on `ubuntu-24.04`, and succeeded on the
first attempt. Times are UTC on 9 November 2026.

| Run | Tested commit | Duration | Build and test | Kubernetes smoke test |
| --- | ------------- | -------- | -------------- | --------------------- |
| [42689715455](https://github.com/rinesaavdiu/University_Helpdesk/actions/runs/42689715455) | `f4875fb8c0c5d86f50756936cc0b34eb739d8061` | 20:06:18 to 20:09:32 | Passed in 55 s | Passed in 2 min 7 s |
| [42690701136](https://github.com/rinesaavdiu/University_Helpdesk/actions/runs/42690701136) | `da59588be70216e07f3f2a0f2457c178f9932081` | 20:12:22 to 20:15:10 | Passed in 32 s | Passed in 2 min 10 s |
| [42694031970](https://github.com/rinesaavdiu/University_Helpdesk/actions/runs/42694031970) | `4bf38b0c67b753f0c0313a888a136e223d301831` | 20:33:07 to 20:35:44 | Passed | Passed |
| [42696574964](https://github.com/rinesaavdiu/University_Helpdesk/actions/runs/42696574964) | `4804f4750f8934fe9b4e9f92c8d1cc681b6f2db5` | 20:49:12 to 20:52:11 | Passed | Passed |
| [42697737387](https://github.com/rinesaavdiu/University_Helpdesk/actions/runs/42697737387) | `7c0b1f8b29265040e805a480ab1e05bd1cc1c908` | 20:56:36 to 20:59:21 | Passed | Passed |

In the second run, the Kubernetes job built the images in 29 s, created the
kind cluster in 40 s, deployed in 46 s (including the `db-init` Job and the
rollouts), and ran the smoke test in 5 s. Each run uploaded a `test-results`
and a `kubernetes-diagnostics` artifact.

The job logs and artifact contents require signing in to GitHub and were not
read for this record, so the hosted test counts are not quoted here. The test
command is the same as the ordinary suite above.

## Performance

**Workload.** The benchmark in `benchmarks/UniversityHelpdesk.Benchmark` ran
against the Compose stack with 20 concurrent users: 21 simultaneous logins,
1,000 `POST /tickets`, 1,000 `GET /tickets` as a technician over all 1,001
tickets, 1,000 `GET /tickets` as students over their own tickets, 50 cold
`GET /categories` one at a time with the Redis key deleted before each, and
1,000 warm `GET /categories`. The client ran on Windows; the details are in
[`benchmark-results.md`](benchmark-results.md).

**NFR-P1 is met.** Expected: p95 of `GET /tickets` under 300 ms. Actual: 76.5,
117.1, and 99.9 ms in the three runs on 8 November, and 98.2, 109.2, 83.0, and
133.0 ms in the four runs on 9 November, with 0 failures in every run.

**No cache speedup was demonstrated.** The API's cache counters show that the
cache works as designed: each run added exactly 50 misses (the cold reads) and
1,012 hits (the warm reads plus 12 setup requests). But the timings do not show
that a cache hit is faster:

| `GET /categories` on 8 Nov | Concurrency | Median (ms) | p95 (ms) |
| -------------------------- | ----------: | ----------: | -------: |
| Cold (cache miss, PostgreSQL) | 1 | 8.8 to 10.1 | 14.8 to 20.1 |
| Warm (cache hit, Redis) | 20 | 16.6 to 24.2 | 33.2 to 48.7 |

Warm reads were slower, not faster. The two scenarios are not comparable: the
cold reads ran one at a time, while the warm reads competed with 19 other users
for the same 4 cores. The cold path is also cheap, a query for 6 categories, so
most of each request's time is HTTP handling, the gateway, and WSL forwarding,
which is the same with or without the cache. To show a difference, cold and
warm reads would have to run at the same concurrency on a larger or slower
query.

**Improvement: connect to `127.0.0.1` instead of `localhost`.** On 8 November,
21 simultaneous logins had a median of 2,705 to 2,800 ms. The first explanation
recorded was password-hashing cost. The measurements below contradict it:

| Measurement (9 Nov) | Result |
| ------------------- | ------ |
| 21 simultaneous logins sent with curl inside WSL, three runs | Median 607, 639, 821 ms; p95 757, 778, 951 ms |
| First request from a new .NET 10 process to `http://localhost:58080/health/live` | 2,223 and 2,254 ms |
| The same to `http://127.0.0.1:58080/health/live` | 138 and 86 ms |
| 21 simultaneous new connections to `localhost`, `/health/live` | Median 2,056 and 2,069 ms |
| 21 simultaneous new connections to `127.0.0.1`, `/health/live` | Median 28 and 48 ms |

On this host, `localhost` resolves to `::1` first and then `127.0.0.1`. Compose
publishes the gateway only on `127.0.0.1`, so every new connection to
`localhost` spent about 2 s before it reached the gateway. Logins are the first
scenario, so they open the benchmark's 21 connections; later scenarios reuse
those pooled connections, which is why only the logins were affected.

Running the same benchmark with `--base-url http://127.0.0.1:58080`, alternated
with the default `localhost`:

| Run (9 Nov) | Base URL | Login median (ms) | Login p95 (ms) | Logins/s | Failures |
| ----------- | -------- | ----------------: | -------------: | -------: | -------: |
| 1 | `http://localhost:58080` | 2,540.4 | 2,734.4 | 8 | 0 |
| 2 | `http://127.0.0.1:58080` | 645.4 | 765.9 | 27 | 0 |
| 3 | `http://localhost:58080` | 2,658.5 | 2,844.6 | 7 | 0 |
| 4 | `http://127.0.0.1:58080` | 545.3 | 670.0 | 30 | 0 |

Using `127.0.0.1` cut the login median by 75 to 80%, in line with the
measurement inside WSL. The other scenarios stayed within the normal
run-to-run variation. The slow logins came from the client's connection setup,
not from password hashing in the API. The README's benchmark command now passes
`--base-url http://127.0.0.1:58080`.

## Limitations

- **Integration and recovery tests are not run in CI.** GitHub Actions runs
  only the ordinary suite and the Kubernetes smoke test; the 24 integration
  tests and 5 recovery checks were run locally (against Compose on 8 November,
  and the integration tests again against a local PostgreSQL on 9 November).
- **One machine.** The benchmark, the API, and every dependency shared one
  4-core laptop, and results vary by about 30% between runs. They show that
  NFR-P1 is met here, not how the system scales.
- **Kubernetes was not benchmarked.** `kubectl port-forward` sends every
  request to one pod, so it would not represent the Service. The kind cluster
  has a single node.
- **The 9 November benchmark runs skipped the cold category scenario.** After
  WSL restarted, Windows could not reach the Redis port 56379 for over a minute
  while the other ports worked, so those runs were started without `--redis`.
- **Not production configuration.** Both deployments run with
  `ASPNETCORE_ENVIRONMENT=Development`, over plain HTTP, and with demo
  accounts. HTTPS was checked only with `dotnet run` and an untrusted
  development certificate; TLS for a published deployment is designed but not
  implemented or tested (README, "HTTPS"). No Prometheus server is deployed;
  metrics were read directly from each pod.
- **Known log line.** The API image logs a harmless `libgssapi_krb5.so.2`
  loading error from Npgsql on Alpine.
- **Not automated.** Duplicate emails after a worker crash between sending and
  acknowledging are possible by design (at-least-once delivery) but are not
  covered by a test, and neither is SQL injection-style input (all queries are
  parameterized by EF Core).
- **Clean package not yet verified.** NFR-M3 was checked on this machine, not
  from a clean, extracted copy of the submission package. That check stays
  open until the packaging step.
- **Frontend.** A web frontend is planned and was not tested; all checks used
  the API directly.

## Follow-up findings

These were found during verification and are not fixed yet:

| Finding | Evidence | Suggested fix |
| ------- | -------- | ------------- |
| Compose infrastructure does not restart on its own | When the WSL VM stopped and started again on 9 November, the API, gateway, and worker restarted (`restart: unless-stopped`), but PostgreSQL, RabbitMQ, Redis, and Mailpit stayed exited, so the API and worker reported unhealthy until `docker compose up -d` was run again | Add `restart: unless-stopped` to those four services in `compose.yaml` |
| The benchmark's default address is slow on Windows | `--base-url` defaults to `http://localhost:58080`; each new connection to `localhost` took about 2 s longer than to `127.0.0.1` (see Performance) | Change the default in `BenchmarkOptions.cs` to `http://127.0.0.1:58080`; the README command already passes it |
