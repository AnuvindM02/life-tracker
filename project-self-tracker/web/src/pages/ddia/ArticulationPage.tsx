import { Pencil, Trash2 } from "lucide-react";
import { useState } from "react";
import { api, call, type S } from "@/api/client";
import { ddiaKeys, useAction, useArticulation, useArticulationPrompts } from "@/api/hooks";
import { Attachments } from "@/components/Attachments";
import { ConfirmDialog, EmptyState, Field, Loading, PageHeader, RatingInput, selectClass } from "@/components/common";
import { MarkdownEditor } from "@/components/MarkdownEditor";
import { Stopwatch } from "@/components/Stopwatch";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { isoDate, prettyDate } from "@/lib/format";
import { articulationKindLabel, DdiaNav } from "./DdiaShared";

const blank = {
  date: isoDate(),
  kind: "WhyXOverY" as S["ArticulationKind"],
  prompt: "",
  answerMd: "",
  tradeOffsMd: "",
  evidenceMd: "",
  selfRating: null as number | null,
  notes: "",
  minutes: 0,
};

export function ArticulationPage() {
  const [kind, setKind] = useState<S["ArticulationKind"] | "">("");
  const logs = useArticulation(kind || undefined);
  const prompts = useArticulationPrompts();
  const [editing, setEditing] = useState<S["ArticulationLogDto"] | null>(null);
  const [f, setF] = useState(blank);

  const save = useAction(
    () => {
      const body = { ...f, answerMd: f.answerMd || null, tradeOffsMd: f.tradeOffsMd || null, evidenceMd: f.evidenceMd || null, notes: f.notes || null, rowVersion: editing?.rowVersion ?? null };
      return editing
        ? call(api.PUT("/api/v1/ddia/articulation/{id}", { params: { path: { id: editing.id } }, body }))
        : call(api.POST("/api/v1/ddia/articulation", { body }));
    },
    {
      invalidate: [ddiaKeys.all, ["today"], ["log"]],
      success: "Saved.",
      onSuccess: () => {
        setEditing(null);
        setF({ ...blank, date: isoDate() });
      },
    },
  );
  const del = useAction((id: number) => call(api.DELETE("/api/v1/ddia/articulation/{id}", { params: { path: { id } } })), {
    invalidate: [ddiaKeys.all, ["today"]],
    success: "Moved to Trash.",
  });

  const edit = (a: S["ArticulationLogDto"]) => {
    setEditing(a);
    setF({
      date: a.date,
      kind: a.kind,
      prompt: a.prompt,
      answerMd: a.answerMd ?? "",
      tradeOffsMd: a.tradeOffsMd ?? "",
      evidenceMd: a.evidenceMd ?? "",
      selfRating: a.selfRating,
      notes: a.notes ?? "",
      minutes: a.minutes,
    });
    window.scrollTo({ top: 0, behavior: "smooth" });
  };

  return (
    <div>
      <DdiaNav />
      <PageHeader title="Articulation drills" subtitle="Weekly 'why X over Y': defend the choice and state when the alternative wins. Record and review for filler words and missing trade-offs." />
      <div className="grid gap-6 lg:grid-cols-3">
        <Card className="lg:col-span-2">
          <CardHeader>
            <CardTitle className="text-base">{editing ? "Edit entry" : "New entry"}</CardTitle>
            <CardDescription>Structure: answer first, then reasoning, trade-offs, and the evidence you would measure.</CardDescription>
          </CardHeader>
          <CardContent className="grid gap-4">
            <div className="grid gap-3 sm:grid-cols-3">
              <Field label="Date">{(id) => <Input id={id} type="date" max={isoDate()} value={f.date} onChange={(e) => setF({ ...f, date: e.target.value })} />}</Field>
              <Field label="Kind">
                {(id) => (
                  <select id={id} className={selectClass} value={f.kind} onChange={(e) => setF({ ...f, kind: e.target.value as S["ArticulationKind"] })}>
                    {Object.entries(articulationKindLabel).map(([k, v]) => <option key={k} value={k}>{v}</option>)}
                  </select>
                )}
              </Field>
              <Field label="Self-rating">{(id) => <RatingInput id={id} label="Self-rating" value={f.selfRating} onChange={(n) => setF({ ...f, selfRating: n })} />}</Field>
            </div>
            <Field label="Prompt">
              {(id) => (
                <>
                  <Input id={id} list="articulation-prompts" value={f.prompt} onChange={(e) => setF({ ...f, prompt: e.target.value })} placeholder="e.g. Queue vs direct call" />
                  <datalist id="articulation-prompts">
                    {prompts.data?.items.filter((p) => p.isActive).map((p) => <option key={p.id} value={p.text} />)}
                  </datalist>
                </>
              )}
            </Field>
            <div className="flex flex-wrap gap-1">
              {prompts.data?.items.filter((p) => p.isActive && p.kind === "WhyXOverY").map((p) => (
                <Button key={p.id} type="button" size="sm" variant="outline" onClick={() => setF({ ...f, kind: p.kind, prompt: p.text })}>{p.text}</Button>
              ))}
            </div>
            <Field label="Answer first">{(id) => <MarkdownEditor id={id} value={f.answerMd} onChange={(v) => setF({ ...f, answerMd: v })} rows={3} />}</Field>
            <Field label="Trade-offs (and when the alternative wins)">{(id) => <MarkdownEditor id={id} value={f.tradeOffsMd} onChange={(v) => setF({ ...f, tradeOffsMd: v })} rows={3} />}</Field>
            <Field label="Evidence I would measure">{(id) => <MarkdownEditor id={id} value={f.evidenceMd} onChange={(v) => setF({ ...f, evidenceMd: v })} rows={2} />}</Field>
            <Field label="Notes (filler words, gaps)">{(id) => <Input id={id} value={f.notes} onChange={(e) => setF({ ...f, notes: e.target.value })} />}</Field>
            <div className="flex flex-wrap items-center gap-3">
              <Stopwatch storageKey="articulation" label="Articulation timer" onStop={(m) => setF((x) => ({ ...x, minutes: x.minutes + m }))} />
              <Input type="number" min={0} className="w-24" aria-label="Minutes" value={f.minutes} onChange={(e) => setF({ ...f, minutes: Number(e.target.value) })} />
              <span className="text-sm text-muted-foreground">min</span>
              <Button className="ml-auto" disabled={!f.prompt.trim() || save.isPending} onClick={() => save.mutate(undefined)}>{editing ? "Save changes" : "Save entry"}</Button>
              {editing && <Button variant="ghost" onClick={() => { setEditing(null); setF({ ...blank, date: isoDate() }); }}>Cancel</Button>}
            </div>
            {editing && <Attachments entityType="ArticulationLog" entityId={editing.id} label="Articulation recording" compact />}
          </CardContent>
        </Card>
        <Card className="content-start">
          <CardHeader className="flex-row items-center gap-2">
            <CardTitle className="text-base">History</CardTitle>
            <select className={`${selectClass} ml-auto w-44`} value={kind} onChange={(e) => setKind(e.target.value as S["ArticulationKind"])} aria-label="Filter by kind">
              <option value="">All kinds</option>
              {Object.entries(articulationKindLabel).map(([k, v]) => <option key={k} value={k}>{v}</option>)}
            </select>
          </CardHeader>
          <CardContent className="grid gap-2 text-sm">
            {logs.isPending && <Loading />}
            {logs.data?.items.length === 0 && <EmptyState title="No entries yet" />}
            {logs.data?.items.map((a) => (
              <div key={a.id} className="grid gap-0.5 rounded-md border p-2">
                <div className="flex items-center gap-2">
                  <span className="font-medium">{a.prompt}</span>
                  <span className="ml-auto flex">
                    <Button size="icon" variant="ghost" className="size-7" onClick={() => edit(a)} aria-label="Edit entry"><Pencil /></Button>
                    <ConfirmDialog trigger={<Button size="icon" variant="ghost" className="size-7" aria-label="Delete entry"><Trash2 /></Button>} title="Delete this entry?" onConfirm={() => del.mutate(a.id)} />
                  </span>
                </div>
                <span className="text-xs text-muted-foreground">
                  {prettyDate(a.date, { day: "numeric", month: "short" })} · {articulationKindLabel[a.kind]}{a.selfRating ? ` · ${a.selfRating}/5` : ""}{a.minutes ? ` · ${a.minutes} min` : ""}
                  {a.attachmentCount > 0 && <Badge variant="outline" className="ml-1">{a.attachmentCount} file</Badge>}
                </span>
              </div>
            ))}
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
