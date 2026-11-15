import { Pencil, Plus, Trash2 } from "lucide-react";
import { useMemo, useState } from "react";
import { useSearchParams } from "react-router";
import { api, call, type S } from "@/api/client";
import { m5Keys, useAction, useAllTasks, useToday } from "@/api/hooks";
import { ConfirmDialog, ErrorBox, Field, Loading, PageHeader, selectClass } from "@/components/common";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { trackLabel } from "@/lib/format";
import { cn } from "@/lib/utils";
import { PlanNav, TaskStatusBadge, taskStatusLabel } from "./PlanShared";

type View = "board" | "week" | "track" | "phase";
const STATUSES: S["PlanTaskStatus"][] = ["NotStarted", "InProgress", "Done", "Deferred", "Skipped"];
const weekOf = (t: S["PlanTaskDto"]) => t.planWeek ?? Math.min(52, Math.floor((t.dayFrom - 1) / 7) + 1);

export function TasksPage() {
  const [search, setSearch] = useSearchParams();
  const view = (search.get("view") as View) || "week";
  const tasks = useAllTasks();
  const today = useToday();
  const [phase, setPhase] = useState<number | "">(today.data?.planDay?.phaseNumber ?? "");
  const [editing, setEditing] = useState<S["PlanTaskDto"] | "new" | null>(null);

  const setStatus = useAction(
    (args: { task: S["PlanTaskDto"]; status: S["PlanTaskStatus"] }) =>
      call(api.PATCH("/api/v1/plan/tasks/{id}/status", { params: { path: { id: args.task.id } }, body: { status: args.status, rowVersion: args.task.rowVersion } })),
    { invalidate: [m5Keys.tasks, ["today"]] },
  );

  const shown = useMemo(() => (tasks.data?.items ?? []).filter((t) => !phase || t.phaseNumber === phase), [tasks.data, phase]);
  const groups = useMemo(() => {
    const key = (t: S["PlanTaskDto"]) => (view === "track" ? trackLabel[t.track] : view === "phase" ? `Phase ${t.phaseNumber}` : `Week ${weekOf(t)}`);
    const map = new Map<string, S["PlanTaskDto"][]>();
    shown.forEach((t) => map.set(key(t), [...(map.get(key(t)) ?? []), t]));
    return [...map.entries()];
  }, [shown, view]);

  if (tasks.isPending) return <Loading />;
  if (tasks.isError) return <ErrorBox error={tasks.error} />;

  return (
    <div>
      <PlanNav />
      <PageHeader
        title="Plan tasks"
        subtitle="Weekly DSA and DDIA tasks complete from your problems and chapters (BR-18); you can still defer or skip them. Cut scope, not consistency."
        actions={<Button onClick={() => setEditing("new")}><Plus /> Add task</Button>}
      />
      <div className="mb-4 flex flex-wrap items-center gap-2 text-sm">
        <div className="flex rounded-md border p-0.5" role="tablist" aria-label="View">
          {(["week", "track", "phase", "board"] as const).map((v) => (
            <button key={v} role="tab" aria-selected={view === v} onClick={() => setSearch({ view: v })} className={cn("rounded px-3 py-1", view === v && "bg-accent font-medium")}>
              {v === "board" ? "Board" : `By ${v}`}
            </button>
          ))}
        </div>
        <select className={cn(selectClass, "w-40")} value={phase} onChange={(e) => setPhase(e.target.value ? Number(e.target.value) : "")} aria-label="Phase">
          <option value="">All phases</option>
          {[1, 2, 3, 4].map((p) => <option key={p} value={p}>Phase {p}</option>)}
        </select>
      </div>

      {view === "board" ? (
        <div className="grid gap-3 md:grid-cols-5">
          {STATUSES.map((s) => (
            <div key={s} className="grid content-start gap-2 rounded-lg border bg-card/40 p-2">
              <p className="px-1 text-sm font-medium">{taskStatusLabel[s]} <span className="text-muted-foreground">({shown.filter((t) => t.status === s).length})</span></p>
              {shown.filter((t) => t.status === s).map((t) => (
                <div key={t.id} className="grid gap-1 rounded-md border bg-card p-2 text-sm">
                  <span className="font-medium">{t.title}</span>
                  <span className="text-xs text-muted-foreground">{trackLabel[t.track]} · days {t.dayFrom}-{t.dayTo}{t.kind !== "Manual" ? " · from progress" : ""}</span>
                  <select
                    className={cn(selectClass, "h-7 text-xs")}
                    value={t.status}
                    aria-label={`Status of ${t.title}`}
                    onChange={(e) => setStatus.mutate({ task: t, status: e.target.value as S["PlanTaskStatus"] })}
                  >
                    {(t.kind === "Manual" ? STATUSES : (["NotStarted", "Deferred", "Skipped"] as const)).map((x) => (
                      <option key={x} value={x}>{t.kind !== "Manual" && x === "NotStarted" ? "Follow progress" : taskStatusLabel[x]}</option>
                    ))}
                    {t.kind !== "Manual" && !["NotStarted", "Deferred", "Skipped"].includes(t.status) && <option value={t.status}>{taskStatusLabel[t.status]} (from progress)</option>}
                  </select>
                </div>
              ))}
            </div>
          ))}
        </div>
      ) : (
        <div className="grid gap-4">
          {groups.map(([group, list]) => (
            <Card key={group}>
              <CardHeader>
                <CardTitle className="text-base">{group}</CardTitle>
              </CardHeader>
              <CardContent className="grid gap-1">
                {list.map((t) => (
                  <div key={t.id} className="flex flex-wrap items-center gap-2 rounded-md px-2 py-1.5 text-sm hover:bg-accent/40">
                    <Badge variant="outline" className="text-[10px]">{trackLabel[t.track]}</Badge>
                    <span className="font-medium">{t.title}</span>
                    {t.ddiaChapters?.length ? <span className="text-xs text-primary">{t.ddiaChapters.join(" · ")}</span> : null}
                    <span className="text-xs text-muted-foreground">days {t.dayFrom}-{t.dayTo}</span>
                    <span className="ml-auto flex items-center gap-1">
                      <TaskStatusBadge status={t.status} />
                      <Button size="icon" variant="ghost" className="size-7" onClick={() => setEditing(t)} aria-label={`Edit ${t.title}`}><Pencil /></Button>
                    </span>
                  </div>
                ))}
              </CardContent>
            </Card>
          ))}
        </div>
      )}
      <TaskDialog value={editing} defaultPhase={today.data?.planDay?.phaseNumber ?? 1} defaultDay={today.data?.planDay?.day ?? 1} onClose={() => setEditing(null)} />
    </div>
  );
}

function TaskDialog({ value, defaultPhase, defaultDay, onClose }: { value: S["PlanTaskDto"] | "new" | null; defaultPhase: number; defaultDay: number; onClose: () => void }) {
  const existing = value && value !== "new" ? value : null;
  const [loadedFor, setLoadedFor] = useState<unknown>(null);
  const [f, setF] = useState({ phase: 1, track: "Other" as S["PlanTrack"], title: "", description: "", output: "", fallback: "", from: 1, to: 7, status: "NotStarted" as S["PlanTaskStatus"], notes: "", url: "" });
  if (value !== loadedFor) {
    setLoadedFor(value);
    const day = Math.max(1, defaultDay);
    setF({
      phase: existing?.phaseNumber ?? defaultPhase,
      track: existing?.track ?? "Other",
      title: existing?.title ?? "",
      description: existing?.description ?? "",
      output: existing?.outputRequired ?? "",
      fallback: existing?.fallback ?? "",
      from: existing?.dayFrom ?? day,
      to: existing?.dayTo ?? Math.min(day + 6, 365),
      status: existing?.status ?? "NotStarted",
      notes: "",
      url: "",
    });
  }
  const derived = existing && existing.kind !== "Manual";
  const save = useAction(
    () => {
      const body: S["PlanTaskInput"] = {
        phaseNumber: f.phase, track: f.track, title: f.title, description: f.description || null, outputRequired: f.output || null, fallback: f.fallback || null,
        dayFrom: f.from, dayTo: f.to, status: f.status, notesMd: f.notes || null, evidenceUrl: f.url || null, sortOrder: 500, rowVersion: existing?.rowVersion ?? null,
      };
      return existing ? call(api.PUT("/api/v1/plan/tasks/{id}", { params: { path: { id: existing.id } }, body })) : call(api.POST("/api/v1/plan/tasks", { body }));
    },
    { invalidate: [m5Keys.tasks, ["today"]], success: "Task saved.", onSuccess: onClose },
  );
  const del = useAction(() => call(api.DELETE("/api/v1/plan/tasks/{id}", { params: { path: { id: existing!.id } } })), {
    invalidate: [m5Keys.tasks, ["today"]],
    success: "Task moved to Trash.",
    onSuccess: onClose,
  });
  return (
    <Dialog open={value !== null} onOpenChange={(o) => !o && onClose()}>
      <DialogContent className="sm:max-w-2xl">
        <DialogHeader>
          <DialogTitle>{existing ? "Edit task" : "Add task"}</DialogTitle>
          {derived && <DialogDescription>This weekly task completes from your logged progress. Set Deferred or Skipped to override it; anything else follows progress again.</DialogDescription>}
        </DialogHeader>
        <div className="grid gap-3 sm:grid-cols-2">
          <Field label="Title" className="sm:col-span-2">{(id) => <Input id={id} value={f.title} onChange={(e) => setF({ ...f, title: e.target.value })} />}</Field>
          <Field label="Phase">
            {(id) => <select id={id} className={selectClass} value={f.phase} onChange={(e) => setF({ ...f, phase: Number(e.target.value) })}>{[1, 2, 3, 4].map((p) => <option key={p} value={p}>Phase {p}</option>)}</select>}
          </Field>
          <Field label="Track">
            {(id) => <select id={id} className={selectClass} value={f.track} onChange={(e) => setF({ ...f, track: e.target.value as S["PlanTrack"] })}>{Object.entries(trackLabel).map(([k, v]) => <option key={k} value={k}>{v}</option>)}</select>}
          </Field>
          <Field label="From plan day">{(id) => <Input id={id} type="number" min={1} value={f.from} onChange={(e) => setF({ ...f, from: Number(e.target.value) })} />}</Field>
          <Field label="To plan day">{(id) => <Input id={id} type="number" min={1} value={f.to} onChange={(e) => setF({ ...f, to: Number(e.target.value) })} />}</Field>
          <Field label="Status">
            {(id) => <select id={id} className={selectClass} value={f.status} onChange={(e) => setF({ ...f, status: e.target.value as S["PlanTaskStatus"] })}>{STATUSES.map((s) => <option key={s} value={s}>{taskStatusLabel[s]}</option>)}</select>}
          </Field>
          <Field label="Evidence link (optional)">{(id) => <Input id={id} value={f.url} onChange={(e) => setF({ ...f, url: e.target.value })} />}</Field>
          <Field label="Description" className="sm:col-span-2">{(id) => <Textarea id={id} rows={2} value={f.description} onChange={(e) => setF({ ...f, description: e.target.value })} />}</Field>
          <Field label="Output required">{(id) => <Input id={id} value={f.output} onChange={(e) => setF({ ...f, output: e.target.value })} />}</Field>
          <Field label="Fallback">{(id) => <Input id={id} value={f.fallback} onChange={(e) => setF({ ...f, fallback: e.target.value })} />}</Field>
        </div>
        <DialogFooter>
          {existing && <ConfirmDialog trigger={<Button variant="ghost"><Trash2 /> Delete</Button>} title="Delete this task?" description="It moves to the Trash." onConfirm={() => del.mutate(undefined)} />}
          <Button disabled={!f.title.trim() || save.isPending} onClick={() => save.mutate(undefined)}>Save</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
