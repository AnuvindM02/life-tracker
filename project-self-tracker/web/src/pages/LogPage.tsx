import { zodResolver } from "@hookform/resolvers/zod";
import { useQueryClient } from "@tanstack/react-query";
import { CheckCircle2, ChevronLeft, ChevronRight, Circle, Info, Plane, Trash2 } from "lucide-react";
import { useEffect, useMemo, useState } from "react";
import { Controller, useForm } from "react-hook-form";
import { Link, useLocation, useNavigate, useParams, useSearchParams } from "react-router";
import { toast } from "sonner";
import { z } from "zod";
import { api, ApiError, call, errorText, type S } from "@/api/client";
import { keys, useAction, useLinkableArtifacts, useLogByDate, useToday } from "@/api/hooks";
import { Attachments } from "@/components/Attachments";
import { ConfirmDialog, ErrorBox, Field, Loading, PageHeader, RatingInput, selectClass } from "@/components/common";
import { MarkdownEditor } from "@/components/MarkdownEditor";
import { Stopwatch } from "@/components/Stopwatch";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Checkbox } from "@/components/ui/checkbox";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { addDays, categoryLabel, isoDate, minutes, prettyDate, travelRoleLabel } from "@/lib/format";
import { CLOSE_DAY, START_RITUAL, TICK_CRITERIA, toInput } from "@/lib/log";
import { cn } from "@/lib/utils";

const optionalText = z.string().max(1000).optional();

/** Client-side schema aligned with DailyLogInput (server validates again). */
const logSchema = z.object({
  dayType: z.enum(["Full", "Mvd", "Rest"]).nullable(),
  isTravelDay: z.boolean(),
  energy: z.number().int().min(1).max(5).nullable(),
  sleepHours: z.union([z.number().min(0).max(24), z.nan()]).nullable(),
  wakeTime: z.string().optional(),
  lightsOutTime: z.string().optional(),
  dsaLine: optionalText,
  coreLine: optionalText,
  buildLine: optionalText,
  tomorrowDsa: z.string().max(500).optional(),
  tomorrowBuild: z.string().max(500).optional(),
  notesMd: z.string().optional(),
  gymStatus: z.enum(["Done", "Modified", "Skipped", "Rest", "Walk", "Zone2"]).nullable(),
  gymSession: z.string().max(120).optional(),
  gymNote: z.string().max(2000).optional(),
  rituals: z.record(z.string(), z.boolean()),
});
type LogForm = z.infer<typeof logSchema>;

const RITUAL_KEYS = [...START_RITUAL, ...CLOSE_DAY].map(([k]) => k);

function toForm(l: S["DailyLogDto"] | null, presetType: S["DayType"], travel: boolean, gymSession: string | undefined): LogForm {
  return {
    dayType: l?.dayType ?? presetType ?? null,
    isTravelDay: l?.isTravelDay ?? travel,
    energy: l?.energy ?? null,
    sleepHours: l?.sleepHours ?? null,
    wakeTime: l?.wakeTime?.slice(0, 5) ?? "",
    lightsOutTime: l?.lightsOutTime?.slice(0, 5) ?? "",
    dsaLine: l?.dsaLine ?? "",
    coreLine: l?.coreLine ?? "",
    buildLine: l?.buildLine ?? "",
    tomorrowDsa: l?.tomorrowDsa ?? "",
    tomorrowBuild: l?.tomorrowBuild ?? "",
    notesMd: l?.notesMd ?? "",
    gymStatus: l?.gymStatus ?? null,
    gymSession: l?.gymSession ?? gymSession ?? "",
    gymNote: l?.gymNote ?? "",
    rituals: Object.fromEntries(RITUAL_KEYS.map((k) => [k, Boolean(l?.[k as keyof S["DailyLogDto"]])])),
  };
}

function toBody(f: LogForm, existing: S["DailyLogDto"] | null): S["DailyLogInput"] {
  const t = (s?: string) => (s && s.trim() ? s.trim() : null);
  return {
    ...(existing ? toInput(existing) : { blocksDone: null, rowVersion: null }),
    dayType: f.dayType,
    isTravelDay: f.isTravelDay,
    energy: f.energy,
    sleepHours: f.sleepHours === null || Number.isNaN(f.sleepHours) ? null : f.sleepHours,
    wakeTime: f.wakeTime ? `${f.wakeTime}:00` : null,
    lightsOutTime: f.lightsOutTime ? `${f.lightsOutTime}:00` : null,
    dsaLine: t(f.dsaLine),
    coreLine: t(f.coreLine),
    buildLine: t(f.buildLine),
    tomorrowDsa: t(f.tomorrowDsa),
    tomorrowBuild: t(f.tomorrowBuild),
    notesMd: t(f.notesMd),
    gymStatus: f.gymStatus,
    gymSession: t(f.gymSession),
    gymNote: t(f.gymNote),
    ...(f.rituals as Record<string, boolean>),
  } as S["DailyLogInput"];
}

export function LogPage({ today }: { today?: boolean }) {
  const params = useParams();
  const date = today ? isoDate() : (params.date ?? isoDate());
  const [search] = useSearchParams();
  const preset = (search.get("type") as S["DayType"]) ?? null;
  const logQ = useLogByDate(date);
  const view = useToday(date);

  if (logQ.isPending || view.isPending) return <Loading />;
  if (logQ.isError) return <ErrorBox error={logQ.error} />;
  if (view.isError) return <ErrorBox error={view.error} />;
  return <LogEditor key={`${date}-${logQ.data?.id ?? "new"}`} date={date} log={logQ.data} view={view.data} preset={preset} />;
}

function LogEditor({ date, log, view, preset }: { date: string; log: S["DailyLogDto"] | null; view: S["TodayView"]; preset: S["DayType"] }) {
  const qc = useQueryClient();
  const navigate = useNavigate();
  const location = useLocation();
  const draftKey = `selftracker.draft.log.${date}`;
  const travelSuggested = view.planDay?.travelRole !== "None";
  const defaults = useMemo(() => toForm(log, preset, travelSuggested, view.gym?.sessionName), [log, preset, travelSuggested, view.gym?.sessionName]);
  const form = useForm<LogForm>({ resolver: zodResolver(logSchema), defaultValues: defaults });
  const { register, control, handleSubmit, formState, reset, watch } = form;
  const [draft, setDraft] = useState<LogForm | null>(() => {
    try {
      const raw = localStorage.getItem(draftKey);
      return raw ? (JSON.parse(raw) as LogForm) : null;
    } catch {
      return null;
    }
  });

  // Autosave a local draft while editing; warn before leaving with unsaved changes (spec 8).
  useEffect(() => {
    const sub = watch((values) => {
      if (!formState.isDirty) return;
      try {
        localStorage.setItem(draftKey, JSON.stringify(values));
      } catch {
        /* ignore */
      }
    });
    return () => sub.unsubscribe();
  }, [watch, formState.isDirty, draftKey]);
  useEffect(() => {
    const onUnload = (e: BeforeUnloadEvent) => {
      if (formState.isDirty) e.preventDefault();
    };
    window.addEventListener("beforeunload", onUnload);
    return () => window.removeEventListener("beforeunload", onUnload);
  }, [formState.isDirty]);
  useEffect(() => {
    if (location.hash === "#time") document.getElementById("time")?.scrollIntoView({ behavior: "smooth" });
  }, [location.hash]);

  const invalidate = async () => {
    await qc.invalidateQueries({ queryKey: keys.log(date) });
    await qc.invalidateQueries({ queryKey: ["today"] });
    await qc.invalidateQueries({ queryKey: ["logs"] });
    await qc.invalidateQueries({ queryKey: ["heatmap"] });
  };

  const save = useAction(
    async (f: LogForm) => {
      const body = toBody(f, log);
      return log
        ? call(api.PUT("/api/v1/daily-logs/{id}", { params: { path: { id: log.id } }, body }))
        : call(api.POST("/api/v1/daily-logs", { body: { date, log: body } }));
    },
    {
      success: "Saved.",
      onSuccess: async (saved) => {
        try {
          localStorage.removeItem(draftKey);
        } catch {
          /* ignore */
        }
        setDraft(null);
        reset(toForm(saved, null, saved.isTravelDay, undefined));
        await invalidate();
      },
    },
  );

  const remove = useAction(() => call(api.DELETE("/api/v1/daily-logs/{id}", { params: { path: { id: log!.id } } })), {
    onSuccess: async () => {
      await invalidate();
      toast.success("Moved to Trash.", { action: { label: "Undo", onClick: () => void undo() } });
      navigate("/logs");
    },
  });
  async function undo() {
    try {
      await call(api.POST("/api/v1/trash/{entityType}/{id}/restore", { params: { path: { entityType: "DailyLog", id: log!.id } } }));
      await invalidate();
      navigate(`/logs/${date}`);
    } catch (e) {
      toast.error(errorText(e));
    }
  }

  const onSubmit = handleSubmit((f) =>
    save.mutate(f, {
      onError: (e) => {
        if (e instanceof ApiError && e.isConflict) {
          toast.error("This log changed elsewhere.", {
            action: { label: "Reload", onClick: () => void invalidate() },
            duration: 10_000,
          });
        }
      },
    }),
  );

  const future = date > isoDate();
  const dayType = watch("dayType");

  return (
    <div className="grid gap-6">
      <PageHeader
        title={prettyDate(date, { weekday: "long", day: "numeric", month: "long", year: "numeric" })}
        subtitle={
          <span className="flex flex-wrap items-center gap-2">
            {view.planDay && <span>Day {view.planDay.day} · Week {view.planDay.week ?? "-"} · Schedule {view.planDay.schedulePhase}</span>}
            {view.planDay?.isPaused && <Badge variant="secondary">Paused</Badge>}
            {travelSuggested && (
              <Badge variant="outline" className="gap-1 border-primary/50 text-primary">
                <Plane className="size-3" /> {travelRoleLabel[view.planDay!.travelRole]}
              </Badge>
            )}
            {log?.loggedLate && <Badge variant="outline">Logged later</Badge>}
            {log && !log.dayType && <Badge variant="outline">In progress</Badge>}
          </span>
        }
        actions={
          <>
            <Button variant="ghost" size="icon" asChild aria-label="Previous day">
              <Link to={`/logs/${addDays(date, -1)}`}>
                <ChevronLeft />
              </Link>
            </Button>
            <Button variant="ghost" size="icon" asChild aria-label="Next day" disabled={date >= isoDate()}>
              <Link to={`/logs/${addDays(date, 1)}`}>
                <ChevronRight />
              </Link>
            </Button>
            {log && (
              <ConfirmDialog
                trigger={
                  <Button variant="outline" size="icon" aria-label="Delete this log">
                    <Trash2 />
                  </Button>
                }
                title="Delete this daily log?"
                description="It moves to the Trash with its ticks and time entries; you can restore it."
                onConfirm={() => remove.mutate(undefined)}
              />
            )}
          </>
        }
      />

      {draft && formState.isDirty === false && JSON.stringify(draft) !== JSON.stringify(defaults) && (
        <div className="flex flex-wrap items-center gap-3 rounded-md border border-primary/40 bg-primary/5 px-3 py-2 text-sm">
          <Info className="size-4 text-primary" /> An unsaved draft of this log exists.
          <Button size="sm" variant="outline" onClick={() => reset(draft, { keepDefaultValues: true })}>
            Restore draft
          </Button>
          <Button
            size="sm"
            variant="ghost"
            onClick={() => {
              localStorage.removeItem(draftKey);
              setDraft(null);
            }}
          >
            Discard
          </Button>
        </div>
      )}

      {future ? (
        <ErrorBox error={new Error("Future dates cannot be logged.")} />
      ) : (
        <form onSubmit={onSubmit} className="grid gap-6 lg:grid-cols-3">
          <div className="grid content-start gap-6 lg:col-span-2">
            <Card>
              <CardHeader>
                <CardTitle className="text-base">Day</CardTitle>
                <CardDescription>Full sessions and MVDs are recorded separately. Rest is part of the plan, not a miss.</CardDescription>
              </CardHeader>
              <CardContent className="grid gap-4">
                <Controller
                  control={control}
                  name="dayType"
                  render={({ field }) => (
                    <div role="radiogroup" aria-label="Day type" className="flex flex-wrap gap-2">
                      {(
                        [
                          ["Full", "Full session"],
                          ["Mvd", "Minimum Viable Day"],
                          ["Rest", "Rest"],
                          [null, "Still in progress"],
                        ] as const
                      ).map(([v, label]) => (
                        <button
                          type="button"
                          key={label}
                          role="radio"
                          aria-checked={field.value === v}
                          onClick={() => field.onChange(v)}
                          className={cn(
                            "rounded-md border px-3 py-1.5 text-sm",
                            field.value === v ? "border-primary bg-primary text-primary-foreground" : "hover:bg-accent",
                          )}
                        >
                          {label}
                        </button>
                      ))}
                    </div>
                  )}
                />
                <label className="flex items-center gap-2 text-sm">
                  <Controller control={control} name="isTravelDay" render={({ field }) => <Checkbox checked={field.value} onCheckedChange={(c) => field.onChange(c === true)} />} />
                  Travel day {travelSuggested ? "(suggested from the travel pattern)" : ""}
                </label>
                <div className="grid gap-4 sm:grid-cols-4">
                  <Field label="Energy (1-5)" className="sm:col-span-2">
                    {(id) => <Controller control={control} name="energy" render={({ field }) => <RatingInput id={id} label="Energy" value={field.value} onChange={field.onChange} />} />}
                  </Field>
                  <Field label="Sleep (h)">{(id) => <Input id={id} type="number" step="0.25" min={0} max={24} {...register("sleepHours", { valueAsNumber: true })} />}</Field>
                  <div className="grid grid-cols-2 gap-2">
                    <Field label="Wake">{(id) => <Input id={id} type="time" {...register("wakeTime")} />}</Field>
                    <Field label="Lights out">{(id) => <Input id={id} type="time" {...register("lightsOutTime")} />}</Field>
                  </div>
                </div>
              </CardContent>
            </Card>

            <Card>
              <CardHeader>
                <CardTitle className="text-base">Three lines</CardTitle>
                <CardDescription>Output over input: what did each block produce?</CardDescription>
              </CardHeader>
              <CardContent className="grid gap-4">
                <Field label="DSA" hint="Problem, pattern, attempt time, confidence (1-5), key error.">
                  {(id) => <Textarea id={id} rows={2} {...register("dsaLine")} />}
                </Field>
                <Field label="Core block" hint="What you studied and the artifact (note, diagram, code).">
                  {(id) => <Textarea id={id} rows={3} {...register("coreLine")} />}
                </Field>
                <Field label="Build" hint="Commit, test or design change.">
                  {(id) => <Textarea id={id} rows={2} {...register("buildLine")} />}
                </Field>
                <div className="grid gap-4 sm:grid-cols-2">
                  <Field label="Tomorrow: DSA target">{(id) => <Input id={id} {...register("tomorrowDsa")} />}</Field>
                  <Field label="Tomorrow: next smallest build target">{(id) => <Input id={id} {...register("tomorrowBuild")} />}</Field>
                </div>
                <Field label="Notes (Markdown)">
                  {(id) => <Controller control={control} name="notesMd" render={({ field }) => <MarkdownEditor id={id} value={field.value ?? ""} onChange={field.onChange} rows={4} />} />}
                </Field>
              </CardContent>
            </Card>

            <Card>
              <CardHeader>
                <CardTitle className="text-base">Gym</CardTitle>
                <CardDescription>Records what happened only. Exercise choice follows your clinician/physio guidance.</CardDescription>
              </CardHeader>
              <CardContent className="grid gap-4 sm:grid-cols-3">
                <Field label="Session" hint={view.gym ? `Planned: ${view.gym.sessionName}` : "No session planned today"}>
                  {(id) => <Input id={id} {...register("gymSession")} />}
                </Field>
                <Field label="Status">
                  {(id) => (
                    <Controller
                      control={control}
                      name="gymStatus"
                      render={({ field }) => (
                        <select id={id} className={selectClass} value={field.value ?? ""} onChange={(e) => field.onChange(e.target.value || null)}>
                          <option value="">Not recorded</option>
                          {["Done", "Modified", "Skipped", "Rest", "Walk", "Zone2"].map((s) => (
                            <option key={s} value={s}>
                              {s === "Zone2" ? "Zone 2" : s}
                            </option>
                          ))}
                        </select>
                      )}
                    />
                  )}
                </Field>
                <Field label="Recovery / symptoms note">{(id) => <Input id={id} {...register("gymNote")} />}</Field>
              </CardContent>
            </Card>

            <Card>
              <CardHeader>
                <CardTitle className="text-base">Rituals</CardTitle>
              </CardHeader>
              <CardContent className="grid gap-4 sm:grid-cols-2">
                {[
                  ["Five-minute start", START_RITUAL],
                  ["Close the day", CLOSE_DAY],
                ].map(([title, items]) => (
                  <div key={title as string} className="grid gap-1.5">
                    <p className="text-sm font-medium">{title as string}</p>
                    {(items as typeof START_RITUAL).map(([k, label]) => (
                      <label key={k} className="flex items-center gap-2 text-sm">
                        <Controller
                          control={control}
                          name={`rituals.${k}` as `rituals.${string}`}
                          render={({ field }) => <Checkbox checked={Boolean(field.value)} onCheckedChange={(c) => field.onChange(c === true)} />}
                        />
                        {label}
                      </label>
                    ))}
                  </div>
                ))}
              </CardContent>
            </Card>

            <div className="sticky bottom-3 z-10 flex items-center gap-3 rounded-lg border bg-card/95 p-3 shadow-lg backdrop-blur">
              <Button type="submit" disabled={save.isPending}>
                {save.isPending ? "Saving..." : log ? "Save log" : "Create log"}
              </Button>
              {formState.isDirty && <span className="text-sm text-muted-foreground">Unsaved changes (a local draft is kept).</span>}
              {dayType === "Mvd" && <span className="ml-auto text-xs text-muted-foreground">MVD: resume normally tomorrow; nothing needs to be repaid.</span>}
            </div>
          </div>

          <div className="grid content-start gap-6">
            {log ? <TickPanel log={log} onChange={invalidate} /> : <p className="text-sm text-muted-foreground">Save the log once to add ticks, time and attachments.</p>}
            {log && <MvdPanel log={log} />}
            <TimePanel date={date} log={log} onChange={invalidate} />
            {log && (
              <Card>
                <CardHeader>
                  <CardTitle className="text-base">Attachments</CardTitle>
                </CardHeader>
                <CardContent>
                  <Attachments entityType="DailyLog" entityId={log.id} label="Daily log" />
                </CardContent>
              </Card>
            )}
          </div>
        </form>
      )}
    </div>
  );
}

function TickPanel({ log, onChange }: { log: S["DailyLogDto"]; onChange: () => Promise<void> }) {
  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base">Tracker ticks</CardTitle>
        <CardDescription>Tracker is the truth: each tick needs evidence.</CardDescription>
      </CardHeader>
      <CardContent className="grid gap-4">
        <div className="grid gap-1 rounded-md border p-3 text-sm">
          <div className="flex items-center gap-2 font-medium">
            {log.dsaTick ? <CheckCircle2 className="size-4 text-success" /> : <Circle className="size-4 text-muted-foreground" />} {TICK_CRITERIA.Dsa.title}
          </div>
          <p className="text-xs text-muted-foreground">Derived from logged DSA attempts and cold reviews (DSA module, M3); it cannot be ticked by hand.</p>
          <Criteria type="Dsa" />
        </div>
        <TickEditor log={log} type="Core" tick={log.coreTick} onChange={onChange} />
        <TickEditor log={log} type="Build" tick={log.buildTick} onChange={onChange} />
      </CardContent>
    </Card>
  );
}

function Criteria({ type }: { type: keyof typeof TICK_CRITERIA }) {
  const c = TICK_CRITERIA[type];
  return (
    <details className="text-xs">
      <summary className="cursor-pointer text-muted-foreground">What counts?</summary>
      <p className="mt-1"><span className="font-medium">Counts:</span> {c.counts}</p>
      <p><span className="font-medium">Does not count:</span> {c.doesNot}</p>
    </details>
  );
}

function TickEditor({ log, type, tick, onChange }: { log: S["DailyLogDto"]; type: "Core" | "Build"; tick: S["TickDto"] | null; onChange: () => Promise<void> }) {
  const [editing, setEditing] = useState(false);
  const [note, setNote] = useState(tick?.evidenceNote ?? "");
  const [url, setUrl] = useState(tick?.evidenceUrl ?? "");
  const [fileIds, setFileIds] = useState<number[]>([]);
  const [errors, setErrors] = useState<string[]>([]);
  const [link, setLink] = useState(tick?.linkedEntityType && tick.linkedEntityId ? `${tick.linkedEntityType}:${tick.linkedEntityId}` : "");
  const artifacts = useLinkableArtifacts(log.date, editing && type === "Core");

  const save = useAction(
    () =>
      call(
        api.PUT("/api/v1/daily-logs/{id}/ticks/{type}", {
          params: { path: { id: log.id, type } },
          body: {
            evidenceNote: note,
            evidenceUrl: url || null,
            linkedEntityType: link ? link.split(":")[0] : null,
            linkedEntityId: link ? Number(link.split(":")[1]) : null,
            attachmentFileIds: fileIds,
            rowVersion: tick?.rowVersion ?? null,
          },
        }),
      ),
    {
      success: `${TICK_CRITERIA[type].title} ticked.`,
      onSuccess: async () => {
        setEditing(false);
        setFileIds([]);
        setErrors([]);
        await onChange();
      },
    },
  );
  const clear = useAction(() => call(api.DELETE("/api/v1/daily-logs/{id}/ticks/{type}", { params: { path: { id: log.id, type } } })), {
    onSuccess: onChange,
  });

  return (
    <div className="grid gap-2 rounded-md border p-3 text-sm">
      <div className="flex items-center gap-2 font-medium">
        {tick ? <CheckCircle2 className="size-4 text-success" /> : <Circle className="size-4 text-muted-foreground" />} {TICK_CRITERIA[type].title}
        <span className="ml-auto flex gap-1">
          {!editing && (
            <Button type="button" size="sm" variant="outline" onClick={() => setEditing(true)}>
              {tick ? "Edit" : "Tick"}
            </Button>
          )}
          {tick && !editing && (
            <Button type="button" size="sm" variant="ghost" onClick={() => clear.mutate(undefined)}>
              Untick
            </Button>
          )}
        </span>
      </div>
      {tick && !editing && (
        <p className="whitespace-pre-wrap text-xs text-muted-foreground">
          {tick.evidenceNote}
          {tick.evidenceUrl && <> · {tick.evidenceUrl}</>}
          {tick.linkedEntityType && <> · linked {tick.linkedEntityType} #{tick.linkedEntityId}</>}
          {tick.attachmentCount > 0 && <> · {tick.attachmentCount} file(s)</>}
        </p>
      )}
      <Criteria type={type} />
      {editing && (
        <div className="grid gap-2">
          <Field label="Evidence note" hint={type === "Core" ? "A 3+ line note counts on its own." : undefined}>
            {(id) => <Textarea id={id} rows={3} value={note} onChange={(e) => setNote(e.target.value)} />}
          </Field>
          <Field label={type === "Build" ? "Commit hash, commit URL or PR URL" : "Link (optional)"}>
            {(id) => <Input id={id} value={url} onChange={(e) => setUrl(e.target.value)} placeholder={type === "Build" ? "3f2a9c1 or https://..." : "https://..."} />}
          </Field>
          {type === "Core" && (
            <Field label="Link a note, drill or articulation (optional)">
              {(id) => (
                <select id={id} className={selectClass} value={link} onChange={(e) => setLink(e.target.value)}>
                  <option value="">No linked artifact</option>
                  {artifacts.data?.map((a) => <option key={`${a.entityType}:${a.id}`} value={`${a.entityType}:${a.id}`}>{a.label}</option>)}
                </select>
              )}
            </Field>
          )}
          <Attachments compact label={`${type} tick evidence`} onUploaded={(files) => setFileIds((ids) => [...ids, ...files.map((f) => f.id)])} />
          {fileIds.length > 0 && <p className="text-xs text-muted-foreground">{fileIds.length} file(s) will be attached to this tick.</p>}
          {errors.map((e) => (
            <p key={e} className="text-xs text-destructive">{e}</p>
          ))}
          <div className="flex gap-2">
            <Button
              type="button"
              size="sm"
              disabled={save.isPending}
              onClick={() =>
                save.mutate(undefined, { onError: (e) => setErrors(e instanceof ApiError ? e.messages : [errorText(e)]) })
              }
            >
              Save tick
            </Button>
            <Button type="button" size="sm" variant="ghost" onClick={() => setEditing(false)}>
              Cancel
            </Button>
          </div>
        </div>
      )}
    </div>
  );
}

function MvdPanel({ log }: { log: S["DailyLogDto"] }) {
  const m = log.mvd;
  const row = (done: boolean, text: string) => (
    <li className="flex items-start gap-2">
      {done ? <CheckCircle2 className="mt-0.5 size-4 shrink-0 text-success" /> : <Circle className="mt-0.5 size-4 shrink-0 text-muted-foreground" />}
      <span>{text}</span>
    </li>
  );
  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base">MVD checklist</CardTitle>
        <CardDescription>An indicator, never a gate. 45-75 minutes in total.</CardDescription>
      </CardHeader>
      <CardContent className="grid gap-2 text-sm">
        {m.travelRuleApplied ? (
          <ul className="grid gap-1">
            {row(m.dsaDone, "DSA review (or a 20+ minute attempt)")}
            {row(m.coreDone, "A short written note")}
          </ul>
        ) : (
          <ul className="grid gap-1">
            {row(m.dsaDone, "DSA attempt or cold review, 20-30 min")}
            {row(m.coreDone, "15-20 min DDIA/core with a three-line note")}
            {row(m.buildDone, "10-15 min on the build, or one tiny test/commit")}
          </ul>
        )}
        <p className="text-xs text-muted-foreground">
          {m.satisfied ? "MVD satisfied." : "Log time below and a note to fill this in."} A 20-24 minute attempt satisfies the MVD but not the DSA tick (25+ minutes).
        </p>
      </CardContent>
    </Card>
  );
}

function TimePanel({ date, log, onChange }: { date: string; log: S["DailyLogDto"] | null; onChange: () => Promise<void> }) {
  const [category, setCategory] = useState<S["TimeCategory"]>("Dsa");
  const [mins, setMins] = useState("");
  const [note, setNote] = useState("");
  const add = useAction(
    (args: { minutes: number; source: S["TimeSource"] }) =>
      call(
        api.POST("/api/v1/daily-logs/by-date/{date}/time-entries", {
          params: { path: { date } },
          body: { category, minutes: args.minutes, note: note || null, source: args.source, rowVersion: null },
        }),
      ),
    {
      success: (l) => `Logged. ${minutes(l.timeEntries.reduce((a, t) => a + t.minutes, 0))} today.`,
      onSuccess: async () => {
        setMins("");
        setNote("");
        await onChange();
      },
    },
  );
  const del = useAction((id: number) => call(api.DELETE("/api/v1/time-entries/{id}", { params: { path: { id } } })), { onSuccess: onChange });
  const total = log?.timeEntries.reduce((a, t) => a + t.minutes, 0) ?? 0;

  return (
    <Card id="time">
      <CardHeader>
        <CardTitle className="text-base">Time</CardTitle>
        <CardDescription>{total ? `${minutes(total)} logged` : "Minutes per category feed weekly hours."}</CardDescription>
      </CardHeader>
      <CardContent className="grid gap-3 text-sm">
        <div className="grid gap-2 sm:grid-cols-2">
          <select className={selectClass} value={category} onChange={(e) => setCategory(e.target.value as S["TimeCategory"])} aria-label="Category">
            {Object.entries(categoryLabel).map(([k, v]) => (
              <option key={k} value={k}>
                {v}
              </option>
            ))}
          </select>
          <Input value={note} onChange={(e) => setNote(e.target.value)} placeholder="Note (optional)" aria-label="Time note" />
        </div>
        <Stopwatch storageKey={`time-${date}`} label="Block timer" onStop={(m) => add.mutate({ minutes: m, source: "Timer" })} />
        <div className="flex gap-2">
          <Input type="number" min={1} max={1440} value={mins} onChange={(e) => setMins(e.target.value)} placeholder="Minutes" aria-label="Minutes" />
          <Button type="button" variant="outline" disabled={!Number(mins) || add.isPending} onClick={() => add.mutate({ minutes: Number(mins), source: "Manual" })}>
            Add
          </Button>
        </div>
        {log && log.timeEntries.length > 0 && (
          <ul className="grid gap-1">
            {log.timeEntries.map((t) => (
              <li key={t.id} className="flex items-center gap-2 rounded border px-2 py-1">
                <span className="w-28 shrink-0">{categoryLabel[t.category]}</span>
                <span className="font-medium">{t.minutes} min</span>
                <span className="min-w-0 flex-1 truncate text-xs text-muted-foreground">{t.note}</span>
                {t.source !== "Derived" && (
                  <Button type="button" size="icon" variant="ghost" className="size-7" onClick={() => del.mutate(t.id)} aria-label="Delete time entry">
                    <Trash2 />
                  </Button>
                )}
              </li>
            ))}
          </ul>
        )}
      </CardContent>
    </Card>
  );
}
