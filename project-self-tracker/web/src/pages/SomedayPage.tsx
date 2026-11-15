import { ArrowUpRight, Trash2 } from "lucide-react";
import { useState } from "react";
import { Link } from "react-router";
import { api, call, type S } from "@/api/client";
import { useAction, useSomeday } from "@/api/hooks";
import { ConfirmDialog, EmptyState, Field, Loading, PageHeader, selectClass } from "@/components/common";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { prettyDate } from "@/lib/format";
import { cn } from "@/lib/utils";

export const somedayCategoryLabel: Record<S["SomedayCategory"], string> = {
  Tool: "Tool",
  Course: "Course",
  Framework: "Framework",
  ProjectIdea: "Project idea",
  Other: "Other",
};

const invalidate = [["someday"], ["monthly-prefill"], ["tasks"], ["today"]] as const;

export function SomedayRow({ item, triage }: { item: S["SomedayDto"]; triage?: boolean }) {
  const promote = useAction(() => call(api.POST("/api/v1/someday/{id}/promote", { params: { path: { id: item.id } } })), {
    invalidate,
    success: "Promoted to a plan task in the current phase.",
  });
  const decide = useAction((action: S["SomedayTriage"]) => call(api.POST("/api/v1/someday/{id}/triage", { params: { path: { id: item.id } }, body: { action } })), {
    invalidate,
  });
  const del = useAction(() => call(api.DELETE("/api/v1/someday/{id}", { params: { path: { id: item.id } } })), { invalidate, success: "Moved to Trash." });
  return (
    <div className="flex flex-wrap items-center gap-2 rounded-md px-2 py-1.5 text-sm hover:bg-accent/40">
      <Badge variant="outline" className="text-[10px]">{somedayCategoryLabel[item.category]}</Badge>
      <span className={cn("font-medium", item.status === "Dropped" && "text-muted-foreground line-through")}>{item.title}</span>
      {item.notesMd && <span className="max-w-xs truncate text-xs text-muted-foreground">{item.notesMd}</span>}
      <span className="text-xs text-muted-foreground">added {prettyDate(item.createdOn, { day: "numeric", month: "short" })}{item.reviewedOn ? ` · reviewed ${prettyDate(item.reviewedOn, { day: "numeric", month: "short" })}` : ""}</span>
      <span className="ml-auto flex items-center gap-1">
        {item.status === "Promoted" && item.promotedTaskId && <Link to="/plan/tasks" className="text-xs text-primary underline">plan task</Link>}
        {item.status === "Parked" && (
          <>
            <Button size="sm" variant="outline" onClick={() => promote.mutate(undefined)}><ArrowUpRight /> Promote</Button>
            {triage && <Button size="sm" variant="ghost" onClick={() => decide.mutate("Keep")}>Keep</Button>}
            <Button size="sm" variant="ghost" onClick={() => decide.mutate("Drop")}>Drop</Button>
          </>
        )}
        {!triage && <ConfirmDialog trigger={<Button size="icon" variant="ghost" className="size-7" aria-label={`Delete ${item.title}`}><Trash2 /></Button>} title="Delete this idea?" onConfirm={() => del.mutate(undefined)} />}
      </span>
    </div>
  );
}

export function SomedayPage() {
  const [status, setStatus] = useState<S["SomedayStatus"] | "">("Parked");
  const items = useSomeday(status || undefined);
  return (
    <div>
      <PageHeader title="Someday list" subtitle="Rule 2: one flagship, not five. New tools, courses and project ideas park here and are only revisited at the monthly review." />
      <div className="grid gap-6 lg:grid-cols-3">
        <Card className="lg:col-span-2">
          <CardHeader className="flex-row items-center gap-2">
            <CardTitle className="text-base">Ideas</CardTitle>
            <select className={cn(selectClass, "ml-auto w-40")} value={status} onChange={(e) => setStatus(e.target.value as S["SomedayStatus"])} aria-label="Status">
              <option value="">All</option>
              <option value="Parked">Parked</option>
              <option value="Promoted">Promoted</option>
              <option value="Dropped">Dropped</option>
            </select>
          </CardHeader>
          <CardContent className="grid gap-1">
            {items.isPending && <Loading />}
            {items.data?.items.length === 0 && <EmptyState title="Nothing here" />}
            {items.data?.items.map((i) => <SomedayRow key={i.id} item={i} />)}
          </CardContent>
        </Card>
        <Card className="content-start">
          <CardHeader>
            <CardTitle className="text-base">Park an idea</CardTitle>
            <CardDescription>Ctrl+K, then "Add someday idea" works from any page.</CardDescription>
          </CardHeader>
          <CardContent>
            <SomedayForm />
          </CardContent>
        </Card>
      </div>
    </div>
  );
}

function SomedayForm({ onDone }: { onDone?: () => void }) {
  const [title, setTitle] = useState("");
  const [notes, setNotes] = useState("");
  const [category, setCategory] = useState<S["SomedayCategory"]>("Tool");
  const add = useAction(() => call(api.POST("/api/v1/someday", { body: { title, notesMd: notes || null, category, status: "Parked", rowVersion: null } })), {
    invalidate,
    success: "Parked for the monthly review.",
    onSuccess: () => {
      setTitle("");
      setNotes("");
      onDone?.();
    },
  });
  return (
    <form
      className="grid gap-3"
      onSubmit={(e) => {
        e.preventDefault();
        add.mutate(undefined);
      }}
    >
      <Field label="Idea">{(id) => <Input id={id} value={title} onChange={(e) => setTitle(e.target.value)} placeholder="e.g. Try .NET Aspire for local orchestration" autoFocus />}</Field>
      <Field label="Category">
        {(id) => <select id={id} className={selectClass} value={category} onChange={(e) => setCategory(e.target.value as S["SomedayCategory"])}>{Object.entries(somedayCategoryLabel).map(([k, v]) => <option key={k} value={k}>{v}</option>)}</select>}
      </Field>
      <Field label="Why it might matter (optional)">{(id) => <Textarea id={id} rows={2} value={notes} onChange={(e) => setNotes(e.target.value)} />}</Field>
      <Button type="submit" disabled={!title.trim() || add.isPending}>Park it</Button>
    </form>
  );
}

export function SomedayQuickAdd({ open, onOpenChange }: { open: boolean; onOpenChange: (o: boolean) => void }) {
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Park a someday idea</DialogTitle>
          <DialogDescription>It waits for the monthly review instead of derailing this week.</DialogDescription>
        </DialogHeader>
        <SomedayForm onDone={() => onOpenChange(false)} />
        <DialogFooter />
      </DialogContent>
    </Dialog>
  );
}

