import { CheckCircle2, Circle, Plus } from "lucide-react";
import { useEffect, useState } from "react";
import { Link, useLocation, useParams } from "react-router";
import { api, call, type S } from "@/api/client";
import { m5Keys, useAction, useAllTasks, useCheckpoints, useExitCriteria, usePhases, useReplans } from "@/api/hooks";
import { Attachments } from "@/components/Attachments";
import { EmptyState, ErrorBox, Field, Loading, PageHeader, selectClass } from "@/components/common";
import { MarkdownEditor } from "@/components/MarkdownEditor";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Checkbox } from "@/components/ui/checkbox";
import { Input } from "@/components/ui/input";
import { isoDate, prettyDate, trackLabel } from "@/lib/format";
import { cn } from "@/lib/utils";
import { checkpointStateLabel, PlanNav, TaskStatusBadge, triggerLabel } from "./PlanShared";

export function CheckpointsPage() {
  const cps = useCheckpoints();
  const { hash } = useLocation();
  useEffect(() => {
    if (hash) document.getElementById(hash.slice(1))?.scrollIntoView({ behavior: "smooth" });
  }, [hash, cps.data]);
  if (cps.isPending) return <Loading />;
  if (cps.isError) return <ErrorBox error={cps.error} />;
  return (
    <div>
      <PlanNav />
      <PageHeader title="Checkpoints" subtitle="Answer each in writing. Due dates follow your plan days, so pauses move them. Re-plan only when the evidence says so." />
      <div className="grid gap-4">
        {cps.data.map((c) => <CheckpointCard key={c.id} c={c} />)}
      </div>
    </div>
  );
}

function CheckpointCard({ c }: { c: S["CheckpointDto"] }) {
  const [answer, setAnswer] = useState(c.answerMd ?? "");
  const [outcome, setOutcome] = useState<S["CheckpointOutcome"] | "">(c.outcome ?? "");
  const [open, setOpen] = useState(c.state === "Soon" || c.state === "DueToday" || c.state === "Overdue");
  const save = useAction(
    () => call(api.PUT("/api/v1/plan/checkpoints/{id}", { params: { path: { id: c.id } }, body: { answerMd: answer || null, outcome: outcome || null, rowVersion: c.rowVersion } })),
    { invalidate: [m5Keys.checkpoints, ["today"]], success: "Checkpoint saved." },
  );
  return (
    <Card id={`cp-${c.id}`} className={cn((c.state === "Soon" || c.state === "DueToday" || c.state === "Overdue") && "border-primary/50")}>
      <CardHeader className="flex-row flex-wrap items-center gap-2">
        <CardTitle className="text-base">Day {c.planDay}{c.dayTo ? `-${c.dayTo}` : ""}: {c.title}</CardTitle>
        <Badge variant={c.state === "Answered" ? "default" : c.state === "Upcoming" ? "outline" : "secondary"}>{checkpointStateLabel[c.state]}</Badge>
        {c.dueDate && <span className="text-xs text-muted-foreground">{prettyDate(c.dueDate)}{c.windowEnd ? ` to ${prettyDate(c.windowEnd)}` : ""}</span>}
        <Button size="sm" variant="ghost" className="ml-auto" onClick={() => setOpen((o) => !o)}>{open ? "Close" : c.answerMd ? "View answer" : "Answer"}</Button>
      </CardHeader>
      <CardContent className="grid gap-3 text-sm">
        <p>{c.question}</p>
        {c.context.length > 0 && (
          <ul className="grid gap-0.5 rounded-md bg-muted/50 p-2 text-xs">
            {c.context.map((x) => <li key={x}>{x}</li>)}
          </ul>
        )}
        {open && (
          <>
            <MarkdownEditor value={answer} onChange={setAnswer} rows={5} placeholder="Answer in writing." />
            <div className="flex flex-wrap items-center gap-2">
              <select className={cn(selectClass, "w-48")} value={outcome ?? ""} onChange={(e) => setOutcome(e.target.value as S["CheckpointOutcome"])} aria-label="Outcome">
                <option value="">No outcome yet</option>
                <option value="OnTrack">On track</option>
                <option value="Slip">Slip</option>
                <option value="Replan">Re-plan</option>
              </select>
              <Button onClick={() => save.mutate(undefined)} disabled={save.isPending}>Save answer</Button>
              {outcome === "Replan" && <Link to={`/plan/replans?checkpoint=${c.id}`} className="text-primary underline">Record the re-plan</Link>}
            </div>
            <Attachments entityType="Checkpoint" entityId={c.id} label="Checkpoint" compact />
          </>
        )}
      </CardContent>
    </Card>
  );
}

const FOUR_TRIGGERS = [
  "Multiple relevant postings or interviews show the same skill mismatch",
  "Phase 1 is more than three weeks behind",
  "Your current job, health, finances or family commitments change materially",
  "A package/framework change blocks a milestone for more than two focused sessions",
];

export function ReplansPage() {
  const replans = useReplans();
  const cps = useCheckpoints();
  const { search } = useLocation();
  const fromCheckpoint = new URLSearchParams(search).get("checkpoint");
  const [trigger, setTrigger] = useState<S["ReplanTrigger"]>(fromCheckpoint ? "Checkpoint" : "Phase1Behind");
  const [checks, setChecks] = useState<boolean[]>([false, false, false, false]);
  const [evidence, setEvidence] = useState("");
  const [decision, setDecision] = useState("");
  const [checkpointId, setCheckpointId] = useState<number | "">(fromCheckpoint ? Number(fromCheckpoint) : "");
  const save = useAction(
    () =>
      call(
        api.POST("/api/v1/plan/replans", {
          body: { date: isoDate(), trigger, evidenceMd: evidence, decisionMd: decision || null, checkpointId: checkpointId || null, rowVersion: null },
        }),
      ),
    {
      invalidate: [m5Keys.replans],
      success: "Re-plan recorded.",
      onSuccess: () => {
        setEvidence("");
        setDecision("");
        setChecks([false, false, false, false]);
      },
    },
  );
  return (
    <div>
      <PlanNav />
      <PageHeader title="Re-plan log" subtitle="Re-plan deliberately, only at a checkpoint or when one of the four triggers is true. One friend's offer or one viral post is not enough evidence." />
      <div className="grid gap-6 lg:grid-cols-3">
        <Card className="lg:col-span-2">
          <CardHeader>
            <CardTitle className="text-base">Record a re-plan</CardTitle>
            <CardDescription>Do not revise the plan again unless checkpoint evidence tells you to.</CardDescription>
          </CardHeader>
          <CardContent className="grid gap-4">
            <fieldset className="grid gap-1.5 rounded-md border p-3 text-sm">
              <legend className="px-1 text-sm font-medium">What would change this plan (roadmap 1.5)</legend>
              {FOUR_TRIGGERS.map((t, i) => (
                <label key={t} className="flex items-start gap-2">
                  <Checkbox className="mt-0.5" checked={checks[i]} onCheckedChange={(c) => setChecks(checks.map((x, j) => (j === i ? c === true : x)))} /> {t}
                </label>
              ))}
              {!checks.some(Boolean) && trigger !== "Checkpoint" && <p className="text-xs text-muted-foreground">None ticked: consider keeping the plan and reviewing trends at the next checkpoint.</p>}
            </fieldset>
            <div className="grid gap-3 sm:grid-cols-2">
              <Field label="Trigger">
                {(id) => <select id={id} className={selectClass} value={trigger} onChange={(e) => setTrigger(e.target.value as S["ReplanTrigger"])}>{Object.entries(triggerLabel).map(([k, v]) => <option key={k} value={k}>{v}</option>)}</select>}
              </Field>
              <Field label="Checkpoint (optional)">
                {(id) => (
                  <select id={id} className={selectClass} value={checkpointId} onChange={(e) => setCheckpointId(e.target.value ? Number(e.target.value) : "")}>
                    <option value="">None</option>
                    {cps.data?.map((c) => <option key={c.id} value={c.id}>Day {c.planDay}: {c.title}</option>)}
                  </select>
                )}
              </Field>
            </div>
            <Field label="Evidence">{(id) => <MarkdownEditor id={id} value={evidence} onChange={setEvidence} rows={4} />}</Field>
            <Field label="Decision">{(id) => <MarkdownEditor id={id} value={decision} onChange={setDecision} rows={3} />}</Field>
            <Button className="justify-self-start" disabled={!evidence.trim() || save.isPending} onClick={() => save.mutate(undefined)}>Record re-plan</Button>
          </CardContent>
        </Card>
        <Card className="content-start">
          <CardHeader>
            <CardTitle className="text-base">History</CardTitle>
          </CardHeader>
          <CardContent className="grid gap-2 text-sm">
            {replans.data?.items.length === 0 && <EmptyState title="No re-plans" />}
            {replans.data?.items.map((r) => (
              <div key={r.id} className="grid gap-0.5 rounded-md border p-2">
                <span className="font-medium">{prettyDate(r.date)}</span>
                <span className="text-xs text-muted-foreground">{triggerLabel[r.trigger]}</span>
                <span className="line-clamp-3 text-xs">{r.decisionMd ?? r.evidenceMd}</span>
              </div>
            ))}
          </CardContent>
        </Card>
      </div>
    </div>
  );
}

export function PhasePage() {
  const n = Number(useParams().n);
  const phases = usePhases();
  const exits = useExitCriteria(n);
  const tasks = useAllTasks();
  const phase = phases.data?.find((p) => p.number === n);
  if (phases.isPending || exits.isPending) return <Loading />;
  if (!phase) return <EmptyState title="Unknown phase" />;
  const inPhase = (tasks.data?.items ?? []).filter((t) => t.phaseNumber === n);
  const groups = [...new Set((exits.data ?? []).map((e) => e.group ?? ""))];
  return (
    <div>
      <PlanNav />
      <PageHeader
        title={`Phase ${phase.number}: ${phase.name}`}
        subtitle={`Days ${phase.dayFrom}-${phase.dayTo} · ${phase.objective}`}
        actions={<Button variant="outline" asChild><Link to="/plan/tasks?view=board">Task board</Link></Button>}
      />
      <div className="grid gap-6 lg:grid-cols-3">
        <div className="grid content-start gap-6 lg:col-span-2">
          {groups.map((g) => (
            <Card key={g || "exit"}>
              <CardHeader>
                <CardTitle className="text-base">{g || "Exit criteria"}</CardTitle>
                <CardDescription>{g ? "Readiness checklist." : `Exit evidence: ${phase.exitEvidence}`}</CardDescription>
              </CardHeader>
              <CardContent className="grid gap-3">
                {(exits.data ?? []).filter((e) => (e.group ?? "") === g).map((e) => <ExitRow key={e.id} e={e} />)}
              </CardContent>
            </Card>
          ))}
          <AddExit phase={n} />
        </div>
        <Card className="content-start">
          <CardHeader>
            <CardTitle className="text-base">Tasks in this phase</CardTitle>
            <CardDescription>{inPhase.filter((t) => t.status === "Done").length} of {inPhase.length} done</CardDescription>
          </CardHeader>
          <CardContent className="grid max-h-[32rem] gap-1 overflow-auto text-sm">
            {inPhase.map((t) => (
              <div key={t.id} className="flex items-center gap-2">
                <span className="min-w-0 flex-1 truncate" title={t.title}>{t.title}</span>
                <span className="text-[10px] text-muted-foreground">{trackLabel[t.track]}</span>
                <TaskStatusBadge status={t.status} />
              </div>
            ))}
          </CardContent>
        </Card>
      </div>
    </div>
  );
}

function ExitRow({ e }: { e: S["ExitCriterionDto"] }) {
  const [evidence, setEvidence] = useState(e.evidenceText ?? "");
  const [showFiles, setShowFiles] = useState(false);
  const save = useAction(
    (done: boolean) =>
      call(
        api.PUT("/api/v1/plan/exit-criteria/{id}", {
          params: { path: { id: e.id } },
          body: { phaseNumber: e.phaseNumber, group: e.group, text: e.text, evidenceHint: e.evidenceHint, done, evidenceText: evidence || null, sortOrder: e.sortOrder, rowVersion: e.rowVersion },
        }),
      ),
    { invalidate: [["exits"], m5Keys.checkpoints] },
  );
  return (
    <div className="grid gap-2 rounded-md border p-3 text-sm">
      <label className="flex items-start gap-2">
        <Checkbox className="mt-0.5" checked={e.done} onCheckedChange={(c) => save.mutate(c === true)} />
        <span className={cn("font-medium", e.done && "text-muted-foreground")}>{e.text}</span>
        {e.done ? <CheckCircle2 className="ml-auto size-4 text-success" aria-label="done" /> : <Circle className="ml-auto size-4 text-muted-foreground" aria-label="not done" />}
      </label>
      {e.evidenceHint && <p className="pl-6 text-xs text-muted-foreground">Evidence to show: {e.evidenceHint}</p>}
      <div className="flex gap-2 pl-6">
        <Input value={evidence} onChange={(x) => setEvidence(x.target.value)} placeholder="Evidence (link, number, where it lives)" aria-label="Evidence" />
        <Button size="sm" variant="outline" onClick={() => save.mutate(e.done)}>Save</Button>
        <Button size="sm" variant="ghost" onClick={() => setShowFiles((s) => !s)}>Files{e.attachmentCount ? ` (${e.attachmentCount})` : ""}</Button>
      </div>
      {e.doneOn && <p className="pl-6 text-xs text-muted-foreground">Done {prettyDate(e.doneOn)}</p>}
      {showFiles && <div className="pl-6"><Attachments entityType="ExitCriterion" entityId={e.id} label="Exit criterion evidence" compact /></div>}
    </div>
  );
}

function AddExit({ phase }: { phase: number }) {
  const [text, setText] = useState("");
  const add = useAction(
    () => call(api.POST("/api/v1/plan/exit-criteria", { body: { phaseNumber: phase, group: null, text, evidenceHint: null, done: false, evidenceText: null, sortOrder: 500, rowVersion: null } })),
    { invalidate: [["exits"]], onSuccess: () => setText("") },
  );
  return (
    <div className="flex gap-2">
      <Input value={text} onChange={(e) => setText(e.target.value)} placeholder="Add your own exit criterion" aria-label="New exit criterion" />
      <Button variant="outline" disabled={!text.trim()} onClick={() => add.mutate(undefined)}><Plus /> Add</Button>
    </div>
  );
}
