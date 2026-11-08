# Benchmark results

Results of `benchmarks/UniversityHelpdesk.Benchmark` for requirement NFR-P1:
`GET /tickets` has a p95 latency under 300 ms with 20 concurrent users and
1,000 seeded tickets on the local Docker Compose setup. See the README for how
to run the benchmark.

## Environment

| Item       | Value |
| ---------- | ----- |
| Date       | 8 November 2026 |
| Deployment | Docker Compose v5.7.0 on Docker Engine 29.9.3 inside WSL 2 (Ubuntu 22.04); the nginx gateway, two API replicas, the worker, PostgreSQL 18.6, RabbitMQ 4.1, Redis 8.2, and Mailpit, as defined in `compose.yaml` |
| Host       | Intel Core i7-7700HQ (4 cores, 8 threads), 15.8 GB RAM, Windows 10 22H2 (build 19045) |
| Client     | The benchmark ran on the same Windows host (.NET 10.0.14) and reached the gateway at `http://localhost:58080` through WSL localhost forwarding |
| Dataset    | 1,000 tickets created by the 20 benchmark users during each run, plus 1 existing ticket; 1,001 tickets visible to the technician |

## Workload

All scenarios use 20 concurrent users, each sending its requests one after
another, except the cold category reads, which run one at a time.

| Scenario | Requests |
| -------- | -------- |
| `POST /auth/login` | 21 (20 students and 1 technician, all at once) |
| `POST /tickets` | 1,000 (50 per student) |
| `GET /tickets` as the technician | 1,000 (50 per user), pages 1 to 5 of 20 tickets over all tickets |
| `GET /tickets` as a student | 1,000 (50 per student), pages 1 to 3 of the student's own 50 tickets |
| `GET /categories`, cold | 50; the Redis key is deleted before each request, outside the timing |
| `GET /categories`, warm | 1,000 (50 per user), after one request has filled the cache |

Each request is timed from sending until the whole response body has been read.
The percentiles use the nearest-rank method.

## Results

Three consecutive runs; all latencies in milliseconds.

| Scenario | Run | Requests | Failures | Median | p95 | Max | Requests/s |
| -------- | --: | -------: | -------: | -----: | --: | --: | ---------: |
| `GET /tickets` (technician, all tickets) | 1 | 1,000 | 0 | 42.8 | 99.9 | 228.8 | 388 |
|                                          | 2 | 1,000 | 0 | 51.5 | 117.1 | 259.2 | 331 |
|                                          | 3 | 1,000 | 0 | 38.2 | 76.5 | 143.3 | 433 |
| `GET /tickets` (student, own tickets)    | 1 | 1,000 | 0 | 65.9 | 150.2 | 268.5 | 258 |
|                                          | 2 | 1,000 | 0 | 48.9 | 103.5 | 234.7 | 356 |
|                                          | 3 | 1,000 | 0 | 58.6 | 114.4 | 177.5 | 310 |
| `POST /tickets`                          | 1 | 1,000 | 0 | 64.8 | 118.8 | 181.7 | 272 |
|                                          | 2 | 1,000 | 0 | 51.4 | 102.4 | 217.9 | 347 |
|                                          | 3 | 1,000 | 0 | 34.6 | 60.6 | 106.0 | 529 |
| `GET /categories` (cold)                 | 1 | 50 | 0 | 10.1 | 20.1 | 50.4 | 85 |
|                                          | 2 | 50 | 0 | 8.8 | 14.8 | 32.2 | 101 |
|                                          | 3 | 50 | 0 | 9.8 | 16.2 | 26.9 | 93 |
| `GET /categories` (warm)                 | 1 | 1,000 | 0 | 17.9 | 45.4 | 104.9 | 901 |
|                                          | 2 | 1,000 | 0 | 16.6 | 33.2 | 58.6 | 1,076 |
|                                          | 3 | 1,000 | 0 | 24.2 | 48.7 | 74.5 | 735 |
| `POST /auth/login`                       | 1 | 21 | 0 | 2,705.0 | 2,841.4 | 2,848.9 | 7 |
|                                          | 2 | 21 | 0 | 2,776.6 | 2,897.2 | 2,897.3 | 7 |
|                                          | 3 | 21 | 0 | 2,799.8 | 2,920.8 | 2,928.1 | 7 |

The API's `helpdesk_category_cache_lookups_total` counters, summed over both
replicas, rose by exactly 50 misses and 1,012 hits per run: every cold request
was a cache miss, and every warm request (plus 12 setup requests) a hit. Every
run removed its 20 students, its technician, and its 1,000 tickets; no
`@benchmark.test` account remained afterwards.

## Assessment

- **NFR-P1 is met.** The p95 latency of `GET /tickets` over all tickets was
  76.5 to 117.1 ms, well under the 300 ms target, with no failures. A student's
  own list stayed under 151 ms.
- Cold category reads, which query PostgreSQL and refill Redis, took about
  10 ms. Warm reads under 20 concurrent users took about 17 to 24 ms at the
  median, so most of that time is queueing for the CPU rather than the lookup.
- Logging in is slow when many users log in at once: about 2.8 s when 21 users
  log in together. Password verification is deliberately expensive and runs on
  the 4-core host shared by every container, so 21 simultaneous logins queue.
  This is outside NFR-P1. Logins are rare compared with other requests, because
  a token lasts until it expires.
- The numbers vary by about 30% between runs, because the API, the database,
  and the benchmark share one laptop. Compare results only from the same
  machine.

The Kubernetes deployment was checked for function only, not benchmarked: a
`kubectl port-forward` sends every request to one pod, so its numbers would not
represent the Service.
