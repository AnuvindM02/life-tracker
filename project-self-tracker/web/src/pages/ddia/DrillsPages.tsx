import { Mic, PenLine } from "lucide-react";
import { useState } from "react";
import { Link, useNavigate, useParams } from "react-router";
import { api, call, type S } from "@/api/client";
import { ddiaKeys, useAction, useDrill, useDrills, useToday } from "@/api/hooks";
import { Attachments } from "@/components/Attachments";
import { ErrorBox, Field, Loading, PageHeader, selectClass } from "@/components/common";
import { SegmentTimer } from "@/components/Stopwatch";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Checkbox } from "@/components/ui/checkbox";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { isoDate, prettyDate } from "@/lib/format";
import { cn } from "@/lib/utils";
import { DdiaNav, drillStatusLabel } from "./DdiaShared";

/** Appendix H 45-minute framework; keys match the drill's note fields. */
export const SEGMENTS = [
  { field: "clarifyNotes", label: "Clarify requirements / scale", minutes: 5 },
  { field: "estimateNotes", label: "Estimate load / storage", minutes: 5 },
  { field: "apiDataNotes", label: "API and data model", minutes: 8 },
  { field: "highLevelNotes", label: "High-level design", minutes: 10 },
  { field: "deepDiveNotes", label: "Two hardest deep dives", minutes: 10 },
  { field: "tradeOffNotes", label: "Bottlenecks, failure modes and trade-offs", minutes: 7 },
] as const;

export function DrillsPage() {
  const drills = useDrills();
  const today = useToday();
  const week = today.data?.planDay?.week ?? 0;
  if (drills.isPending) return <Loading />;
  if (drills.isError) return <ErrorBox error={drills.error} />;
  const groups = [...new Set(drills.data.map((d) => d.group))];

  return (
    <div>
      <DdiaNav />
      <PageHeader
        title="System design drills"
        subtitle="One per week from around Day 80. Write drills 1-8 in full; record later drills as spoken mocks. 45 minutes: clarify 5, estimate 5, API and data 8, high-level 10, deep dives 10, trade-offs 7."
      />
      <div className="grid gap-4 md:grid-cols-2">
        {groups.map((g) => (
          <Card key={g}>
            <CardHeader>
              <CardTitle className="text-base">Drills {g}</CardTitle>
            </CardHeader>
            <CardContent className="grid gap-1">
              {drills.data.filter((d) => d.group === g).map((d) => (
                <Link key={d.id} to={`/ddia/drills/${d.id}`} className={cn("flex flex-wrap items-center gap-2 rounded-md px-2 py-1.5 text-sm hover:bg-accent/50", d.plannedWeek === week && "bg-primary/5 ring-1 ring-primary/40")}>
                  <span className="w-6 text-right font-mono text-muted-foreground">{d.number}</span>
                  <span className="font-medium">{d.prompt}</span>
                  {d.plannedWeek && <span className="text-xs text-muted-foreground">W{d.plannedWeek}</span>}
                  {d.plannedWeek === week && <Badge>this week</Badge>}
                  <span className="ml-auto flex items-center gap-1">
                    {d.number <= 8 ? <PenLine className="size-3.5 text-muted-foreground" aria-label="write in full" /> : <Mic className="size-3.5 text-muted-foreground" aria-label="spoken mock" />}
                    <Badge variant={d.status === "NotStarted" ? "outline" : "secondary"}>{drillStatusLabel[d.status]}</Badge>
                  </span>
                </Link>
              ))}
            </CardContent>
          </Card>
        ))}
      </div>
    </div>
  );
}

export function DrillPage() {
  const id = Number(useParams().id);
  const drill = useDrill(id);
  if (drill.isPending) return <Loading />;
  if (drill.isError) return <ErrorBox error={drill.error} />;
  return <DrillEditor key={drill.data.rowVersion} d={drill.data} />;
}

function DrillEditor({ d }: { d: S["DesignDrillDto"] }) {
  const navigate = useNavigate();
  const [f, setF] = useState({
    clarifyNotes: d.clarifyNotes ?? "",
    estimateNotes: d.estimateNotes ?? "",
    apiDataNotes: d.apiDataNotes ?? "",
    highLevelNotes: d.highLevelNotes ?? "",
    deepDiveNotes: d.deepDiveNotes ?? "",
    tradeOffNotes: d.tradeOffNotes ?? "",
    status: d.status,
    spokenMock: d.spokenMock,
    practicedOn: d.practicedOn ?? "",
    minutes: d.minutes,
  });
  const save = useAction(
    () =>
      call(
        api.PUT("/api/v1/ddia/drills/{id}", {
          params: { path: { id: d.id } },
          body: {
            number: d.number,
            prompt: d.prompt,
            group: d.group,
            plannedWeek: d.plannedWeek,
            ...f,
            practicedOn: f.practicedOn || null,
            rowVersion: d.rowVersion,
          },
        }),
      ),
    { invalidate: [ddiaKeys.all, ["today"], ["log"]], success: "Drill saved." },
  );

  return (
    <div>
      <DdiaNav />
      <PageHeader
        title={`Drill ${d.number}: ${d.prompt}`}
        subtitle={`${d.number <= 8 ? "Write this one in full." : "Record this one as a spoken mock and attach the recording."}${d.plannedWeek ? ` Planned for week ${d.plannedWeek}.` : ""}${d.reviewedOn ? ` Reviewed ${prettyDate(d.reviewedOn)}.` : ""}`}
        actions={<Button variant="outline" onClick={() => navigate("/ddia/drills")}>All drills</Button>}
      />
      <div className="grid gap-6 lg:grid-cols-3">
        <Card className="lg:col-span-2">
          <CardHeader>
            <CardTitle className="text-base">45-minute framework</CardTitle>
            <CardDescription>
              <SegmentTimer
                storageKey={`drill-${d.id}`}
                segments={SEGMENTS}
                onStop={(m) => setF((x) => ({ ...x, minutes: x.minutes + m, practicedOn: x.practicedOn || isoDate() }))}
              />
            </CardDescription>
          </CardHeader>
          <CardContent className="grid gap-4">
            {SEGMENTS.map((s) => (
              <Field key={s.field} label={`${s.label} (${s.minutes} min)`}>
                {(id) => <Textarea id={id} rows={3} value={f[s.field]} onChange={(e) => setF({ ...f, [s.field]: e.target.value })} />}
              </Field>
            ))}
          </CardContent>
        </Card>
        <div className="grid content-start gap-6">
          <Card>
            <CardHeader>
              <CardTitle className="text-base">Status</CardTitle>
              <CardDescription>"Written" needs all six parts. Use the 45-minute structure for spoken mocks too.</CardDescription>
            </CardHeader>
            <CardContent className="grid gap-3">
              <Field label="Status">
                {(id) => (
                  <select id={id} className={selectClass} value={f.status} onChange={(e) => setF({ ...f, status: e.target.value as S["DrillStatus"] })}>
                    {Object.entries(drillStatusLabel).map(([k, v]) => <option key={k} value={k}>{v}</option>)}
                  </select>
                )}
              </Field>
              <label className="flex items-center gap-2 text-sm"><Checkbox checked={f.spokenMock} onCheckedChange={(c) => setF({ ...f, spokenMock: c === true })} /> Done as a spoken mock</label>
              <div className="grid grid-cols-2 gap-2">
                <Field label="Practised on">{(id) => <Input id={id} type="date" max={isoDate()} value={f.practicedOn} onChange={(e) => setF({ ...f, practicedOn: e.target.value })} />}</Field>
                <Field label="Minutes">{(id) => <Input id={id} type="number" min={0} value={f.minutes} onChange={(e) => setF({ ...f, minutes: Number(e.target.value) })} />}</Field>
              </div>
              <Button onClick={() => save.mutate(undefined)} disabled={save.isPending}>Save drill</Button>
            </CardContent>
          </Card>
          <Card>
            <CardHeader>
              <CardTitle className="text-base">Recording and diagrams</CardTitle>
            </CardHeader>
            <CardContent>
              <Attachments entityType="DesignDrill" entityId={d.id} label="Design drill" compact />
            </CardContent>
          </Card>
        </div>
      </div>
    </div>
  );
}
