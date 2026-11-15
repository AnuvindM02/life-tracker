import { useQueryClient } from "@tanstack/react-query";
import { CalendarCheck, CheckCircle2, Circle, Dumbbell, Lightbulb, Plane, Timer, Upload } from "lucide-react";
import { Link, useNavigate } from "react-router";
import { toast } from "sonner";
import { errorText, type S } from "@/api/client";
import { useToday } from "@/api/hooks";
import { ErrorBox, Loading, PageHeader } from "@/components/common";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Checkbox } from "@/components/ui/checkbox";
import { Progress } from "@/components/ui/progress";
import { dayTypeLabel, prettyDate, trackLabel, travelRoleLabel } from "@/lib/format";
import { CLOSE_DAY, type LogInput, patchLog, START_RITUAL } from "@/lib/log";
import { cn } from "@/lib/utils";
import { ProblemRow, ReviewsDueCard } from "@/pages/dsa/DsaPages";
import { HoursCard } from "@/pages/ReviewsPages";
import { useWeeklyHours } from "@/api/hooks";
import { useState } from "react";

export function TodayPage() {
  const today = useToday();
  const navigate = useNavigate();
  const qc = useQueryClient();

  if (today.isPending) return <Loading />;
  if (today.isError) return <ErrorBox error={today.error} />;
  const v = today.data;
  const day = v.planDay!;
  const log = v.log;

  async function toggle(change: (i: LogInput) => LogInput) {
    try {
      await patchLog(v.date, change);
      await qc.invalidateQueries({ queryKey: ["today"] });
      await qc.invalidateQueries({ queryKey: ["log", v.date] });
    } catch (e) {
      toast.error(errorText(e));
    }
  }

  const dayLabel =
    day.status === "NotStarted" ? "Plan not started yet" : day.status === "Year2" ? `Day ${day.day} (Year 2)` : `Day ${day.day} of 365`;

  return (
    <div className="grid gap-6">
      <PageHeader
        title={prettyDate(v.date, { weekday: "long", day: "numeric", month: "long", year: "numeric" })}
        subtitle={
          <span className="flex flex-wrap items-center gap-2">
            <span className="font-medium text-foreground">{dayLabel}</span>
            {day.week && <span>· Week {day.week}</span>}
            {day.phaseName && <span>· Phase {day.phaseNumber}: {day.phaseName}</span>}
            <span>· Schedule {day.schedulePhase}</span>
            {day.isPaused && <Badge variant="secondary">Paused{day.pauseReason ? `: ${day.pauseReason}` : ""}</Badge>}
            {day.travelRole !== "None" && (
              <Badge variant="outline" className="gap-1 border-primary/50 text-primary">
                <Plane className="size-3" /> {travelRoleLabel[day.travelRole]}
              </Badge>
            )}
          </span>
        }
        actions={
          <>
            <Button onClick={() => navigate(`/logs/${v.date}?type=Full`)}>Log full day</Button>
            <Button variant="secondary" onClick={() => navigate(`/logs/${v.date}?type=Mvd`)}>Log MVD</Button>
            <Button variant="outline" onClick={() => navigate(`/logs/${v.date}?type=Rest`)}>Rest day</Button>
          </>
        }
      />

      <Nudges nudges={v.nudges} date={v.date} />

      <div className="grid gap-6 lg:grid-cols-3">
        <div className="grid gap-6 lg:col-span-2">
          <Card>
            <CardHeader className="flex-row items-center justify-between">
              <CardTitle className="text-base">This week's targets</CardTitle>
              <Link to="/plan" className="text-sm text-primary underline">Full plan</Link>
            </CardHeader>
            <CardContent className="grid gap-3">
              {v.weekTasks.length === 0 && <p className="text-sm text-muted-foreground">No plan tasks for this day.</p>}
              {v.weekTasks.map((t) => (
                <TaskRow key={t.id} task={t} />
              ))}
              {v.nextTasks.length > 0 && (
                <div className="mt-2 border-t pt-3">
                  <p className="mb-2 text-xs font-medium uppercase text-muted-foreground">Still open from earlier</p>
                  {v.nextTasks.map((t) => (
                    <TaskRow key={t.id} task={t} compact />
                  ))}
                </div>
              )}
            </CardContent>
          </Card>

          {v.dsaProblems.length > 0 && (
            <Card>
              <CardHeader className="flex-row items-center justify-between">
                <CardTitle className="text-base">Week {day.week} DSA problems</CardTitle>
                <Link to="/dsa/problems" className="text-sm text-primary underline">Library</Link>
              </CardHeader>
              <CardContent className="grid gap-0.5">
                {v.dsaProblems.map((p) => (
                  <ProblemRow key={p.id} problem={p} />
                ))}
              </CardContent>
            </Card>
          )}

          <Card>
            <CardHeader className="flex-row items-center justify-between">
              <CardTitle className="text-base">Schedule {day.schedulePhase} for today</CardTitle>
              <span className="text-xs text-muted-foreground">Ticking blocks is optional</span>
            </CardHeader>
            <CardContent className="grid gap-1">
              {v.scheduleBlocks.filter((b) => !b.isStretch).map((b) => (
                <label key={b.id} className="flex cursor-pointer items-start gap-3 rounded-md px-2 py-1.5 hover:bg-accent/50">
                  <Checkbox
                    className="mt-0.5"
                    checked={b.done}
                    onCheckedChange={(c) =>
                      void toggle((i) => {
                        const set = new Set((i.blocksDone ?? []).map(Number));
                        if (c === true) set.add(b.id);
                        else set.delete(b.id);
                        return { ...i, blocksDone: [...set] };
                      })
                    }
                  />
                  <span className="w-28 shrink-0 font-mono text-xs text-muted-foreground">{b.time}</span>
                  <span className="text-sm">
                    <span className={cn("font-medium", b.done && "text-muted-foreground line-through")}>{b.title}</span>
                    <span className="block text-xs text-muted-foreground">{b.rule}</span>
                  </span>
                </label>
              ))}
              {v.gym && (
                <p className="mt-2 flex items-center gap-2 rounded-md bg-secondary/60 px-3 py-2 text-sm">
                  <Dumbbell className="size-4" /> 18:15 {v.gym.sessionName}
                  {v.gym.isOptional && <Badge variant="outline">optional</Badge>}
                  <span className="text-xs text-muted-foreground">{v.gym.notes}</span>
                </p>
              )}
            </CardContent>
          </Card>
        </div>

        <div className="grid content-start gap-6">
          <ReviewsDueCard reviews={v.reviewsDue} />
          <Card>
            <CardHeader>
              <CardTitle className="text-base">Today's log</CardTitle>
            </CardHeader>
            <CardContent className="grid gap-3 text-sm">
              {log ? (
                <>
                  <div className="flex items-center gap-2">
                    <Badge>{log.dayType ? dayTypeLabel[log.dayType] : "In progress"}</Badge>
                    <Link to={`/logs/${v.date}`} className="text-primary underline">Open log</Link>
                  </div>
                  <TickLine label="DSA" on={log.dsaTick} hint="from logged attempts" />
                  <TickLine label="Core" on={log.coreTick !== null} />
                  <TickLine label="Build" on={log.buildTick !== null} />
                  <div className="text-xs text-muted-foreground">
                    MVD: {[log.mvd.dsaDone && "DSA", log.mvd.coreDone && "core", log.mvd.buildDone && "build"].filter(Boolean).join(", ") || "nothing yet"}
                    {log.mvd.satisfied && " (satisfied)"}
                  </div>
                </>
              ) : (
                <p className="text-muted-foreground">Nothing logged yet today.</p>
              )}
              <div className="flex flex-wrap gap-2">
                <Button size="sm" variant="outline" onClick={() => navigate(`/logs/${v.date}#time`)}>
                  <Timer /> Add time
                </Button>
                <Button size="sm" variant="outline" onClick={() => navigate("/files?upload=1")}>
                  <Upload /> Upload
                </Button>
              </div>
            </CardContent>
          </Card>

          <WeekHours />
          {v.metrics && (
            <Card>
              <CardHeader>
                <CardTitle className="text-base">Consistency</CardTitle>
              </CardHeader>
              <CardContent className="grid gap-2 text-sm">
                <div className="flex justify-between">
                  <span>Active days, last 30</span>
                  <span className="font-medium">
                    {v.metrics.active30.activeDays} <span className="text-muted-foreground">/ aim {v.metrics.active30.target}</span>
                  </span>
                </div>
                <Progress value={(v.metrics.active30.activeDays / v.metrics.active30.target) * 100} aria-label="Active days against the aim of 26" />
                <p className="text-xs text-muted-foreground">
                  Includes {v.metrics.active30.mvdDays} MVDs{v.metrics.active30.pausedDays ? `; ${v.metrics.active30.pausedDays} paused days excluded` : ""}. A tick measures behaviour, not mastery.
                </p>
                <p className="flex items-center gap-2">
                  <CalendarCheck className="size-4 text-primary" />
                  {v.metrics.misses.neverMissedTwice ? "Never missed twice since the last pause." : "Two days in a row were missed once. An MVD is all it takes to continue."}
                </p>
              </CardContent>
            </Card>
          )}

          <Card>
            <CardHeader>
              <CardTitle className="text-base">Five-minute start</CardTitle>
            </CardHeader>
            <CardContent className="grid gap-1.5">
              {START_RITUAL.map(([k, label]) => (
                <label key={k} className="flex items-center gap-2 text-sm">
                  <Checkbox checked={Boolean(log?.[k as keyof S["DailyLogDto"]])} onCheckedChange={(c) => void toggle((i) => ({ ...i, [k]: c === true }))} />
                  {label}
                </label>
              ))}
              <p className="mt-3 text-xs font-medium uppercase text-muted-foreground">Close the day</p>
              {CLOSE_DAY.map(([k, label]) => (
                <label key={k} className="flex items-center gap-2 text-sm">
                  <Checkbox checked={Boolean(log?.[k as keyof S["DailyLogDto"]])} onCheckedChange={(c) => void toggle((i) => ({ ...i, [k]: c === true }))} />
                  {label}
                </label>
              ))}
            </CardContent>
          </Card>
        </div>
      </div>
    </div>
  );
}

function TaskRow({ task, compact }: { task: S["PlanTaskDto"]; compact?: boolean }) {
  const done = task.status === "Done";
  return (
    <div className="grid gap-0.5 text-sm">
      <div className="flex items-center gap-2">
        {done ? <CheckCircle2 className="size-4 text-success" aria-label="Done" /> : <Circle className="size-4 text-muted-foreground" aria-label={task.status} />}
        <Badge variant="outline" className="text-[10px]">{trackLabel[task.track]}</Badge>
        <span className="font-medium">{task.title}</span>
        {task.status !== "NotStarted" && !done && <Badge variant="secondary">{task.status}</Badge>}
      </div>
      {!compact && task.ddiaChapters && task.ddiaChapters.length > 0 && (
        <p className="pl-6 text-xs font-medium text-primary">{task.ddiaChapters.join(" · ")}</p>
      )}
      {!compact && task.description && <p className="pl-6 text-muted-foreground">{task.description}</p>}
      {!compact && task.outputRequired && <p className="pl-6 text-xs"><span className="text-muted-foreground">Output:</span> {task.outputRequired}</p>}
      {!compact && task.fallback && <p className="pl-6 text-xs"><span className="text-muted-foreground">Fallback:</span> {task.fallback}</p>}
    </div>
  );
}

function TickLine({ label, on, hint }: { label: string; on: boolean; hint?: string }) {
  return (
    <div className="flex items-center gap-2">
      {on ? <CheckCircle2 className="size-4 text-success" /> : <Circle className="size-4 text-muted-foreground" />}
      <span>{label}</span>
      <span className="sr-only">{on ? "ticked" : "not ticked"}</span>
      {hint && <span className="text-xs text-muted-foreground">({hint})</span>}
    </div>
  );
}

function WeekHours() {
  const hours = useWeeklyHours();
  if (!hours.data) return null;
  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base">This week's hours</CardTitle>
      </CardHeader>
      <CardContent>
        <HoursCard hours={hours.data} />
      </CardContent>
    </Card>
  );
}

const dismissKey = (date: string) => `selftracker.nudges.dismissed.${date}`;

/** Nudges with "dismiss for today" (spec 4.12); dismissals live in this browser only. */
function Nudges({ nudges, date }: { nudges: S["NudgeDto"][]; date: string }) {
  const [dismissed, setDismissed] = useState<string[]>(() => {
    try {
      return JSON.parse(localStorage.getItem(dismissKey(date)) ?? "[]") as string[];
    } catch {
      return [];
    }
  });
  const shown = nudges.filter((n) => !dismissed.includes(n.key));
  if (shown.length === 0) return null;
  const dismiss = (key: string) => {
    const next = [...dismissed, key];
    setDismissed(next);
    try {
      localStorage.setItem(dismissKey(date), JSON.stringify(next));
    } catch {
      /* ignore */
    }
  };
  return (
    <div className="grid gap-2">
      {shown.map((n) => (
        <NudgeCard key={n.key} nudge={n} onDismiss={() => dismiss(n.key)} />
      ))}
    </div>
  );
}

/** Calm, practical nudge: never red, never a shaming total (BR-14). */
export function NudgeCard({ nudge, onDismiss }: { nudge: S["NudgeDto"]; onDismiss?: () => void }) {
  const navigate = useNavigate();
  return (
    <div className="flex flex-wrap items-center gap-3 rounded-lg border border-primary/30 bg-primary/5 px-4 py-3" role="status">
      <Lightbulb className="size-5 text-primary" aria-hidden />
      <div className="min-w-0 flex-1">
        <p className="font-medium">{nudge.title}</p>
        <p className="text-sm text-muted-foreground">{nudge.message}</p>
      </div>
      <Button size="sm" variant="outline" onClick={() => navigate(nudge.actionLink)}>
        {nudge.actionLabel}
      </Button>
      {onDismiss && (
        <Button size="sm" variant="ghost" onClick={onDismiss} aria-label={`Dismiss "${nudge.title}" for today`}>
          Not today
        </Button>
      )}
    </div>
  );
}
