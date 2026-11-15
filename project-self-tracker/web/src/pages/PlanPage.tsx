import { CheckCircle2, Circle, Pencil, Plus, Trash2 } from "lucide-react";
import { useState } from "react";
import { Link } from "react-router";
import { api, call, type S } from "@/api/client";
import { keys, useAction, usePauses, usePhases, usePlanTasks, useSettings, useToday, useTravelOverrides } from "@/api/hooks";
import { ConfirmDialog, EmptyState, ErrorBox, Field, Loading, PageHeader, selectClass } from "@/components/common";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { pauseReasonLabel, prettyDate, trackLabel } from "@/lib/format";
import { cn } from "@/lib/utils";
import { PlanNav, TaskStatusBadge } from "@/pages/plan/PlanShared";

export function PlanPage() {
  const phases = usePhases();
  const today = useToday();
  const currentWeek = today.data?.planDay?.week ?? 1;
  const [week, setWeek] = useState<number | null>(null);
  const shownWeek = week ?? currentWeek;
  const tasks = usePlanTasks({ week: shownWeek });

  return (
    <div className="grid gap-6">
      <PlanNav />
      <PageHeader
        title="Plan"
        subtitle="The day-by-day order is a target. A delayed week shifts the sequence; it does not require a restart."
      />
      {phases.isError && <ErrorBox error={phases.error} />}
      <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
        {phases.data?.map((p) => {
          const current = today.data?.planDay?.phaseNumber === p.number;
          return (
            <Card key={p.id} className={cn(current && "border-primary")}>
              <CardHeader>
                <CardTitle className="flex items-center gap-2 text-base">
                  <Link to={`/plan/phases/${p.number}`} className="hover:underline">{p.number}. {p.name}</Link> {current && <Badge>Now</Badge>}
                </CardTitle>
                <CardDescription>Days {p.dayFrom}-{p.dayTo} · Weeks {p.weekFrom}-{p.weekTo}</CardDescription>
              </CardHeader>
              <CardContent className="grid gap-2 text-sm">
                <p>{p.objective}</p>
                <p className="text-xs text-muted-foreground"><span className="font-medium">Exit evidence:</span> {p.exitEvidence}</p>
              </CardContent>
            </Card>
          );
        })}
      </div>

      <Card>
        <CardHeader className="flex-row flex-wrap items-center gap-3">
          <CardTitle className="text-base">Week {shownWeek} tasks</CardTitle>
          <select className={cn(selectClass, "ml-auto w-44")} value={shownWeek} onChange={(e) => setWeek(Number(e.target.value))} aria-label="Plan week">
            {Array.from({ length: 52 }, (_, i) => i + 1).map((w) => (
              <option key={w} value={w}>
                Week {w}{w === currentWeek ? " (current)" : ""}
              </option>
            ))}
          </select>
        </CardHeader>
        <CardContent className="grid gap-3">
          {tasks.isPending ? (
            <Loading />
          ) : tasks.isError ? (
            <ErrorBox error={tasks.error} />
          ) : tasks.data.items.length === 0 ? (
            <EmptyState title="No tasks in this week" />
          ) : (
            tasks.data.items.map((t) => (
              <div key={t.id} className="grid gap-0.5 rounded-md border p-3 text-sm">
                <div className="flex flex-wrap items-center gap-2">
                  {t.status === "Done" ? <CheckCircle2 className="size-4 text-success" /> : <Circle className="size-4 text-muted-foreground" />}
                  <TaskStatusBadge status={t.status} />
                  <Badge variant="outline">{trackLabel[t.track]}</Badge>
                  <span className="font-medium">{t.title}</span>
                  <span className="ml-auto text-xs text-muted-foreground">Days {t.dayFrom}-{t.dayTo}</span>
                </div>
                {t.ddiaChapters && t.ddiaChapters.length > 0 && <p className="text-xs font-medium text-primary">{t.ddiaChapters.join(" · ")}</p>}
                {t.description && <p className="text-muted-foreground">{t.description}</p>}
                {t.outputRequired && <p className="text-xs"><span className="text-muted-foreground">Output:</span> {t.outputRequired}</p>}
                {t.fallback && <p className="text-xs"><span className="text-muted-foreground">Fallback:</span> {t.fallback}</p>}
              </div>
            ))
          )}
          <p className="text-xs text-muted-foreground">
            Edit tasks and use the board on <Link to="/plan/tasks" className="text-primary underline">Tasks</Link>. Weekly DSA and DDIA tasks complete from your logged problems and chapters.
          </p>
        </CardContent>
      </Card>
    </div>
  );
}

/** Pause ranges (BR-01) and per-week travel overrides (BR-04). */
export function PausesPage() {
  const pauses = usePauses();
  const travel = useTravelOverrides();
  const settings = useSettings();
  const [editing, setEditing] = useState<S["PauseRangeDto"] | "new" | null>(null);
  const del = useAction((id: number) => call(api.DELETE("/api/v1/plan/pause-ranges/{id}", { params: { path: { id } } })), {
    invalidate: [keys.pauses, ["today"], ["heatmap"]],
    success: "Pause moved to Trash.",
  });

  return (
    <div className="grid gap-6">
      <PlanNav />
      <PageHeader title="Pauses & travel" subtitle="Plan days do not advance during a pause; logging is still allowed. Removing a pause recomputes dates but never changes stored logs." />
      <Card>
        <CardHeader className="flex-row items-center justify-between">
          <CardTitle className="text-base">Pause ranges</CardTitle>
          <Button size="sm" onClick={() => setEditing("new")}><Plus /> Add pause</Button>
        </CardHeader>
        <CardContent>
          {pauses.isPending ? (
            <Loading />
          ) : pauses.data?.items.length === 0 ? (
            <EmptyState title="No pauses">Illness, travel, a work spike or a family event can pause the plan.</EmptyState>
          ) : (
            <ul className="grid gap-2">
              {pauses.data?.items.map((p) => (
                <li key={p.id} className="flex flex-wrap items-center gap-3 rounded-md border px-3 py-2 text-sm">
                  <span className="font-medium">{prettyDate(p.startDate)} to {prettyDate(p.endDate)}</span>
                  <Badge variant="secondary">{p.reason ? pauseReasonLabel[p.reason] : ""}</Badge>
                  <span className="min-w-0 flex-1 truncate text-muted-foreground">{p.notes}</span>
                  <Button size="icon" variant="ghost" onClick={() => setEditing(p)} aria-label="Edit pause"><Pencil /></Button>
                  <ConfirmDialog
                    trigger={<Button size="icon" variant="ghost" aria-label="Delete pause"><Trash2 /></Button>}
                    title="Delete this pause?"
                    description="Plan days after it will move forward again."
                    onConfirm={() => del.mutate(p.id)}
                  />
                </li>
              ))}
            </ul>
          )}
        </CardContent>
      </Card>
      <PauseDialog value={editing} onClose={() => setEditing(null)} />

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Travel fortnight</CardTitle>
          <CardDescription>
            {settings.data?.travelEnabled && settings.data.travelAnchorFriday
              ? `Trips run Friday to Monday every 14 days from ${prettyDate(settings.data.travelAnchorFriday)}. Friday is a pre-planned MVD, Saturday a timed attempt, Sunday review, Monday a short review. No deep build is expected and nothing is made up later.`
              : "Travel pattern is off. Set an anchor Friday in Settings."}
          </CardDescription>
        </CardHeader>
        <CardContent>
          <TravelOverrides items={travel.data?.items ?? []} />
        </CardContent>
      </Card>
    </div>
  );
}

function PauseDialog({ value, onClose }: { value: S["PauseRangeDto"] | "new" | null; onClose: () => void }) {
  const existing = value && value !== "new" ? value : null;
  const [start, setStart] = useState("");
  const [end, setEnd] = useState("");
  const [reason, setReason] = useState<NonNullable<S["PauseReason"]>>("Illness");
  const [notes, setNotes] = useState("");
  const [loadedFor, setLoadedFor] = useState<unknown>(null);
  if (value !== loadedFor) {
    setLoadedFor(value);
    setStart(existing?.startDate ?? "");
    setEnd(existing?.endDate ?? "");
    setReason(existing?.reason ?? "Illness");
    setNotes(existing?.notes ?? "");
  }

  const save = useAction(
    () => {
      const body = { startDate: start, endDate: end, reason, notes: notes || null, rowVersion: existing?.rowVersion ?? null };
      return existing
        ? call(api.PUT("/api/v1/plan/pause-ranges/{id}", { params: { path: { id: existing.id } }, body }))
        : call(api.POST("/api/v1/plan/pause-ranges", { body }));
    },
    { invalidate: [keys.pauses, ["today"], ["heatmap"]], success: "Pause saved.", onSuccess: onClose },
  );

  return (
    <Dialog open={value !== null} onOpenChange={(o) => !o && onClose()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{existing ? "Edit pause" : "Add pause"}</DialogTitle>
        </DialogHeader>
        <div className="grid gap-3">
          <div className="grid grid-cols-2 gap-3">
            <Field label="From">{(id) => <Input id={id} type="date" value={start} onChange={(e) => setStart(e.target.value)} />}</Field>
            <Field label="To (inclusive)">{(id) => <Input id={id} type="date" value={end} onChange={(e) => setEnd(e.target.value)} />}</Field>
          </div>
          <Field label="Reason">
            {(id) => (
              <select id={id} className={selectClass} value={reason} onChange={(e) => setReason(e.target.value as NonNullable<S["PauseReason"]>)}>
                {Object.entries(pauseReasonLabel).map(([k, v]) => (
                  <option key={k} value={k}>{v}</option>
                ))}
              </select>
            )}
          </Field>
          <Field label="Notes">{(id) => <Input id={id} value={notes} onChange={(e) => setNotes(e.target.value)} />}</Field>
        </div>
        <DialogFooter>
          <Button onClick={() => save.mutate(undefined)} disabled={!start || !end || save.isPending}>Save</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

function TravelOverrides({ items }: { items: S["TravelOverrideDto"][] }) {
  const [friday, setFriday] = useState("");
  const [isTravel, setIsTravel] = useState(false);
  const add = useAction(
    () => call(api.POST("/api/v1/plan/travel-overrides", { body: { weekFriday: friday, isTravel, notes: null, rowVersion: null } })),
    { invalidate: [keys.travel, ["today"], ["heatmap"]], success: "Override saved.", onSuccess: () => setFriday("") },
  );
  const del = useAction((id: number) => call(api.DELETE("/api/v1/plan/travel-overrides/{id}", { params: { path: { id } } })), {
    invalidate: [keys.travel, ["today"], ["heatmap"]],
  });
  const isFriday = !friday || new Date(`${friday}T00:00:00`).getDay() === 5;
  return (
    <div className="grid gap-3 text-sm">
      <div className="flex flex-wrap items-end gap-2">
        <Field label="Week (its Friday)" error={isFriday ? undefined : "Pick a Friday."}>
          {(id) => <Input id={id} type="date" value={friday} onChange={(e) => setFriday(e.target.value)} className="w-44" />}
        </Field>
        <select className={cn(selectClass, "w-48")} value={isTravel ? "1" : "0"} onChange={(e) => setIsTravel(e.target.value === "1")} aria-label="Override">
          <option value="0">No trip that week</option>
          <option value="1">Trip that week</option>
        </select>
        <Button variant="outline" disabled={!friday || !isFriday} onClick={() => add.mutate(undefined)}>Add override</Button>
      </div>
      {items.map((o) => (
        <div key={o.id} className="flex items-center gap-2 rounded-md border px-3 py-1.5">
          <span>{prettyDate(o.weekFriday)}</span>
          <Badge variant={o.isTravel ? "default" : "secondary"}>{o.isTravel ? "Trip" : "No trip"}</Badge>
          <Button size="icon" variant="ghost" className="ml-auto size-7" onClick={() => del.mutate(o.id)} aria-label="Remove override"><Trash2 /></Button>
        </div>
      ))}
    </div>
  );
}
