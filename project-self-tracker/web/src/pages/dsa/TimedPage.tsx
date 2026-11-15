import { useState } from "react";
import { useNavigate } from "react-router";
import { api, call } from "@/api/client";
import { dsaKeys, useAction, useDsaProblems } from "@/api/hooks";
import { Field, PageHeader, RatingInput, selectClass } from "@/components/common";
import { Stopwatch } from "@/components/Stopwatch";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Checkbox } from "@/components/ui/checkbox";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { isoDate } from "@/lib/format";
import { DsaNav } from "./DsaNav";

interface Entry {
  problemId: number | null;
  minutes: number;
  confidence: number;
  solved: boolean;
  missed: string;
  pattern: string;
}

const blank = (): Entry => ({ problemId: null, minutes: 0, confidence: 0, solved: false, missed: "", pattern: "" });

/** Phase 3-4 timed session (spec 4.4): 1-2 problems, one combined timer, one Timed attempt logged per problem. */
export function TimedPage() {
  const problems = useDsaProblems();
  const navigate = useNavigate();
  const [company, setCompany] = useState("");
  const [entries, setEntries] = useState<Entry[]>([blank()]);
  const set = (i: number, patch: Partial<Entry>) => setEntries((es) => es.map((e, j) => (j === i ? { ...e, ...patch } : e)));

  const save = useAction(
    async () => {
      for (const e of entries) {
        await call(
          api.POST("/api/v1/dsa/attempts", {
            body: {
              date: isoDate(),
              problemId: e.problemId,
              titleFreeText: null,
              type: "Timed",
              pass1Min: e.minutes,
              hintLevel: "None",
              pass2Min: 0,
              pass3Min: 0,
              totalMin: null,
              solvedUnaided: e.solved,
              pattern: e.pattern || null,
              keyInsight: null,
              whatIMissed: e.missed || null,
              timeComplexity: null,
              spaceComplexity: null,
              confidence: e.confidence,
              codeMd: null,
              company: company || null,
              mistakeTags: [],
              reviewItemId: null,
              rowVersion: null,
            },
          }),
        );
      }
      return entries.length;
    },
    { invalidate: [dsaKeys.all, ["today"], ["log"]], success: (n) => `Logged ${n} timed attempt(s).`, onSuccess: () => navigate("/dsa/attempts") },
  );

  const ready = entries.every((e) => e.problemId && e.confidence && e.minutes > 0);

  return (
    <div>
      <DsaNav />
      <PageHeader title="Timed session" subtitle="Two timed problems in a session can be enough. Company-tagged practice only for genuine target roles; review mistakes afterwards." />
      <div className="grid gap-6 lg:grid-cols-3">
        <Card className="lg:col-span-2">
          <CardHeader>
            <CardTitle className="text-base">Problems</CardTitle>
            <CardDescription>Stopping the timer splits the minutes evenly across the problems; adjust if needed.</CardDescription>
          </CardHeader>
          <CardContent className="grid gap-4">
            {entries.map((e, i) => (
              <div key={i} className="grid gap-3 rounded-md border p-3 sm:grid-cols-2">
                <Field label={`Problem ${i + 1}`}>
                  {(id) => (
                    <select id={id} className={selectClass} value={e.problemId ?? ""} onChange={(ev) => set(i, { problemId: ev.target.value ? Number(ev.target.value) : null })}>
                      <option value="">Choose...</option>
                      {problems.data?.items.map((p) => <option key={p.id} value={p.id}>{p.topicName}: {p.title}</option>)}
                    </select>
                  )}
                </Field>
                <Field label="Minutes">{(id) => <Input id={id} type="number" min={0} value={e.minutes} onChange={(ev) => set(i, { minutes: Number(ev.target.value) })} />}</Field>
                <Field label="Confidence">{(id) => <RatingInput id={id} label={`Confidence for problem ${i + 1}`} value={e.confidence || null} onChange={(n) => set(i, { confidence: n ?? 0 })} />}</Field>
                <label className="flex items-center gap-2 self-end pb-2 text-sm">
                  <Checkbox checked={e.solved} onCheckedChange={(c) => set(i, { solved: c === true })} /> Solved unaided
                </label>
                <Field label="Pattern">{(id) => <Input id={id} value={e.pattern} onChange={(ev) => set(i, { pattern: ev.target.value })} />}</Field>
                <Field label="What I missed">{(id) => <Textarea id={id} rows={2} value={e.missed} onChange={(ev) => set(i, { missed: ev.target.value })} />}</Field>
              </div>
            ))}
            <div className="flex gap-2">
              {entries.length < 2 && <Button variant="outline" onClick={() => setEntries((es) => [...es, blank()])}>Add a second problem</Button>}
              {entries.length > 1 && <Button variant="ghost" onClick={() => setEntries((es) => es.slice(0, 1))}>Remove the second problem</Button>}
            </div>
          </CardContent>
        </Card>
        <Card className="content-start">
          <CardHeader>
            <CardTitle className="text-base">Session</CardTitle>
          </CardHeader>
          <CardContent className="grid gap-4">
            <Stopwatch storageKey="timed-session" label="Combined timer" onStop={(m) => setEntries((es) => es.map((e) => ({ ...e, minutes: e.minutes + Math.max(1, Math.round(m / es.length)) })))} />
            <Field label="Company tag (optional)">{(id) => <Input id={id} value={company} onChange={(e) => setCompany(e.target.value)} />}</Field>
            <Button disabled={!ready || save.isPending} onClick={() => save.mutate(undefined)}>Log {entries.length} timed attempt(s)</Button>
          </CardContent>
        </Card>
      </div>
    </div>
  );
}

