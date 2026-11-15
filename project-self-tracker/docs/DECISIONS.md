# Decisions

ADR-style log of choices the spec left open or that deviate from it. Newest last. Spec = `docs/SPEC.md` (v1.1).

## D-001 Versions (verified 15 Nov 2026, pinned)
- .NET SDK 10.0.300 (`global.json`, rollForward latestFeature); .NET 10 LTS. Microsoft.* packages pinned to **10.0.15**: nuget.org already lists 11.0.0 (EF Core 11 / .NET 11), which is not LTS and needs a newer SDK, so the 10.0.x line is used deliberately.
- Serilog.AspNetCore 10.0.0, Serilog.Sinks.File 7.0.0, Scalar.AspNetCore 2.21.0, Testcontainers.MsSql 4.16.0, Respawn 7.0.0 (referenced for later use), xunit.v3 4.0.3, Microsoft.Testing.Extensions.CodeCoverage 18.12.0, dotnet-ef 10.0.15 (local tool).
- SQL Server image `mcr.microsoft.com/mssql/server:2025-CU11-ubuntu-24.04` (newest 2025 CU at build time), Developer edition.
- Node 24; React 19.3.1, react-router 8.5.1, Vite 8.3.8, TanStack Query 5.107.1, react-hook-form 7.92.0 + zod 4.6.7, Tailwind 4.3.5, vitest 5.1.0.
- **TypeScript 5.9.3, not 7.0.2**: TS 7 (native compiler) is out, but typescript-eslint supports `<6.1` and openapi-typescript peers on `^5`. Revisit when they support TS 7.
- NuGet: the machine had only the offline VS feed configured, so the repo carries a `nuget.config` pointing at nuget.org.

## D-002 Tests run on Microsoft.Testing.Platform
.NET 10's `dotnet test` no longer drives VSTest for xunit.v3 projects. `global.json` opts into MTP; run tests with `dotnet test --solution SelfTracker.slnx` (or `--project`). Coverage via Microsoft.Testing.Extensions.CodeCoverage (`-- --coverage`).

## D-003 Validation: built-in .NET 10 minimal-API validation
DataAnnotations on request records, `builder.Services.AddValidation()`. Domain rules that matter (tick evidence, date ranges, pause overlaps) are re-checked in handlers and return 422 ProblemDetails with field errors. Plain validation failures return 400.

## D-004 Application talks to EF through IAppDbContext
No repository wrapper (spec 2). `SelfTracker.Application` references `Microsoft.EntityFrameworkCore.Relational` so handlers can use `DbSet<T>`, `AsNoTracking`, projections and split queries directly; Infrastructure owns the concrete `AppDbContext`, configuration and migrations.

## D-005 Handlers: IHandler<TIn,TOut> plus a generic CRUD service
Every feature operation is an `IHandler<TIn,TOut>` registered by assembly scan (no MediatR). Simple editable lists (pause ranges, travel overrides, schedule blocks, gym plan) share `CrudService<TEntity,TDto,TInput>` + an `ICrudSpec` describing projection, filters, validation and apply. It keeps those lists consistent (paging, sort, 201 + Location, rowVersion, soft delete) without dozens of near-identical handlers.

## D-006 Concurrency token in the body
Updates send `rowVersion` (base64 of the SQL `rowversion`) in the body. A stale value returns 409 "This record changed elsewhere. Reload it, or overwrite with your version." If-Match support was not added because the generic CRUD inputs already carry the token; add it if a client needs it.

## D-007 Data folder and connection string
- `data/` (files, backups, sqlbackups) and `logs/` live at the repository root, found by walking up to `SelfTracker.slnx`; `SELFTRACKER_DATA` overrides it.
- The API reads the same `.env` that docker-compose uses (only keys not already set as real environment variables). `ConnectionStrings__Default` wins if set; otherwise the connection string is built from `MSSQL_SA_PASSWORD`, `DB_PORT`, `DB_NAME`. Secrets never live in appsettings.

## D-008 Local-request guard instead of antiforgery (spec v1.1, 6.2)
No cookies and no auth, so antiforgery tokens add nothing. `LocalRequestGuardMiddleware` runs before every endpoint:
1. Host must be `localhost`, `127.0.0.1` or `[::1]` (all methods): blocks DNS rebinding.
2. Unsafe methods under `/api`: an `Origin` header, if present, must be a loopback origin.
3. Unsafe methods must carry `X-SelfTracker: 1`. A cross-site HTML form cannot send custom headers, and `fetch` with one triggers a CORS preflight that only the Vite dev origin passes.

The multipart upload and import endpoints call `DisableAntiforgery()` explicitly; the guard covers them (integration tests: cross-origin upload 403, wrong Host 400). Kestrel listens on `localhost` only. File content responses add `nosniff`, a restrictive CSP (sandboxed for text), and HTML/SVG/unknown types are always served as attachments.

## D-009 Upload target in the query string
`POST /api/v1/files?entityType=&entityId=&label=` with the files as multipart sections. Query parameters let the server validate the target before reading any file bytes, and the streaming `MultipartReader` never buffers a section. The per-file limit (`MaxUploadMb`) is enforced while streaming; the request limit is raised for this endpoint only.

## D-010 Daily log details
- `DayType = null` is "In progress". `POST /daily-logs/by-date/{date}/ensure` creates it; adding time or ticking a ritual on Today does that automatically. Past In progress logs show as "Unclassified" (neither Active nor Missed).
- Core/Build ticks are rows; the DSA tick is computed (always false until M3 adds attempts). `PUT .../ticks/Dsa` returns 400.
- Tick + evidence file links are written in one transaction inside the SQL retry execution strategy (`EnableRetryOnFailure` forbids bare user transactions).
- Deleting a log soft-deletes its ticks and time entries with the same timestamp; restoring the log restores exactly those.
- MVD indicator (BR-07) in M2 uses time entries: DSA minutes for the DSA part, DDIA/design minutes + the core note for the core part, Main track minutes or a Build tick for the build part. Cold reviews feed it from M3.

## D-011 Nudges shipped early
M2 ships the calm "one missed day", "two missed days", "DDIA edition unconfirmed in week 2+" and "schedule phase starts (weeks 4 and 9)" nudges because they help from the first week. The rest of BR-13 lands with M5. A Vitest test already scans UI sources for BR-14 forbidden wording; the OpenAPI scan arrives with M5.

## D-012 Native .bak backups are copied out of the container (deviation from spec v1.1)
Spec v1.1 asked to bind-mount `./data/sqlbackups` to `/var/opt/mssql/backups`. On Docker Desktop for Windows SQL Server cannot write a backup to a bind mount (`OS error 31 ... DiskChangeFileSize`). The compose file keeps only the named volume; `scripts/backup --native` writes the `.bak` inside the volume, `docker cp`s it to `data/sqlbackups/` and deletes the in-container copy. `scripts/restore` does the reverse. The app's own `.zip` backup (tables + files) remains the primary backup.

## D-013 Seeding
- Seed files live in `seed/*.json` (`scripts/gen-seed.py` documents how they were generated from the spec appendices) and are copied next to the API binary.
- Lists seeded so far: phases, plan-tasks (Phase 1 split per Appendix C.1, flagship blocks, Phase 3 workstreams, Phase 4 workstreams, Canary window, Day 1 checklist), checkpoints, schedule-blocks, gym-plan. Later milestones add their lists (DDIA chapters, drills, problems, exit criteria, hour targets, rules).
- Phase A gym days are seeded as Mon/Wed/Fri with Tue/Thu walks: the roadmap only says "3 sessions/week"; they are marked as suggestions and editable.
- Phase C stable and stretch versions of a block are separate rows (`IsStretch`).
- Any user edit of a seeded row sets `IsUserModified`; soft-deleting does not. "Restore defaults" re-applies seed values to unmodified rows or to explicitly chosen keys (and un-deletes them).

## D-014 Plan weeks and tasks
Plan weeks are phase-relative (BR-02, v1.1). Plan tasks store `DayFrom/DayTo` (the source of truth) and an optional `PlanWeek` for derived DSA/DDIA weekly tasks. Task editing, the board, exit criteria and BR-18 derived status ship with M5; until then seeded tasks are read-only in the UI.

## D-015 Milestone commits
M0, M1 and M2 were built and verified together (the spec's kickoff asks to stop after M2), so they land as one commit with all three milestones' acceptance checks green. From M3 on, each milestone gets its own commit as the spec asks.

## D-016 M3 DSA details
- Seeded topics are the 17 from spec 4.4; seeded problems are exactly the 65 Appendix C names, written as the roadmap writes them (for example "Koko", "Rotated Array"). Difficulty and URL are left empty rather than invented; edit them or import a CSV. W11 "Generate Parentheses review" is a review of the W3 problem, so it is not a second row. W10 names "Tries" as a topic but no problem.
- Review ladders are keyed by their source attempt (`DsaReviewItem.SourceAttemptId`). One active ladder per problem: a New/Timed attempt supersedes pending items; a cold review completes the linked item (or the earliest pending one), restarts the ladder at +1 day when rated 1-3 and keeps the remaining items when rated 4-5 (BR-09, v1.1).
- The problem and attempt type cannot change after logging (they own the review dates); delete and log again instead. Editing confidence or date regenerates the attempt's pending items if its ladder is still active.
- Deleting an attempt supersedes its pending reviews, reopens a review it completed, and soft-deletes its derived time entry; restoring it from the Trash restores the time entry but not the superseded reviews (log a new attempt to schedule again).
- Purging a problem keeps its attempts as free-text history (the title is copied) and removes its review items. A topic with problems cannot be deleted; defer it instead.
- Problem status: Solved once solved unaided (never downgraded), otherwise Attempted after any attempt; Deferred is manual.
- DSA tick (BR-06): a counted attempt or a completed review on the date. The MVD "DSA" part also accepts any cold review.
- Progress weeks are Monday-Sunday calendar weeks (same as BR-12); the cumulative chart is plotted against plan day so the Day 90/220 bands line up after pauses.
- Charts use Recharts 3.10.1; every chart has a table toggle and the roadmap caveat as its caption.
- The DSA half of BR-18 (weekly DSA tasks completing from problem progress) stays in M5 with the rest of plan-task status.

## D-017 Local-only repository
The user keeps this repository on their machine only (15 Nov 2026): no remote, no push, no PRs. Milestones are committed locally. The GitHub Actions workflow is kept as documentation of the checks but never runs.

## D-018 M4 DDIA details
- Both editions are seeded (`seed/ddia-chapters-2e.json`, `seed/ddia-chapters-1e.json`); the app shows the confirmed edition, or the 2nd edition labelled "unconfirmed" until you confirm. Confirming never rewrites anything: it only changes which list is shown and how DDIA plan tasks resolve.
- DDIA weekly plan tasks stay edition-neutral in the database; `DdiaTaskResolver` adds `ddiaChapters` (for example "Ch. 1: Reliable, Scalable, and Maintainable Applications") when tasks are read, using `PlannedWeekFrom/To` of the shown edition.
- 1st-edition chapters copy the Ask-Ops connection of the 2nd-edition chapter(s) on the same topic; chapter 12 notes that it includes "Doing the Right Thing".
- Chapter progress follows the reading log: pages read is the sum, started on is the first reading date, Reading on the first entry, finished on is the date the running total reached the chapter's page count (when you set one).
- A note is Complete when it has the full Template C: five key ideas, trade-offs, failure modes, one Ask-Ops decision and three interview questions with answers. A complete note moves the chapter to Note done; a 90-second explanation rating on top moves it to Explained. Status never moves backwards automatically; the chapter page lets you set it by hand.
- A drill can be marked Written only when all six parts of the 45-minute framework have notes. Reviewed stamps the review date. Drill and articulation minutes become derived DDIA/design time entries (BR-19), as do reading-log minutes.
- `DerivedTime` is now the shared BR-19 helper. Trash restore and purge handle derived time entries for any source type by name.
- Core ticks can link a DDIA note, design drill or articulation log (`GET /ddia/artifacts?date=`).
- The edition banner is global (above every page) from plan Week 2 while unconfirmed, with buttons to confirm; the Today nudge stays as well.

## D-019 M5 plan, reviews and nudges
- Task status is computed when tasks are read (`PlanTaskEnricher`): weekly DSA tasks from their problems, weekly DDIA tasks from the shown edition's chapters (BR-18). A manual Deferred/Skipped is stored as `StatusOverride` and wins; setting any other status on a derived task clears the override. Manual tasks store their status; Done stamps `CompletedOn`. Because the effective status is computed, the task list filters and pages in memory (a few hundred tasks at most).
- "Behind" (BR-13) uses that effective status: the oldest open task of the current phase whose last day is more than 14 plan days ago; Phase 1 uses 21 days for re-plan trigger 2. The Day 1 checklist tasks count, so tick them off.
- Checkpoint due dates use BR-03 (pauses move them); the Canary window shows its end date too. Context lines: Day 30 active days, Day 60 edition, Day 90 open Phase 1 exit criteria and tasks; Day 180 and Day 340 point at M7.
- Exit criteria are seeded from Appendix G; the Day 270 readiness checklist sits in Phase 3 under the group "Day 270 readiness" (spec 4.10).
- Weekly reviews are keyed by Monday (BR-12 weeks); the computed snapshot is stored as JSON at save time so a review keeps what was true that week. Build ticks stand in for flagship build sessions until M6.
- Weekly hour targets (Appendix J): weeks 1-3 compare only the total (4-8 h); weeks 4-8 compare the 8-11 h total and show the Phase 1 category ranges as guidance; from week 9 the phase's category ranges plus 12-15 h stable, 15-17 h ambitious and 19-20 h stretch only.
- Nudges are computed on every Today load; "Not today" dismissals are stored per date in the browser. Monthly review nudges appear in the last 3 days of a month and the first 5 days of the next. The posting-count and written-offer nudges ship with M7 (they need postings and applications).
- Someday "promote" creates a manual plan task (track Other) in the current phase for the next 7 plan days.
- The BR-14 contract scan runs against `/openapi/v1.json` in the integration tests (word-bounded, so "inlineAllowed" is fine).

## D-020 One-command run with Docker Compose
`docker compose up -d --build` runs the database and an `app` container built from the repo `Dockerfile` (Vite build, `dotnet publish`, ASP.NET runtime image; tags pinned). Inside the container Kestrel listens on all interfaces (`SELFTRACKER_LISTEN_ANY=true`) because Docker's port mapping cannot reach a loopback-only listener; the port is published on 127.0.0.1 only and the local-request guard still requires a loopback Host header, so the app stays local. The container reaches SQL Server by service name (`DB_HOST=db`), runs migrations at start (`--migrate`), and mounts `./data` and `./logs`, so dev mode and the container share files and backups as long as the file folder stays relative (`data/files`).
