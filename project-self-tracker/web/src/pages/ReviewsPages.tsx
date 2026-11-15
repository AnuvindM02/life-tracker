import { ChevronLeft, ChevronRight } from "lucide-react";
import { useState } from "react";
import { NavLink, useSearchParams } from "react-router";
import { api, call, type S } from "@/api/client";
import { m5Keys, useAction, useMonthlyList, useMonthlyPrefill, useMonthlyReview, useWeeklyList, useWeeklyPrefill, useWeeklyReview } from "@/api/hooks";
import { Attachments } from "@/components/Attachments";
import { Field, Loading, PageHeader, RatingInput } from "@/components/common";
import { MarkdownEditor } from "@/components/MarkdownEditor";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Checkbox } from "@/components/ui/checkbox";
import { Input } from "@/components/ui/input";
import { Progress } from "@/components/ui/progress";
import { addDays, categoryLabel, isoDate, minutes, parseIso, prettyDate } from "@/lib/format";
import { cn } from "@/lib/utils";
import { SomedayRow } from "./SomedayPage";

export function ReviewsNav() {
  return (
    <nav className="mb-6 flex gap-1 border-b pb-2 text-sm" aria-label="Reviews">
      {[["/reviews/weekly", "Weekly"], ["/reviews/monthly", "Monthly"]].map(([to, label]) => (
        <NavLink key={to} to={to} className={({ isActive }) => cn("rounded-md px-3 py-1.5 hover:bg-accent", isActive && "bg-accent font-medium")}>{label}</NavLink>
      ))}
    </nav>
  );
}

const mondayOf = (d: string) => {
  const x = parseIso(d);
  x.setDate(x.getDate() - ((x.getDay() + 6) % 7));
  return isoDate(x);
};

export function HoursCard({ hours }: { hours: S["WeeklyHoursDto"] }) {
  return (
    <div className="grid gap-2 text-sm">
      <p className="text-muted-foreground">
        {minutes(hours.totalMinutes)} logged · stage {hours.stage === "A" || hours.stage === "B" ? `Phase ${hours.stage} schedule` : hours.stage.replace("Phase", "Phase ")}
      </p>
      {hours.targets.map((t) => (
        <div key={t.label} className="grid gap-0.5">
          <div className="flex justify-between text-xs">
            <span>{t.label}{t.isGuidance ? " (guidance)" : ""}</span>
            <span className="text-muted-foreground">{t.actualHours} h / {t.minHours === t.maxHours ? t.minHours : `${t.minHours}-${t.maxHours}`} h{t.isStretch ? " · stretch only" : ""}</span>
          </div>
          <Progress value={Math.min(100, (Number(t.actualHours) / Number(t.minHours || 1)) * 100)} aria-label={`${t.label}: ${t.actualHours} hours`} className={cn(t.isStretch && "opacity-50")} />
        </div>
      ))}
      {hours.byCategory.length > 0 && <p className="text-xs text-muted-foreground">{hours.byCategory.map((c) => `${categoryLabel[c.category]} ${minutes(c.minutes)}`).join(" · ")}</p>}
      <p className="text-xs text-muted-foreground">Targets are ranges. 19-20 h is stretch only; never trade sleep or work performance for it.</p>
    </div>
  );
}

export function WeeklyReviewPage() {
  const [search, setSearch] = useSearchParams();
  const week = mondayOf(search.get("week") ?? isoDate());
  const prefill = useWeeklyPrefill(week);
  const review = useWeeklyReview(week);
  const list = useWeeklyList();
  const go = (d: string) => setSearch({ week: d });

  return (
    <div>
      <ReviewsNav />
      <PageHeader
        title={`Weekly review: week of ${prettyDate(week, { day: "numeric", month: "short", year: "numeric" })}`}
        subtitle="Sunday, 30 minutes. Adjust to trends, not one bad week."
        actions={
          <>
            <Button size="icon" variant="ghost" onClick={() => go(addDays(week, -7))} aria-label="Previous week"><ChevronLeft /></Button>
            <Button size="icon" variant="ghost" disabled={addDays(week, 7) > isoDate()} onClick={() => go(addDays(week, 7))} aria-label="Next week"><ChevronRight /></Button>
          </>
        }
      />
      <div className="grid gap-6 lg:grid-cols-3">
        <div className="grid content-start gap-6 lg:col-span-2">
          {review.isPending ? <Loading /> : <WeeklyForm key={`${week}-${review.data?.rowVersion ?? "new"}`} week={week} review={review.data ?? null} />}
        </div>
        <div className="grid content-start gap-6">
          <Card>
            <CardHeader>
              <CardTitle className="text-base">This week from your logs</CardTitle>
              <CardDescription>Filled in automatically.</CardDescription>
            </CardHeader>
            <CardContent className="grid gap-3 text-sm">
              {prefill.data ? <WeeklyFacts s={prefill.data} /> : <Loading />}
            </CardContent>
          </Card>
          <Card>
            <CardHeader>
              <CardTitle className="text-base">Past reviews</CardTitle>
            </CardHeader>
            <CardContent className="grid gap-1 text-sm">
              {list.data?.length === 0 && <p className="text-muted-foreground">None yet.</p>}
              {list.data?.map((r) => (
                <button key={r.id} className="text-left text-primary hover:underline" onClick={() => go(r.weekStart)}>
                  Week of {prettyDate(r.weekStart, { day: "numeric", month: "short", year: "numeric" })}
                </button>
              ))}
            </CardContent>
          </Card>
        </div>
      </div>
    </div>
  );
}

function WeeklyFacts({ s }: { s: S["WeeklySnapshot"] }) {
  const row = (k: string, v: string | number) => (
    <div className="flex justify-between gap-3"><span className="text-muted-foreground">{k}</span><span className="text-right font-medium">{v}</span></div>
  );
  return (
    <>
      <div className="grid gap-1">
        {row("Active days / MVDs", `${s.activeDays} / ${s.mvdDays}`)}
        {row("Rest days", s.restDays)}
        {row("DSA attempts (counted)", `${s.dsaAttempts} (${s.countedAttempts})`)}
        {row("Cold re-solves", s.coldReviews)}
        {row("Median time", s.medianMinutes != null ? `${s.medianMinutes} min` : "-")}
        {row("Repeated mistakes", s.repeatedMistakes.map((t) => `${t.tag} x${t.count}`).join(", ") || "-")}
        {row("DDIA pages / notes done", `${s.pagesRead} / ${s.notesCompleted}`)}
        {row("Design drills", s.drillsPracticed)}
        {row("Build ticks", s.buildTicks)}
        {row("Gym sessions", s.gymSessions)}
        {row("Sleep / energy avg", `${s.sleepAverage ?? "-"} h / ${s.energyAverage ?? "-"}`)}
        {row("Trips next fortnight", s.tripsNextFortnight.map((d) => prettyDate(d, { day: "numeric", month: "short" })).join(", ") || "none")}
      </div>
      {s.ddiaChapters.length > 0 && <p className="text-xs">DDIA: {s.ddiaChapters.join(" · ")}</p>}
      <HoursCard hours={s.hours} />
    </>
  );
}

function WeeklyForm({ week, review }: { week: string; review: S["WeeklyReviewDto"] | null }) {
  const [f, setF] = useState({
    energy: review?.energy ?? null,
    sleep: review?.sleep ?? null,
    mood: review?.mood ?? null,
    shippedMd: review?.shippedMd ?? "",
    blockedMd: review?.blockedMd ?? "",
    nextSliceMd: review?.nextSliceMd ?? "",
    recoveryNote: review?.recoveryNote ?? "",
    repeatedRequirement: review?.repeatedRequirement ?? "",
    start: review?.start ?? "",
    stop: review?.stop ?? "",
    keep: review?.keep ?? "",
  });
  const save = useAction(
    () => call(api.PUT("/api/v1/reviews/weekly/{weekStart}", { params: { path: { weekStart: week } }, body: { ...f, rowVersion: review?.rowVersion ?? null } })),
    { invalidate: [m5Keys.weekly(week), m5Keys.weeklyList, ["today"]], success: "Weekly review saved." },
  );
  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base">{review ? "Your review" : "Write the review"}</CardTitle>
      </CardHeader>
      <CardContent className="grid gap-4">
        <div className="grid gap-3 sm:grid-cols-3">
          {(["energy", "sleep", "mood"] as const).map((k) => (
            <Field key={k} label={`${k[0].toUpperCase()}${k.slice(1)} (1-5)`}>{(id) => <RatingInput id={id} label={k} value={f[k]} onChange={(n) => setF({ ...f, [k]: n })} />}</Field>
          ))}
        </div>
        <Field label="Build shipped">{(id) => <MarkdownEditor id={id} value={f.shippedMd} onChange={(v) => setF({ ...f, shippedMd: v })} rows={3} />}</Field>
        <div className="grid gap-3 sm:grid-cols-2">
          <Field label="Blocked">{(id) => <MarkdownEditor id={id} value={f.blockedMd} onChange={(v) => setF({ ...f, blockedMd: v })} rows={2} />}</Field>
          <Field label="Next smallest slice">{(id) => <MarkdownEditor id={id} value={f.nextSliceMd} onChange={(v) => setF({ ...f, nextSliceMd: v })} rows={2} />}</Field>
        </div>
        <Field label="Recovery / symptoms needing review">{(id) => <Input id={id} value={f.recoveryNote} onChange={(e) => setF({ ...f, recoveryNote: e.target.value })} />}</Field>
        <Field label="20-posting market check: repeated requirement">{(id) => <Input id={id} value={f.repeatedRequirement} onChange={(e) => setF({ ...f, repeatedRequirement: e.target.value })} />}</Field>
        <div className="grid gap-3 sm:grid-cols-3">
          {(["start", "stop", "keep"] as const).map((k) => (
            <Field key={k} label={k[0].toUpperCase() + k.slice(1)}>{(id) => <Input id={id} value={f[k]} onChange={(e) => setF({ ...f, [k]: e.target.value })} />}</Field>
          ))}
        </div>
        <Button className="justify-self-start" onClick={() => save.mutate(undefined)} disabled={save.isPending || week > isoDate()}>{review ? "Save changes" : "Save review"}</Button>
        {review && <Attachments entityType="WeeklyReview" entityId={review.id} label="Weekly review" compact />}
      </CardContent>
    </Card>
  );
}

export function MonthlyReviewPage() {
  const [search, setSearch] = useSearchParams();
  const month = (search.get("month") ?? isoDate()).slice(0, 7) + "-01";
  const prefill = useMonthlyPrefill(month);
  const review = useMonthlyReview(month);
  const list = useMonthlyList();
  const shift = (n: number) => {
    const d = parseIso(month);
    d.setMonth(d.getMonth() + n);
    setSearch({ month: isoDate(d) });
  };
  const p = prefill.data;
  return (
    <div>
      <ReviewsNav />
      <PageHeader
        title={`Monthly review: ${parseIso(month).toLocaleDateString(undefined, { month: "long", year: "numeric" })}`}
        subtitle="60 minutes: phase progress, the mistake log, 20 live postings, someday-list triage, and a lighter week when needed."
        actions={
          <>
            <Button size="icon" variant="ghost" onClick={() => shift(-1)} aria-label="Previous month"><ChevronLeft /></Button>
            <Button size="icon" variant="ghost" disabled={month >= isoDate().slice(0, 7) + "-01"} onClick={() => shift(1)} aria-label="Next month"><ChevronRight /></Button>
          </>
        }
      />
      <div className="grid gap-6 lg:grid-cols-3">
        <div className="grid content-start gap-6 lg:col-span-2">
          {review.isPending || !p ? <Loading /> : <MonthlyForm key={`${month}-${review.data?.rowVersion ?? "new"}`} month={month} review={review.data ?? null} prefill={p} />}
          {p && (
            <Card>
              <CardHeader>
                <CardTitle className="text-base">Someday-list triage</CardTitle>
                <CardDescription>New tools and course ideas wait here until now. Promote, keep or drop each one.</CardDescription>
              </CardHeader>
              <CardContent className="grid gap-1">
                {p.somedayParked.length === 0 && <p className="text-sm text-muted-foreground">Nothing parked.</p>}
                {p.somedayParked.map((s) => <SomedayRow key={s.id} item={s} triage />)}
              </CardContent>
            </Card>
          )}
        </div>
        <div className="grid content-start gap-6">
          {p && (
            <Card>
              <CardHeader>
                <CardTitle className="text-base">This month from your logs</CardTitle>
              </CardHeader>
              <CardContent className="grid gap-1 text-sm">
                <p>Day {p.planDay}{p.phaseName ? ` · ${p.phaseName}` : ""}</p>
                <p>Phase tasks done: {p.phaseTasksDone} of {p.phaseTasksTotal}</p>
                <p>Exit criteria done: {p.exitCriteriaDone} of {p.exitCriteriaTotal}</p>
                <p>Active days: {p.activeDays} · counted DSA: {p.countedDsa}</p>
                <p>Mistake themes: {p.mistakeThemes.map((t) => `${t.tag} x${t.count}`).join(", ") || "-"}</p>
                <p className="text-muted-foreground">Postings captured: {p.postingsCaptured} of {p.postingsTarget} (the posting log arrives with M7).</p>
              </CardContent>
            </Card>
          )}
          <Card>
            <CardHeader>
              <CardTitle className="text-base">Past reviews</CardTitle>
            </CardHeader>
            <CardContent className="grid gap-1 text-sm">
              {list.data?.map((r) => (
                <button key={r.id} className="text-left text-primary hover:underline" onClick={() => setSearch({ month: r.month })}>
                  {parseIso(r.month).toLocaleDateString(undefined, { month: "long", year: "numeric" })} {r.lighterWeekNeeded && <Badge variant="outline">lighter week</Badge>}
                </button>
              ))}
            </CardContent>
          </Card>
        </div>
      </div>
    </div>
  );
}

function MonthlyForm({ month, review, prefill }: { month: string; review: S["MonthlyReviewDto"] | null; prefill: S["MonthlyPrefill"] }) {
  const [f, setF] = useState({
    phaseProgressMd: review?.phaseProgressMd ?? `Day ${prefill.planDay}${prefill.phaseName ? `, ${prefill.phaseName}` : ""}: ${prefill.phaseTasksDone}/${prefill.phaseTasksTotal} tasks, ${prefill.exitCriteriaDone}/${prefill.exitCriteriaTotal} exit criteria.`,
    mistakeThemesMd: review?.mistakeThemesMd ?? prefill.mistakeThemes.map((t) => `- ${t.tag} (${t.count})`).join("\n"),
    postingsCaptured: review?.postingsCaptured ?? prefill.postingsCaptured,
    lighterWeekNeeded: review?.lighterWeekNeeded ?? false,
    somedayTriageMd: review?.somedayTriageMd ?? "",
  });
  const save = useAction(
    () => call(api.PUT("/api/v1/reviews/monthly/{month}", { params: { path: { month } }, body: { ...f, rowVersion: review?.rowVersion ?? null } })),
    { invalidate: [m5Keys.monthly(month), m5Keys.monthlyList, ["today"]], success: "Monthly review saved." },
  );
  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base">{review ? "Your review" : "Write the review"}</CardTitle>
      </CardHeader>
      <CardContent className="grid gap-4">
        <Field label="Phase progress">{(id) => <MarkdownEditor id={id} value={f.phaseProgressMd} onChange={(v) => setF({ ...f, phaseProgressMd: v })} rows={3} />}</Field>
        <Field label="Mistake-log themes">{(id) => <MarkdownEditor id={id} value={f.mistakeThemesMd} onChange={(v) => setF({ ...f, mistakeThemesMd: v })} rows={3} />}</Field>
        <div className="grid gap-3 sm:grid-cols-2">
          <Field label="Postings captured (target 20)">{(id) => <Input id={id} type="number" min={0} value={f.postingsCaptured} onChange={(e) => setF({ ...f, postingsCaptured: Number(e.target.value) })} />}</Field>
          <label className="flex items-center gap-2 self-end pb-2 text-sm"><Checkbox checked={f.lighterWeekNeeded} onCheckedChange={(c) => setF({ ...f, lighterWeekNeeded: c === true })} /> I need a lighter week</label>
        </div>
        <Field label="Someday triage notes">{(id) => <MarkdownEditor id={id} value={f.somedayTriageMd} onChange={(v) => setF({ ...f, somedayTriageMd: v })} rows={2} />}</Field>
        <Button className="justify-self-start" onClick={() => save.mutate(undefined)} disabled={save.isPending}>{review ? "Save changes" : "Save review"}</Button>
        {review && <Attachments entityType="MonthlyReview" entityId={review.id} label="Monthly review" compact />}
      </CardContent>
    </Card>
  );
}
