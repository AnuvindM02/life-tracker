"""Generate seed/*.json from the build spec appendices (Project: Self v2.3 via spec v1.1).

Run from the repo root: python scripts/gen-seed.py
The JSON files are the source of truth at runtime; this script only documents how they were produced.
"""
import json


def dump(name, rows, note):
    with open(f"seed/{name}.json", "w", encoding="utf8", newline="\n") as fh:
        json.dump({"list": name, "source": f"Project: Self v2.3 via build spec v1.1, {note}", "rows": rows}, fh, indent=2, ensure_ascii=False)
        fh.write("\n")


phases = [
    dict(seedKey="phase-1", number=1, name="Core Gate", dayFrom=1, dayTo=90, weekFrom=1, weekTo=13,
         objective="Build habits, DSA, DDIA, functional React, MEAI/MAF basics.",
         exitEvidence="65-80 meaningful attempts/reviews, edition-correct DDIA notes, small React app, minimal AI exercise, design doc + ADRs."),
    dict(seedKey="phase-2", number=2, name="Flagship Build", dayFrom=91, dayTo=220, weekFrom=14, weekTo=31,
         objective="Build and measure a lean .NET modular monolith plus bounded applied GenAI.",
         exitEvidence="Working vertical slices, tests, one worker, baseline evals, demo and architecture defence."),
    dict(seedKey="phase-3", number=3, name="Scale & Polish", dayFrom=221, dayTo=300, weekFrom=32, weekTo=43,
         objective="Measure, deploy, observe, test and communicate the system.",
         exitEvidence="Load report, CI/eval gate, case study and recorded mock reviews."),
    dict(seedKey="phase-4", number=4, name="Interview Sprint", dayFrom=301, dayTo=365, weekFrom=44, weekTo=52,
         objective="Convert readiness into suitable applications and interviews.",
         exitEvidence="Resume, referrals, mock feedback, funnel metrics and negotiation readiness."),
]
dump("phases", phases, "Appendix A")


def wdays(w):
    f = 1 + (w - 1) * 7
    return f, (90 if w == 13 else f + 6)


DSA = {
    1: ("Arrays & Hashing", "Contains Duplicate, Valid Anagram, Two Sum, Group Anagrams, Top K Frequent, Product Except Self, Valid Sudoku, Longest Consecutive.", "Create repo + mistake log.", "Do not force all 8 problems if setup takes time."),
    2: ("Two Pointers", "Valid Palindrome, Two Sum II, 3Sum, Container With Most Water, Trapping Rain Water.", None, None),
    3: ("Stacks", "Min Stack, Evaluate RPN, Generate Parentheses, Daily Temperatures, Car Fleet, Largest Rectangle; begin Binary Search.", "Explain a monotonic stack.", None),
    4: ("Binary Search", "Binary Search, Search 2D Matrix, Koko, Rotated Array, Time Based KV, Median of Two Sorted Arrays.", "Write binary-search-on-answer template.", None),
    5: ("Sliding Window", "Best Time to Buy/Sell Stock, Longest Substring, Longest Repeating Character Replacement, Permutation in String, Min Window.", None, None),
    6: ("Linked List I", "Reverse, Merge Two Sorted, Reorder, Remove Nth, Linked List Cycle, Add Two Numbers.", "Pointer manipulation drills.", None),
    7: ("Linked List II", "Copy List with Random Pointer, Find Duplicate, LRU Cache, Merge K Sorted, Reverse Nodes in K-Group.", "Build LRU cache.", None),
    8: ("Trees I", "Invert, Max Depth, Diameter, Balanced, Same Tree, Subtree, LCA of BST.", None, None),
    9: ("Trees II", "Level Order, Right Side View, Count Good Nodes, Validate BST, Kth Smallest, Serialize/Deserialize.", None, None),
    10: ("Tries; Heap / Priority Queue", "Tries; Kth Largest, Top K Frequent Words, Task Scheduler, Median from Stream.", None, None),
    11: ("Heap patterns; Backtracking", "Finish heap patterns; Subsets, Combination Sum, Permutations, Generate Parentheses review.", None, None),
    12: ("Backtracking", "Combination Sum II, Word Search, Palindrome Partitioning, Letter Combinations, selected weak-topic review.", None, None),
    13: ("Cold redo", "No new topics; cold-redo 8-10 weakest problems and attempt two mediums back-to-back.", "Cold-redo 8-10 weakest problems; two mediums back-to-back.", None),
}

# Edition-neutral topics: no chapter numbers (spec v1.1, 4.5).
DDIA = {
    1: ("Trade-offs in data systems architecture. Begin a note on operational vs analytical systems and single-node vs distributed design.", None),
    2: ("Defining nonfunctional requirements: latency percentiles, reliability, scalability, maintainability, operability.", "Explain what the system must do and how success is measured. Confirm your DDIA edition."),
    3: ("Data models and query languages: relational/document models, graphs, normalization, CQRS/event sourcing concepts.", "Draw the domain data model."),
    4: ("Storage and retrieval: B-trees, LSM-trees, indexes, OLTP/analytics, vector embeddings.", "Explain index choice."),
    5: ("Encoding and evolution: JSON/Avro/Protobuf, schemas, compatible dataflows.", "Write a schema-evolution note."),
    6: ("Replication: leaders/followers, lag, failover, multi-leader trade-offs.", "Explain read-after-write and replication lag."),
    7: ("Sharding / partitioning: partition keys, skew, rebalancing, routing.", "Explain why hot partitions happen."),
    8: ("Transactions: isolation anomalies, serializability, write skew, distributed transactions.", "Slow down here; explain lost updates and transaction boundaries."),
    9: ("The trouble with distributed systems: partial failures, unreliable networks/clocks, timeouts, leases.", "Explain what happens when one dependency is slow or unavailable."),
    10: ("Consistency and consensus: linearizability, logical clocks, consensus.", None),
    11: ("Batch processing.", None),
    12: ("Stream processing and the philosophy of streaming systems.", None),
    13: ("Doing the right thing: privacy, access control, retention, auditability. Finish chapter notes.", "Phase 1 retrospective; extend only missing fundamentals, not optional tools."),
}

REACT = {
    5: ("React: components, props, state", "Make one functional component."),
    6: ("React: effects and forms", None),
    7: ("React: data fetching and routing", None),
    8: ("React: list/detail + create/edit form calling the API", "Functional state and error handling matter more than styling."),
}

MEAI = {
    10: ("MEAI/MAF W1: IChatClient via DI and first small agent", "Hello-agent runs with current documented package versions.", "Use a mock/local provider if hosted access blocks."),
    11: ("MEAI/MAF W2: one safe function/tool call, structured output and cancellation", "One tool call with validation and tests.", "Do not add a multi-agent design."),
    12: ("MEAI/MAF: minimal RAG over a small corpus", "Retrieval baseline with citations.", "In-memory or local fallback if vector setup blocks progress."),
}

tasks = []


def add(**t):
    t.setdefault("kind", "Manual")
    t.setdefault("status", "NotStarted")
    tasks.append(t)


for w in range(1, 14):
    f, t = wdays(w)
    title, probs, out, fb = DSA[w]
    add(seedKey=f"task-w{w:02d}-dsa", phaseNumber=1, track="Dsa", kind="DerivedDsa", planWeek=w,
        title=f"DSA W{w}: {title}", description=probs, outputRequired=out, fallback=fb, dayFrom=f, dayTo=t, sortOrder=1)
    topic, dout = DDIA[w]
    add(seedKey=f"task-w{w:02d}-ddia", phaseNumber=1, track="Ddia", kind="DerivedDdia", planWeek=w,
        title=f"DDIA reading for W{w}", description=topic, outputRequired=dout,
        fallback="Pacing aid only: keep reading order, adjust week boundaries to the actual page count, keep a useful note.",
        dayFrom=f, dayTo=t, sortOrder=2)
    if w in REACT:
        ti, o = REACT[w]
        add(seedKey=f"task-w{w:02d}-react", phaseNumber=1, track="React", planWeek=w, title=ti, outputRequired=o, dayFrom=f, dayTo=t, sortOrder=3)
    if w in MEAI:
        ti, o, fb2 = MEAI[w]
        add(seedKey=f"task-w{w:02d}-meai", phaseNumber=1, track="MeaiMaf", planWeek=w, title=ti, outputRequired=o, fallback=fb2, dayFrom=f, dayTo=t, sortOrder=3)

f, t = wdays(13)
add(seedKey="task-w13-design-doc", phaseNumber=1, track="Design", planWeek=13, title="Flagship design document (3-5 pages)",
    description="Domain, API, data model, boundaries, threats and failure modes. Keep employer code and data out of the repo.", dayFrom=f, dayTo=t, sortOrder=4)
add(seedKey="task-w13-adrs", phaseNumber=1, track="Design", planWeek=13, title="Write five ADRs",
    description="Use the one-page ADR template (Appendix M, G).", dayFrom=f, dayTo=t, sortOrder=5)
add(seedKey="task-w13-react", phaseNumber=1, track="React", planWeek=13, title="Finish the small functional React app",
    outputRequired="List/detail and a create/edit form calling your API.", dayFrom=f, dayTo=t, sortOrder=6)
add(seedKey="task-w13-meai", phaseNumber=1, track="MeaiMaf", planWeek=13, title="Finish the minimal AI exercise",
    outputRequired="Hello-agent, one safe tool call, minimal RAG baseline.", dayFrom=f, dayTo=t, sortOrder=7)
add(seedKey="task-w13-review", phaseNumber=1, track="Review", planWeek=13, title="Phase 1 retrospective",
    outputRequired="Extend only missing fundamentals, not optional tools.", dayFrom=f, dayTo=t, sortOrder=8)

FLAGSHIP = [
    ("2-1", "2.1 Foundations", 91, 112, "Solution layout, domain modules, PostgreSQL, migrations, tests, CI, health checks, structured logs. One API process.", "Reproducible local run.", "If Docker setup blocks, run API + local database first and document the environment."),
    ("2-2", "2.2 Core behavior", 113, 133, "CRUD/domain rules, validation, ProblemDetails, pagination, authorization, integration tests. Add one worker for an identified async task.", "A vertical slice from request to database with tests.", "No gateway or broker."),
    ("2-3", "2.3 Reliability/data", 134, 154, "Indexes/query plans, cancellation, timeouts, bounded retries only where safe, traces and failure tests.", "One measured before/after improvement.", "Add cache only for a proven hot path."),
    ("2-4", "2.4 AI layer", 155, 182, "Document ingestion, metadata, chunking, retrieval baseline, citations and one safe read-only tool.", "Start with 20-50 documents and known questions.", "Skip reranking until the baseline shows a problem."),
    ("2-5", "2.5 UI/integration", 183, 203, "Minimal React UI or API demo; auth/error/loading states. Python starts as a small HTTP evaluation client.", "Freeze styling.", "No Python sidecar or gRPC service."),
    ("2-6", "2.6 Eval/demo", 204, 220, "20-case golden set, retrieval/answer review, README, architecture diagram, demo and Canary learning notes.", "Document limitations and failures honestly.", "Expand only if time remains."),
]
for i, (k, ti, f, t, d, o, fb) in enumerate(FLAGSHIP):
    add(seedKey=f"task-flagship-{k}", phaseNumber=2, track="Flagship", title=f"Ask-Ops {ti}", description=d, outputRequired=o, fallback=fb, dayFrom=f, dayTo=t, sortOrder=10 + i)

add(seedKey="task-canary-195", phaseNumber=2, track="Interview", title="Canary warm-up applications (optional learning target)",
    description="Send suitable warm-up applications and aim for diagnostic screens if opportunities arise. Optional targets: 8-10 warm-up applications, 2-3 diagnostic screens; no count is guaranteed.",
    outputRequired="Record funnel evidence.", dayFrom=195, dayTo=215, sortOrder=20)

SCALE = [
    ("baseline", "Baseline and bottlenecks", 221, 241, "Run k6 against real project journeys; inspect SQL plans, serialization, connection pool, worker backlog, retrieval latency. Change one variable at a time.", "Before/after RPS, p50/p95/p99, error rate, dataset, machine and test profile."),
    ("deploy", "Production-shaped deployment", 242, 262, "Harden Dockerfile, CI build/test/scan, secrets, health/readiness, logs/traces, reproducible deploy.", "Live URL or repeatable local deploy, pipeline run, dashboard/export and limits."),
    ("reliability", "Reliability and evaluation", 263, 283, "Failure injection, retry/cancellation tests, regression gate for golden set, documented operational procedure.", "CI quality/eval gate; failure runbook; eval delta."),
    ("case-study", "Case study and mocks", 284, 300, "Write architecture case study, record demo, do system-design and project walkthrough mocks; improve weak areas.", "Case study, updated README/resume bullets, 6-8 reviewed mock recordings if schedule allows."),
]
for i, (k, ti, f, t, d, o) in enumerate(SCALE):
    add(seedKey=f"task-scale-{k}", phaseNumber=3, track="Scale", title=ti, description=d, outputRequired=o, dayFrom=f, dayTo=t, sortOrder=30 + i)

PHASE4 = [
    ("timed-dsa", "Timed DSA (5-7 h/week when ready)", "Company-tagged practice only for genuine target roles. Two timed problems in a session can be enough; review mistakes."),
    ("design-mocks", "System design mocks (1-2/week; 2-3 stretch)", "Alternate classic backend design and bounded RAG/LLM integration. Record and review."),
    ("star", "Behavioral / STAR (2 short sessions/week)", "Rehearse out loud; concise 90-second stories with specific ownership and outcomes."),
    ("applications", "Applications (6-10 targeted/week where role fit exists)", "Tailor to real requirements; quality over spray. Reduce volume in work/travel-heavy weeks."),
    ("referrals", "Referral asks (3-5/week where appropriate)", "Specific and respectful. No mass messaging or entitlement to a referral."),
    ("funnel", "Funnel review (30 min weekly)", "Track applications, screens, next stages, offers and recurring rejection patterns."),
]
for i, (k, ti, d) in enumerate(PHASE4):
    add(seedKey=f"task-interview-{k}", phaseNumber=4, track="Interview", title=ti, description=d, dayFrom=301, dayTo=365, sortOrder=40 + i)

DAY1 = [
    ("ddia-edition", "Ddia", "Confirm which DDIA edition you own from the title/copyright page", "Sets the DDIA edition in Settings; the default chapter map is the 2nd edition."),
    ("dotnet", "Other", "Install the current .NET LTS for the personal portfolio (.NET 10)", "Keep any employer-required target framework for work projects."),
    ("dsa-repo", "Dsa", "Create the C# DSA repository with a mistake-log file", "Start Arrays & Hashing with Contains Duplicate and Valid Anagram; attempt unaided for 25 minutes."),
    ("ddia-pages", "Ddia", "Read 7-10 DDIA pages", "Or less, if that allows a meaningful three- to five-line note."),
    ("schedule", "Review", "Use Phase A from the Daily Operating Schedule", "Gym at 18:15 on training days and DSA after dinner; do not add a second daily timetable."),
    ("tracker", "Review", "Open the tracker; distinguish full sessions from MVDs", "Use the Someday page (replaces someday-list.md) for new tools and course ideas."),
]
for i, (k, tr, ti, d) in enumerate(DAY1):
    add(seedKey=f"task-day1-{k}", phaseNumber=1, track=tr, title=f"Day 1: {ti}", description=d, dayFrom=1, dayTo=1, sortOrder=i)

dump("plan-tasks", tasks, "Appendices C, C.1, E, F, O")

CHECKPOINTS = [
    (30, None, "Habit", "Were there 26+ active days including honestly logged MVDs? Does the gym-first evening work?"),
    (60, None, "Foundations", "Can you solve unfamiliar problems from covered patterns? Do DDIA notes match the edition you actually own?"),
    (90, None, "Phase 1 exit", "Which artifacts are complete? What must slip, and by how much?"),
    (180, None, "Market and scope", "Review 20 current postings. Do repeated requirements justify changing the track mix?"),
    (195, 215, "Canary interviews", "Send suitable warm-up applications and aim for diagnostic screens if opportunities arise. Record funnel evidence; no count is guaranteed. (Optional learning targets: 8-10 warm-up applications, 2-3 diagnostic screens.)"),
    (220, None, "Phase 2 exit", "Does the modular monolith run? Are tests and eval numbers recorded? Can you defend the architecture for 30 minutes?"),
    (270, None, "Readiness", "Target company list, referral message, resume review and gap list ready?"),
    (300, None, "Phase 3 exit", "Load report, reproducible deployment, case study and recorded mocks complete?"),
    (340, None, "Funnel check", "What do application-to-screen and screen-to-next-stage patterns show?"),
    (365, None, "Retrospective", "What did you ship, what did interviews teach you, and what is the Year 2 plan?"),
]
dump("checkpoints", [dict(seedKey=f"checkpoint-{d}", planDay=d, dayTo=t, title=ti, question=q) for d, t, ti, q in CHECKPOINTS], "Appendix B")

BLOCKS = [
    ("A", "07:30", None, None, "Wake + morning prep", "500 ml water, daylight, breakfast and lunch prep; no morning study requirement yet.", False),
    ("A", "09:00", "17:30", None, "Office", "Lunch around 13:30. Stand/walk periodically; do not use office equipment for personal project work.", False),
    ("A", "17:45", "18:15", None, "Return + snack / change", "Change first; small pre-workout snack and water.", False),
    ("A", "18:15", "19:05", None, "Gym (3 sessions/week)", "As per the separately reviewed training plan. Tue/Thu can be a short walk.", False),
    ("A", "19:05", "20:00", None, "Return, shower, dinner", "Eat and reset. Do not open a long scrolling session.", False),
    ("A", "20:15", "21:15", None, "DSA", "One problem/review with the four-pass method; log one mistake.", False),
    ("A", "21:15", "21:40", None, "DDIA / design", "About 7 pages plus a three-line note.", False),
    ("A", "21:40", "21:55", None, "Build", "15 minutes: one commit, one doc line or one failing test fixed.", False),
    ("A", "21:55", "22:10", None, "Close day", "Tracker, tomorrow target, breakfast/lunch prep. Screens off; lights out by 23:15 target.", False),
    ("B", "07:00", None, "07:15 (W4); 07:00 (W5-8)", "Morning prep + DSA attempt", "10 min mobility only if already cleared; 30 min unaided DSA attempt. Rewrite/log moves to evening.", False),
    ("B", "18:15", "19:05", None, "Gym (4-day split)", "Use the split in the Daily Operating Schedule; clinician/physio clearance controls lower-body activity.", False),
    ("B", "20:15", "20:30", None, "DSA rewrite + log", "Close solution, re-code in C#, record pattern and bug.", False),
    ("B", "20:30", "21:10", None, "DDIA / design", "7-10 pages or a focused design note.", False),
    ("B", "21:15", "22:15", None, "Build track", "One small committed output. Lights out target around 23:00.", False),
    ("C", "06:30", None, None, "Wake", "500 ml water and daylight. Keep bedtime early enough for at least 7 hours of sleep.", False),
    ("C", "06:50", "07:50", None, "DSA", "Protected C# practice: attempt, unstick, rewrite, log. Default DSA slot from Week 9.", False),
    ("C", "09:00", "17:30", None, "Office", "Normal workday; hourly movement breaks where practical.", False),
    ("C", "18:15", "19:05", None, "Gym / training", "Fixed training anchor. Clinical clearance controls exercise choice.", False),
    ("C", "19:30", "20:10", None, "Dinner / recovery", "Shower, dinner and recovery. Avoid pushing study into sleep time.", False),
    ("C", "20:15", "20:35", None, "DDIA / system design", "Stable: about 20 min + a short note. After the book, a design drill or ADR defence.", False),
    ("C", "20:40", "21:25", None, "Main track", "Phase 1: React / MEAI / MAF; Phase 2: flagship; Phase 3: scale/eval; Phase 4: interviews. Stable: 45 min on 3 evenings.", False),
    ("C", "21:25", "22:10", None, "Close / shutdown", "Tracker, tomorrow targets, prep and sleep routine.", False),
    ("C", "20:15", "20:55", None, "DDIA / system design (stretch)", "Stretch: about 40 min / 7-10 pages. Only when rested.", True),
    ("C", "21:00", "22:25", None, "Main track (stretch)", "Stretch block; the stable version is the default.", True),
    ("C", "22:25", "22:40", None, "Close / shutdown (stretch)", "Stretch only when rested.", True),
]
rows = []
count = {"A": 0, "B": 0, "C": 0}
for ph, s, e, label, title, rule, stretch in BLOCKS:
    count[ph] += 1
    rows.append(dict(seedKey=f"block-{ph.lower()}-{count[ph]:02d}", schedulePhase=ph, startTime=f"{s}:00", endTime=f"{e}:00" if e else None,
                     timeLabel=label, title=title, rule=rule, isStretch=stretch, sortOrder=count[ph] * 10))
dump("schedule-blocks", rows, "Appendix I")

GYM = [
    ("A", "Monday", "Gym session", False, "Phase A: 3 sessions/week as per the separately reviewed training plan (suggested days; edit freely)."),
    ("A", "Tuesday", "Short walk", True, "Tue/Thu can be a short walk."),
    ("A", "Wednesday", "Gym session", False, None),
    ("A", "Thursday", "Short walk", True, "Tue/Thu can be a short walk."),
    ("A", "Friday", "Gym session", False, None),
]
for ph in "BC":
    GYM += [(ph, "Monday", "Upper push", False, None),
            (ph, "Tuesday", "Lower A", False, "Clinician/physio clearance controls lower-body activity."),
            (ph, "Thursday", "Upper pull", False, None),
            (ph, "Friday", "Lower B", False, "Only if cleared; prepare for travel if due.")]
GYM += [("C", "Wednesday", "Zone 2 (optional)", True, "Added cardio follows clinician/physio guidance."),
        ("C", "Saturday", "Zone 2 / core (optional)", True, "Otherwise protect recovery.")]
dump("gym-plan", [dict(seedKey=f"gym-{ph.lower()}-{d[:3].lower()}", schedulePhase=ph, dayOfWeek=d, sessionName=n, isOptional=o, notes=nt)
                  for ph, d, n, o, nt in GYM], "Appendix I")

# ---- DSA (spec 4.4 topics; Appendix C problems, names exactly as the roadmap writes them) ----
TOPICS = ["Arrays & Hashing", "Two Pointers", "Stack", "Binary Search", "Sliding Window", "Linked List", "Trees", "Tries",
          "Heap / Priority Queue", "Backtracking", "Graphs", "1-D DP", "2-D DP", "Greedy", "Intervals", "Math & Geometry", "Bit Manipulation"]


def topic_key(name):
    return "topic-" + "".join(c if c.isalnum() else "-" for c in name.lower()).replace("--", "-").replace("--", "-").strip("-")


dump("dsa-topics", [dict(seedKey=topic_key(n), name=n, sortOrder=(i + 1) * 10, isDeferred=False) for i, n in enumerate(TOPICS)], "spec 4.4")

PROBLEMS = [
    (1, "Arrays & Hashing", ["Contains Duplicate", "Valid Anagram", "Two Sum", "Group Anagrams", "Top K Frequent", "Product Except Self", "Valid Sudoku", "Longest Consecutive"]),
    (2, "Two Pointers", ["Valid Palindrome", "Two Sum II", "3Sum", "Container With Most Water", "Trapping Rain Water"]),
    (3, "Stack", ["Min Stack", "Evaluate RPN", "Generate Parentheses", "Daily Temperatures", "Car Fleet", "Largest Rectangle"]),
    (4, "Binary Search", ["Binary Search", "Search 2D Matrix", "Koko", "Rotated Array", "Time Based KV", "Median of Two Sorted Arrays"]),
    (5, "Sliding Window", ["Best Time to Buy/Sell Stock", "Longest Substring", "Longest Repeating Character Replacement", "Permutation in String", "Min Window"]),
    (6, "Linked List", ["Reverse", "Merge Two Sorted", "Reorder", "Remove Nth", "Linked List Cycle", "Add Two Numbers"]),
    (7, "Linked List", ["Copy List with Random Pointer", "Find Duplicate", "LRU Cache", "Merge K Sorted", "Reverse Nodes in K-Group"]),
    (8, "Trees", ["Invert", "Max Depth", "Diameter", "Balanced", "Same Tree", "Subtree", "LCA of BST"]),
    (9, "Trees", ["Level Order", "Right Side View", "Count Good Nodes", "Validate BST", "Kth Smallest", "Serialize/Deserialize"]),
    (10, "Heap / Priority Queue", ["Kth Largest", "Top K Frequent Words", "Task Scheduler", "Median from Stream"]),
    # W11 "Generate Parentheses review" is a review of the W3 problem, not a new row.
    (11, "Backtracking", ["Subsets", "Combination Sum", "Permutations"]),
    (12, "Backtracking", ["Combination Sum II", "Word Search", "Palindrome Partitioning", "Letter Combinations"]),
]
prob_rows = []
for week, topic, names in PROBLEMS:
    for i, name in enumerate(names):
        slug = "".join(c if c.isalnum() else "-" for c in name.lower()).strip("-")
        prob_rows.append(dict(seedKey=f"dsa-w{week:02d}-{slug}", topicSeedKey=topic_key(topic), title=name, url=None, difficulty=None,
                              planWeek=week, sortOrder=week * 100 + i, status="NotStarted"))
dump("dsa-problems", prob_rows, "Appendix C")

# ---- DDIA (Appendix D, D.2 week map from spec v1.1) ----
DDIA2 = [
    (1, "Trade-Offs in Data Systems Architecture", 1, 1, "Operational vs analytical systems; single-node vs distributed design.",
     "Choose a simple modular monolith before distributed services; explain operational vs analytical needs."),
    (2, "Defining Nonfunctional Requirements", 2, 2, "Latency percentiles, reliability, scalability, maintainability, operability.",
     "Define load, latency, reliability, maintainability and operational limits for Ask-Ops."),
    (3, "Data Models and Query Languages", 3, 3, "Relational/document models, graphs, normalization, CQRS/event sourcing concepts.",
     "Domain entities, relational modeling, indexes, data access patterns and event/CQRS concepts."),
    (4, "Storage and Retrieval", 4, 4, "B-trees, LSM-trees, indexes, OLTP/analytics, vector embeddings.",
     "Storage engines, indexes, full-text/vector retrieval; justify only what the project uses."),
    (5, "Encoding and Evolution", 5, 5, "JSON/Avro/Protobuf, schemas, compatible dataflows.",
     "Version API contracts or event schemas without breaking consumers."),
    (6, "Replication", 6, 6, "Leaders/followers, lag, failover, multi-leader trade-offs.",
     "Understand replication lag and stale reads; discuss before implementing replicas."),
    (7, "Sharding", 7, 7, "Partition keys, skew, rebalancing, routing.",
     "Explain partition-key choice and skew conceptually; do not shard this personal-scale project."),
    (8, "Transactions", 8, 8, "Isolation anomalies, serializability, write skew, distributed transactions.",
     "Protect inventory/order invariants, isolation and idempotent writes."),
    (9, "The Trouble with Distributed Systems", 9, 9, "Partial failures, unreliable networks/clocks, timeouts, leases.",
     "Timeouts, partial failures, clocks, retries and leases."),
    (10, "Consistency and Consensus", 10, 10, "Linearizability, logical clocks, consensus.",
     "Know where strong consistency matters and why consensus is hard; do not implement consensus."),
    (11, "Batch Processing", 11, 11, "Batch processing.", "Document ingestion, imports and offline processing."),
    (12, "Stream Processing", 12, 12, "Stream processing.",
     "Only add stream/broker patterns if there is a concrete event flow; compare batch and streaming."),
    (13, "A Philosophy of Streaming Systems", 12, 12, "A philosophy of streaming systems.",
     "Only add stream/broker patterns if there is a concrete event flow; compare batch and streaming."),
    (14, "Doing the Right Thing", 13, 13, "Privacy, access control, retention, auditability.",
     "Privacy, access control, retention, auditability and responsible use of business data."),
]
dump("ddia-chapters-2e", [dict(seedKey=f"ddia2e-ch{n:02d}", edition="Second", number=n, title=t, plannedWeekFrom=wf, plannedWeekTo=wt,
                               topics=top, askOpsConnection=ask, status="NotStarted", pagesRead=0) for n, t, wf, wt, top, ask in DDIA2], "Appendix D.1")

by2 = {n: (top, ask) for n, _, _, _, top, ask in DDIA2}
# 1st edition: same conceptual progression, located by heading in the user's copy. (number, title, weekFrom, weekTo, 2e chapters it maps to)
DDIA1 = [
    (1, "Reliable, Scalable, and Maintainable Applications", 1, 2, [1, 2]),
    (2, "Data Models and Query Languages", 3, 3, [3]),
    (3, "Storage and Retrieval", 4, 4, [4]),
    (4, "Encoding and Evolution", 5, 5, [5]),
    (5, "Replication", 6, 6, [6]),
    (6, "Partitioning", 7, 7, [7]),
    (7, "Transactions", 8, 8, [8]),
    (8, "The Trouble with Distributed Systems", 9, 9, [9]),
    (9, "Consistency and Consensus", 10, 10, [10]),
    (10, "Batch Processing", 11, 11, [11]),
    (11, "Stream Processing", 12, 12, [12, 13]),
    (12, "The Future of Data Systems", 13, 13, [14]),
]
rows1 = []
for n, t, wf, wt, maps in DDIA1:
    topics = " ".join(by2[m][0] for m in maps)
    if n == 12:
        topics = "Includes \"Doing the Right Thing\": " + topics
    ask = " ".join(dict.fromkeys(by2[m][1] for m in maps))
    rows1.append(dict(seedKey=f"ddia1e-ch{n:02d}", edition="First", number=n, title=t, plannedWeekFrom=wf, plannedWeekTo=wt,
                      topics=topics, askOpsConnection=ask, status="NotStarted", pagesRead=0))
dump("ddia-chapters-1e", rows1, "Appendix D.2 (week map from spec v1.1)")

DRILLS = ["URL shortener", "Rate limiter", "Key-value store", "Distributed cache", "Notification service", "Chat/messaging", "News feed",
          "Search autocomplete", "File storage", "Video streaming basics", "Location/ride matching", "Payments and ledger", "Job scheduler",
          "Logs/metrics aggregation", "Web crawler", "API gateway", "Ticket booking/seat concurrency", "E-commerce order pipeline", "RAG Q&A",
          "LLM gateway with cost control", "Leaderboard", "Unique ID generator", "Recommendation feed", "Distributed lock/leader election"]
dump("design-drills", [dict(seedKey=f"drill-{i + 1:02d}", number=i + 1, prompt=p, group=f"{(i // 4) * 4 + 1}-{(i // 4) * 4 + 4}",
                            plannedWeek=12 + i, status="NotStarted", spokenMock=False, minutes=0) for i, p in enumerate(DRILLS)], "Appendix H")

PROMPTS = [
    ("WhyXOverY", "Queue vs direct call"),
    ("WhyXOverY", "Dapper vs EF Core"),
    ("WhyXOverY", "pgvector vs keyword search"),
    ("Explanation90", "Give this DDIA chapter or ADR a 90-second explanation; review for filler words and missing trade-offs."),
    ("Other", "Think aloud on a DSA problem: restate, examples, brute force, optimize, complexity."),
]
dump("articulation-prompts", [dict(seedKey=f"prompt-{i + 1:02d}", kind=k, text=t, sortOrder=(i + 1) * 10, isActive=True) for i, (k, t) in enumerate(PROMPTS)],
     "Appendix H articulation training")

# ---- Exit criteria (Appendix G) ----
EXIT = [
    (1, None, ["65-80 meaningful DSA attempts/reviews.", "Edition-correct DDIA notes for chapters covered.",
               "Small functional React app (list/detail + create/edit form calling your API).",
               "Minimal MEAI/MAF exercise (hello-agent, one safe tool call, minimal RAG baseline).",
               "Flagship design document (3-5 pages) and five ADRs."]),
    (2, None, [("Reproducible backend", "One modular-monolith API + database + one worker runs from documented steps; tests pass; key domain flow works."),
               ("Reliability", "Health/log/trace evidence, clear timeout and retry policy, at least one injected failure and observed outcome."),
               ("Ask-Ops", "Small document corpus, retrieval with citations, one guarded tool and graceful model/tool failure."),
               ("Evaluation", "Repeatable golden set and baseline; known limitations and human spot-check findings written down."),
               ("Demo / explanation", "Five-minute demo, architecture diagram, honest README and 30-minute spoken walkthrough."),
               ("DSA", "About 120-135 selected attempts/reviews; cold re-solves and pattern coverage matter more than raw count.")]),
    (3, None, ["Load report with before/after numbers and test conditions.", "Reproducible deployment and CI quality/eval gate.",
               "Architecture case study and updated README/resume bullets.", "Recorded mock reviews (6-8 if schedule allows)."]),
    (3, "Day 270 readiness", ["Resume reviewed against real roles; each technical claim backed by code, a test, a measurement or a clear explanation.",
                              "Target list includes role fit, location, experience requirement, fixed/variable compensation and application link/date.",
                              "At least two full project walkthroughs and several design/DSA mock reviews recorded; weak points converted into a weekly practice list."]),
    (4, None, ["Resume, referrals, mock feedback, funnel metrics and negotiation readiness.", "Day 365 retrospective answered in writing."]),
]
exit_rows = []
for phase, group, items in EXIT:
    for i, item in enumerate(items):
        text, hint = item if isinstance(item, tuple) else (item, None)
        key = f"exit-p{phase}-{'d270-' if group else ''}{i + 1:02d}"
        exit_rows.append(dict(seedKey=key, phaseNumber=phase, group=group, text=text, evidenceHint=hint, done=False,
                              sortOrder=(100 if group else 0) + i))
dump("exit-criteria", exit_rows, "Appendix G")

# ---- Weekly hour targets (Appendix J, category mapping from spec v1.1) ----
H = [
    ("A", "Total (60-90 min most weekdays + one short weekend session)", "", 4, 8, True, False),
    ("B", "Total (build toward 8-11 focused hours)", "", 8, 11, True, False),
    ("Full", "Total, stable", "", 12, 15, True, False),
    ("Full", "Total, ambitious", "", 15, 17, True, False),
    ("Full", "Stretch only", "", 19, 20, True, True),
    ("Phase1", "DSA", "Dsa", 4, 6, False, False),
    ("Phase1", "DDIA / design", "DdiaDesign", 2, 3, False, False),
    ("Phase1", "Main track", "MainTrack", 3, 5, False, False),
    ("Phase1", "Review", "Review", 1, 1, False, False),
    ("Phase2", "DSA", "Dsa", 4, 5, False, False),
    ("Phase2", "Design", "DdiaDesign", 1, 2, False, False),
    ("Phase2", "Flagship (main track)", "MainTrack", 5, 8, False, False),
    ("Phase2", "Review / outreach", "Review,Applications", 1, 1, False, False),
    ("Phase3", "DSA", "Dsa", 4, 6, False, False),
    ("Phase3", "Mocks", "Mocks", 2, 2, False, False),
    ("Phase3", "Load / deploy / evals (main track)", "MainTrack", 4, 6, False, False),
    ("Phase3", "Review", "Review", 1, 1, False, False),
    ("Phase4", "DSA", "Dsa", 5, 7, False, False),
    ("Phase4", "System design", "DdiaDesign", 2, 3, False, False),
    ("Phase4", "Applications / mocks", "Mocks,Applications", 3, 5, False, False),
]
dump("weekly-hour-targets", [dict(seedKey=f"hours-{st.lower()}-{i + 1:02d}", stage=st, label=l, categories=c or None, minHours=mn, maxHours=mx,
                                   isTotal=t, isStretch=sx, sortOrder=(i + 1) * 10) for i, (st, l, c, mn, mx, t, sx) in enumerate(H)],
     "Appendix J (J.1 totals, J.2 categories)")

print(f"{len(exit_rows)} exit criteria; {len(tasks)} plan tasks, {len(prob_rows)} DSA problems, {len(DDIA2)}+{len(rows1)} DDIA chapters, {len(DRILLS)} drills written")
