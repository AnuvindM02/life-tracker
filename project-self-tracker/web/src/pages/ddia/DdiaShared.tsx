import { AlertTriangle } from "lucide-react";
import { NavLink } from "react-router";
import { api, call, type S } from "@/api/client";
import { ddiaKeys, keys, useAction, useDdiaSummary } from "@/api/hooks";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { cn } from "@/lib/utils";

const LINKS = [
  ["/ddia", "Chapters"],
  ["/ddia/drills", "Design drills"],
  ["/ddia/articulation", "Articulation"],
] as const;

export function DdiaNav() {
  return (
    <nav className="mb-6 flex flex-wrap gap-1 border-b pb-2 text-sm" aria-label="DDIA and design">
      {LINKS.map(([to, label]) => (
        <NavLink key={to} to={to} end className={({ isActive }) => cn("rounded-md px-3 py-1.5 hover:bg-accent", isActive && "bg-accent font-medium")}>
          {label}
        </NavLink>
      ))}
    </nav>
  );
}

export const chapterStatusLabel: Record<S["ChapterStatus"], string> = {
  NotStarted: "Not started",
  Reading: "Reading",
  NoteDone: "Note done",
  Explained: "Explained in 90 s",
};

export function ChapterStatusBadge({ status }: { status: S["ChapterStatus"] }) {
  return <Badge variant={status === "NotStarted" ? "outline" : status === "Reading" ? "secondary" : "default"}>{chapterStatusLabel[status]}</Badge>;
}

export const editionLabel = (e: S["DdiaEdition"]) => (e === "First" ? "1st edition" : e === "Second" ? "2nd edition" : "Unconfirmed");

export const drillStatusLabel: Record<S["DrillStatus"], string> = {
  NotStarted: "Not started",
  Written: "Written",
  SpokenMock: "Spoken mock",
  Reviewed: "Reviewed",
};

export const articulationKindLabel: Record<S["ArticulationKind"], string> = {
  WhyXOverY: "Why X over Y",
  Explanation90: "90-second explanation",
  Other: "Other",
};

/** STOP BEFORE WEEK 2 (spec 4.5): persistent banner while the edition is unconfirmed from plan Week 2. */
export function EditionBanner() {
  const summary = useDdiaSummary();
  const confirm = useAction((edition: S["DdiaEdition"]) => call(api.POST("/api/v1/settings/confirm-ddia-edition", { body: { edition } })), {
    invalidate: [ddiaKeys.all, keys.settings, ["today"], ["tasks"]],
    success: "Edition confirmed. Chapter numbers now follow your copy.",
  });
  if (!summary.data?.showEditionWarning) return null;
  return (
    <div role="alert" className="flex flex-wrap items-center gap-3 border-b border-primary/40 bg-primary/10 px-4 py-2 text-sm">
      <AlertTriangle className="size-4 text-primary" aria-hidden />
      <span className="font-medium">{summary.data.editionWarning}</span>
      <span className="text-muted-foreground">Until then chapters follow the 2nd edition (2026, 14 chapters).</span>
      <span className="ml-auto flex gap-2">
        <Button size="sm" variant="outline" onClick={() => confirm.mutate("Second")}>I own the 2nd edition</Button>
        <Button size="sm" variant="outline" onClick={() => confirm.mutate("First")}>I own the 1st edition</Button>
      </span>
    </div>
  );
}
