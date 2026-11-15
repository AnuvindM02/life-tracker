import { CalendarClock, CheckCircle2, Play } from "lucide-react";
import { useState } from "react";
import { Link, useNavigate } from "react-router";
import { api, call, type S } from "@/api/client";
import { dsaKeys, useAction, useDsaAttempts, useDsaProgress, useDsaReviews, useMistakeTags, useToday } from "@/api/hooks";
import { EmptyState, ErrorBox, Field, Loading, PageHeader, selectClass } from "@/components/common";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Progress } from "@/components/ui/progress";
import { isoDate, prettyDate } from "@/lib/format";
import { cn } from "@/lib/utils";
import { attemptTypeLabel, DsaNav, StatusBadge } from "./DsaNav";

export function DsaOverviewPage() {
  const today = useToday();
  const progress = useDsaProgress();
  const recent = useDsaAttempts({ page: 1 });
  const navigate = useNavigate();
  const v = today.data;
  const p = progress.data;

  return (
    <div>
      <DsaNav />
      <PageHeader
        title="DSA in C#"
        subtitle="Make a genuine 25-30 minute attempt before hints. Quality and cold re-solves matter more than the raw total."
        actions={<Button onClick={() => navigate("/dsa/attempts/new")}><Play /> Log an attempt</Button>}
      />
      <div className="grid gap-6 lg:grid-cols-3">
        <Card className="lg:col-span-2">
          <CardHeader>
            <CardTitle className="text-base">Week {v?.planDay?.week ?? "-"} problems</CardTitle>
          </CardHeader>
          <CardContent className="grid gap-1">
            {today.isPending ? (
              <Loading />
            ) : v?.dsaProblems.length ? (
              v.dsaProblems.map((pr) => <ProblemRow key={pr.id} problem={pr} />)
            ) : (
              <p className="text-sm text-muted-foreground">No problems are planned for this week. Use the library or the weakest list.</p>
            )}
          </CardContent>
        </Card>
        <div className="grid content-start gap-6">
          <ReviewsDueCard reviews={v?.reviewsDue ?? []} />
          {p && (
            <Card>
              <CardHeader>
                <CardTitle className="text-base">Meaningful attempts</CardTitle>
                <CardDescription>{p.nextMarker.label}</CardDescription>
              </CardHeader>
              <CardContent className="grid gap-2 text-sm">
                <p>
                  <span className="text-2xl font-semibold">{p.countedTotal}</span> <span className="text-muted-foreground">counted · {p.attemptsTotal} logged · Day {p.planDay}</span>
                </p>
                <Progress value={Math.min(100, (p.countedTotal / p.nextMarker.max) * 100)} aria-label={`Counted attempts against ${p.nextMarker.min}-${p.nextMarker.max}`} />
                <p className="text-xs text-muted-foreground">{p.caveat}</p>
                <Link to="/dsa/progress" className="text-primary underline">Progress and weakest problems</Link>
              </CardContent>
            </Card>
          )}
        </div>
      </div>
      <Card className="mt-6">
        <CardHeader>
          <CardTitle className="text-base">Recent attempts</CardTitle>
        </CardHeader>
        <CardContent>
          {recent.data ? <AttemptTable items={recent.data.items.slice(0, 8)} /> : <Loading />}
        </CardContent>
      </Card>
    </div>
  );
}

export function ProblemRow({ problem }: { problem: S["DsaProblemBrief"] }) {
  const navigate = useNavigate();
  return (
    <div className="flex flex-wrap items-center gap-2 rounded-md px-2 py-1.5 text-sm hover:bg-accent/40">
      <span className="font-medium">{problem.title}</span>
      <span className="text-xs text-muted-foreground">{problem.topicName}</span>
      <StatusBadge status={problem.status} />
      {problem.lastConfidence && <span className="text-xs text-muted-foreground">last conf {problem.lastConfidence}</span>}
      <Button size="sm" variant="outline" className="ml-auto" onClick={() => navigate(`/dsa/attempts/new?problemId=${problem.id}`)}>
        <Play /> Start attempt
      </Button>
    </div>
  );
}

export function ReviewsDueCard({ reviews }: { reviews: S["DsaReviewDto"][] }) {
  const navigate = useNavigate();
  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2 text-base">
          <CalendarClock className="size-4" /> Cold reviews due
        </CardTitle>
      </CardHeader>
      <CardContent className="grid gap-1 text-sm">
        {reviews.length === 0 && <p className="text-muted-foreground">Nothing due today.</p>}
        {reviews.slice(0, 6).map((r) => (
          <div key={r.id} className="flex items-center gap-2">
            <span className="min-w-0 flex-1 truncate">{r.problemTitle}</span>
            {r.daysOverdue > 0 && <span className="text-xs text-muted-foreground">from {prettyDate(r.dueDate, { day: "numeric", month: "short" })}</span>}
            <Button size="sm" variant="ghost" onClick={() => navigate(`/dsa/attempts/new?type=ColdReview&problemId=${r.problemId}&reviewItemId=${r.id}`)}>
              Review
            </Button>
          </div>
        ))}
        {reviews.length > 6 && <Link to="/dsa/reviews" className="text-primary underline">{reviews.length - 6} more</Link>}
      </CardContent>
    </Card>
  );
}

export function AttemptTable({ items }: { items: S["DsaAttemptSummaryDto"][] }) {
  if (items.length === 0) return <EmptyState title="No attempts yet" />;
  return (
    <div className="overflow-x-auto">
      <table className="w-full text-sm">
        <thead className="text-left text-xs text-muted-foreground">
          <tr>
            <th className="p-2">Date</th>
            <th className="p-2">Problem</th>
            <th className="p-2">Type</th>
            <th className="p-2">Time</th>
            <th className="p-2">Conf</th>
            <th className="p-2">Counted</th>
            <th className="p-2">What I missed</th>
          </tr>
        </thead>
        <tbody>
          {items.map((a) => (
            <tr key={a.id} className="border-t hover:bg-accent/40">
              <td className="p-2 whitespace-nowrap">{prettyDate(a.date, { day: "numeric", month: "short" })}</td>
              <td className="p-2">
                <Link to={`/dsa/attempts/${a.id}`} className="font-medium text-primary hover:underline">{a.title}</Link>
                {a.topicName && <span className="block text-xs text-muted-foreground">{a.topicName}</span>}
              </td>
              <td className="p-2">{attemptTypeLabel[a.type]}</td>
              <td className="p-2">{a.totalMin} min</td>
              <td className="p-2">{a.confidence}</td>
              <td className="p-2">{a.isCounted ? <CheckCircle2 className="size-4 text-success" aria-label="counted" /> : <span className="text-xs text-muted-foreground">no</span>}</td>
              <td className="max-w-xs truncate p-2 text-muted-foreground" title={a.whatIMissed ?? ""}>{a.whatIMissed}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

export function AttemptsPage() {
  const [from, setFrom] = useState("");
  const [to, setTo] = useState("");
  const [type, setType] = useState<S["AttemptType"] | "">("");
  const [counted, setCounted] = useState("");
  const [tag, setTag] = useState("");
  const [page, setPage] = useState(1);
  const tags = useMistakeTags();
  const attempts = useDsaAttempts({
    from: from || undefined,
    to: to || undefined,
    type: type || undefined,
    counted: counted === "" ? undefined : counted === "1",
    tag: tag || undefined,
    page,
  });
  return (
    <div>
      <DsaNav />
      <PageHeader title="Attempts" subtitle="The mistake log: one row per attempt." actions={<Button asChild><Link to="/dsa/attempts/new">Log an attempt</Link></Button>} />
      <div className="mb-4 flex flex-wrap gap-2">
        <Input type="date" aria-label="From" value={from} onChange={(e) => setFrom(e.target.value)} className="w-40" />
        <Input type="date" aria-label="To" value={to} onChange={(e) => setTo(e.target.value)} className="w-40" />
        <select className={cn(selectClass, "w-36")} aria-label="Type" value={type} onChange={(e) => setType(e.target.value as S["AttemptType"])}>
          <option value="">All types</option>
          <option value="New">New</option>
          <option value="ColdReview">Cold review</option>
          <option value="Timed">Timed</option>
        </select>
        <select className={cn(selectClass, "w-36")} aria-label="Counted" value={counted} onChange={(e) => setCounted(e.target.value)}>
          <option value="">Counted or not</option>
          <option value="1">Counted</option>
          <option value="0">Not counted</option>
        </select>
        <select className={cn(selectClass, "w-44")} aria-label="Mistake tag" value={tag} onChange={(e) => setTag(e.target.value)}>
          <option value="">Any mistake tag</option>
          {tags.data?.map((t) => <option key={t.name} value={t.name}>{t.name} ({t.uses})</option>)}
        </select>
      </div>
      {attempts.isError ? <ErrorBox error={attempts.error} /> : attempts.data ? <AttemptTable items={attempts.data.items} /> : <Loading />}
      {attempts.data && attempts.data.total > attempts.data.pageSize && (
        <div className="mt-3 flex items-center gap-2 text-sm">
          <Button size="sm" variant="outline" disabled={page <= 1} onClick={() => setPage((x) => x - 1)}>Previous</Button>
          <span>Page {page}</span>
          <Button size="sm" variant="outline" disabled={page * attempts.data.pageSize >= attempts.data.total} onClick={() => setPage((x) => x + 1)}>Next</Button>
        </div>
      )}
    </div>
  );
}

export function ReviewsPage() {
  const overdue = useDsaReviews("Overdue");
  const due = useDsaReviews("Due");
  const upcoming = useDsaReviews("Upcoming");
  const [dialog, setDialog] = useState<{ kind: "skip" | "reschedule"; review: S["DsaReviewDto"] } | null>(null);
  return (
    <div>
      <DsaNav />
      <PageHeader title="Review queue" subtitle="Reviews turn 'I have seen this' into 'I can solve this under pressure'. Completing one means logging a cold review." />
      <div className="grid gap-6">
        <ReviewSection title="Due today" q={due} onAction={setDialog} />
        <ReviewSection title="From earlier days" q={overdue} onAction={setDialog} hint="Pick them up when you can; there is nothing to repay." />
        <ReviewSection title="Upcoming" q={upcoming} onAction={setDialog} />
      </div>
      <ReviewDialog value={dialog} onClose={() => setDialog(null)} />
    </div>
  );
}

function ReviewSection({
  title,
  q,
  onAction,
  hint,
}: {
  title: string;
  q: ReturnType<typeof useDsaReviews>;
  onAction: (v: { kind: "skip" | "reschedule"; review: S["DsaReviewDto"] }) => void;
  hint?: string;
}) {
  const navigate = useNavigate();
  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base">{title} {q.data ? <span className="text-muted-foreground">({q.data.length})</span> : null}</CardTitle>
        {hint && <CardDescription>{hint}</CardDescription>}
      </CardHeader>
      <CardContent className="grid gap-1 text-sm">
        {q.isPending && <Loading />}
        {q.data?.length === 0 && <p className="text-muted-foreground">None.</p>}
        {q.data?.map((r) => (
          <div key={r.id} className="flex flex-wrap items-center gap-2 rounded-md px-2 py-1.5 hover:bg-accent/40">
            <span className="font-medium">{r.problemTitle}</span>
            <span className="text-xs text-muted-foreground">{r.topicName}</span>
            <Badge variant="outline">+{r.intervalDays} d</Badge>
            <span className="text-xs text-muted-foreground">due {prettyDate(r.dueDate, { day: "numeric", month: "short" })} · source conf {r.sourceConfidence}</span>
            {r.rescheduleReason && <span className="text-xs text-muted-foreground">(moved: {r.rescheduleReason})</span>}
            <span className="ml-auto flex gap-1">
              <Button size="sm" onClick={() => navigate(`/dsa/attempts/new?type=ColdReview&problemId=${r.problemId}&reviewItemId=${r.id}`)}>Cold review</Button>
              <Button size="sm" variant="ghost" onClick={() => onAction({ kind: "reschedule", review: r })}>Reschedule</Button>
              <Button size="sm" variant="ghost" onClick={() => onAction({ kind: "skip", review: r })}>Skip</Button>
            </span>
          </div>
        ))}
      </CardContent>
    </Card>
  );
}

function ReviewDialog({ value, onClose }: { value: { kind: "skip" | "reschedule"; review: S["DsaReviewDto"] } | null; onClose: () => void }) {
  const [reason, setReason] = useState("");
  const [date, setDate] = useState(isoDate());
  const act = useAction(
    () =>
      value!.kind === "skip"
        ? call(api.POST("/api/v1/dsa/reviews/{id}/skip", { params: { path: { id: value!.review.id } }, body: { reason } }))
        : call(api.POST("/api/v1/dsa/reviews/{id}/reschedule", { params: { path: { id: value!.review.id } }, body: { dueDate: date, reason } })),
    {
      invalidate: [dsaKeys.all, ["today"]],
      success: value?.kind === "skip" ? "Review skipped." : "Review moved.",
      onSuccess: () => {
        setReason("");
        onClose();
      },
    },
  );
  return (
    <Dialog open={value !== null} onOpenChange={(o) => !o && onClose()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{value?.kind === "skip" ? "Skip" : "Reschedule"} review: {value?.review.problemTitle}</DialogTitle>
        </DialogHeader>
        <div className="grid gap-3">
          {value?.kind === "reschedule" && <Field label="New date">{(id) => <Input id={id} type="date" min={isoDate()} value={date} onChange={(e) => setDate(e.target.value)} />}</Field>}
          <Field label="Reason" hint="A short reason is required.">{(id) => <Input id={id} value={reason} onChange={(e) => setReason(e.target.value)} />}</Field>
        </div>
        <DialogFooter>
          <Button disabled={reason.trim().length < 3 || act.isPending} onClick={() => act.mutate(undefined)}>{value?.kind === "skip" ? "Skip review" : "Move review"}</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

