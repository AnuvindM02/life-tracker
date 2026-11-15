# Project Self Tracker

A local-first tracker for the 365-day **Project: Self v2.3** roadmap: daily logs with evidence-backed ticks and Minimum Viable Days, the phase/week plan, schedule blocks, a gym log, files, and (in later milestones) DSA, DDIA, reviews, the flagship build and the interview pipeline.

Single user, runs on your laptop, no cloud, no telemetry. Spec: [`docs/SPEC.md`](docs/SPEC.md) (v1.1). Decisions: [`docs/DECISIONS.md`](docs/DECISIONS.md). Architecture: [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md).

> **Upload notice.** Do not upload employer code, data, credentials or confidential architecture. The roadmap forbids moving company material into personal repositories and tools.

## Status

| Milestone | State |
|---|---|
| M0 Scaffold | Done |
| M1 Foundation and files | Done |
| M2 Daily log and schedule | Done |
| M3 DSA (library, attempts with four-pass timers, spaced review queue, progress, weakest list, CSV import, timed sessions) | Done |
| M4 DDIA and design (edition check and banner, both chapter lists, reading log, Template C notes with interview Q&A and 90-second explanation, 24 design drills with a 45-minute segment timer, articulation drills) | Done |
| M5 Plan tasks (list, board, derived status), exit criteria, checkpoints, re-plan log, weekly and monthly reviews with prefill, weekly hours, someday list, full nudge rules | Done |
| M6 Flagship · M7 Market/interviews · M8 Dashboard/search/backup | Not started |

This repository is local-only: it is not pushed to GitHub or any remote. `.github/workflows/ci.yml` documents the checks but never runs; use the test commands below.

## Prerequisites

- Docker Desktop (or Docker Engine) running
- .NET SDK 10.0.300+ (`dotnet --list-sdks`)
- Node.js 24 + npm

## Run everything in one go (Docker)

With Docker Desktop running and `.env` in place:

```bash
docker compose up -d --build
```

Open http://localhost:5080. This starts the database and the app (API plus the built web UI) as two containers, applies database updates and seeds new plan data. Stop with `docker compose stop`; start again later with `docker compose up -d` (add `--build` after pulling or changing code). Files, backups and logs go to the same `data/` and `logs/` folders as dev mode. Keep the setup page's file folder as the default `data/files` so both modes find your files. Dev mode and the container both use port 5080, so stop one before starting the other (`docker compose stop app`).

## Quick start (dev)

```bash
cp .env.example .env            # then set a strong MSSQL_SA_PASSWORD
npm --prefix web ci             # first time only
```

```bash
docker compose up -d db
```

```bash
dotnet run --project src/SelfTracker.Api -- --migrate
```

```bash
npm --prefix web run dev
```

Open http://localhost:5173. The first visit shows a short setup page (plan start date, DDIA edition, travel anchor Friday, file folder); then you land on **Today**. API docs: http://localhost:5080/scalar (Development only).

Or run everything with one script: `scripts/run-dev.ps1` (PowerShell) or `scripts/run-dev.sh` (bash).

## Local "prod" (one process)

```bash
scripts/run-local.sh
```

PowerShell: `scripts/run-local.ps1`. Builds the SPA into the API's `wwwroot` and serves everything on http://localhost:5080. It runs migrations because it passes `--migrate`.

## Environment variables

| Variable | Default | Purpose |
|---|---|---|
| `MSSQL_SA_PASSWORD` | (required) | SQL Server SA password; used by docker-compose and the API. 8+ chars with upper, lower, digit, symbol. |
| `DB_PORT` | `1433` | Host port for SQL Server (published on 127.0.0.1 only). |
| `DB_NAME` | `SelfTracker` | Database name. |
| `API_PORT` | `5080` | API port (localhost only). |
| `ConnectionStrings__Default` | (unset) | Overrides the connection built from the values above, e.g. to use another SQL Server instance. |
| `SELFTRACKER_DATA` | `<repo>/data` | Where files and backups live. |

`.env` is read by both docker-compose and the API, is git-ignored, and must never be committed.

## Backup and restore

- **App backup (recommended):** Settings & data → *Back up now*, or `scripts/backup.sh` / `scripts/backup.ps1` while the API runs. Writes `data/backups/selftracker-backup-*.zip` (every table as JSON plus all files with SHA-256 hashes). *Check a backup (dry run)* verifies a zip. Full restore into an empty database arrives with M8.
- **Native SQL backup:** `scripts/backup.sh --native` / `scripts/backup.ps1 -Native` writes `data/sqlbackups/*.bak` (database only, not files). Restore with `scripts/restore.sh <file.bak>` / `scripts/restore.ps1 <file.bak>` (stop the API first; it replaces the database and asks you to confirm).
- **Verify files:** Settings & data → *Verify files* reports missing files, files changed on disk and orphans.

## Tests

```bash
dotnet test --solution SelfTracker.slnx
```

Domain unit tests (business rules BR-01 to BR-10 and file rules) and API integration tests (Testcontainers starts a throwaway SQL Server, so Docker must be running).

```bash
npm --prefix web test
```

Vitest + React Testing Library (components, BR-14 wording scan). Lint and type-check: `npm --prefix web run lint` and `npx --prefix web tsc -b`.

After changing the API contract, regenerate the typed client with the API running: `npm --prefix web run gen:api`.

## Troubleshooting

- **SQL Server container not healthy:** `docker compose logs db`. The most common cause is a weak `MSSQL_SA_PASSWORD`; change it in `.env`, then `docker compose down -v` (this deletes the database volume) and start again. The API retries the connection with backoff while SQL Server boots.
- **Port already in use:** change `DB_PORT` or `API_PORT` in `.env` (Vite expects the API on 5080; adjust `web/vite.config.ts` if you change it).
- **Upload fails with 413:** the file is larger than *Max upload size* in Settings (default 500 MB).
- **Blocked file type:** `.exe .bat .cmd .ps1 .sh .dll .msi .js .vbs .scr .com` are rejected unless allowed in Settings.
- **403 "Missing X-SelfTracker header":** the API only accepts state-changing requests from the app's own client (see DECISIONS D-008). Use the UI or send the header from scripts.
- **Tests fail to start:** Docker must be running for the integration tests.
