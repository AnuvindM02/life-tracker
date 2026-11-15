import { CheckCircle2, Circle, Trash2 } from "lucide-react";
import { useEffect, useState } from "react";
import { useParams } from "react-router";
import { toast } from "sonner";
import { api, ApiError, call, type S } from "@/api/client";
import { ddiaKeys, useAction, useDdiaChapter, useDdiaNote, useReadingLog } from "@/api/hooks";
import { Attachments } from "@/components/Attachments";
import { ConfirmDialog, ErrorBox, Field, Loading, PageHeader, RatingInput, selectClass } from "@/components/common";
import { MarkdownEditor } from "@/components/MarkdownEditor";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { prettyDate } from "@/lib/format";
import { QuickReading } from "./ChaptersPage";
import { chapterStatusLabel, DdiaNav, editionLabel } from "./DdiaShared";

export function ChapterPage() {
  const id = Number(useParams().id);
  const chapter = useDdiaChapter(id);
  const note = useDdiaNote(id);
  const reading = useReadingLog(id);
  if (chapter.isPending || note.isPending) return <Loading />;
  if (chapter.isError) return <ErrorBox error={chapter.error} />;
  if (note.isError) return <ErrorBox error={note.error} />;
  const c = chapter.data;

  return (
    <div>
      <DdiaNav />
      <PageHeader
        title={`Ch. ${c.number}: ${c.title}`}
        subtitle={`${editionLabel(c.edition)} · planned week ${c.plannedWeekFrom}${c.plannedWeekTo !== c.plannedWeekFrom ? `-${c.plannedWeekTo}` : ""} · ${chapterStatusLabel[c.status]}`}
      />
      <div className="grid gap-6 lg:grid-cols-3">
        <div className="grid content-start gap-6 lg:col-span-2">
          <NoteEditor key={note.data?.rowVersion ?? "new"} chapter={c} note={note.data} />
        </div>
        <div className="grid content-start gap-6">
          <ChapterCard chapter={c} />
          <QuickReading chapters={[c]} defaultId={c.id} />
          <Card>
            <CardHeader>
              <CardTitle className="text-base">Reading log</CardTitle>
              <CardDescription>{c.pagesRead} pages{c.totalPages ? ` of ${c.totalPages}` : ""} · {c.readingMinutes} min</CardDescription>
            </CardHeader>
            <CardContent className="grid gap-1 text-sm">
              {reading.data?.items.length === 0 && <p className="text-muted-foreground">Nothing logged yet.</p>}
              {reading.data?.items.map((r) => <ReadingRow key={r.id} row={r} />)}
            </CardContent>
          </Card>
        </div>
      </div>
    </div>
  );
}

function ReadingRow({ row }: { row: S["ReadingLogDto"] }) {
  const del = useAction(() => call(api.DELETE("/api/v1/ddia/reading-log/{id}", { params: { path: { id: row.id } } })), {
    invalidate: [ddiaKeys.all, ["today"], ["log"]],
    success: "Reading entry moved to Trash.",
  });
  return (
    <div className="flex items-center gap-2">
      <span className="w-24">{prettyDate(row.date, { day: "numeric", month: "short" })}</span>
      <span>{row.pagesRead} pp · {row.minutes} min</span>
      <span className="min-w-0 flex-1 truncate text-xs text-muted-foreground">{row.note}</span>
      <Button size="icon" variant="ghost" className="size-7" onClick={() => del.mutate(undefined)} aria-label="Delete reading entry"><Trash2 /></Button>
    </div>
  );
}

function ChapterCard({ chapter: c }: { chapter: S["DdiaChapterDto"] }) {
  const [title, setTitle] = useState(c.title);
  const [total, setTotal] = useState(c.totalPages ? String(c.totalPages) : "");
  const [status, setStatus] = useState(c.status);
  const save = useAction(
    () =>
      call(
        api.PUT("/api/v1/ddia/chapters/{id}", {
          params: { path: { id: c.id } },
          body: {
            edition: c.edition,
            number: c.number,
            title,
            plannedWeekFrom: c.plannedWeekFrom,
            plannedWeekTo: c.plannedWeekTo,
            topics: c.topics,
            askOpsConnection: c.askOpsConnection,
            totalPages: total ? Number(total) : null,
            status,
            rowVersion: c.rowVersion,
          },
        }),
      ),
    { invalidate: [ddiaKeys.all], success: "Chapter saved." },
  );
  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base">Chapter</CardTitle>
        <CardDescription>{c.topics}</CardDescription>
      </CardHeader>
      <CardContent className="grid gap-3 text-sm">
        {c.askOpsConnection && <p><span className="text-muted-foreground">Ask-Ops:</span> {c.askOpsConnection}</p>}
        <Field label="Title (edit if your copy differs)">{(id) => <Input id={id} value={title} onChange={(e) => setTitle(e.target.value)} />}</Field>
        <div className="grid grid-cols-2 gap-2">
          <Field label="Total pages">{(id) => <Input id={id} type="number" min={1} value={total} onChange={(e) => setTotal(e.target.value)} />}</Field>
          <Field label="Status">
            {(id) => (
              <select id={id} className={selectClass} value={status} onChange={(e) => setStatus(e.target.value as S["ChapterStatus"])}>
                {Object.entries(chapterStatusLabel).map(([k, v]) => <option key={k} value={k}>{v}</option>)}
              </select>
            )}
          </Field>
        </div>
        <p className="text-xs text-muted-foreground">
          {c.startedOn && `Started ${prettyDate(c.startedOn)}.`} {c.finishedOn && `Finished ${prettyDate(c.finishedOn)}.`}
        </p>
        <Button variant="outline" onClick={() => save.mutate(undefined)} disabled={save.isPending}>Save chapter</Button>
      </CardContent>
    </Card>
  );
}

const IDEAS = ["keyIdea1", "keyIdea2", "keyIdea3", "keyIdea4", "keyIdea5"] as const;

type NoteForm = Omit<S["DdiaNoteInput"], "questions" | "rowVersion"> & { questions: { question: string; answer60sMd: string }[] };

function NoteEditor({ chapter, note }: { chapter: S["DdiaChapterDto"]; note: S["DdiaNoteDto"] | null }) {
  const draftKey = `selftracker.draft.ddia-note.${chapter.id}`;
  const initial: NoteForm = {
    keyIdea1: note?.keyIdea1 ?? "",
    keyIdea2: note?.keyIdea2 ?? "",
    keyIdea3: note?.keyIdea3 ?? "",
    keyIdea4: note?.keyIdea4 ?? "",
    keyIdea5: note?.keyIdea5 ?? "",
    tradeOffsMd: note?.tradeOffsMd ?? "",
    failureModesMd: note?.failureModesMd ?? "",
    askOpsDecision: note?.askOpsDecision ?? "",
    explanationRating: note?.explanationRating ?? null,
    explanationNotes: note?.explanationNotes ?? "",
    questions: Array.from({ length: Math.max(3, note?.questions.length ?? 0) }, (_, i) => ({
      question: note?.questions[i]?.question ?? "",
      answer60sMd: note?.questions[i]?.answer60sMd ?? "",
    })),
  };
  const [f, setF] = useState<NoteForm>(initial);
  const [dirty, setDirty] = useState(false);
  const [draft] = useState<NoteForm | null>(() => {
    try {
      const raw = localStorage.getItem(draftKey);
      return raw ? (JSON.parse(raw) as NoteForm) : null;
    } catch {
      return null;
    }
  });
  const set = (patch: Partial<NoteForm>) => {
    setF((x) => {
      const next = { ...x, ...patch };
      try {
        localStorage.setItem(draftKey, JSON.stringify(next));
      } catch {
        /* ignore */
      }
      return next;
    });
    setDirty(true);
  };
  useEffect(() => {
    const onUnload = (e: BeforeUnloadEvent) => {
      if (dirty) e.preventDefault();
    };
    window.addEventListener("beforeunload", onUnload);
    return () => window.removeEventListener("beforeunload", onUnload);
  }, [dirty]);

  const save = useAction(
    () => call(api.PUT("/api/v1/ddia/notes/by-chapter/{chapterId}", { params: { path: { chapterId: chapter.id } }, body: { ...f, rowVersion: note?.rowVersion ?? null } })),
    {
      invalidate: [ddiaKeys.all, ["today"]],
      success: (n) => (n.status === "Complete" ? "Note complete." : `Saved as a draft: ${n.missing.length} item(s) to go.`),
      onSuccess: () => {
        localStorage.removeItem(draftKey);
        setDirty(false);
      },
    },
  );
  const del = useAction(() => call(api.DELETE("/api/v1/ddia/notes/{id}", { params: { path: { id: note!.id } } })), {
    invalidate: [ddiaKeys.all],
    success: "Note moved to Trash.",
  });

  return (
    <Card>
      <CardHeader className="flex-row flex-wrap items-start gap-2">
        <div className="grid gap-1">
          <CardTitle className="text-base">Chapter note</CardTitle>
          <CardDescription>
            {editionLabel(chapter.edition)}, ch. {chapter.number}: {chapter.title}. One page: five key ideas, trade-offs, failure modes, one Ask-Ops decision, three interview questions.
          </CardDescription>
        </div>
        {note && (
          <ConfirmDialog
            trigger={<Button size="icon" variant="ghost" className="ml-auto" aria-label="Delete note"><Trash2 /></Button>}
            title="Delete this note?"
            description="It moves to the Trash with its interview questions."
            onConfirm={() => del.mutate(undefined)}
          />
        )}
      </CardHeader>
      <CardContent className="grid gap-4">
        {draft && !dirty && JSON.stringify(draft) !== JSON.stringify(initial) && (
          <div className="flex items-center gap-2 rounded-md border border-primary/40 bg-primary/5 px-3 py-2 text-sm">
            An unsaved draft exists.
            <Button size="sm" variant="outline" onClick={() => { setF(draft); setDirty(true); }}>Restore draft</Button>
          </div>
        )}
        {note && (
          <div className="grid gap-1 rounded-md border p-3 text-sm">
            {note.missing.length === 0 ? (
              <p className="flex items-center gap-2 text-success"><CheckCircle2 className="size-4" /> Note complete.</p>
            ) : (
              note.missing.map((m) => <p key={m} className="flex items-center gap-2 text-muted-foreground"><Circle className="size-3.5" /> Still to add: {m}</p>)
            )}
          </div>
        )}
        <fieldset className="grid gap-2">
          <legend className="mb-1 text-sm font-medium">Five key ideas in your own words</legend>
          {IDEAS.map((k, i) => (
            <Input key={k} aria-label={`Key idea ${i + 1}`} placeholder={`${i + 1}.`} value={f[k] ?? ""} onChange={(e) => set({ [k]: e.target.value })} />
          ))}
        </fieldset>
        <Field label="Trade-offs" hint="Option A vs B: what you gain, what you pay and when each wins.">
          {(id) => <MarkdownEditor id={id} value={f.tradeOffsMd ?? ""} onChange={(v) => set({ tradeOffsMd: v })} rows={4} />}
        </Field>
        <Field label="Failure modes" hint="What goes wrong and how it appears in a real system.">
          {(id) => <MarkdownEditor id={id} value={f.failureModesMd ?? ""} onChange={(v) => set({ failureModesMd: v })} rows={3} />}
        </Field>
        <Field label="In Ask-Ops I will..." hint="One concrete decision this chapter changes.">
          {(id) => <Input id={id} value={f.askOpsDecision ?? ""} onChange={(e) => set({ askOpsDecision: e.target.value })} />}
        </Field>
        <fieldset className="grid gap-3">
          <legend className="mb-1 text-sm font-medium">Interview questions with 60-second answers</legend>
          {f.questions.map((q, i) => (
            <div key={i} className="grid gap-1 rounded-md border p-2">
              <Input aria-label={`Question ${i + 1}`} placeholder={`Question ${i + 1}`} value={q.question} onChange={(e) => set({ questions: f.questions.map((x, j) => (j === i ? { ...x, question: e.target.value } : x)) })} />
              <Textarea aria-label={`Answer ${i + 1}`} rows={2} placeholder="Answer first, reasoning, trade-offs, evidence you would measure." value={q.answer60sMd} onChange={(e) => set({ questions: f.questions.map((x, j) => (j === i ? { ...x, answer60sMd: e.target.value } : x)) })} />
            </div>
          ))}
          <Button type="button" size="sm" variant="ghost" className="justify-self-start" onClick={() => set({ questions: [...f.questions, { question: "", answer60sMd: "" }] })}>Add a question</Button>
        </fieldset>
        <div className="grid gap-2 rounded-md border p-3">
          <p className="text-sm font-medium">90-second explanation</p>
          <p className="text-xs text-muted-foreground">Say it aloud, record it if you can, then rate it. Note filler words and missing trade-offs.</p>
          <RatingInput label="Explanation self-rating" value={f.explanationRating} onChange={(n) => set({ explanationRating: n })} />
          <Textarea aria-label="Explanation notes" rows={2} placeholder="Filler words, missing trade-offs..." value={f.explanationNotes ?? ""} onChange={(e) => set({ explanationNotes: e.target.value })} />
        </div>
        <div className="flex items-center gap-3">
          <Button
            onClick={() =>
              save.mutate(undefined, {
                onError: (e) => {
                  if (e instanceof ApiError && e.isConflict) toast.info("Reload the page to get the latest note; your draft is kept.");
                },
              })
            }
            disabled={save.isPending}
          >
            {note ? "Save note" : "Create note"}
          </Button>
          {dirty && <span className="text-sm text-muted-foreground">Unsaved changes (a local draft is kept).</span>}
        </div>
        {note && (
          <div className="grid gap-2 border-t pt-3">
            <p className="text-sm font-medium">Attachments (diagrams, the explanation recording)</p>
            <Attachments entityType="DdiaNote" entityId={note.id} label="DDIA note" />
          </div>
        )}
      </CardContent>
    </Card>
  );
}
