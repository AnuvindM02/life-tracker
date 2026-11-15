# Project Self Tracker - Build Specification v1.1

> Converted from docs/Project_Self_Tracker_Build_Spec_v1.1.docx (the .docx is authoritative).

**Project Self Tracker**

Build specification for Claude Code: a local-first tracker for the 365-day Project: Self v2.3 roadmap

| **Version / date** | 1.1 / 15 November 2026 (review fixes; see "Changes in v1.1") |
|---|---|
| **Owner** | Anuvind (single user, runs on his own laptop) |
| **Stack** | .NET 10 (ASP.NET Core Minimal APIs, EF Core) · React 19 + TypeScript · Microsoft SQL Server · local file storage |
| **Source roadmap** | Project: Self v2.3 (Schedule-Integrated), updated 31 Oct 2026. All plan content needed by the app is reproduced in Appendices A to O. |

> **Instructions to Claude Code (read first)**
> Build the complete app described here, milestone by milestone (Section 13). Keep the app runnable after every milestone and commit after each one.
> **MUST** means required. **SHOULD** means default unless you document a better choice. Where this document is silent, choose the latest stable best-practice option and record it in `docs/DECISIONS.md`. Do not stop to ask questions unless you are blocked by something only the user can provide.
> Verify current stable versions of the .NET SDK, Node, SQL Server image and every package at install time (`dotnet package search`, `npm view`) and pin them. Do not rely on remembered version numbers.
> All plan data (phases, weekly plan, DDIA chapters, drills, checkpoints, schedule blocks, rules) is in Appendices A to O. Implement it as JSON seed files so the original PDF is not needed at runtime.
> This file is a .docx. If you cannot read it directly, convert it first: `pandoc spec.docx -t gfm -o docs/SPEC.md`, then work from the Markdown.
> The user does not want to spend time on front-end design. Make UI decisions yourself: clean, functional, dark-mode-first, built from shadcn/ui components. Backend correctness, data integrity and file handling take priority over visual polish.
> Create a `CLAUDE.md` at the repo root (commands, conventions, architecture summary, the rules in Section 1.2) in milestone M0 and keep it current.

## Changes in v1.1

v1.1 fixes gaps found when the spec was checked against the roadmap PDF. Seed content is unchanged in meaning; only how the app derives and stores it changed.

- **Week numbering (BR-02):** plan weeks are phase-relative so they match the roadmap labels (Day 91 = W14, Day 301 = W44); day ranges are the source of truth.
- **Edition-neutral DDIA tasks (4.5, Appendix C, D.2):** plan tasks never embed 2nd-edition chapter numbers; a 1st-edition week map was added.
- **Track split (Appendix C.1):** mixed DDIA/React/MEAI cells are split into separate tasks; Output/fallback is stored on tasks, not seeded as a track.
- **Derived task status (BR-18):** DSA and DDIA weekly tasks complete from problem and chapter progress, so nothing is logged twice.
- **Derived time entries and hour targets (BR-12, BR-19, Appendix J):** source records create their own time entries; categories map to phase targets; weeks 1-8 use the graduated J.1 totals.
- **Spaced review (BR-08, BR-09):** reviews for every attempt on a library problem, one active ladder per problem, unlinked cold re-dos count.
- **Banned-wording scan (BR-14):** quoted roadmap rules are allowlisted.
- **Local-request guard (6.2):** Host/Origin checks and a required custom header on state-changing requests; SQL Server published on 127.0.0.1 only.
- **Smaller items:** In-progress daily logs, MVD tick clarification (BR-07), Day 1 checklist (Appendix O), design-drill weeks, read-only FileRoot, mapped SQL backup folder.

## 1. Product Overview

### 1.1 Purpose

A single-user web app, running locally on the user's laptop, that operates the whole 365-day plan: log every day (full session, Minimum Viable Day or rest), track DSA, DDIA/system design, the React/MEAI/MAF track, the Ask-Ops flagship build, reviews, checkpoints, market checks and the interview pipeline, and store the files that go with all of it (notes, diagrams, mock-interview recordings, resumes, reports, design documents). Everything must be creatable, editable and deletable. It replaces the spreadsheet/HTML tracker.

### 1.2 Design Principles (taken from the roadmap)

- **Tracker is the truth.** Full sessions and MVDs are recorded separately. A tick must be backed by evidence (a logged attempt, a note, a commit reference). The app never lets the user claim work that has no artifact.
- **Never miss twice, never repay hours.** After a missed day the app suggests an MVD and a calm recovery. It must NEVER show "hours owed", "catch-up" totals or guilt messaging. A delayed week shifts the sequence; it does not restart the plan.
- **Output over input.** Every block ends with a note, solved problem, commit, test, diagram or ADR. Forms are built around the output, not around time spent.
- **Honest metrics.** Show counts, targets and caveats as the roadmap states them (targets are ranges, not quotas). No fake precision.
- **Cut scope, not consistency.** Optional items can be marked Deferred or Skipped without breaking the plan.
- **Edit everything.** Every record can be created, edited and soft-deleted (with undo). Seeded plan data can be edited too and restored to defaults per list.
- **Local and private.** No cloud, no telemetry, no external calls at runtime. The upload screen and README must remind the user that employer code, data and credentials must never be stored here.

### 1.3 Scope

|  | **Items** |
|---|---|
| **In scope** | Plan and settings, Today view, daily log with ticks and MVD checks, schedule blocks and gym log, DSA library, attempt log, spaced-review queue and timers, DDIA reading log and chapter notes, system-design drills, articulation drills, plan tasks, exit criteria and checkpoints, weekly and monthly reviews, rule-based nudges, someday list, re-plan log, flagship build hours, ADRs, evaluation and load-test runs, agent-defect log, market check (job postings), interview pipeline (targets, applications, referrals, mocks, STAR stories, resume versions, offers), dashboards, global search, file storage, export/import and backups. |
| **Out of scope** | Multi-user, login/auth (design so it can be added later), cloud hosting, mobile app, push or email notifications, AI/LLM features, calendar or GitHub integrations (store links only), payments, a rich UI design system. |

### 1.4 Environment

- One user on a Windows, macOS or Linux laptop. Docker Desktop (or Docker Engine) is assumed for SQL Server; the connection string must still be configurable so any SQL Server instance works.
- Timezone default is Asia/Kolkata (configurable). Calendar days are `DateOnly` values; timestamps are UTC `DateTimeOffset`.

## 2. Tech Stack

| **Layer** | **Choice** | **Notes** |
|---|---|---|
| Runtime / API | .NET 10 LTS, C# 14, ASP.NET Core Minimal APIs with endpoint groups per feature | Nullable enabled, warnings as errors, central package management (`Directory.Packages.props`), `Directory.Build.props`, `.editorconfig`. |
| Data access | EF Core 10 (SQL Server provider), code-first migrations | No repository wrapper over DbContext. Use `AsNoTracking` for reads, projections to DTOs, explicit indexes, global soft-delete filter, `rowversion` concurrency tokens. |
| Database | Microsoft SQL Server (current GA Linux container image) via Docker Compose | Volume-backed, healthcheck, SA password from `.env` (never committed). Full-Text Search is NOT available in the stock image, so do not depend on it. |
| Validation | Built-in .NET 10 minimal-API validation (DataAnnotations) or FluentValidation. Pick one, document it. | Validate at the API boundary and again in domain rules that matter (tick evidence, date ranges, 1-5 ratings). |
| Application style | Clean Architecture (Domain, Application, Infrastructure, Api). Plain command/query handler classes registered via DI. | Do NOT add MediatR (its licensing changed in 2025). A tiny `IHandler<TIn,TOut>` abstraction is enough. |
| Logging / errors | Serilog (console + rolling file under `logs/`), correlation ID per request, RFC 9457 ProblemDetails, global exception handler | Structured logs; never log file contents or note bodies. |
| API docs | Built-in OpenAPI (`Microsoft.AspNetCore.OpenApi`) plus Scalar UI in Development | The React client types are generated from this OpenAPI document. |
| Backend tests | xUnit, Testcontainers for SQL Server (integration), NSubstitute or Moq only where a fake is truly needed | Prefer real DB integration tests over mocking EF Core. Respawn or equivalent to reset between tests. |
| Frontend | React 19, TypeScript (strict), Vite, React Router, TanStack Query, React Hook Form + Zod, Tailwind CSS + shadcn/ui, Recharts, react-markdown, TanStack Table | Typed API client generated from OpenAPI (openapi-typescript + openapi-fetch, or Orval). ESLint + Prettier. |
| Frontend tests | Vitest + React Testing Library; Playwright for 3 end-to-end smoke flows | Flows: log a day; log a DSA attempt and see review dates; upload and download a file. |
| Packaging | Dev: Vite dev server proxying to the API. Local "prod": API serves the built SPA from `wwwroot`, one process on `http://localhost:5080`. | Scripts for both PowerShell and bash: `run-dev`, `run-local`, `backup`, `restore`. |
| Security posture | Bind to localhost only; CORS only for the Vite dev origin; no auth in v1; local-request guard on every request (Section 6.2) | Keep endpoint structure auth-ready. Secrets via user-secrets or env vars. Upload hardening in Section 6. |

## 3. Architecture

### 3.1 Repository Layout

> `project-self-tracker/`
> `  CLAUDE.md  README.md  docker-compose.yml  .env.example`
> `  docs/            SPEC.md  DECISIONS.md  ARCHITECTURE.md  (original roadmap PDF optional)`
> `  seed/            phases.json  plan-tasks.json  ddia-chapters-2e.json  ddia-chapters-1e.json`
> `                   checkpoints.json  design-drills.json  dsa-problems.json  schedule-blocks.json`
> `                   exit-criteria.json  weekly-hour-targets.json  rules.json  templates.json`
> `  src/`
> `    SelfTracker.Domain/          entities, enums, domain services (pure, no EF)`
> `    SelfTracker.Application/     feature handlers, DTOs, interfaces (IClock, IFileStorage ...)`
> `    SelfTracker.Infrastructure/  DbContext, configurations, migrations, LocalFileStorage, seeder, backup`
> `    SelfTracker.Api/             endpoint groups, middleware, OpenAPI, static SPA hosting`
> `  tests/`
> `    SelfTracker.Domain.Tests/    business-rule unit tests (Section 9)`
> `    SelfTracker.Api.Tests/       Testcontainers integration tests`
> `  web/                           React app (Vite)`
> `  scripts/                       run-dev, run-local, backup, restore (.ps1 and .sh)`

### 3.2 Conventions

- Dependencies point inward: Api and Infrastructure depend on Application; Application depends on Domain; Domain depends on nothing.
- Keep calculations (plan day, phase, spaced review, streaks, nudges, metrics) in Domain as pure, unit-testable services that take an `IClock`/dates as input.
- Primary keys are `int` identity, except `FileAsset` which also carries a `Guid StorageKey`. Every editable entity has `CreatedAt`, `UpdatedAt` (UTC), `IsDeleted` and a `rowversion`. Edits with a stale version return HTTP 409 with ProblemDetails.
- Soft delete everywhere (global query filter). Provide an "undo" within the UI session and a Trash page that can restore or permanently purge.
- Seeded rows carry a unique `SeedKey` and an `IsUserModified` flag. The seeder is idempotent: it inserts missing rows only and never overwrites user-modified rows (Section 10).
- Tags and skills are normalized (`Tag` table) and reused across DSA attempts and job postings.
- Attachments use one polymorphic link table (`FileLink`: FileAssetId, EntityType, EntityId, Label). The app layer enforces valid targets and removes links when an entity is deleted or purged.
- Apply EF migrations automatically on startup in Development and with an explicit `--migrate` flag in local-prod mode. Migrations are committed.
- Every list endpoint supports paging, sorting and the filters named in Section 7. Never return EF entities directly; use DTOs.

## 4. Functional Requirements

Every module below MUST support create, view, edit and soft-delete of its records unless stated otherwise. Attachments (Section 6) can be added to any record marked "Attach = Yes" in Section 5.

### 4.1 Plan and Settings

- **Settings** (single row, editable): `PlanStartDate` (Day 1 is the day the user starts the tracker), `TimeZone`, `DdiaEdition` (Unconfirmed / First / Second) and `DdiaEditionConfirmedOn`, `DsaUnaidedMinMinutes` (default 25), spaced-review intervals (default 1, 3, 7, 21 days), `HighConfidenceReviewDays` (default 30), `TravelAnchorFriday` (a Friday on which an Edappal trip starts; trips then repeat every 14 days), `TravelEnabled`, `FileRoot`, `MaxUploadMb` (default 500), `TrashRetentionDays` (default 30), `NightlyBackupEnabled`. FileRoot is chosen at first-run setup and is read-only in the UI afterwards; moving it is a documented manual step (stop the app, move the folder, start with --file-root) because stored paths are relative to it.
- **Pause ranges**: the user can add a date range (reason: Illness, Travel, Work spike, Family event, Other) during which plan days do not advance. This implements "a delayed week shifts the sequence; it does not require a restart" (BR-01). Logging is still allowed on paused dates.
- **Re-plan log**: record each deliberate re-plan with date, trigger (1: repeated skill mismatch in postings or interviews; 2: Phase 1 more than three weeks behind; 3: job, health, finances or family change; 4: package/framework blocker over two focused sessions; or Checkpoint), evidence text and decision. Show the roadmap rule "Do not revise the plan again unless checkpoint evidence tells you to" next to the form.
- **Phases** (seeded, editable): four phases with day ranges, objective and exit evidence (Appendix A). Phase and plan week are always derived from the plan day. Plan weeks are phase-relative (BR-02); store and compare plan content by day range, never by week number alone.

### 4.2 Today View (home page)

- Header: today's date, **Day N of 365**, plan week, phase name, schedule phase (A, B or C), and a badge when the date is inside a pause range or a travel window.
- **Today's targets** from the plan: the week's DSA problem list (with a one-click "start attempt"), the DDIA chapter and output, the current build/track target, and next unfinished plan tasks.
- **Schedule blocks** for today based on the schedule phase (Appendix I), with the gym session planned for the weekday. Blocks can be ticked off but ticking is optional.
- **Review queue**: DSA cold reviews due today or overdue (Section 4.4), the weekly review if it is Sunday or overdue, checkpoint reminders within 3 days, and monthly review/market-check reminders.
- **Nudges** (Section 4.12): calm, rule-based messages. Never red alarms, streak shaming or debt totals.
- **Quick actions**: Log full day, Log MVD, Log rest day, Add DSA attempt, Add someday idea, Upload file. A global shortcut (Ctrl/Cmd+K) opens a command palette for these and for navigation.
- **Five-minute start ritual** and **close-day** checklists (Appendix L): open tracker, read week target, write one DSA target and one build target, phone away, start timer; close day = tracker, tomorrow target, prep. Both are simple checkboxes stored on the daily log.

### 4.3 Daily Log, Ticks and MVD

- One daily log per calendar date (unique, non-deleted). Backfilling past dates is allowed; the record gets `LoggedLate = true` automatically when created after its date.
- **Day type**: Full, MVD, Rest, or unset (In progress). A log is auto-created as In progress the first time anything is recorded for a date (time entry, tick, ritual checkbox). An In progress log for a past date shows as "Unclassified" and asks the user to choose a type; it counts as neither Active nor Missed until then. Flag IsTravelDay (auto-suggested from the travel anchor, user can override). Active day = Full or MVD (BR-05).
- **Fields** (Template A): Energy 1-5, SleepHours, WakeTime, LightsOutTime, DSA line, Core block line, Build line, Tomorrow DSA target, Tomorrow next-smallest-build target, free notes (Markdown).
- **Three tracker ticks**: DSA reps, System design / core skill, Build / ship. Each tick requires evidence per Appendix L (BR-06). The DSA tick is **derived** from logged DSA attempts/reviews for that date and cannot be set by hand. Core and Build ticks require an evidence note and may link a DDIA note, ADR, flagship session, commit URL/hash, PR URL or an attached file. The UI shows the "counts / does not count" criteria beside each tick.
- **MVD checklist**: shows progress against the MVD definition (DSA attempt or cold review 20-30 min; 15-20 min DDIA/core with a three-line note; 10-15 min build or one tiny test/commit). On a travel day the MVD is satisfied by a DSA review plus a short written note (BR-07).
- **Time entries**: minutes per category (DSA, DDIA / design, Main track, Review, Mocks, Applications, Other). Most entries are created automatically so time is never typed twice (BR-19); manual entries cover everything else (React or MEAI/MAF study, reviews, applications). Used for weekly hours (BR-12).
- **Gym**: planned session (from the schedule), status Done / Modified / Skipped / Rest / Walk / Zone 2, and a free-text recovery/symptoms note. The app records and reports only; it must not prescribe exercise or give medical advice.
- **Daily log list** with filters (month, type, tick state), a month calendar and a 365-day heatmap (Section 4.13). Day states: Full, MVD, Rest, Missed, Paused, Future (use shapes/labels as well as color).

### 4.4 DSA Module

- **Topics and problem library**: seeded topics (Arrays & Hashing, Two Pointers, Stack, Binary Search, Sliding Window, Linked List, Trees, Tries, Heap / Priority Queue, Backtracking, plus empty Graphs, 1-D DP, 2-D DP, Greedy, Intervals, Math & Geometry, Bit Manipulation) and the problems from Appendix C with plan week, order, optional URL and difficulty. User can add, edit, reorder, defer or delete problems and topics. A problem has status Not started / Attempted / Solved / Deferred.
- **Attempt log** (Template B plus the four-pass method). Fields: date, problem (or free-text title), type (New / Cold review / Timed session), Pass 1 minutes (unaided attempt), hint level (None, Topic, Pattern, Solution), Pass 2 minutes (unstick), Pass 3 minutes (rewrite in C#), total minutes, solved unaided (bool), pattern, key insight, what I missed, mistake tags, time and space complexity, confidence 1-5, optional C# code (Markdown code block) and attachments, optional company tag (Phase 4).
- **Meaningful attempt rule (BR-08)**: counts toward the cumulative marker only if Pass 1 minutes >= `DsaUnaidedMinMinutes` and "what I missed" (the mistake-log line) is filled, or the entry is a scheduled cold review. Other entries save fine but are labelled "not counted" with the reason.
- **Four-pass timers**: a stopwatch/countdown component with the guide durations (Attempt 25-30, Unstick 10-15, Rewrite 10, Log 2 minutes). Stopping a pass writes minutes into the form. Timers must survive a page refresh (persist in browser storage).
- **Spaced review (BR-09)**: every attempt on a library problem schedules cold reviews (+1, +3, +7, +21 days for confidence 1-3; one later review for 4-5), and each problem has one active ladder. Review queue page lists Due today, Overdue, Upcoming; completing a review means logging a Cold review attempt. A review rated 1-3 restarts the ladder at +1 day. The user can reschedule or skip a review with a reason.
- **Progress view**: cumulative meaningful attempts against the markers (65-80 by Day 90, about 120-135 by Day 220), weekly new-problem count against the 5-6 guide, per-topic coverage, median time, confidence trend, repeated mistake tags by week, and a **weakest problems** list (confidence <= 3, oldest review first) used for the Week 13 cold-redo and "two mediums back-to-back" session.
- **Timed session mode** (Phase 3 and 4): pick 1-2 problems (optionally company-tagged), run a combined timer, log one attempt per problem.

### 4.5 DDIA and System Design

- **Edition check**: if `DdiaEdition` is Unconfirmed from plan Week 2 onward, show a persistent banner: "STOP BEFORE WEEK 2: check the title/copyright page and ISBN of your copy." Choosing First or Second loads the matching chapter list (Appendix D). Chapters remain fully editable; the app must never apply 2nd-edition chapter numbers to a 1st-edition book. Plan tasks never embed a chapter number: a DDIA task is linked to its plan week and resolves its chapter(s) at display time from the DdiaChapter rows of the confirmed edition whose planned week range covers that week. While the edition is Unconfirmed, show the 2nd-edition topic labelled "2nd edition (unconfirmed)".
- **Chapters**: number, title, planned week, topics, Ask-Ops connection, total pages (user entered), pages read, status (Not started / Reading / Note done / Explained in 90 seconds), started and finished dates.
- **Reading log**: date, chapter, pages read, minutes. Show 7-10 pages/day as a guide, not a quota.
- **Chapter note** (Template C, one page per chapter, Markdown, attachments): edition and chapter title (auto), five key ideas, trade-offs (Option A vs B, what you gain, what you pay, when each wins), failure modes, "In Ask-Ops I will..." decision, three interview questions each with a 60-second answer, and a 90-second explanation record (rating, filler-word and missing-trade-off notes, optional audio/video attachment).
- **Design drills** (Appendix H, 24 seeded prompts, one per week from about Day 80): status Not started / Written / Spoken mock / Reviewed. Each drill has the 45-minute framework as a structured form with a segment timer (clarify 5, estimate 5, API and data model 8, high-level design 10, two deep dives 10, bottlenecks and trade-offs 7) and a notes field per segment. Drills 1-8 are written in full; later drills are spoken mocks with a recording attachment.
- **Articulation drills** log: weekly "why X over Y" prompts (queue vs direct call, Dapper vs EF Core, pgvector vs keyword search, plus user-defined). Fields: prompt, answer-first response, trade-offs, evidence I would measure, self-rating, recording attachment. The structure hint "answer first, reasoning, trade-offs, evidence" shows beside the form.

### 4.6 Plan Tasks, Exit Criteria and Checkpoints

- **Plan tasks** (seeded from Appendices C, E, F, plus user-added): track (DSA, DDIA, React, MEAI/MAF, Design, Flagship, Scale, Interview, Review, Other), title, description, required output or evidence, fallback, day or week range, status (Not started / In progress / Done / Deferred / Skipped), completed date, notes, evidence links and attachments. Views by phase, by week and by track, plus a board view by status. Seeded DSA and DDIA weekly tasks get their status from progress, not from manual ticks (BR-18); the user can still set Deferred or Skipped, which overrides the derived value.
- **Exit criteria** per phase (Appendix G) as checklists with evidence text and attachments. A phase page shows exit-criteria completion and linked artifacts.
- **Checkpoints** (Appendix B, ten seeded): due date derived from plan day; each stores a written answer (Markdown), outcome (On track / Slip / Re-plan), and attachments. The Today view reminds 3 days before and when overdue. Day 30 shows the active-day count against 26; Day 60 shows the DDIA edition status; Day 90 lists incomplete Phase 1 artifacts; Day 180 and the monthly review link to the market check; Day 340 shows funnel metrics.
- "What would change this plan" (roadmap 1.5) appears as a four-item checklist inside the re-plan form.

### 4.7 Flagship (Ask-Ops) Tracker

- **Build sessions**: date, minutes, milestone block (2.1 to 2.6), what shipped, commit hash or URL, notes. Show total focused hours against the 75-120 h planning envelope and per milestone block. The envelope is a planning constraint, not a score: show it as a gauge with no "behind" language.
- **ADRs** (Template G): number, title, status (Proposed / Accepted / Superseded), date, context scale, context, options, decision, revisit trigger, references (with version and date checked). Phase 1 expects five ADRs. Export any ADR as a `.md` file.
- **Design document registry**: the 3-5 page flagship design doc (domain, API, data model, boundaries, threats, failure modes) as a record with versions and attachments.
- **Evaluation runs**: date, golden-set size (20, expanding toward 50), retrieval hit@k, MRR, answer correctness, citation support, human spot-check findings, commit reference, report attachment. Show the trend and the "known limitations" text.
- **Load-test runs** (Phase 3): date, dataset, machine, test profile, variable changed (one at a time), RPS, p50, p95, p99, error rate, before/after label, k6 report attachment.
- **Agent defect log** (AI-native practice): date, one-line defect or misleading claim found in AI-generated code, category (authorization, concurrency, input validation, error handling, privacy, dependency change, hidden coupling, other), fix reference.
- **Exit criteria** for Day 220 and Day 300 as checklists (Appendix G).

### 4.8 Reviews

- **Weekly review** (Template D, Sunday, 30 min). The app prefills computed fields: active days and MVDs, DSA attempts, cold re-solves, median time, repeated mistakes, DDIA chapter and note status, build sessions, gym sessions logged, travel in the next fortnight. The user adds: energy/sleep/mood 1-5, shipped, blocked, next smallest slice, recovery or symptoms needing review, 20-posting repeated requirement, Start / Stop / Keep.
- **Monthly review** (60 min): phase progress, mistake-log themes, postings captured this month against 20, "do I need a lighter week?" decision, someday-list triage (promote / keep / drop each item).
- Reviews are editable after saving and attachable. A review reminder appears on Sunday and when a week has no review.

### 4.9 Market Check (Job Postings)

- **Posting log**: captured date, posting date, company, company type (Product, GCC, Services, Startup, Other), role title, location, experience band, URL, required skills (tags), preferred skills (tags), compensation as listed (optional text), fit 1-5, notes.
- **Monthly counter**: postings captured this calendar month against the target of 20.
- **Analytics**: frequency of each skill tag split by **required** vs **preferred**, filterable by location, company type and month. Show a caution line from the roadmap: one posting is not a trend; "nice to have" is not a reason to derail the phase. No salary statistics are presented as fact.

### 4.10 Interview Pipeline

- **Target companies** (Day 270 readiness): company, role, location, experience requirement, fixed and variable compensation, application link, priority, fit notes.
- **Applications**: company, role, applied date, channel (Direct, Referral, Recruiter, Other), resume version used, **Canary flag** (Days 195-215 warm-up applications), current status. **Stage events** (Applied, Callback, Recruiter screen, Technical screen, Loop/Onsite, Offer, Rejected, Withdrawn) each with date, notes and "what failed / what I learned". Closing a stage prompts for the specific practice block it implies.
- **Offer details** on an application: fixed pay, variable pay, joining bonus, benefits, notice terms, joining date, scope, manager/team, location, and a **Written offer received** checkbox. A side-by-side offer comparison view shows these fields. If an offer is not marked written, show: "Never resign on a verbal assurance."
- **Funnel dashboard**: applications, callbacks, screens, next-stage rate, offers, by channel and by week; Phase 4 weekly targets (6-10 targeted applications, 3-5 referral asks); the diagnostic checklist from roadmap 8.2 when there are no callbacks (role fit, resume clarity, ATS terminology, seniority mismatch, application channel).
- **Referral asks**: person, company, date, message (prefilled from Template F, editable), status (Drafted, Sent, Replied, Referred, No reply).
- **Mock interviews**: type (DSA, System design, Project walkthrough, Behavioral), date, duration, topic/prompt, self-rating, weak points, action items, **recording attachment** (video/audio), and a "reviewed recording" checkbox. Action items feed a **weekly practice list**. Counters: 6-8 mocks in Phase 3, 1-2 per week in Phase 4.
- **STAR stories**: 8-10 stories mapped to themes (Ownership, Debugging, Conflict, Learning, Reliability, Other): Situation, Task (what was mine), Action (three concrete steps, say "I"), Result (a real number if possible), Reflection, rehearsal count, last rehearsed date, 90-second speaking timer and optional recording.
- **Resume versions**: label, target role, date, file attachment, review notes. Day 270 readiness checklist items are seeded as exit criteria.

### 4.11 Someday List

- Replaces `someday-list.md`. Fields: title, notes, category (Tool, Course, Framework, Project idea, Other), created date, status (Parked, Promoted, Dropped), reviewed-at date.
- Add from anywhere with the global quick-add. Roadmap rule: new tools and course ideas go here and are only revisited at the monthly review.

### 4.12 Nudges (Rules Engine)

Nudges are computed on the fly from data (not stored) by `NudgeService` using the thresholds below (BR-13). Tone is neutral and practical. Each nudge has a short title, one sentence, an action link and a dismiss-for-today option.

| **Condition** | **Message intent** | **Action** |
|---|---|---|
| One missed day (no log, not paused) | MVD today; log it and move on. | Open MVD log form |
| Two consecutive missed days | Never miss twice: do an MVD now, ramp back over 2-3 days, do not repay hours. | Open MVD log form |
| 2+ weeks behind the plan | Cut optional infrastructure, UI polish and extra features; preserve DSA, DDIA and review. | Open plan tasks, mark Deferred |
| Phase 1 more than 3 weeks behind | Re-plan trigger 2 is met. | Open re-plan form |
| DSA plateau: average confidence not improving for 3 weeks and below 3.5 | Pick one weak pattern; practise it with spaced reviews for two weeks. Do not switch languages. | Open weakest problems |
| Sleep average below 7 h over 5 logged days, or energy <= 2 for 3 days | Take a lighter week; shrink the third study block first. | Open weekly review |
| Weekly review not done by Sunday evening | Weekly review is 30 minutes. | Open weekly review |
| Postings captured < 20 and month has <= 10 days left | Monthly market check is due. | Open posting log |
| DDIA edition Unconfirmed in Week 2+ | Confirm your edition before continuing. | Open settings |
| Checkpoint within 3 days or overdue | Answer the checkpoint question in writing. | Open checkpoint |
| Schedule phase changes (Week 4, Week 9) | Run the same phase for at least two weeks before changing it. | Open schedule view |
| Written offer missing on an Offer stage | Never resign on a verbal assurance. | Open application |

### 4.13 Dashboard and Metrics

- Phase progress and plan day; **active days in the last 30** against the target of 26 (MVDs included); "never missed twice" status (no two consecutive missed days since last pause).
- 365-day heatmap; DSA cumulative line with the 65-80 and 120-135 marker bands; DDIA chapter progress; flagship hours gauge; mocks per week; application funnel; sleep and energy trends; gym adherence per week; weekly hours stacked by category against the phase target range (Appendix J).
- Each chart shows the roadmap caveat in a tooltip or caption: "A tick measures behavior, not mastery", "Cold re-solves and pattern coverage matter more than raw count".
- All charts have a data-table view for accessibility.

### 4.14 Time, Workload and Travel

- **Weekly hours** aggregated by category (BR-12) against the stage targets (Appendix J). Show the target as a range; flag the 19-20 h stretch band as "Stretch only".
- **Travel fortnight**: Edappal trip Friday to Monday every other week (BR-04). Auto-mark Friday as a pre-planned MVD, Saturday timed attempt, Sunday review, Monday short review. No deep build is expected and no make-up hours are shown. Any week can be overridden as travel or non-travel.
- **Schedule view**: the Phase A, B and C time blocks (Appendix I) as a read/edit reference, with the current schedule phase highlighted.

### 4.15 Search, Export, Import and Backup

- **Global search** across notes, problems, attempts, ADRs, drills, postings, applications, someday items and file names using indexed `LIKE` queries (no Full-Text dependency). Results grouped by type with a snippet.
- **Export**: full backup as one `.zip` (JSON per table plus the files folder) from the UI and CLI; CSV export per list; Markdown export for notes, ADRs and reviews.
- **Import / restore**: from a full backup `.zip` into an empty database, with a dry-run summary first. Round trip must be lossless (tested).
- **Automatic backup**: if enabled and the last backup is older than 24 hours at startup, write a backup to `data/backups/` and keep the latest 14. Also provide a script that runs `BACKUP DATABASE` for a native `.bak`. The .bak is written inside the container to /var/opt/mssql/backups, which is bind-mounted to data/sqlbackups.
- **Trash page**: list soft-deleted records and files; restore or purge permanently; auto-purge after `TrashRetentionDays`.

## 5. Data Model

Entities and their key fields. Add the common columns from Section 3.2 (Id, CreatedAt, UpdatedAt, IsDeleted, RowVersion, and SeedKey/IsUserModified on seedable entities). Choose sensible lengths and types; use `nvarchar(max)` for Markdown bodies. "Attach" = can have files linked.

| **Entity** | **Key fields** | **Attach** |
|---|---|---|
| **AppSettings** | PlanStartDate, TimeZone, DdiaEdition, DdiaEditionConfirmedOn, DsaUnaidedMinMinutes, ReviewIntervalsCsv, HighConfidenceReviewDays, TravelAnchorFriday, TravelEnabled, FileRoot, MaxUploadMb, TrashRetentionDays, NightlyBackupEnabled | No |
| **PauseRange** | StartDate, EndDate, Reason (enum), Notes | No |
| **ReplanEvent** | Date, Trigger (enum), EvidenceMd, DecisionMd, CheckpointId? | Yes |
| **Phase** | Number, Name, DayFrom, DayTo, WeekFrom, WeekTo, Objective, ExitEvidence | No |
| **PlanTask** | PhaseId, Track (enum), Kind (Manual / DerivedDsa / DerivedDdia), Title, Description, OutputRequired, Fallback, DayFrom, DayTo, WeekFrom, WeekTo (derived), Status, StatusOverride?, CompletedOn?, NotesMd, EvidenceUrl, SortOrder | Yes |
| **ExitCriterion** | PhaseId, Text, Done, DoneOn?, EvidenceText | Yes |
| **Checkpoint** | PlanDay (or DayFrom/DayTo for 195-215), Title, Question, AnswerMd, Outcome, AnsweredOn? | Yes |
| **ScheduleBlock** | SchedulePhase (A/B/C), StartTime, EndTime, Title, Rule, IsStretch, SortOrder | No |
| **GymPlanDay** | SchedulePhase, DayOfWeek, SessionName, IsOptional, Notes | No |
| **DailyLog** | Date (unique), DayType, IsTravelDay, LoggedLate, Energy, SleepHours, WakeTime, LightsOutTime, DsaLine, CoreLine, BuildLine, TomorrowDsa, TomorrowBuild, NotesMd, StartRitualDone flags, CloseDayDone flags, GymStatus, GymSession, GymNote | Yes |
| **DailyTick** | DailyLogId, Type (Dsa/Core/Build), EvidenceNote, LinkedEntityType?, LinkedEntityId?, EvidenceUrl, Counted (bool, derived for Dsa) | Yes |
| **TimeEntry** | Date, DailyLogId?, Category (enum), Minutes, Note, Source (Manual / Timer / Derived), SourceEntityType?, SourceEntityId? | No |
| **DsaTopic** | Name, SortOrder, IsDeferred | No |
| **DsaProblem** | TopicId, Title, Url, Difficulty?, PlanWeek?, SortOrder, Status | Yes |
| **DsaAttempt** | Date, ProblemId?, TitleFreeText?, Type, Pass1Min, HintLevel, Pass2Min, Pass3Min, TotalMin, SolvedUnaided, Pattern, KeyInsight, WhatIMissed, TimeComplexity, SpaceComplexity, Confidence (1-5), CodeMd, Company?, IsCounted, NotCountedReason, ReviewItemId? | Yes |
| **DsaReviewItem** | ProblemId, SourceAttemptId, DueDate, IntervalDays, Status (Pending / Done / Skipped / Superseded), CompletedAttemptId?, SkipReason | No |
| **Tag / DsaAttemptTag** | Tag(Name, Kind: Mistake/Skill); join table between attempts and tags | No |
| **DdiaChapter** | Edition, Number, Title, PlannedWeekFrom, PlannedWeekTo, Topics, AskOpsConnection, TotalPages?, PagesRead, Status, StartedOn?, FinishedOn? | Yes |
| **ReadingLog** | Date, ChapterId, PagesRead, Minutes | No |
| **DdiaNote** | ChapterId, KeyIdeas (5 fields), TradeOffsMd, FailureModesMd, AskOpsDecision, Explanation90 (rating, notes), Status | Yes |
| **InterviewQa** | DdiaNoteId, Question, Answer60sMd | No |
| **DesignDrill** | Number, Prompt, Group, PlannedWeek, Status, PerSegment notes (6 fields), SpokenMock (bool), ReviewedOn? | Yes |
| **ArticulationLog** | Date, Kind (WhyXOverY / 90sExplanation / Other), Prompt, AnswerMd, TradeOffsMd, EvidenceMd, SelfRating, Notes | Yes |
| **Adr** | Number, Title, Status, Date, ContextScale, ContextMd, OptionsMd, DecisionMd, RevisitTrigger, ReferencesMd | Yes |
| **FlagshipSession** | Date, Minutes, MilestoneBlock (2.1-2.6), Shipped, CommitRef, NotesMd | Yes |
| **EvalRun** | Date, GoldenSetSize, HitAtK, Mrr, AnswerCorrectness, CitationSupport, SpotCheckMd, CommitRef, LimitationsMd | Yes |
| **LoadTestRun** | Date, Dataset, Machine, Profile, VariableChanged, Rps, P50Ms, P95Ms, P99Ms, ErrorRate, BeforeAfter, NotesMd | Yes |
| **AgentDefect** | Date, Description, Category, FixRef | No |
| **WeeklyReview** | WeekStart (Monday), computed snapshot (JSON), Energy, Sleep, Mood, ShippedMd, BlockedMd, NextSliceMd, RecoveryNote, RepeatedRequirement, Start, Stop, Keep | Yes |
| **MonthlyReview** | Month (first day), PhaseProgressMd, MistakeThemesMd, PostingsCaptured, LighterWeekNeeded, SomedayTriageMd | Yes |
| **JobPosting / JobPostingSkill** | CapturedOn, PostedOn, Company, CompanyType, Title, Location, ExperienceBand, Url, CompensationText, Fit, NotesMd; skill rows (TagId, IsRequired) | Yes |
| **TargetCompany** | Company, Role, Location, ExperienceReq, FixedComp, VariableComp, Link, Priority, NotesMd | No |
| **Application** | TargetCompanyId?, Company, Role, AppliedOn, Channel, ResumeVersionId?, IsCanary, Status, OfferFixed, OfferVariable, JoiningBonus, Benefits, NoticeTerms, JoiningDate, OfferWritten, NotesMd | Yes |
| **ApplicationStageEvent** | ApplicationId, Stage, Date, NotesMd, LearningMd | No |
| **ReferralAsk** | Person, Company, Date, MessageMd, Status | No |
| **MockInterview** | Date, Type, DurationMin, Topic, SelfRating, WeakPointsMd, ActionItemsMd, RecordingReviewed | Yes |
| **PracticeListItem** | Source (Mock/Drill/DSA/Manual), Text, Done, DoneOn? | No |
| **StarStory** | Title, Theme, Situation, Task, Action, Result, Reflection, RehearsalCount, LastRehearsedOn | Yes |
| **ResumeVersion** | Label, TargetRole, Date, NotesMd | Yes |
| **SomedayItem** | Title, NotesMd, Category, Status, ReviewedOn? | No |
| **WeeklyHourTarget** | Stage (A / B / Phase1-4), Categories (one or more), MinHours, MaxHours, IsTotal, IsStretch | No |
| **FileAsset / FileLink** | See Section 6 | n/a |

- Index: `DailyLog(Date)` unique filtered on `IsDeleted = 0`; `DsaReviewItem(DueDate, Status)`; `DsaAttempt(Date)`, `DsaAttempt(ProblemId)`; `FileLink(EntityType, EntityId)`; `FileAsset(Sha256)`; `JobPosting(CapturedOn)`; `Application(Status)`.
- Use `DateOnly` and `TimeOnly` mappings for dates and clock times; enums stored as strings; money fields as text or `decimal(18,2)` with a currency note (the user may enter lakhs per annum).

## 6. File Storage

The user needs to store files with any record: notes, diagrams, design docs, ADR exports, k6 and eval reports, resumes, and large mock-interview recordings. Files live on disk; metadata lives in SQL Server.

### 6.1 Storage Design

- Files are stored under `FileRoot` (default `./data/files`, outside `wwwroot`, never served as static files). Layout: `{yyyy}/{MM}/{StorageKey}{ext}` where StorageKey is a new GUID. The original file name is metadata only and is never used as a path.
- `FileAsset`: Id, StorageKey, OriginalName, DisplayTitle, Description, ContentType (detected from content, not only the extension), Kind (Document, Image, Audio, Video, Code, Archive, Other), SizeBytes, Sha256, UploadedAt, IsDeleted, DeletedAt, tags.
- `FileLink`: FileAssetId, EntityType, EntityId, Label. One file can link to many records; a file with no links appears in the Files library as "Unattached".
- Writes are atomic: stream to a temp file in the same volume, compute SHA-256 while streaming, move into place, then insert the row. On any failure delete the temp file and roll back. A startup/maintenance job reports orphaned files (on disk without a row) and missing files (row without a file).
- Soft delete moves nothing on disk; the purge job removes the physical file after `TrashRetentionDays` or on explicit "Delete permanently".
- Duplicate detection: if the SHA-256 already exists, tell the user and offer to link the existing file instead of storing another copy.

### 6.2 Behaviour and Limits

- Default max size 500 MB per file (`MaxUploadMb`). Raise Kestrel `MaxRequestBodySize` and form limits for the upload endpoint only. Use streaming multipart reading so large videos never load fully into memory.
- Multiple files per upload, with an optional link target (entity type and id) so the uploader on a record attaches in one step.
- Preview and download: `GET /api/v1/files/{id}/content` serves inline with correct content type and **HTTP Range support** (`enableRangeProcessing`) so audio and video can be scrubbed; `GET /api/v1/files/{id}/download` forces attachment. Inline preview only for images (not SVG), PDF, audio, video, plain text and Markdown. HTML, SVG and unknown types are always served as attachment.
- Always send `X-Content-Type-Options: nosniff`. Block dangerous extensions (`.exe .bat .cmd .ps1 .sh .dll .msi .js .vbs .scr .com`) by default; allow override in settings with an explicit warning.
- Sanitize names (strip path separators and control characters, cap length), reject path traversal, and never trust client content types.
- No antiforgery tokens (no cookies, no auth). Instead a local-request guard middleware MUST run before all endpoints: (1) the Host header must be localhost:{port} or 127.0.0.1:{port}, which blocks DNS rebinding (all methods); (2) on POST, PUT, PATCH and DELETE, an Origin header, if present, must be the app or Vite dev origin; (3) those methods must also carry the header X-SelfTracker: 1, which a cross-site HTML form cannot send and which forces a CORS preflight for scripts. This covers the multipart upload, which CORS alone does not protect. The React client adds the header to every request. Record the reasoning in DECISIONS.md.
- Backups include the files folder. Restore verifies each file against its stored SHA-256 and reports mismatches.
- Storage dashboard: total size, count by kind, largest files, unattached files, free disk space warning under 5 GB.

> **Upload notice (show in the uploader and README)**
> Do not upload employer code, data, credentials or confidential architecture. The roadmap forbids moving company material into personal repositories and tools.

### 6.3 File Endpoints

| **Method** | **Route** | **Purpose** |
|---|---|---|
| POST | /api/v1/files | Multipart upload of 1..n files; optional `entityType`, `entityId`, `label`; returns FileAsset list and duplicate hints |
| GET | /api/v1/files | List/search: `q`, `kind`, `entityType`, `entityId`, `tag`, `unattached`, paging, sort |
| GET | /api/v1/files/{id} | Metadata and links |
| GET | /api/v1/files/{id}/content | Inline stream with Range support (allowed types only) |
| GET | /api/v1/files/{id}/download | Attachment download with original name |
| PATCH | /api/v1/files/{id} | Edit title, description, tags |
| POST / DELETE | /api/v1/files/{id}/links | Add or remove a link to a record |
| DELETE | /api/v1/files/{id} | Soft delete (restore and purge via `/api/v1/trash`) |
| GET | /api/v1/files/stats | Storage dashboard numbers |
| POST | /api/v1/files/maintenance/verify | Orphan, missing and hash verification report |

## 7. API Conventions and Endpoint Inventory

- Base path `/api/v1`. JSON with camelCase, enums as strings, ISO 8601 dates. List endpoints accept `page`, `pageSize` (max 200), `sort`, and the filters listed below; return `{ items, total, page, pageSize }`.
- Create returns 201 with Location; update uses `PUT` (full) or `PATCH` (partial) and requires the `rowVersion` (header `If-Match` or body field); stale version returns 409. Delete is soft and returns 204. Errors are ProblemDetails with validation details.
- All endpoints take a `CancellationToken`. Health: `/health/live` and `/health/ready` (database reachable, migrations applied, file root writable, free disk).
- Computed read models (Today, dashboard, progress, nudges, funnel) live under `/api/v1/views/...` and are never written to directly.

| **Resource** | **Endpoints and notable filters** |
|---|---|
| **settings** | GET, PUT `/settings`; POST `/settings/confirm-ddia-edition`; POST `/settings/restore-defaults/{list}` |
| **plan** | GET `/plan/phases`; GET `/plan/day?date=`; CRUD `/plan/pause-ranges`, `/plan/replans`; CRUD `/plan/tasks` (filter: phase, week, track, status); CRUD `/plan/exit-criteria`, `/plan/checkpoints` |
| **schedule** | CRUD `/schedule/blocks` (filter: phase), `/schedule/gym-plan` |
| **daily logs** | CRUD `/daily-logs` (filter: from, to, type); GET `/daily-logs/by-date/{date}`; PUT `/daily-logs/{id}/ticks/{type}`; CRUD `/daily-logs/{id}/time-entries` |
| **dsa** | CRUD `/dsa/topics`, `/dsa/problems` (filter: topic, status, week); CRUD `/dsa/attempts` (filter: from, to, problem, confidence, type, tag, company); GET `/dsa/reviews?bucket=due\|overdue\|upcoming`; POST `/dsa/reviews/{id}/skip\|reschedule`; GET `/dsa/progress`, `/dsa/weakest` |
| **ddia** | CRUD `/ddia/chapters` (filter: edition); CRUD `/ddia/reading-log`, `/ddia/notes`, `/ddia/drills`, `/ddia/articulation` |
| **flagship** | CRUD `/flagship/sessions`, `/flagship/adrs`, `/flagship/eval-runs`, `/flagship/load-runs`, `/flagship/agent-defects`; GET `/flagship/adrs/{id}/export.md`; GET `/flagship/summary` |
| **reviews** | CRUD `/reviews/weekly`, `/reviews/monthly`; GET `/reviews/weekly/prefill?weekStart=`; GET `/reviews/monthly/prefill?month=` |
| **market** | CRUD `/market/postings` (filter: month, location, companyType, tag); GET `/market/analytics` |
| **interviews** | CRUD `/interviews/targets`, `/interviews/applications` (+ `/{id}/stages`), `/interviews/referrals`, `/interviews/mocks`, `/interviews/star-stories`, `/interviews/resumes`, `/interviews/practice-list`; GET `/interviews/funnel`, `/interviews/offers/compare?ids=` |
| **someday** | CRUD `/someday` (filter: status); POST `/someday/{id}/promote` |
| **views** | GET `/views/today?date=`, `/views/dashboard`, `/views/nudges`, `/views/weekly-hours`, `/views/heatmap?year=` |
| **search** | GET `/search?q=&types=` |
| **data** | POST `/data/export`, POST `/data/import` (dry-run flag), GET `/data/backups`, POST `/data/backups`; GET, POST, DELETE `/trash` |

## 8. Frontend Specification

- Dark mode is the default (the roadmap itself is dark mode) with a light toggle. Desktop-first, usable on a narrow window. Use shadcn/ui primitives and Tailwind; do not hand-write bespoke CSS systems.
- All server state through TanStack Query with optimistic updates for ticks, status changes and checkboxes. Forms use React Hook Form + Zod, with Zod schemas aligned to the API contract. Long forms (notes, reviews, ADRs, DDIA notes) autosave a local draft and warn on unsaved changes.
- Destructive actions use a confirm dialog and a toast with **Undo**. Show friendly 409 conflict handling ("this record changed elsewhere, reload or overwrite").
- Accessibility: labelled inputs, visible focus, keyboard navigation, sufficient contrast, never color alone (heatmap cells carry a letter or pattern), data-table alternative for every chart.
- Command palette (Ctrl/Cmd+K) and a persistent quick-add button for DSA attempt, daily log, someday idea and file upload.
- Reusable components: `Attachments` (list, drag-and-drop upload with progress, preview, remove), `MarkdownEditor` (textarea with live preview), `TagInput`, `RatingInput` (1-5), `Stopwatch` and `CountdownTimer` (persisted), `DataTable`, `ConfirmDialog`, `EmptyState`, `Heatmap`, `NudgeCard`, `ChecklistEditor`.

| **Page** | **Route** | **Contents** |
|---|---|---|
| **Today** | / | Section 4.2 |
| **Daily logs** | /logs, /logs/:date | List, calendar, heatmap, log form with ticks, MVD checklist, time entries, gym, attachments |
| **DSA** | /dsa, /dsa/problems, /dsa/attempts, /dsa/reviews, /dsa/progress | Library, attempt form with timers, review queue, progress and weakest list, timed session |
| **DDIA and design** | /ddia, /ddia/chapters/:id, /ddia/drills, /ddia/articulation | Edition banner, chapters, reading log, notes, drills with segment timer, articulation drills |
| **Plan** | /plan, /plan/phases/:n, /plan/tasks, /plan/checkpoints, /plan/replans, /plan/schedule | Phase pages with exit criteria, task views and board, checkpoints, re-plan log, schedule blocks, pause ranges |
| **Flagship** | /flagship, /flagship/adrs, /flagship/runs, /flagship/defects | Hours, sessions, ADRs, eval and load runs, defect log, exit criteria |
| **Reviews** | /reviews/weekly, /reviews/monthly | Prefilled review forms and history |
| **Market** | /market | Posting log, monthly counter, required vs preferred analytics |
| **Interviews** | /interviews/* | Targets, applications with stage timeline, offers compare, referrals, mocks, STAR stories, resumes, practice list, funnel |
| **Someday** | /someday | List with promote, keep, drop |
| **Files** | /files | Library grid/list, filters, search, preview, rename, retag, delete, storage stats |
| **Dashboard** | /dashboard | Section 4.13 |
| **Search** | /search | Section 4.15 |
| **Data and settings** | /settings, /settings/data, /trash | Settings, export/import, backups, trash, maintenance (file verify) |

## 9. Business Rules and Calculations

Implement each rule as a pure Domain service with unit tests named after the rule ID (for example `BR09_FailedReviewRestartsLadder`).

| **ID** | **Rule** | **Definition** |
|---|---|---|
| **BR-01** | Plan day | `PlanDay(date)` = number of non-paused calendar days from `PlanStartDate` to `date` inclusive. Before the start date: "not started". On a paused date the value equals the previous day's value and the date is flagged Paused. Above 365: "Year 2" (allowed, flagged). Adding or removing a pause range recomputes every derived date (checkpoint due dates) but never changes stored logs. |
| **BR-02** | Phase, week, schedule phase | Phase by plan day: 1-90, 91-220, 221-300, 301-365. Plan week is phase-relative so it matches the roadmap labels: PlanWeek = Phase.WeekFrom + floor((day - Phase.DayFrom) / 7), clamped to Phase.WeekTo (Phase 1 W1-13, Phase 2 W14-31, Phase 3 W32-43, Phase 4 W44-52). Do NOT use ceil(day / 7): it gives W13 for Day 91, W43 for Day 301 and W53 for Day 365. Day ranges are the source of truth; week labels are for display and grouping, and a phase's last partial week folds into its final week (W13 = Days 85-90, W31 = Days 210-220, W43 = Days 298-300, W52 = Days 357-365). Schedule phase: weeks 1-3 = A, weeks 4-8 = B, week 9 onward = C. Test boundaries: Days 1, 84, 85, 90, 91, 97, 98, 216, 217, 220, 221, 241, 242, 300, 301, 356, 357, 365. |
| **BR-03** | Date for plan day | Inverse of BR-01: the first non-paused date whose plan day equals N. Used for checkpoint due dates and milestone dates. |
| **BR-04** | Travel window | When enabled, trips start on `TravelAnchorFriday + 14k days` (any integer k) and run Friday to Monday inclusive. Roles: Friday = pre-planned MVD, Saturday = timed DSA attempt, Sunday = review, Monday = short review. A trip can be cancelled or added per week. |
| **BR-05** | Day states and active days | Active = Full or MVD log. Rest = Rest log (not a miss). Missed = date from the plan start to yesterday, not paused, with no non-deleted log. Paused and Future are self-explanatory. "Active days in last 30" counts Active logs in the last 30 calendar days; paused dates are excluded from the window and shown. Target line: 26. An In progress log with no day type on a past date is Unclassified (neither Active nor Missed). |
| **BR-06** | Tick evidence | DSA tick: derived; true if the date has at least one counted attempt (BR-08) or a completed review. Core tick: needs an evidence note and at least one of a linked artifact (DDIA note, drill, ADR, design note), an attachment, a URL, or a note of 3+ lines. Build tick: needs an evidence note and at least one of a commit hash/URL, PR URL, linked flagship session/ADR, or attachment. The API rejects a tick without evidence (422). PUT on the Dsa tick returns 400 because it is derived. |
| **BR-07** | MVD satisfied | DSA attempt or cold review of 20-30 minutes, plus 15-20 minutes DDIA/core with a 3-line note, plus 10-15 minutes build or one tiny commit/test. On a travel day (BR-04) a DSA review plus a short written note is sufficient. This is an indicator, never a gate: a day can still be logged as MVD manually. Note: a 20-24 minute DSA attempt satisfies the MVD indicator but earns the DSA tick and cumulative marker only if it is a cold review, because the roadmap's tick rule is 25+ minutes (BR-08). Show this beside the MVD checklist. |
| **BR-08** | Meaningful attempt | Counted if (Type = New or Timed and Pass1Min >= DsaUnaidedMinMinutes and WhatIMissed is non-empty) or (Type = Cold review on a library problem, linked to a review item or not; this includes the Week 13 cold re-do of the weakest problems). Otherwise saved with IsCounted = false and a reason string. |
| **BR-09** | Spaced review | Every saved attempt on a library problem (ProblemId set) schedules reviews, counted or not: counting affects markers and ticks, not memory. Confidence 1-3 creates pending items at +1, +3, +7 and +21 days from the attempt date; 4-5 creates one item at HighConfidenceReviewDays. A problem has one active ladder: a New or Timed attempt marks that problem's remaining pending items Superseded and starts a new ladder. A Cold review linked to an item completes it; an unlinked Cold review completes the problem's earliest pending item, or starts a ladder if none is pending. If the review confidence is 1-3 the remaining items are superseded and the ladder restarts at +1 day from the review date; if 4-5 the remaining items stay. Free-text attempts get no reviews until the user adds the problem to the library (the form offers this on save). Editing an attempt's confidence regenerates its pending items. Skipping or rescheduling needs a reason. |
| **BR-10** | DSA markers | Cumulative counted attempts/reviews up to a plan day compared to bands: 65-80 at Day 90, about 120-135 at Day 220. Weekly new-problem guide 5-6. Show as ranges, never as a pass/fail. |
| **BR-11** | Checkpoint timing | Due date = BR-03 of the checkpoint plan day (for 195-215 use the start of the window and show the window). Reminder 3 days before; overdue when past due and unanswered. |
| **BR-12** | Weekly hours | Sum of `TimeEntry.Minutes` per Monday-Sunday calendar week and category, compared to the targets for the current phase (Appendix J). Reviews also use Monday-Sunday weeks. Plan content still uses plan weeks. Targets by stage, matched by the plan week of the calendar week's first plan day: weeks 1-3 (Phase A) compare only the weekly total against about 4-8 h (60-90 min on most weekdays plus one short weekend session); weeks 4-8 (Phase B) compare the total against 8-11 h and show the Phase 1 category ranges as guidance only; from week 9 use the per-category ranges of the current phase (Appendix J.2) with a 12-15 h stable total, 15-17 h ambitious and 19-20 h "Stretch only". |
| **BR-13** | Nudge thresholds | As in Section 4.12. "Behind" = the oldest plan task of the current phase that is not Done, Deferred or Skipped whose end day is more than 14 plan days in the past. Phase 1 "more than three weeks behind" uses the same measure with 21 days. DSA plateau: weekly average confidence of new attempts did not increase over 3 consecutive weeks and the latest average is below 3.5. Derived tasks (BR-18) use their derived status, so logging problems and chapters is enough to stay current. |
| **BR-14** | No debt | No API field, UI text or chart may express hours owed, catch-up, backlog hours or "behind by N hours". Include an automated test scanning the OpenAPI schema and UI strings for forbidden terms (`owed`, `catch up`, `make up hours`, `debt`). The scan covers app-authored strings only (UI labels, messages, API schema names). Text from seed JSON and quoted roadmap rules, which negate these ideas (for example "no make-up hours", "do not repay hours", "Repay missed hours with a binge" in a Never-do column), is excluded by matching against the seed files. |
| **BR-15** | Seeding | Seed rows are upserted by `SeedKey`: insert if missing, skip if present, never overwrite rows with `IsUserModified = true`. Any user edit of a seeded row sets the flag. "Restore defaults" for a list re-applies seed values to unmodified or explicitly chosen rows only. |
| **BR-16** | Concurrency | All updates compare `RowVersion`; conflict returns 409 and no data loss. |
| **BR-17** | File integrity | SHA-256 stored on upload; export/import and the verify job recompute and compare; any mismatch is reported, never silently ignored. |
| **BR-18** | Derived task status | A DerivedDsa task is Done when every DsaProblem with that PlanWeek is Solved, Deferred or has a counted attempt, and In progress when at least one has any attempt. A DerivedDdia task is Done when every chapter of the confirmed edition planned for that week has status Note done or later, and In progress when one is Reading. Manual Deferred or Skipped overrides the derived status. Recompute on read; do not store a stale copy. |
| **BR-19** | Derived time entries | Saving a DSA attempt creates a DSA entry of TotalMin; a reading-log row, design drill or articulation log creates DDIA / design; a flagship session creates Main track; a mock interview creates Mocks. The source record owns the entry: editing it updates the entry, deleting or restoring it deletes or restores the entry, and derived entries cannot be edited directly. Entries are created in the same transaction as the source row. |

## 10. Seed Data

- Create one JSON file per list under `seed/` from Appendices A to O, each row with a stable `seedKey` (for example `phase-1`, `checkpoint-30`, `ddia2e-ch08`, `drill-07`, `task-w05-react`).
- The seeder runs after migrations at startup, is idempotent (BR-15), logs counts inserted/skipped, and has an integration test that runs it twice and asserts identical row counts.
- The 150-problem DSA list in the roadmap is only named in part. Seed exactly the problems listed in Appendix C; the user will add the remaining problems of the original sequence through the UI. Offer a CSV import for DSA problems (title, topic, URL, difficulty, week).
- Do not translate roadmap wording into new claims. Keep targets as ranges and keep the caveats.

## 11. Non-Functional Requirements and Testing

| **Area** | **Requirement** |
|---|---|
| **Performance** | Typical page loads under 300 ms on a laptop with a year of data. Index date and foreign-key columns. No N+1 queries (verify with query logging in tests for Today and Dashboard). |
| **Reliability** | Health endpoints, graceful startup when the DB is still booting (retry with backoff), transactional multi-row writes (attempt + review items; log + ticks), safe file writes (Section 6). |
| **Data safety** | Nightly backup option, export/import round trip, soft delete with Trash, rowversion concurrency. |
| **Privacy / security** | Localhost binding, no telemetry, no outbound calls at runtime, secrets outside the repo, upload hardening, no sensitive data in logs, `.gitignore` covers `data/`, `logs/`, `.env`. SQL Server published on 127.0.0.1 only; local-request guard (Section 6.2). |
| **Accessibility** | WCAG 2.2 AA intent: keyboard operable, labelled controls, contrast, non-color cues, reduced-motion respected. |
| **Maintainability** | Analyzers on, warnings as errors, formatting enforced in CI, small handlers, no dead code, ADR-style entries in `docs/DECISIONS.md` for every non-obvious choice. |
| **Domain tests** | Every BR-xx rule has unit tests including boundaries (pause ranges, Day 90/220 edges, travel anchor in the past, leap day, timezone midnight). |
| **Integration tests** | Testcontainers SQL Server: CRUD for every module, 409 conflict, tick evidence rejection (422), review-ladder behaviour, seed idempotency, file upload/download/Range/duplicate/blocked extension, backup-restore round trip including files. Local-request guard: cross-origin upload without the custom header returns 403, wrong Host returns 400. |
| **Frontend tests** | Vitest + RTL for the log form, attempt form, Attachments component and nudge card. Playwright smoke: log a day, log an attempt and see review dates, upload and download a file. |
| **Coverage guide** | At least 80% line coverage on Domain and Application; do not chase coverage in generated code. |
| **CI (SHOULD)** | GitHub Actions workflow: restore, build, test (Testcontainers), web lint, web test, web build. Fails on warnings. |

## 12. Repository Deliverables and Runbook

- `README.md`: prerequisites, quick start, environment variables, how to back up and restore, how to run tests, troubleshooting (SQL Server container not healthy, port conflicts, upload size errors), and the upload notice.
- `docker-compose.yml` (SQL Server with healthcheck and named volume), `.env.example` (SA password placeholder, ports), `scripts/` for run-dev, run-local, backup, restore in PowerShell and bash. Publish the SQL Server port as 127.0.0.1:1433:1433 only, and bind-mount ./data/sqlbackups to /var/opt/mssql/backups so native .bak files land on the host.
- `docs/ARCHITECTURE.md` with a Mermaid diagram, `docs/DECISIONS.md`, `CLAUDE.md`, `docs/SPEC.md`.
- Quick start must work in three commands: `docker compose up -d db`, `dotnet run --project src/SelfTracker.Api -- --migrate`, `npm --prefix web run dev` (and one command for local-prod mode: `scripts/run-local`).
- First-run experience: if the database is empty the app opens a short setup page (plan start date, DDIA edition if known, travel anchor Friday, file root) then seeds and lands on Today. Seed the roadmap's Day 1 checklist (Appendix O) as Day 1 plan tasks.

## 13. Milestones and Acceptance Criteria

Work strictly in order. Each milestone ends with: all tests green, no analyzer warnings, migrations committed, README/CLAUDE.md updated, a git commit, and a short note of decisions in `docs/DECISIONS.md`.

| **#** | **Milestone** | **Scope** | **Acceptance** |
|---|---|---|---|
| **M0** | Scaffold | Solution and projects, central package management, Docker Compose SQL Server, Serilog, ProblemDetails, OpenAPI + Scalar, health endpoints, React shell with routing, generated API client, CI, CLAUDE.md. | Quick start works from a clean clone; `/health/ready` is green; SPA shows a page that calls the API through the generated client. |
| **M1** | Foundation and Files | Settings and first-run setup, phases, pause ranges, BR-01 to BR-04 services, seeder framework, soft delete, concurrency, Trash, local-request guard, complete File storage module with Attachments component and Files library, export/import skeleton. | BR-01 to BR-04, BR-15 to BR-17 tests pass; upload, Range playback, download, duplicate detection, purge all work; seeds run twice with no changes.; a cross-site upload without the custom header is rejected. |
| **M2** | Daily log and schedule | Daily log, ticks with evidence rules, MVD checklist, time entries, gym log, schedule blocks, Today view (without review queue), heatmap, active-day metric. | BR-05 to BR-07 tests pass; log a Full day, an MVD and a Rest day; tick without evidence is rejected; heatmap and Today render.; recording a time entry first creates an In progress log. |
| **M3** | DSA | Topics, problems, attempt form with timers, counted-attempt rule, spaced review engine and queue, progress, weakest list, CSV import, timed session. | BR-08 to BR-10 tests pass; a confidence-2 attempt creates four review items; failing a review restarts the ladder; DSA tick derives correctly.; a new attempt supersedes the pending ladder; an attempt creates its derived time entry (BR-19). |
| **M4** | DDIA and design | Edition check, chapters (2e and 1e seed), reading log, chapter notes with Q&A, drills with segment timer, articulation log. | Edition banner shows in Week 2 when unconfirmed; switching edition loads the right chapters; a note links to a chapter and holds attachments. |
| **M5** | Plan, checkpoints, reviews, nudges | Plan tasks and board, exit criteria, checkpoints, re-plan log, weekly and monthly reviews with prefill, someday list, NudgeService and Today review queue. | BR-11 to BR-14 tests pass; each nudge in 4.12 can be triggered in a test; weekly review prefill matches logged data.; BR-18 derived task status matches logged problems and chapters. |
| **M6** | Flagship tracker | Build sessions and hours gauge, ADRs with Markdown export, eval runs, load-test runs, agent defects, design doc registry, Day 220 and 300 exit criteria. | ADR exports a valid `.md`; hours total per block is correct; eval and load runs chart a trend. |
| **M7** | Market and interviews | Postings, tags and analytics, targets, applications with stage events, offers compare, referrals, mocks with recordings, practice list, STAR stories, resume versions, funnel. | Required vs preferred counts are correct; funnel rates correct; a large video attached to a mock plays with scrubbing; offer without written flag shows the warning. |
| **M8** | Dashboard, search, backup, hardening | Dashboard charts, global search, full export/import round trip, automatic backup, maintenance tools, accessibility pass, Playwright smoke flows, docs complete. | Backup then restore into an empty DB yields identical data and verified files; all test suites green; README quick start verified on a clean machine. |

## 14. Non-Goals and Guardrails

- Keep the system a single deployable modular app. Do NOT add microservices, a message broker, an API gateway, a distributed cache, or a separate worker process. The roadmap itself says to add such pieces only when a measured need exists. If a background task is needed (purge, backup), use a hosted `BackgroundService`.
- Do NOT add authentication, multi-tenancy, cloud services, telemetry, AI/LLM calls or third-party SaaS integrations.
- Do NOT add features outside this document without recording the reason in `docs/DECISIONS.md`. Cut optional polish before core behaviour if time is short.
- Do NOT make the UI shame the user: no streak-loss alerts, no red failure states for missed days, no debt or catch-up wording (BR-14).
- Do NOT store or request secrets, tokens or employer material. Do NOT commit `.env`, `data/`, `logs/` or uploaded files.
- Do NOT silently change seed content meaning. If an item in Appendices A to O looks inconsistent, keep it as written and note it in `DECISIONS.md`.

## Appendices: Seed Data and Reference (from Project: Self v2.3)

These appendices reproduce the roadmap content the app must seed or display. Wording is kept close to the roadmap. Implement each as a JSON file under `seed/` (Section 10).

### Appendix A: Phases

| **#** | **Name** | **Days** | **Weeks** | **Primary objective** | **Exit evidence** |
|---|---|---|---|---|---|
| 1 | Core Gate | 1-90 | 1-13 | Build habits, DSA, DDIA, functional React, MEAI/MAF basics. | 65-80 meaningful attempts/reviews, edition-correct DDIA notes, small React app, minimal AI exercise, design doc + ADRs. |
| 2 | Flagship Build | 91-220 | 14-31 | Build and measure a lean .NET modular monolith plus bounded applied GenAI. | Working vertical slices, tests, one worker, baseline evals, demo and architecture defence. |
| 3 | Scale & Polish | 221-300 | 32-43 | Measure, deploy, observe, test and communicate the system. | Load report, CI/eval gate, case study and recorded mock reviews. |
| 4 | Interview Sprint | 301-365 | 44-52 | Convert readiness into suitable applications and interviews. | Resume, referrals, mock feedback, funnel metrics and negotiation readiness. |

### Appendix B: Checkpoints (answer in writing)

| **Day** | **Checkpoint** | **Question to answer in writing** |
|---|---|---|
| **30** | Habit | Were there 26+ active days including honestly logged MVDs? Does the gym-first evening work? |
| **60** | Foundations | Can you solve unfamiliar problems from covered patterns? Do DDIA notes match the edition you actually own? |
| **90** | Phase 1 exit | Which artifacts are complete? What must slip, and by how much? |
| **180** | Market and scope | Review 20 current postings. Do repeated requirements justify changing the track mix? |
| **195-215** | Canary interviews | Send suitable warm-up applications and aim for diagnostic screens if opportunities arise. Record funnel evidence; no count is guaranteed. (Optional learning targets: 8-10 warm-up applications, 2-3 diagnostic screens.) |
| **220** | Phase 2 exit | Does the modular monolith run? Are tests and eval numbers recorded? Can you defend the architecture for 30 minutes? |
| **270** | Readiness | Target company list, referral message, resume review and gap list ready? |
| **300** | Phase 3 exit | Load report, reproducible deployment, case study and recorded mocks complete? |
| **340** | Funnel check | What do application-to-screen and screen-to-next-stage patterns show? |
| **365** | Retrospective | What did you ship, what did interviews teach you, and what is the Year 2 plan? |

### Appendix C: Phase 1 Week-by-Week Plan (Weeks 1-13)

Seed PlanTasks from each row as described in C.1. Every seeded task stores DayFrom/DayTo from the Week column's day range (WeekFrom/WeekTo are derived for display). Seed the listed problems into `DsaProblem` under the named topic with `PlanWeek`. The DDIA column shows **2nd-edition** chapters for reference only; DDIA tasks are seeded edition-neutral (Section 4.5, Appendix D.2).

| **Week (days)** | **DSA in C#** | **DDIA 2nd edition / study track** | **Output / fallback** |
|---|---|---|---|
| **W1 (1-7)** | Arrays & Hashing: Contains Duplicate, Valid Anagram, Two Sum, Group Anagrams, Top K Frequent, Product Except Self, Valid Sudoku, Longest Consecutive. | Ch. 1 Trade-Offs in Data Systems Architecture. Begin a note on operational vs analytical systems and single-node vs distributed design. | Create repo + mistake log. Do not force all 8 problems if setup takes time. |
| **W2 (8-14)** | Two Pointers: Valid Palindrome, Two Sum II, 3Sum, Container With Most Water, Trapping Rain Water. | Ch. 2 Defining Nonfunctional Requirements. Latency percentiles, reliability, scalability, maintainability, operability. | Explain what the system must do and how success is measured. Confirm your DDIA edition. |
| **W3 (15-21)** | Stacks: Min Stack, Evaluate RPN, Generate Parentheses, Daily Temperatures, Car Fleet, Largest Rectangle; begin Binary Search. | Ch. 3 Data Models and Query Languages. Relational/document models, graphs, normalization, CQRS/event sourcing concepts. | Explain a monotonic stack; draw the domain data model. |
| **W4 (22-28)** | Binary Search: Binary Search, Search 2D Matrix, Koko, Rotated Array, Time Based KV, Median of Two Sorted Arrays. | Ch. 4 Storage and Retrieval. B-trees, LSM-trees, indexes, OLTP/analytics, vector embeddings. | Write binary-search-on-answer template; explain index choice. |
| **W5 (29-35)** | Sliding Window: Best Time to Buy/Sell Stock, Longest Substring, Longest Repeating Character Replacement, Permutation in String, Min Window. | Ch. 5 Encoding and Evolution. JSON/Avro/Protobuf, schemas, compatible dataflows. React starts: components, props, state. | Write a schema-evolution note; make one functional component. |
| **W6 (36-42)** | Linked List I: Reverse, Merge Two Sorted, Reorder, Remove Nth, Linked List Cycle, Add Two Numbers. | Ch. 6 Replication. Leaders/followers, lag, failover, multi-leader trade-offs. React: effects and forms. | Explain read-after-write and replication lag; pointer manipulation drills. |
| **W7 (43-49)** | Linked List II: Copy List with Random Pointer, Find Duplicate, LRU Cache, Merge K Sorted, Reverse Nodes in K-Group. | Ch. 7 Sharding. Partition keys, skew, rebalancing, routing. React: data fetching/routing. | Build LRU cache; explain why hot partitions happen. |
| **W8 (50-56)** | Trees I: Invert, Max Depth, Diameter, Balanced, Same Tree, Subtree, LCA of BST. | Ch. 8 Transactions. Isolation anomalies, serializability, write skew, distributed transactions. React list/detail + form. | Slow down here; explain lost updates and transaction boundaries. |
| **W9 (57-63)** | Trees II: Level Order, Right Side View, Count Good Nodes, Validate BST, Kth Smallest, Serialize/Deserialize. | Ch. 9 The Trouble with Distributed Systems. Partial failures, unreliable networks/clocks, timeouts, leases. | Explain what happens when one dependency is slow or unavailable. |
| **W10 (64-70)** | Tries; Heap/Priority Queue: Kth Largest, Top K Frequent Words, Task Scheduler, Median from Stream. | Ch. 10 Consistency and Consensus. Linearizability, logical clocks, consensus. MEAI/MAF W1: IChatClient via DI and first small agent. | Hello-agent runs with current documented package versions; use mock/local provider if hosted access blocks. |
| **W11 (71-77)** | Finish heap patterns; Backtracking: Subsets, Combination Sum, Permutations, Generate Parentheses review. | Ch. 11 Batch Processing. MAF W2: one safe function/tool call, structured output, cancellation. | One tool call with validation and tests; do not add a multi-agent design. |
| **W12 (78-84)** | Backtracking: Combination Sum II, Word Search, Palindrome Partitioning, Letter Combinations, selected weak-topic review. | Ch. 12 Stream Processing; Ch. 13 A Philosophy of Streaming Systems. Minimal RAG over a small corpus. | Retrieval baseline with citations; in-memory or local fallback if vector setup blocks progress. |
| **W13 (85-90)** | No new topics; cold-redo 8-10 weakest problems and attempt two mediums back-to-back. | Ch. 14 Doing the Right Thing. Finish notes, design doc + 5 ADRs, React and minimal AI exercise. | Phase 1 retrospective; extend only missing fundamentals, not optional tools. |

The chapter/week grouping is a pacing aid; chapters are not equal length. Keep reading order, adjust week boundaries to the actual page count, and retain a useful note. End-of-week review: for each DSA problem log pattern, key insight, what you missed, time and confidence; for DDIA write what the design choice buys, what it costs and which failure appears first. If behind, reduce new problems and cold-review key patterns before increasing volume.

#### C.1 Track split for seeding

The Output / fallback column is not a track. Store its text as OutputRequired and Fallback on the tasks of that week: DSA lines (monotonic stack, binary-search template, pointer drills, LRU cache, cold-redo) on the DSA task; DDIA lines (edition check, data model, index choice, schema evolution, replication lag, hot partitions, lost updates, slow dependencies) on the DDIA task; the rest on the matching track task below.

- **DSA:** one task per week W1-W13 (seed key task-wNN-dsa). Title from the topic, e.g. "Arrays & Hashing (8 problems)".
- **DDIA:** one task per week W1-W13 (task-wNN-ddia), titled "DDIA reading for WNN" and resolved to the confirmed edition's chapter(s) at display time (4.5).
- **React:** W5 components, props, state (output: one functional component); W6 effects and forms; W7 data fetching and routing; W8 list/detail + create/edit form calling the API (task-wNN-react).
- **MEAI/MAF:** W10 IChatClient via DI and first small agent (output: hello-agent runs with current documented package versions; fallback: mock/local provider); W11 one safe function/tool call, structured output and cancellation (output: one tool call with validation and tests; fallback: no multi-agent design); W12 minimal RAG over a small corpus (output: retrieval baseline with citations; fallback: in-memory or local) (task-wNN-meai).
- **Design and wrap-up (W13):** flagship design doc, 3-5 pages (task-w13-design-doc); five ADRs (task-w13-adrs); finish React app (task-w13-react); finish minimal AI exercise (task-w13-meai); Phase 1 retrospective (task-w13-review, track Review).

### Appendix D: DDIA Chapters

#### D.1 Second edition (14 chapters, default) with Ask-Ops connection

| **Ch.** | **Title** | **Plan week** | **Project connection / interview question** |
|---|---|---|---|
| **1** | Trade-Offs in Data Systems Architecture | W1 | Choose a simple modular monolith before distributed services; explain operational vs analytical needs. |
| **2** | Defining Nonfunctional Requirements | W2 | Define load, latency, reliability, maintainability and operational limits for Ask-Ops. |
| **3** | Data Models and Query Languages | W3 | Domain entities, relational modeling, indexes, data access patterns and event/CQRS concepts. |
| **4** | Storage and Retrieval | W4 | Storage engines, indexes, full-text/vector retrieval; justify only what the project uses. |
| **5** | Encoding and Evolution | W5 | Version API contracts or event schemas without breaking consumers. |
| **6** | Replication | W6 | Understand replication lag and stale reads; discuss before implementing replicas. |
| **7** | Sharding | W7 | Explain partition-key choice and skew conceptually; do not shard this personal-scale project. |
| **8** | Transactions | W8 | Protect inventory/order invariants, isolation and idempotent writes. |
| **9** | The Trouble with Distributed Systems | W9 | Timeouts, partial failures, clocks, retries and leases. |
| **10** | Consistency and Consensus | W10 | Know where strong consistency matters and why consensus is hard; do not implement consensus. |
| **11** | Batch Processing | W11 | Document ingestion, imports and offline processing. |
| **12** | Stream Processing | W12 | Only add stream/broker patterns if there is a concrete event flow; compare batch and streaming. |
| **13** | A Philosophy of Streaming Systems | W12 | Same as chapter 12. |
| **14** | Doing the Right Thing | W13 | Privacy, access control, retention, auditability and responsible use of business data. |

#### D.2 First edition (12 chapters, optional seed, selected when the user owns the 1st edition)

1 Reliable, Scalable, and Maintainable Applications; 2 Data Models and Query Languages; 3 Storage and Retrieval; 4 Encoding and Evolution; 5 Replication; 6 Partitioning; 7 Transactions; 8 The Trouble with Distributed Systems; 9 Consistency and Consensus; 10 Batch Processing; 11 Stream Processing; 12 The Future of Data Systems. Follow the same conceptual progression and locate each topic by heading in the user's copy; never apply 2nd-edition numbers to this book. The user can edit titles if their copy differs.

Seed ddia-chapters-1e.json with these planned weeks (the topic order matches the 2nd edition): Ch. 1 W1-W2 (covers the topics of 2e Ch. 1-2); Ch. 2 W3; Ch. 3 W4; Ch. 4 W5; Ch. 5 W6; Ch. 6 W7; Ch. 7 W8; Ch. 8 W9; Ch. 9 W10; Ch. 10 W11; Ch. 11 W12; Ch. 12 W13 (includes "Doing the Right Thing"). Copy the Ask-Ops connection from the 2nd-edition chapter on the same topic. Because a chapter can span weeks, DdiaChapter stores PlannedWeekFrom and PlannedWeekTo.

### Appendix E: Phase 2 Flagship (Ask-Ops) Scope

**Ask-Ops**: an operations platform for a fictional distributor (orders, inventory, internal knowledge) with ordinary REST endpoints and a bounded assistant that answers from a small document set (SOPs, runbooks, incident notes) and, only where justified, guarded read-only structured data. Conventional backend engineering first, applied GenAI second. Practical build budget: roughly **75-120 focused hours** across Days 91-220.

#### E.1 Milestone blocks

| **Block** | **Days** | **Build target** | **Evidence / fallback** |
|---|---|---|---|
| **2.1 Foundations** | 91-112 | Solution layout, domain modules, PostgreSQL, migrations, tests, CI, health checks, structured logs. One API process. | Reproducible local run. If Docker setup blocks, run API + local database first and document the environment. |
| **2.2 Core behavior** | 113-133 | CRUD/domain rules, validation, ProblemDetails, pagination, authorization, integration tests. Add one worker for an identified async task. | A vertical slice from request to database with tests. No gateway or broker. |
| **2.3 Reliability/data** | 134-154 | Indexes/query plans, cancellation, timeouts, bounded retries only where safe, traces and failure tests. | One measured before/after improvement. Add cache only for a proven hot path. |
| **2.4 AI layer** | 155-182 | Document ingestion, metadata, chunking, retrieval baseline, citations and one safe read-only tool. | Start with 20-50 documents and known questions; skip reranking until the baseline shows a problem. |
| **2.5 UI/integration** | 183-203 | Minimal React UI or API demo; auth/error/loading states. Python starts as a small HTTP evaluation client. | Freeze styling. No Python sidecar or gRPC service. |
| **2.6 Eval/demo** | 204-220 | 20-case golden set, retrieval/answer review, README, architecture diagram, demo and Canary learning notes. | Expand only if time remains; document limitations and failures honestly. |

#### E.2 Initial architecture choices to defend (do not add complexity until the trigger is met)

| **Concern** | **Start with** | **Add only when** |
|---|---|---|
| **Application** | One ASP.NET Core API / modular monolith (Orders, Inventory, Knowledge/Ask-Ops). | A module has distinct scaling, security, release or ownership needs. |
| **Data** | One PostgreSQL database; explicit migrations and indexes; vector only if retrieval needs it. | Measured need for more. |
| **Background work** | One hosted worker; DB-backed work record/outbox only if a failure mode requires it. | Independent scaling, multiple consumers, durable buffering or backpressure is needed. |
| **Broker** | None. | Measurements or clear reliability needs; then document delivery, retry, ordering and idempotency. |
| **API gateway** | None. | Multiple independent services, edge routing, centralized policy or separately managed clients. |
| **Cache** | No distributed cache. | A measured hot-read bottleneck exists and invalidation/staleness is understood. |
| **Reads vs writes** | EF Core for writes; Dapper/projections where query needs justify it. | Measured query plans or latency show an issue. |
| **Security** | Parameterized queries, explicit authorization, least privilege, audit events; treat model output as untrusted. | Never permit arbitrary model-generated SQL writes. |

Note for the tracker: Ask-Ops uses PostgreSQL in the roadmap. The **tracker app** uses SQL Server as the user requested; they are separate projects.

### Appendix F: Other Tracks and Phases 3-4

#### F.1 Phase 1 tracks

- **DSA** in C#: about 5-6 new problems per week when stable; 65-80 meaningful attempts/reviews by Day 90; keep the mistake log.
- **DDIA**: edition-aware map (Appendix D); 7-10 pages/day as a guide; one note per chapter.
- **React (Weeks 5-8)**: small functional SPA with list/detail and a create/edit form calling the API; state and error handling matter more than styling.
- **MEAI + MAF (Weeks 10-13)**: IChatClient and DI, then one function/tool call and a minimal agent/RAG loop; check current docs and exact stable NuGet versions before coding.
- **Flagship design**: 3-5 page design doc (domain, API, data model, boundaries, threats, failure modes) and five ADRs. Keep employer code and data out of the repo.

#### F.2 Phase 3 workstreams (Days 221-300)

| **Weeks / days** | **Workstream** | **Concrete tasks** | **Evidence produced** |
|---|---|---|---|
| **W32-34 / 221-241** | Baseline and bottlenecks | Run k6 against real project journeys; inspect SQL plans, serialization, connection pool, worker backlog, retrieval latency. Change one variable at a time. | Before/after RPS, p50/p95/p99, error rate, dataset, machine and test profile. |
| **W35-37 / 242-262** | Production-shaped deployment | Harden Dockerfile, CI build/test/scan, secrets, health/readiness, logs/traces, reproducible deploy. | Live URL or repeatable local deploy, pipeline run, dashboard/export and limits. |
| **W38-40 / 263-283** | Reliability and evaluation | Failure injection, retry/cancellation tests, regression gate for golden set, documented operational procedure. | CI quality/eval gate; failure runbook; eval delta. |
| **W41-43 / 284-300** | Case study and mocks | Write architecture case study, record demo, do system-design and project walkthrough mocks; improve weak areas. | Case study, updated README/resume bullets, 6-8 reviewed mock recordings if schedule allows. |

#### F.3 Phase 4 workstreams (Days 301-365; applications may start earlier)

| **Workstream** | **Weekly guide** | **Notes** |
|---|---|---|
| **Timed DSA** | 5-7 h when ready | Company-tagged practice only for genuine target roles. Two timed problems in a session can be enough; review mistakes. |
| **System design mocks** | 1-2; 2-3 stretch | Alternate classic backend design and bounded RAG/LLM integration. Record and review. |
| **Behavioral / STAR** | 2 short sessions | Rehearse out loud; concise 90-second stories with specific ownership and outcomes. |
| **Applications** | 6-10 targeted where role fit exists | Tailor to real requirements; quality over spray. Reduce volume in work/travel-heavy weeks. |
| **Referral asks** | 3-5 where appropriate | Specific and respectful. No mass messaging or entitlement to a referral. |
| **Funnel review** | 30 min weekly | Track applications, screens, next stages, offers and recurring rejection patterns. |

- Resume headline: Backend Software Engineer | .NET, Distributed Systems, Applied GenAI. Do not use an "AI Engineer" label the experience cannot support.
- Offers: compare written fixed/variable pay, benefits, scope, manager/team, location, joining bonus, notice terms and delayed-joining risk. Never resign on a verbal assurance. Do not resign without a signed offer, clear joining terms and a realistic notice-period plan.
- Honest-scale answer template: "This ran at personal scale; here are the test conditions, the measured bottleneck and what I would measure before scaling."

### Appendix G: Exit Criteria (seed as checklists)

#### Phase 1 (Day 90)

- 65-80 meaningful DSA attempts/reviews.
- Edition-correct DDIA notes for chapters covered.
- Small functional React app (list/detail + create/edit form calling your API).
- Minimal MEAI/MAF exercise (hello-agent, one safe tool call, minimal RAG baseline).
- Flagship design document (3-5 pages) and five ADRs.

#### Phase 2 (Day 220)

| **Criterion** | **Evidence to show** |
|---|---|
| **Reproducible backend** | One modular-monolith API + database + one worker runs from documented steps; tests pass; key domain flow works. |
| **Reliability** | Health/log/trace evidence, clear timeout and retry policy, at least one injected failure and observed outcome. |
| **Ask-Ops** | Small document corpus, retrieval with citations, one guarded tool and graceful model/tool failure. |
| **Evaluation** | Repeatable golden set and baseline; known limitations and human spot-check findings written down. |
| **Demo / explanation** | Five-minute demo, architecture diagram, honest README and 30-minute spoken walkthrough. |
| **DSA** | About 120-135 selected attempts/reviews; cold re-solves and pattern coverage matter more than raw count. |

#### Phase 3 (Day 300)

- Load report with before/after numbers and test conditions.
- Reproducible deployment and CI quality/eval gate.
- Architecture case study and updated README/resume bullets.
- Recorded mock reviews (6-8 if schedule allows).

#### Day 270 readiness review

- Resume reviewed against real roles; each technical claim backed by code, a test, a measurement or a clear explanation.
- Target list includes role fit, location, experience requirement, fixed/variable compensation and application link/date.
- At least two full project walkthroughs and several design/DSA mock reviews recorded; weak points converted into a weekly practice list.

#### Phase 4 (Day 365)

- Resume, referrals, mock feedback, funnel metrics and negotiation readiness.
- Day 365 retrospective answered in writing.

### Appendix H: System Design Drills (24)

One per week from around Day 80. 45-minute framework: clarify requirements/scale (5), estimate load/storage (5), API and data model (8), high-level design (10), two hardest deep dives (10), bottlenecks, failure modes and trade-offs (7). Write drills 1-8 in full; record later drills as spoken mocks. Seed PlannedWeek = 11 + drill number: drill 1 in W12 (Days 78-84, nearest Day 80) through drill 24 in W35. Drills shift with the plan like everything else and stay editable.

| **Drills** | **Prompts** |
|---|---|
| **1-4** | 1 URL shortener; 2 rate limiter; 3 key-value store; 4 distributed cache. |
| **5-8** | 5 Notification service; 6 chat/messaging; 7 news feed; 8 search autocomplete. |
| **9-12** | 9 File storage; 10 video streaming basics; 11 location/ride matching; 12 payments and ledger. |
| **13-16** | 13 Job scheduler; 14 logs/metrics aggregation; 15 web crawler; 16 API gateway. |
| **17-20** | 17 Ticket booking/seat concurrency; 18 e-commerce order pipeline; 19 RAG Q&A; 20 LLM gateway with cost control. |
| **21-24** | 21 Leaderboard; 22 unique ID generator; 23 recommendation feed; 24 distributed lock/leader election. |

#### Articulation training (seed as recurring prompts)

- Think aloud on every DSA problem: restate, examples, brute force, optimize, complexity.
- Give each DDIA chapter and ADR a 90-second explanation; record and review for filler words and missing trade-offs.
- Weekly "why X over Y" drills: queue vs direct call, Dapper vs EF Core, pgvector vs keyword search. Defend the choice and state when the alternative wins.
- Answer structure: answer first, reasoning, trade-offs, evidence you would measure.

### Appendix I: Daily Schedule Blocks and Gym Plan

Seed into `ScheduleBlock`. Shift only for commute, work overruns or travel; keep the sequence stable. Run one phase for at least two weeks before changing it. Phase B is a progressive transfer of DSA to the morning, not a second routine to alternate randomly.

#### Phase A: Cold Start (Weeks 1-3): gym first, then study

| **Time** | **Block** | **Rule** |
|---|---|---|
| 07:30 | Wake + morning prep | 500 ml water, daylight, breakfast and lunch prep; no morning study requirement yet. |
| 09:00-17:30 | Office | Lunch around 13:30. Stand/walk periodically; do not use office equipment for personal project work. |
| 17:45-18:15 | Return + snack / change | Change first; small pre-workout snack and water. |
| 18:15-19:05 | Gym (3 sessions/week) | As per the separately reviewed training plan. Tue/Thu can be a short walk. |
| 19:05-20:00 | Return, shower, dinner | Eat and reset. Do not open a long scrolling session. |
| 20:15-21:15 | DSA | One problem/review with the four-pass method; log one mistake. |
| 21:15-21:40 | DDIA / design | About 7 pages plus a three-line note. |
| 21:40-21:55 | Build | 15 minutes: one commit, one doc line or one failing test fixed. |
| 21:55-22:10 | Close day | Tracker, tomorrow target, breakfast/lunch prep. Screens off; lights out by 23:15 target. |

#### Phase B: Build (Weeks 4-8)

| **Time** | **Block** | **Rule** |
|---|---|---|
| 07:15 (W4); 07:00 (W5-8) | Morning prep + DSA attempt | 10 min mobility only if already cleared; 30 min unaided DSA attempt. Rewrite/log moves to evening. |
| 18:15-19:05 | Gym (4-day split) | Use the split in the Daily Operating Schedule; clinician/physio clearance controls lower-body activity. |
| 20:15-20:30 | DSA rewrite + log | Close solution, re-code in C#, record pattern and bug. |
| 20:30-21:10 | DDIA / design | 7-10 pages or a focused design note. |
| 21:15-22:15 | Build track | One small committed output. Lights out target around 23:00. |

#### Phase C: Full schedule (Week 9 onward)

| **Time** | **Block** | **Rule** |
|---|---|---|
| 06:30 | Wake | 500 ml water and daylight. Keep bedtime early enough for at least 7 hours of sleep. |
| 06:50-07:50 | DSA | Protected C# practice: attempt, unstick, rewrite, log. Default DSA slot from Week 9. |
| 09:00-17:30 | Office | Normal workday; hourly movement breaks where practical. |
| 18:15-19:05 | Gym / training | Fixed training anchor. Clinical clearance controls exercise choice. |
| 19:30-20:10 | Dinner / recovery | Shower, dinner and recovery. Avoid pushing study into sleep time. |
| 20:15-20:35 stable; 20:15-20:55 stretch | DDIA / system design | Stable: about 20 min + a short note. Stretch: about 40 min / 7-10 pages. After the book, a design drill or ADR defence. |
| 20:40-21:25 stable; 21:00-22:25 stretch | Main track | Phase 1: React / MEAI / MAF; Phase 2: flagship; Phase 3: scale/eval; Phase 4: interviews. Stable: 45 min on 3 evenings. |
| 21:25-22:10 stable; 22:25-22:40 stretch | Close / shutdown | Tracker, tomorrow targets, prep and sleep routine. Stretch only when rested. |

#### Weekly template and gym plan

| **Day** | **Study** | **Body / schedule** |
|---|---|---|
| **Mon-Thu** | DSA most days; DDIA/design 2-4 short blocks; build 2-3 evenings. | 18:15 training: Mon upper push, Tue lower A, Wed optional Zone 2 (Phase C), Thu upper pull. |
| **Friday (normal week)** | Short DSA and review; stop at a sensible hour. | 18:15 Lower B in the Phase B/C split only if cleared; prepare for travel if due. |
| **Saturday (home week)** | Timed DSA + review; one 90-150 min flagship deep-work block in Phase 2. | Optional Zone 2/core in Phase C; otherwise protect recovery. |
| **Sunday** | Light review, next-week setup; weekly review 30 min. | Rest and personal time. |
| **Edappal Fri-Mon (alternate weeks)** | Preplanned MVD Friday; Saturday timed attempt; Sunday review; Monday short review. | No deep build expected; no make-up hours. |

Seed `GymPlanDay`: Phase A = 3 sessions/week (suggested Mon/Wed/Fri, Tue/Thu short walk; user-editable). Phase B/C = Mon Upper push, Tue Lower A, Thu Upper pull, Fri Lower B; Phase C adds optional Zone 2 on Wed and Sat. Lower-body and added cardio follow clinician/physio guidance; the app only records what was done.

### Appendix J: Hours, Targets and Metrics

#### J.1 Graduated workload

| **Stage** | **Weekly study target** | **What to protect** |
|---|---|---|
| **Weeks 1-3 / Phase A** | 60-90 min on most weekdays + one short weekend session | Make the evening routine repeatable; 3 gym sessions as cleared. |
| **Weeks 4-8 / Phase B** | Build toward 8-11 focused hours | Short morning DSA attempt; fixed 18:15 gym; evening rewrite/DDIA/build. |
| **Week 9 onward / Phase C** | 12-15 h stable; 15-17 h ambitious | Morning DSA, gym at 18:15, two to three evening build blocks and Sunday review. |
| **19-20 hours** | Stretch only | Never treat this as a debt or trade sleep/current-job performance for it. |

#### J.2 Weekly hours by phase (seed WeeklyHourTarget)

| **Phase** | **Target distribution** | **Travel adjustment** |
|---|---|---|
| **1. Core Gate** | DSA 4-6 h; DDIA 2-3 h; main track 3-5 h; review 1 h. | On travel weekends keep DSA and notes; reduce React/MAF build scope. |
| **2. Flagship Build** | DSA 4-5 h; design 1-2 h; flagship about 5-8 h when a normal week permits; review/outreach 1 h. | Protect non-travel Saturdays; thin vertical slices. |
| **3. Scale & Polish** | DSA 4-6 h; mocks 2 h; load/deploy/evals 4-6 h; review 1 h. | Cut polish and optional infrastructure before measurable reliability work. |
| **4. Interview Sprint** | DSA 5-7 h; system design 2-3 h; applications/mocks 3-5 h. | Apply to suitable roles when ready; keep travel weeks lighter. |

Phase 1 distribution applies from Week 9; Weeks 1-8 follow the graduated totals in J.1 (BR-12). Seed WeeklyHourTarget with this category mapping: Phase 1: DSA 4-6, DDIA / design 2-3, Main track 3-5, Review 1. Phase 2: DSA 4-5, DDIA / design 1-2, Main track 5-8 (flagship), Review + Applications 1 (review/outreach). Phase 3: DSA 4-6, Mocks 2, Main track 4-6 (load/deploy/evals), Review 1. Phase 4: DSA 5-7, DDIA / design 2-3 (system design), Mocks + Applications 3-5. "Other" has no target. Totals: Phase A about 4-8 h, Phase B 8-11 h, from Week 9 12-15 h stable, 15-17 h ambitious, 19-20 h stretch only.

#### J.3 Metrics and honest caveats

| **Metric** | **Target / checkpoint** | **Honest caveat** |
|---|---|---|
| **Active days per 30** | Aim for 26 including MVDs honestly. | A tick measures behavior, not mastery. |
| **DSA cumulative** | 65-80 by Day 90; about 120-135 selected by Day 220. | Cold re-solves and pattern coverage matter more than raw count. |
| **DDIA notes** | Useful notes for chapters in your actual edition by Day 90-100. | Can you explain a concept in about a minute? Do not speed-read to hit a chapter number. |
| **Flagship artifacts** | Meaningful commits/tests/ADRs during active build blocks. | A working vertical slice is the truth; commit count alone is not achievement. |
| **Mock interviews** | Ramp to 1-2/week in final phases if sustainable. | Reviewing the recording creates the learning. |
| **Application funnel** | Log role fit, callbacks, screens and next-stage outcomes. | Hiring outcomes are noisy and not solely under your control. |
| **Training / recovery** | Follow the approved gym schedule and log pain/recovery where relevant. | The roadmap allocates time; it does not medically clear an exercise or prescribe rehabilitation. |

### Appendix K: Risks and If/Then Rules (seed as a read-only Rules page; used by nudges)

| **If this happens...** | **Then do this** | **Never do this** |
|---|---|---|
| **Miss one day** | MVD tomorrow; log it and move on. | Wait until Monday to restart. |
| **Fall 2+ weeks behind** | Cut optional infra, UI polish and features; preserve core DSA/DDIA/review. | Push every milestone silently or work all night. |
| **Phase 2 build is too large** | Return to one modular monolith + one worker; ship one end-to-end slice. | Add a broker, gateway or microservices to make the diagram look impressive. |
| **DSA plateau 3+ weeks** | Choose one weak pattern; practise it and spaced reviews for two weeks. | Switch languages/platforms out of frustration. |
| **Workload spikes / urgent release** | Use MVDs on weekdays; move build work to a suitable non-travel weekend. | Let one busy week become a month-long stop. |
| **Illness, family event or travel** | Rest where required; use MVD only if practical; ramp back over 2-3 days. | Repay missed hours with a binge. |
| **Urge to switch stacks/course** | Put it on the someday list and revisit at monthly review. | Spend another week comparing roadmaps. |
| **Canary screens go poorly / no callbacks** | Inspect role fit, resume top third, ATS terminology and the weak interview stage. | Treat a small sample as a verdict on ability. |
| **MAF / MEAI package changes** | Check official docs/release notes; keep pinned versions and a thin application boundary; use a deterministic mock fallback. | Rewrite the whole system mid-phase. |
| **Good offer arrives early** | Compare written terms, role, runway, notice and joining date; decide from facts. | Reject or accept just to obey the calendar. |
| **Signals: retention drops; dread, irritability or poor sleep persists** | Reduce volume and restore cold recall; take a lighter week and shrink the third block first. | Treat the plan as a second full-time job you cannot recover from. |

### Appendix L: Operating Rules, Tick Criteria, MVD and Rituals

#### L.1 The ten rules (seed as a read-only Rules page)

| **#** | **Rule** | **How to apply it** |
|---|---|---|
| 1 | DSA consistently before passive input | Keep the fixed slot from your phase schedule. Make a genuine 25-30 minute attempt before hints. |
| 2 | One flagship, not five | Depth you can explain beats several shallow demos. Put new ideas on the someday list. |
| 3 | Output over input | Every block ends with a note, solved problem, commit, test, diagram or ADR. |
| 4 | Free-first | Do not buy a course or subscription until a specific gap is proven and the expense is safe. |
| 5 | Never miss twice | Use an MVD on the next available day. Do not wait for Monday or restart the 365-day clock. |
| 6 | Tracker is the truth | Mark full sessions and MVDs separately. Never claim a task you did not do. |
| 7 | AI is a power tool, not a DSA crutch | Use coding agents for the flagship, but review diffs. Attempt DSA unaided first. |
| 8 | Be honest about scale | State dataset, hardware, traffic pattern and test conditions. Do not claim production-scale results from a personal demo. |
| 9 | Cut scope, not consistency | Cut optional broker/gateway/cache, polish and extra features before core learning. |
| 10 | Review weekly | Spend 30 minutes reviewing learning, outputs, sleep, travel and job evidence. Adjust to trends, not one bad week. |

These rules do not override sleep, health, current-job obligations or clinician guidance.

#### L.2 What earns each tracker tick (show beside the tick controls)

| **Tick** | **Counts as done when...** | **Does not count** |
|---|---|---|
| **DSA reps** | One genuine attempt for 25+ minutes, followed by a mistake-log line; a scheduled cold review also counts. | Watching a solution, reading an editorial or copying code. |
| **System design / core skill** | A focused DDIA, design, React or MEAI/MAF block ends with a note, diagram or code artifact. | Scrolling or passive reading with no output. |
| **Build / ship** | A meaningful commit, test, design/ADR change, PR or deployed artifact on the current target. | Planning, or changing config for an hour without a durable result. |

#### L.3 Minimum Viable Day and rituals

- **MVD (45-75 minutes)**: one DSA attempt or cold review (20-30 min), 15-20 minutes of DDIA/core with a three-line note, 10-15 minutes on the build or one tiny test/commit. On a packed travel day, DSA review plus a short written note is enough. Resume normally; do not repay hours.
- **Five-minute start ritual**: open the tracker; read the current week target; write one DSA target and one build target; put the phone away; start the timer.
- **Close day**: update tracker, set tomorrow targets, prepare breakfast/lunch, screens off.
- **Daily review (2 min)**: record full/MVD/rest, what happened, tomorrow's DSA target and next smallest build task.

### Appendix M: Templates (use as form layouts and prefilled text)

| **Template** | **Fields** |
|---|---|
| **A. Daily log (three lines)** | Date / Day #; Full / MVD / Rest; Energy 1-5. DSA: problem, pattern, attempt time, confidence 1-5, key error. Core block: what I studied and artifact. Build: commit / test / design change. Tomorrow: DSA target and next smallest build target. |
| **B. DSA mistake log (one row per problem)** | Date, Problem, Pattern, Key insight, What I missed, Time, Confidence 1-5, Review dates (+1d / +3d / +7d / +21d). |
| **C. DDIA chapter note (one page)** | Edition and chapter title at the top. Five key ideas in your own words. Trade-offs: Option A vs B, what you gain, what you pay and when each wins. Failure modes: what goes wrong and how it appears in a real system. "In Ask-Ops I will..." one concrete decision this chapter changes. Three interview questions with 60-second spoken answers. |
| **D. Weekly review (Sunday, 30 min)** | Active days / MVDs. Energy / sleep / mood 1-5. DSA attempts, cold re-solves, median time, repeated mistakes. DDIA chapter/title + note. Build shipped, blocked, next smallest slice. Gym sessions per schedule. Recovery / symptoms needing review. Travel next fortnight. 20-posting market check: repeated requirement. Start / Stop / Keep. |
| **E. STAR story skeleton (target 90 seconds)** | Situation (one sentence) -> Task (what was yours specifically) -> Action (three concrete steps; say "I", not "we") -> Result (a real number if possible) -> Reflection (what you would change). Prepare 8-10 stories mapped to ownership, debugging, conflict, learning and reliability. |
| **F. Referral / Canary outreach message** | Hi [Name], I am a backend engineer working mainly with C#/.NET. I have built a personal project combining a data-backed API with a bounded RAG assistant, and documented its tests and evaluation results ([link]). I noticed [Team] is hiring for [Role] ([link]). Would you be open to sharing how your team evaluates candidates or referring me if the fit looks appropriate? Thank you either way. |
| **G. One-page ADR** | Title: ADR-00X: [Decision]. Status & date: Proposed / Accepted / Superseded, date, context scale (documents, requests/sec, test environment). Context: which requirement, query pattern, consistency condition or threat forced a choice. Options: A vs B vs C with measured latency, complexity, cost, security and failure trade-offs. Decision: why this wins now; what interface isolates it. Revisit trigger: a measurable threshold and the exact test conditions; avoid universal scale thresholds. References: official sources used, version numbers and date checked. |

### Appendix N: Kickoff Prompt (paste into Claude Code)

> `Read docs/SPEC.md (converted from the attached Word spec) completely before writing code.`
> `Build the Project Self Tracker exactly as specified, milestone by milestone (M0 to M8).`
> `Use .NET 10, EF Core, SQL Server (Docker Compose), React 19 + TypeScript + Vite.`
> `Verify and pin the latest stable package versions yourself. Do not ask me questions`
> `unless you are blocked; record every assumption in docs/DECISIONS.md.`
> `Create CLAUDE.md in M0. After each milestone: run all tests, update README, commit.`
> `Start now with M0. Stop after M2 and show me how to run it so I can start logging.`

Tip: if the session starts mid-way, point Claude Code at `docs/SPEC.md` and `CLAUDE.md`, say which milestone is next, and let it continue.

### Appendix O: Day 1 Checklist (seed as Day 1 plan tasks)

- Confirm which DDIA edition you own from the title/copyright page (sets DdiaEdition; the default chapter map is the 2nd edition).
- Install the current .NET LTS for the personal portfolio (.NET 10); keep any employer-required target framework for work projects.
- Create the C# DSA repository with a mistake-log file. Start Arrays & Hashing with Contains Duplicate and Valid Anagram; attempt unaided for 25 minutes.
- Read 7-10 DDIA pages, or less if that allows a meaningful three- to five-line note.
- Use Phase A from the Daily Operating Schedule: gym at 18:15 on training days and DSA after dinner; do not add a second daily timetable.
- Open the tracker, distinguish full sessions from MVDs, and use the Someday page (replaces someday-list.md) for new tools and course ideas.