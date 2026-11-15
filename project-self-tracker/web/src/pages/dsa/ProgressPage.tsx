import { type ReactNode, useState } from "react";
import { useNavigate } from "react-router";
import { Bar, BarChart, CartesianGrid, Line, LineChart, ReferenceArea, ReferenceLine, ResponsiveContainer, Tooltip, XAxis, YAxis } from "recharts";
import { useDsaProgress, useDsaWeakest } from "@/api/hooks";
import { EmptyState, ErrorBox, Loading, PageHeader } from "@/components/common";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { prettyDate } from "@/lib/format";
import { DsaNav } from "./DsaNav";

const axis = { stroke: "var(--muted-foreground)", fontSize: 11 };
const tooltipStyle = { background: "var(--popover)", border: "1px solid var(--border)", borderRadius: 8, color: "var(--popover-foreground)", fontSize: 12 };

/** Every chart has a data-table view (spec 8, accessibility) and a caveat caption (spec 4.13). */
function ChartCard({ title, caption, table, children }: { title: string; caption: string; table: ReactNode; children: ReactNode }) {
  const [showTable, setShowTable] = useState(false);
  return (
    <Card>
      <CardHeader className="flex-row items-start justify-between gap-2">
        <div>
          <CardTitle className="text-base">{title}</CardTitle>
          <CardDescription>{caption}</CardDescription>
        </div>
        <Button size="sm" variant="ghost" onClick={() => setShowTable((s) => !s)}>{showTable ? "Chart" : "Table"}</Button>
      </CardHeader>
      <CardContent>{showTable ? <div className="max-h-72 overflow-auto">{table}</div> : <div className="h-64">{children}</div>}</CardContent>
    </Card>
  );
}

function SimpleTable({ head, rows }: { head: string[]; rows: (string | number)[][] }) {
  return (
    <table className="w-full text-sm">
      <thead className="text-left text-xs text-muted-foreground">
        <tr>{head.map((h) => <th key={h} className="p-1.5">{h}</th>)}</tr>
      </thead>
      <tbody>
        {rows.map((r, i) => (
          <tr key={i} className="border-t">{r.map((c, j) => <td key={j} className="p-1.5">{c}</td>)}</tr>
        ))}
      </tbody>
    </table>
  );
}

const short = (d: string) => prettyDate(d, { day: "numeric", month: "short" });

export function ProgressPage() {
  const progress = useDsaProgress();
  const weakest = useDsaWeakest();
  const navigate = useNavigate();
  if (progress.isPending) return <Loading />;
  if (progress.isError) return <ErrorBox error={progress.error} />;
  const p = progress.data;
  const maxDay = Math.max(240, p.planDay + 10);
  const cumulative = [{ planDay: 0, cumulative: 0, date: "" }, ...p.cumulative];

  return (
    <div>
      <DsaNav />
      <PageHeader title="DSA progress" subtitle={`${p.countedTotal} meaningful attempts/reviews counted (${p.attemptsTotal} logged). ${p.caveat}`} />
      <div className="grid gap-6 lg:grid-cols-2">
        <ChartCard
          title="Cumulative meaningful attempts"
          caption="Bands: 65-80 by Day 90, about 120-135 by Day 220. Ranges, not pass/fail."
          table={<SimpleTable head={["Date", "Plan day", "Cumulative"]} rows={p.cumulative.map((c) => [c.date, c.planDay, c.cumulative])} />}
        >
          <ResponsiveContainer>
            <LineChart data={cumulative} margin={{ top: 8, right: 12, left: -16, bottom: 0 }}>
              <CartesianGrid stroke="var(--border)" strokeDasharray="3 3" />
              <XAxis dataKey="planDay" type="number" domain={[0, maxDay]} {...axis} label={{ value: "plan day", position: "insideBottomRight", offset: -2, fill: "var(--muted-foreground)", fontSize: 11 }} />
              <YAxis domain={[0, 150]} {...axis} />
              {p.markers.map((m) => (
                <ReferenceArea key={m.day} x1={m.day - 4} x2={m.day + 4} y1={m.min} y2={m.max} fill="var(--primary)" fillOpacity={0.25} stroke="var(--primary)" strokeOpacity={0.5} />
              ))}
              <ReferenceLine x={p.planDay} stroke="var(--muted-foreground)" strokeDasharray="4 4" label={{ value: "today", fill: "var(--muted-foreground)", fontSize: 10, position: "top" }} />
              <Tooltip contentStyle={tooltipStyle} />
              <Line type="stepAfter" dataKey="cumulative" stroke="var(--info)" strokeWidth={2} dot={false} isAnimationActive={false} />
            </LineChart>
          </ResponsiveContainer>
        </ChartCard>

        <ChartCard
          title="New problems per week"
          caption={`Guide: about ${p.weeklyGuideMin}-${p.weeklyGuideMax} new problems a week when stable.`}
          table={<SimpleTable head={["Week of", "New problems"]} rows={p.newProblemsByWeek.map((w) => [w.weekStart, w.count])} />}
        >
          <ResponsiveContainer>
            <BarChart data={p.newProblemsByWeek.map((w) => ({ ...w, label: short(w.weekStart) }))} margin={{ top: 8, right: 12, left: -16, bottom: 0 }}>
              <CartesianGrid stroke="var(--border)" strokeDasharray="3 3" vertical={false} />
              <XAxis dataKey="label" {...axis} />
              <YAxis allowDecimals={false} domain={[0, (max: number) => Math.max(8, max)]} {...axis} />
              <ReferenceArea y1={p.weeklyGuideMin} y2={p.weeklyGuideMax} fill="var(--primary)" fillOpacity={0.15} />
              <Tooltip contentStyle={tooltipStyle} cursor={{ fill: "var(--accent)" }} />
              <Bar dataKey="count" name="new problems" fill="var(--info)" radius={[4, 4, 0, 0]} isAnimationActive={false} />
            </BarChart>
          </ResponsiveContainer>
        </ChartCard>

        <ChartCard
          title="Confidence of new attempts"
          caption="Weekly average, 1-5. Trends matter more than one week."
          table={<SimpleTable head={["Week of", "Average", "Attempts"]} rows={p.confidenceByWeek.map((w) => [w.weekStart, w.average ?? "-", w.attempts])} />}
        >
          <ResponsiveContainer>
            <LineChart data={p.confidenceByWeek.map((w) => ({ ...w, label: short(w.weekStart) }))} margin={{ top: 8, right: 12, left: -16, bottom: 0 }}>
              <CartesianGrid stroke="var(--border)" strokeDasharray="3 3" />
              <XAxis dataKey="label" {...axis} />
              <YAxis domain={[1, 5]} ticks={[1, 2, 3, 4, 5]} {...axis} />
              <Tooltip contentStyle={tooltipStyle} />
              <Line dataKey="average" stroke="var(--success)" strokeWidth={2} connectNulls isAnimationActive={false} />
            </LineChart>
          </ResponsiveContainer>
        </ChartCard>

        <Card>
          <CardHeader>
            <CardTitle className="text-base">Topic coverage</CardTitle>
            <CardDescription>
              Median time per counted new attempt: {p.medianMinutes != null ? `${p.medianMinutes} min` : "-"} · Cold reviews done: {p.coldReviewsDone} · Reviews due now: {p.reviewsDueOrOverdue}
            </CardDescription>
          </CardHeader>
          <CardContent className="max-h-72 overflow-auto">
            <SimpleTable
              head={["Topic", "Problems", "Attempted", "Solved", "Deferred"]}
              rows={p.topics.filter((t) => t.total > 0 || !t.isDeferred).map((t) => [t.topic + (t.isDeferred ? " (deferred)" : ""), t.total, t.attempted, t.solved, t.deferred])}
            />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="text-base">Repeated mistakes (last 8 weeks)</CardTitle>
            <CardDescription>Tags used twice or more. Pick one weak pattern and practise it with spaced reviews.</CardDescription>
          </CardHeader>
          <CardContent>
            {p.repeatedMistakes.length === 0 ? (
              <p className="text-sm text-muted-foreground">No repeated mistake tags yet.</p>
            ) : (
              <SimpleTable head={["Week of", "Tag", "Times"]} rows={p.repeatedMistakes.map((t) => [t.weekStart, t.tag, t.count])} />
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="text-base">Weakest problems</CardTitle>
            <CardDescription>Latest confidence 3 or lower, oldest review first. Used for the Week 13 cold redo and two mediums back-to-back.</CardDescription>
          </CardHeader>
          <CardContent className="grid gap-1 text-sm">
            {weakest.data?.length === 0 && <EmptyState title="Nothing weak yet" />}
            {weakest.data?.map((w) => (
              <div key={w.problemId} className="flex flex-wrap items-center gap-2">
                <span className="font-medium">{w.title}</span>
                <span className="text-xs text-muted-foreground">{w.topicName} · conf {w.lastConfidence} · {w.attempts}x{w.nextReviewDue ? ` · review ${short(w.nextReviewDue)}` : ""}</span>
                <Button size="sm" variant="ghost" className="ml-auto" onClick={() => navigate(`/dsa/attempts/new?type=ColdReview&problemId=${w.problemId}`)}>Cold redo</Button>
              </div>
            ))}
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
