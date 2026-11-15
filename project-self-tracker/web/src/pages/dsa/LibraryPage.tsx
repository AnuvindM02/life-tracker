import { ArrowDown, ArrowUp, ExternalLink, FileUp, Pencil, Play, Plus, Trash2 } from "lucide-react";
import { useState } from "react";
import { useNavigate } from "react-router";
import { api, call, type S } from "@/api/client";
import { dsaKeys, useAction, useDsaProblems, useDsaTopics } from "@/api/hooks";
import { ConfirmDialog, ErrorBox, Field, Loading, PageHeader, selectClass } from "@/components/common";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Checkbox } from "@/components/ui/checkbox";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { prettyDate } from "@/lib/format";
import { cn } from "@/lib/utils";
import { DsaNav, StatusBadge, statusLabel } from "./DsaNav";

type ProblemDialogValue = S["DsaProblemDto"] | { topicId: number } | null;

const problemBody = (p: S["DsaProblemDto"], patch: Partial<S["DsaProblemInput"]> = {}): S["DsaProblemInput"] => ({
  topicId: p.topicId,
  title: p.title,
  url: p.url,
  difficulty: p.difficulty,
  planWeek: p.planWeek,
  sortOrder: p.sortOrder,
  status: p.status,
  rowVersion: p.rowVersion,
  ...patch,
});

export function LibraryPage() {
  const topics = useDsaTopics();
  const problems = useDsaProblems();
  const navigate = useNavigate();
  const [status, setStatus] = useState("");
  const [q, setQ] = useState("");
  const [editing, setEditing] = useState<ProblemDialogValue>(null);
  const [editingTopic, setEditingTopic] = useState<S["DsaTopicDto"] | "new" | null>(null);
  const [importing, setImporting] = useState(false);

  const update = useAction((args: { id: number; body: S["DsaProblemInput"] }) => call(api.PUT("/api/v1/dsa/problems/{id}", { params: { path: { id: args.id } }, body: args.body })), {
    invalidate: [dsaKeys.all, ["today"]],
  });
  const del = useAction((id: number) => call(api.DELETE("/api/v1/dsa/problems/{id}", { params: { path: { id } } })), {
    invalidate: [dsaKeys.all, ["today"]],
    success: "Problem moved to Trash.",
  });

  async function move(list: S["DsaProblemDto"][], index: number, dir: -1 | 1) {
    const a = list[index];
    const b = list[index + dir];
    if (!b) return;
    // Swap sort orders; if equal, nudge apart.
    const aOrder = b.sortOrder === a.sortOrder ? a.sortOrder + dir : b.sortOrder;
    await update.mutateAsync({ id: a.id, body: problemBody(a, { sortOrder: aOrder }) });
    await update.mutateAsync({ id: b.id, body: problemBody(b, { sortOrder: a.sortOrder }) });
  }

  if (topics.isPending || problems.isPending) return <Loading />;
  if (topics.isError) return <ErrorBox error={topics.error} />;
  if (problems.isError) return <ErrorBox error={problems.error} />;

  const all = problems.data.items.filter((p) => (!status || p.status === status) && (!q || p.title.toLowerCase().includes(q.toLowerCase())));

  return (
    <div>
      <DsaNav />
      <PageHeader
        title="Problem library"
        subtitle="Seeded with the problems the roadmap names (Appendix C). Add the rest of your sequence by hand or by CSV."
        actions={
          <>
            <Button variant="outline" onClick={() => setImporting(true)}><FileUp /> Import CSV</Button>
            <Button variant="outline" onClick={() => setEditingTopic("new")}><Plus /> Topic</Button>
          </>
        }
      />
      <div className="mb-4 flex flex-wrap gap-2">
        <Input className="w-64" placeholder="Search problems" value={q} onChange={(e) => setQ(e.target.value)} aria-label="Search problems" />
        <select className={cn(selectClass, "w-40")} value={status} onChange={(e) => setStatus(e.target.value)} aria-label="Status">
          <option value="">All statuses</option>
          {Object.entries(statusLabel).map(([k, v]) => <option key={k} value={k}>{v}</option>)}
        </select>
      </div>
      <div className="grid gap-4">
        {topics.data.items.map((t) => {
          const list = all.filter((p) => p.topicId === t.id).sort((a, b) => a.sortOrder - b.sortOrder || a.id - b.id);
          if ((q || status) && list.length === 0) return null;
          return (
            <Card key={t.id} className={cn(t.isDeferred && "opacity-70")}>
              <CardHeader className="flex-row flex-wrap items-center gap-2">
                <CardTitle className="text-base">{t.name}</CardTitle>
                <span className="text-xs text-muted-foreground">{list.length} problems</span>
                {t.isDeferred && <Badge variant="outline">deferred</Badge>}
                <span className="ml-auto flex gap-1">
                  <Button size="sm" variant="ghost" onClick={() => setEditing({ topicId: t.id })}><Plus /> Problem</Button>
                  <Button size="icon" variant="ghost" onClick={() => setEditingTopic(t)} aria-label={`Edit topic ${t.name}`}><Pencil /></Button>
                </span>
              </CardHeader>
              {list.length > 0 && (
                <CardContent className="grid gap-0.5">
                  {list.map((p, i) => (
                    <div key={p.id} className="flex flex-wrap items-center gap-2 rounded-md px-2 py-1.5 text-sm hover:bg-accent/40">
                      <div className="flex flex-col">
                        <button type="button" className="text-muted-foreground hover:text-foreground disabled:opacity-30" disabled={i === 0} onClick={() => void move(list, i, -1)} aria-label={`Move ${p.title} up`}><ArrowUp className="size-3" /></button>
                        <button type="button" className="text-muted-foreground hover:text-foreground disabled:opacity-30" disabled={i === list.length - 1} onClick={() => void move(list, i, 1)} aria-label={`Move ${p.title} down`}><ArrowDown className="size-3" /></button>
                      </div>
                      {p.planWeek && <Badge variant="outline" className="text-[10px]">W{p.planWeek}</Badge>}
                      <span className="font-medium">{p.title}</span>
                      {p.url && <a href={p.url} target="_blank" rel="noreferrer noopener" aria-label={`Open ${p.title}`}><ExternalLink className="size-3.5 text-muted-foreground" /></a>}
                      {p.difficulty && <span className="text-xs text-muted-foreground">{p.difficulty}</span>}
                      <StatusBadge status={p.status} />
                      {p.attemptCount > 0 && <span className="text-xs text-muted-foreground">{p.attemptCount}x · last conf {p.lastConfidence}</span>}
                      {p.nextReviewDue && <span className="text-xs text-primary">review {prettyDate(p.nextReviewDue, { day: "numeric", month: "short" })}</span>}
                      <span className="ml-auto flex items-center gap-1">
                        <Button size="sm" variant="outline" onClick={() => navigate(`/dsa/attempts/new?problemId=${p.id}`)}><Play /> Attempt</Button>
                        <Button size="sm" variant="ghost" onClick={() => update.mutate({ id: p.id, body: problemBody(p, { status: p.status === "Deferred" ? (p.attemptCount ? "Attempted" : "NotStarted") : "Deferred" }) })}>
                          {p.status === "Deferred" ? "Undefer" : "Defer"}
                        </Button>
                        <Button size="icon" variant="ghost" onClick={() => setEditing(p)} aria-label={`Edit ${p.title}`}><Pencil /></Button>
                        <ConfirmDialog
                          trigger={<Button size="icon" variant="ghost" aria-label={`Delete ${p.title}`}><Trash2 /></Button>}
                          title={`Delete "${p.title}"?`}
                          description="It moves to the Trash. Its attempts stay in the mistake log."
                          onConfirm={() => del.mutate(p.id)}
                        />
                      </span>
                    </div>
                  ))}
                </CardContent>
              )}
            </Card>
          );
        })}
      </div>
      <ProblemDialog value={editing} topics={topics.data.items} onClose={() => setEditing(null)} />
      <TopicDialog value={editingTopic} onClose={() => setEditingTopic(null)} />
      <ImportDialog open={importing} onClose={() => setImporting(false)} />
    </div>
  );
}

function ProblemDialog({ value, topics, onClose }: { value: ProblemDialogValue; topics: S["DsaTopicDto"][]; onClose: () => void }) {
  const existing = value && "id" in value ? value : null;
  const [loadedFor, setLoadedFor] = useState<unknown>(null);
  const [f, setF] = useState({ topicId: 0, title: "", url: "", difficulty: "", week: "", sort: 0, status: "NotStarted" as S["ProblemStatus"] });
  if (value !== loadedFor) {
    setLoadedFor(value);
    setF({
      topicId: existing?.topicId ?? (value && "topicId" in value ? value.topicId : topics[0]?.id ?? 0),
      title: existing?.title ?? "",
      url: existing?.url ?? "",
      difficulty: existing?.difficulty ?? "",
      week: existing?.planWeek ? String(existing.planWeek) : "",
      sort: existing?.sortOrder ?? 9999,
      status: existing?.status ?? "NotStarted",
    });
  }
  const save = useAction(
    () => {
      const body: S["DsaProblemInput"] = {
        topicId: f.topicId,
        title: f.title,
        url: f.url || null,
        difficulty: (f.difficulty || null) as S["DsaProblemInput"]["difficulty"],
        planWeek: f.week ? Number(f.week) : null,
        sortOrder: f.sort,
        status: f.status,
        rowVersion: existing?.rowVersion ?? null,
      };
      return existing
        ? call(api.PUT("/api/v1/dsa/problems/{id}", { params: { path: { id: existing.id } }, body }))
        : call(api.POST("/api/v1/dsa/problems", { body }));
    },
    { invalidate: [dsaKeys.all, ["today"]], success: "Problem saved.", onSuccess: onClose },
  );
  return (
    <Dialog open={value !== null} onOpenChange={(o) => !o && onClose()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{existing ? "Edit problem" : "Add problem"}</DialogTitle>
        </DialogHeader>
        <div className="grid gap-3">
          <Field label="Title">{(id) => <Input id={id} value={f.title} onChange={(e) => setF({ ...f, title: e.target.value })} />}</Field>
          <Field label="Topic">
            {(id) => (
              <select id={id} className={selectClass} value={f.topicId} onChange={(e) => setF({ ...f, topicId: Number(e.target.value) })}>
                {topics.map((t) => <option key={t.id} value={t.id}>{t.name}</option>)}
              </select>
            )}
          </Field>
          <Field label="URL (optional)">{(id) => <Input id={id} value={f.url} onChange={(e) => setF({ ...f, url: e.target.value })} placeholder="https://leetcode.com/problems/..." />}</Field>
          <div className="grid grid-cols-3 gap-3">
            <Field label="Difficulty">
              {(id) => (
                <select id={id} className={selectClass} value={f.difficulty} onChange={(e) => setF({ ...f, difficulty: e.target.value })}>
                  <option value="">-</option>
                  <option>Easy</option>
                  <option>Medium</option>
                  <option>Hard</option>
                </select>
              )}
            </Field>
            <Field label="Plan week">{(id) => <Input id={id} type="number" min={1} max={52} value={f.week} onChange={(e) => setF({ ...f, week: e.target.value })} />}</Field>
            <Field label="Status">
              {(id) => (
                <select id={id} className={selectClass} value={f.status} onChange={(e) => setF({ ...f, status: e.target.value as S["ProblemStatus"] })}>
                  {Object.entries(statusLabel).map(([k, v]) => <option key={k} value={k}>{v}</option>)}
                </select>
              )}
            </Field>
          </div>
        </div>
        <DialogFooter>
          <Button disabled={!f.title.trim() || !f.topicId || save.isPending} onClick={() => save.mutate(undefined)}>Save</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

function TopicDialog({ value, onClose }: { value: S["DsaTopicDto"] | "new" | null; onClose: () => void }) {
  const existing = value && value !== "new" ? value : null;
  const [loadedFor, setLoadedFor] = useState<unknown>(null);
  const [name, setName] = useState("");
  const [deferred, setDeferred] = useState(false);
  const [sort, setSort] = useState(1000);
  if (value !== loadedFor) {
    setLoadedFor(value);
    setName(existing?.name ?? "");
    setDeferred(existing?.isDeferred ?? false);
    setSort(existing?.sortOrder ?? 1000);
  }
  const save = useAction(
    () => {
      const body = { name, isDeferred: deferred, sortOrder: sort, rowVersion: existing?.rowVersion ?? null };
      return existing ? call(api.PUT("/api/v1/dsa/topics/{id}", { params: { path: { id: existing.id } }, body })) : call(api.POST("/api/v1/dsa/topics", { body }));
    },
    { invalidate: [dsaKeys.all], success: "Topic saved.", onSuccess: onClose },
  );
  const del = useAction(() => call(api.DELETE("/api/v1/dsa/topics/{id}", { params: { path: { id: existing!.id } } })), {
    invalidate: [dsaKeys.all],
    success: "Topic moved to Trash.",
    onSuccess: onClose,
  });
  return (
    <Dialog open={value !== null} onOpenChange={(o) => !o && onClose()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{existing ? "Edit topic" : "Add topic"}</DialogTitle>
          <DialogDescription>Deferring a topic (for example Math & Geometry or Bit Manipulation) keeps it out of the way without deleting anything.</DialogDescription>
        </DialogHeader>
        <div className="grid gap-3">
          <Field label="Name">{(id) => <Input id={id} value={name} onChange={(e) => setName(e.target.value)} />}</Field>
          <Field label="Order">{(id) => <Input id={id} type="number" value={sort} onChange={(e) => setSort(Number(e.target.value))} />}</Field>
          <label className="flex items-center gap-2 text-sm"><Checkbox checked={deferred} onCheckedChange={(c) => setDeferred(c === true)} /> Deferred</label>
        </div>
        <DialogFooter>
          {existing && <Button variant="ghost" onClick={() => del.mutate(undefined)}>Delete topic</Button>}
          <Button disabled={!name.trim() || save.isPending} onClick={() => save.mutate(undefined)}>Save</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

function ImportDialog({ open, onClose }: { open: boolean; onClose: () => void }) {
  const [csv, setCsv] = useState("title,topic,url,difficulty,week\n");
  const [result, setResult] = useState<S["ImportProblemsResult"] | null>(null);
  const run = useAction((dryRun: boolean) => call(api.POST("/api/v1/dsa/problems/import", { body: { csv, dryRun } })), {
    invalidate: [dsaKeys.all],
    onSuccess: setResult,
  });
  return (
    <Dialog open={open} onOpenChange={(o) => { if (!o) { setResult(null); onClose(); } }}>
      <DialogContent className="sm:max-w-2xl">
        <DialogHeader>
          <DialogTitle>Import problems from CSV</DialogTitle>
          <DialogDescription>Header: title, topic, url, difficulty (Easy/Medium/Hard), week. Unknown topics are created; existing problems in the same topic are skipped.</DialogDescription>
        </DialogHeader>
        <Textarea rows={10} className="font-mono text-xs" value={csv} onChange={(e) => setCsv(e.target.value)} aria-label="CSV" />
        <label className="text-sm">
          Or load a file: <input type="file" accept=".csv,text/csv" onChange={async (e) => { const f = e.target.files?.[0]; if (f) setCsv(await f.text()); }} />
        </label>
        {result && (
          <div className="text-sm">
            <p>{result.dryRun ? "Check only: " : "Imported: "}{result.created} new, {result.skipped} already in the library{result.topicsCreated.length ? `, new topics: ${result.topicsCreated.join(", ")}` : ""}.</p>
            {result.errors.map((e) => <p key={e} className="text-destructive">{e}</p>)}
          </div>
        )}
        <DialogFooter>
          <Button variant="outline" onClick={() => run.mutate(true)}>Check</Button>
          <Button onClick={() => run.mutate(false)}>Import</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
