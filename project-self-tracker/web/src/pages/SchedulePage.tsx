import { Pencil, Plus, RotateCcw, Trash2 } from "lucide-react";
import { useState } from "react";
import { api, call, type S } from "@/api/client";
import { keys, useAction, useBlocks, useGymPlan, useToday } from "@/api/hooks";
import { ConfirmDialog, ErrorBox, Field, Loading, PageHeader, selectClass } from "@/components/common";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Checkbox } from "@/components/ui/checkbox";
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { Textarea } from "@/components/ui/textarea";
import { PlanNav } from "@/pages/plan/PlanShared";

const PHASE_INFO: Record<S["SchedulePhase"], string> = {
  A: "Cold start (weeks 1-3): gym first at 18:15, then DSA after dinner.",
  B: "Build (weeks 4-8): short morning DSA attempt, gym at 18:15, rewrite/DDIA/build after dinner.",
  C: "Full schedule (week 9 on): DSA 06:50, gym at 18:15, stable DDIA/build after dinner. Stretch blocks are optional.",
};
const DAYS: S["DayOfWeek"][] = ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday"];

export function SchedulePage() {
  const blocks = useBlocks();
  const gym = useGymPlan();
  const today = useToday();
  const current = today.data?.planDay?.schedulePhase ?? "A";
  const [editing, setEditing] = useState<S["ScheduleBlockDto"] | { phase: S["SchedulePhase"] } | null>(null);
  const del = useAction((id: number) => call(api.DELETE("/api/v1/schedule/blocks/{id}", { params: { path: { id } } })), {
    invalidate: [keys.blocks(), ["today"]],
    success: "Block moved to Trash.",
  });
  const restore = useAction(
    (list: string) => call(api.POST("/api/v1/settings/restore-defaults/{list}", { params: { path: { list } }, body: { keys: null } })),
    { invalidate: [keys.blocks(), keys.gym(), ["today"]], success: (r) => `Restored ${r.restored} unmodified rows.` },
  );

  if (blocks.isPending) return <Loading />;
  if (blocks.isError) return <ErrorBox error={blocks.error} />;

  return (
    <div className="grid gap-6">
      <PlanNav />
      <PageHeader
        title="Daily schedule"
        subtitle="The schedule gives the time slots; the roadmap gives the outputs. Run the same phase for at least two weeks before changing it."
        actions={
          <ConfirmDialog
            trigger={<Button variant="outline"><RotateCcw /> Restore defaults</Button>}
            title="Restore schedule defaults?"
            description="Blocks and gym days you have not edited return to the roadmap values. Your edited rows are kept."
            confirmLabel="Restore"
            onConfirm={() => {
              restore.mutate("schedule-blocks");
              restore.mutate("gym-plan");
            }}
          />
        }
      />
      <Tabs defaultValue={current}>
        <TabsList>
          {(["A", "B", "C"] as const).map((p) => (
            <TabsTrigger key={p} value={p}>
              Phase {p} {p === current && <Badge className="ml-1">now</Badge>}
            </TabsTrigger>
          ))}
        </TabsList>
        {(["A", "B", "C"] as const).map((p) => (
          <TabsContent key={p} value={p} className="mt-4 grid gap-4">
            <p className="text-sm text-muted-foreground">{PHASE_INFO[p]}</p>
            <Card>
              <CardHeader className="flex-row items-center justify-between">
                <CardTitle className="text-base">Blocks</CardTitle>
                <Button size="sm" variant="outline" onClick={() => setEditing({ phase: p })}><Plus /> Add block</Button>
              </CardHeader>
              <CardContent className="grid gap-1">
                {blocks.data.items.filter((b) => b.schedulePhase === p).map((b) => (
                  <div key={b.id} className="flex items-start gap-3 rounded-md px-2 py-2 hover:bg-accent/40">
                    <span className="w-36 shrink-0 font-mono text-xs text-muted-foreground">{b.timeLabel ?? `${b.startTime?.slice(0, 5) ?? ""}${b.endTime ? `-${b.endTime.slice(0, 5)}` : ""}`}</span>
                    <div className="min-w-0 flex-1 text-sm">
                      <span className="font-medium">{b.title}</span> {b.isStretch && <Badge variant="outline">stretch</Badge>} {b.isUserModified && <Badge variant="secondary">edited</Badge>}
                      <p className="text-xs text-muted-foreground">{b.rule}</p>
                    </div>
                    <Button size="icon" variant="ghost" className="size-7" onClick={() => setEditing(b)} aria-label={`Edit ${b.title}`}><Pencil /></Button>
                    <ConfirmDialog
                      trigger={<Button size="icon" variant="ghost" className="size-7" aria-label={`Delete ${b.title}`}><Trash2 /></Button>}
                      title={`Delete "${b.title}"?`}
                      description="It moves to the Trash."
                      onConfirm={() => del.mutate(b.id)}
                    />
                  </div>
                ))}
              </CardContent>
            </Card>
            <GymPlan phase={p} days={gym.data?.items.filter((g) => g.schedulePhase === p) ?? []} />
          </TabsContent>
        ))}
      </Tabs>
      <BlockDialog value={editing} onClose={() => setEditing(null)} />
    </div>
  );
}

function GymPlan({ phase, days }: { phase: S["SchedulePhase"]; days: S["GymPlanDayDto"][] }) {
  const sorted = [...days].sort((a, b) => DAYS.indexOf(a.dayOfWeek) - DAYS.indexOf(b.dayOfWeek));
  const [day, setDay] = useState<S["DayOfWeek"]>("Monday");
  const [name, setName] = useState("");
  const add = useAction(
    () => call(api.POST("/api/v1/schedule/gym-plan", { body: { schedulePhase: phase, dayOfWeek: day, sessionName: name, isOptional: false, notes: null, rowVersion: null } })),
    { invalidate: [keys.gym(), ["today"]], onSuccess: () => setName("") },
  );
  const del = useAction((id: number) => call(api.DELETE("/api/v1/schedule/gym-plan/{id}", { params: { path: { id } } })), { invalidate: [keys.gym(), ["today"]] });
  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base">Gym plan (18:15)</CardTitle>
        <CardDescription>This sets time only. Lower-body work and added cardio follow clinician/physio guidance.</CardDescription>
      </CardHeader>
      <CardContent className="grid gap-2 text-sm">
        {sorted.map((g) => (
          <div key={g.id} className="flex items-center gap-3 rounded-md border px-3 py-1.5">
            <span className="w-24 font-medium">{g.dayOfWeek}</span>
            <span>{g.sessionName}</span>
            {g.isOptional && <Badge variant="outline">optional</Badge>}
            <span className="min-w-0 flex-1 truncate text-xs text-muted-foreground">{g.notes}</span>
            <Button size="icon" variant="ghost" className="size-7" onClick={() => del.mutate(g.id)} aria-label={`Remove ${g.dayOfWeek} session`}><Trash2 /></Button>
          </div>
        ))}
        <div className="flex flex-wrap gap-2">
          <select className={`${selectClass} w-36`} value={day} onChange={(e) => setDay(e.target.value as S["DayOfWeek"])} aria-label="Day">
            {DAYS.map((d) => <option key={d}>{d}</option>)}
          </select>
          <Input className="w-56" value={name} onChange={(e) => setName(e.target.value)} placeholder="Session name" aria-label="Session name" />
          <Button variant="outline" disabled={!name} onClick={() => add.mutate(undefined)}>Add session</Button>
        </div>
      </CardContent>
    </Card>
  );
}

function BlockDialog({ value, onClose }: { value: S["ScheduleBlockDto"] | { phase: S["SchedulePhase"] } | null; onClose: () => void }) {
  const existing = value && "id" in value ? value : null;
  const [loadedFor, setLoadedFor] = useState<unknown>(null);
  const [f, setF] = useState({ start: "", end: "", label: "", title: "", rule: "", stretch: false, sort: 100 });
  if (value !== loadedFor) {
    setLoadedFor(value);
    setF({
      start: existing?.startTime?.slice(0, 5) ?? "",
      end: existing?.endTime?.slice(0, 5) ?? "",
      label: existing?.timeLabel ?? "",
      title: existing?.title ?? "",
      rule: existing?.rule ?? "",
      stretch: existing?.isStretch ?? false,
      sort: existing?.sortOrder ?? 500,
    });
  }
  const phase = existing?.schedulePhase ?? (value && "phase" in value ? value.phase : "A");
  const save = useAction(
    () => {
      const body = {
        schedulePhase: phase,
        startTime: f.start ? `${f.start}:00` : null,
        endTime: f.end ? `${f.end}:00` : null,
        timeLabel: f.label || null,
        title: f.title,
        rule: f.rule,
        isStretch: f.stretch,
        sortOrder: f.sort,
        rowVersion: existing?.rowVersion ?? null,
      };
      return existing
        ? call(api.PUT("/api/v1/schedule/blocks/{id}", { params: { path: { id: existing.id } }, body }))
        : call(api.POST("/api/v1/schedule/blocks", { body }));
    },
    { invalidate: [keys.blocks(), ["today"]], success: "Block saved.", onSuccess: onClose },
  );
  return (
    <Dialog open={value !== null} onOpenChange={(o) => !o && onClose()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{existing ? "Edit block" : `Add block to Phase ${phase}`}</DialogTitle>
        </DialogHeader>
        <div className="grid gap-3">
          <div className="grid grid-cols-3 gap-3">
            <Field label="Start">{(id) => <Input id={id} type="time" value={f.start} onChange={(e) => setF({ ...f, start: e.target.value })} />}</Field>
            <Field label="End">{(id) => <Input id={id} type="time" value={f.end} onChange={(e) => setF({ ...f, end: e.target.value })} />}</Field>
            <Field label="Order">{(id) => <Input id={id} type="number" value={f.sort} onChange={(e) => setF({ ...f, sort: Number(e.target.value) })} />}</Field>
          </div>
          <Field label="Time label (optional)" hint='Shown instead of start-end, e.g. "07:15 (W4); 07:00 (W5-8)".'>
            {(id) => <Input id={id} value={f.label} onChange={(e) => setF({ ...f, label: e.target.value })} />}
          </Field>
          <Field label="Title">{(id) => <Input id={id} value={f.title} onChange={(e) => setF({ ...f, title: e.target.value })} />}</Field>
          <Field label="Rule">{(id) => <Textarea id={id} rows={3} value={f.rule} onChange={(e) => setF({ ...f, rule: e.target.value })} />}</Field>
          <label className="flex items-center gap-2 text-sm">
            <Checkbox checked={f.stretch} onCheckedChange={(c) => setF({ ...f, stretch: c === true })} /> Stretch block (optional, not the baseline)
          </label>
        </div>
        <DialogFooter>
          <Button disabled={!f.title || save.isPending} onClick={() => save.mutate(undefined)}>Save</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
