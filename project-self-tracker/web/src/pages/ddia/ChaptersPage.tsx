import { BookOpen, CheckCircle2, FileText } from "lucide-react";
import { useState } from "react";
import { Link } from "react-router";
import { api, call, type S } from "@/api/client";
import { ddiaKeys, useAction, useDdiaChapters, useDdiaSummary } from "@/api/hooks";
import { ErrorBox, Field, Loading, PageHeader, selectClass } from "@/components/common";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Progress } from "@/components/ui/progress";
import { isoDate } from "@/lib/format";
import { ChapterStatusBadge, DdiaNav, editionLabel } from "./DdiaShared";

export function ChaptersPage() {
  const summary = useDdiaSummary();
  const s = summary.data;
  const chapters = useDdiaChapters(s?.displayEdition);
  if (summary.isPending) return <Loading />;
  if (summary.isError) return <ErrorBox error={summary.error} />;

  return (
    <div>
      <DdiaNav />
      <PageHeader
        title="Designing Data-Intensive Applications"
        subtitle={
          <span>
            {editionLabel(s!.displayEdition)}
            {s!.settingsEdition === "Unconfirmed" && " (unconfirmed: check your copy's title page)"} · {s!.notesDone} of {s!.chapters} chapter notes done ·{" "}
            {s!.pagesPerDayMin}-{s!.pagesPerDayMax} pages a day is a guide, not a quota.
          </span>
        }
      />
      <div className="grid gap-6 lg:grid-cols-3">
        <Card className="lg:col-span-2">
          <CardHeader>
            <CardTitle className="text-base">Chapters</CardTitle>
            <CardDescription>The chapter/week grouping is a pacing aid; chapters are not equal length. Keep reading order and a useful note.</CardDescription>
          </CardHeader>
          <CardContent className="grid gap-1">
            {chapters.isPending && <Loading />}
            {chapters.data?.items.map((c) => {
              const current = s!.weekChapters.some((w) => w.id === c.id);
              return (
                <Link key={c.id} to={`/ddia/chapters/${c.id}`} className={`flex flex-wrap items-center gap-2 rounded-md px-2 py-2 text-sm hover:bg-accent/50 ${current ? "bg-primary/5 ring-1 ring-primary/40" : ""}`}>
                  <span className="w-8 text-right font-mono text-muted-foreground">{c.number}.</span>
                  <span className="font-medium">{c.title}</span>
                  <span className="text-xs text-muted-foreground">W{c.plannedWeekFrom}{c.plannedWeekTo !== c.plannedWeekFrom ? `-${c.plannedWeekTo}` : ""}</span>
                  {current && <Badge>this week</Badge>}
                  <span className="ml-auto flex items-center gap-2">
                    {c.totalPages ? <span className="text-xs text-muted-foreground">{c.pagesRead}/{c.totalPages} pp</span> : c.pagesRead ? <span className="text-xs text-muted-foreground">{c.pagesRead} pp</span> : null}
                    {c.noteStatus === "Complete" ? <FileText className="size-4 text-success" aria-label="note complete" /> : c.noteId ? <FileText className="size-4 text-muted-foreground" aria-label="draft note" /> : null}
                    <ChapterStatusBadge status={c.status} />
                  </span>
                </Link>
              );
            })}
          </CardContent>
        </Card>
        <div className="grid content-start gap-6">
          <Card>
            <CardHeader>
              <CardTitle className="flex items-center gap-2 text-base"><BookOpen className="size-4" /> This week</CardTitle>
            </CardHeader>
            <CardContent className="grid gap-3 text-sm">
              {s!.weekChapters.length === 0 && <p className="text-muted-foreground">No chapter is planned for week {s!.planWeek ?? "-"}. After the book, use a design drill or ADR defence.</p>}
              {s!.weekChapters.map((w) => (
                <div key={w.id} className="grid gap-1">
                  <Link to={`/ddia/chapters/${w.id}`} className="font-medium text-primary hover:underline">Ch. {w.number}: {w.title}</Link>
                  {w.totalPages ? <Progress value={(w.pagesRead / w.totalPages) * 100} aria-label={`${w.pagesRead} of ${w.totalPages} pages`} /> : <span className="text-xs text-muted-foreground">{w.pagesRead} pages read (set the page count on the chapter)</span>}
                </div>
              ))}
            </CardContent>
          </Card>
          <QuickReading chapters={chapters.data?.items ?? []} defaultId={s!.weekChapters[0]?.id} />
        </div>
      </div>
    </div>
  );
}

export function QuickReading({ chapters, defaultId }: { chapters: S["DdiaChapterDto"][]; defaultId?: number }) {
  const [chapterId, setChapterId] = useState<number | "">(defaultId ?? "");
  const [pages, setPages] = useState("");
  const [mins, setMins] = useState("");
  const [note, setNote] = useState("");
  const add = useAction(
    () =>
      call(
        api.POST("/api/v1/ddia/reading-log", {
          body: { date: isoDate(), chapterId: Number(chapterId || defaultId), pagesRead: Number(pages || 0), minutes: Number(mins || 0), note: note || null, rowVersion: null },
        }),
      ),
    {
      invalidate: [ddiaKeys.all, ["today"], ["log"]],
      success: (r) => `Logged ${r.pagesRead} pages of ch. ${r.chapterNumber}.`,
      onSuccess: () => {
        setPages("");
        setMins("");
        setNote("");
      },
    },
  );
  const id = chapterId || defaultId;
  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2 text-base"><CheckCircle2 className="size-4" /> Log reading</CardTitle>
        <CardDescription>About 7 pages plus a three-line note is a good evening.</CardDescription>
      </CardHeader>
      <CardContent className="grid gap-3">
        <Field label="Chapter">
          {(fid) => (
            <select id={fid} className={selectClass} value={id ?? ""} onChange={(e) => setChapterId(e.target.value ? Number(e.target.value) : "")}>
              <option value="">Choose...</option>
              {chapters.map((c) => <option key={c.id} value={c.id}>{c.number}. {c.title}</option>)}
            </select>
          )}
        </Field>
        <div className="grid grid-cols-2 gap-2">
          <Field label="Pages">{(fid) => <Input id={fid} type="number" min={0} value={pages} onChange={(e) => setPages(e.target.value)} />}</Field>
          <Field label="Minutes">{(fid) => <Input id={fid} type="number" min={0} value={mins} onChange={(e) => setMins(e.target.value)} />}</Field>
        </div>
        <Field label="Note (optional)">{(fid) => <Input id={fid} value={note} onChange={(e) => setNote(e.target.value)} placeholder="pp. 40-47" />}</Field>
        <Button disabled={!id || (!Number(pages) && !Number(mins)) || add.isPending} onClick={() => add.mutate(undefined)}>Log</Button>
      </CardContent>
    </Card>
  );
}
