import { NavLink } from "react-router";
import type { S } from "@/api/client";
import { Badge } from "@/components/ui/badge";
import { cn } from "@/lib/utils";

const LINKS = [
  ["/dsa", "Overview"],
  ["/dsa/problems", "Library"],
  ["/dsa/attempts", "Attempts"],
  ["/dsa/reviews", "Review queue"],
  ["/dsa/progress", "Progress"],
  ["/dsa/timed", "Timed session"],
] as const;

export function DsaNav() {
  return (
    <nav className="mb-6 flex flex-wrap gap-1 border-b pb-2 text-sm" aria-label="DSA">
      {LINKS.map(([to, label]) => (
        <NavLink key={to} to={to} end className={({ isActive }) => cn("rounded-md px-3 py-1.5 hover:bg-accent", isActive && "bg-accent font-medium")}>
          {label}
        </NavLink>
      ))}
    </nav>
  );
}

export const statusVariant: Record<S["ProblemStatus"], "default" | "secondary" | "outline"> = {
  NotStarted: "outline",
  Attempted: "secondary",
  Solved: "default",
  Deferred: "outline",
};

export const statusLabel: Record<S["ProblemStatus"], string> = {
  NotStarted: "Not started",
  Attempted: "Attempted",
  Solved: "Solved",
  Deferred: "Deferred",
};

export const attemptTypeLabel: Record<S["AttemptType"], string> = { New: "New", ColdReview: "Cold review", Timed: "Timed" };

export function StatusBadge({ status }: { status: S["ProblemStatus"] }) {
  return <Badge variant={statusVariant[status]}>{statusLabel[status]}</Badge>;
}

/** Four-pass method (spec 9.1): guide durations shown beside each timer. */
export const PASSES = [
  { key: "pass1Min", label: "Pass 1: Attempt", guide: "25-30 min. Restate aloud, examples and edge cases, brute force, optimise. No hints or AI yet.", target: 25 },
  { key: "pass2Min", label: "Pass 2: Unstick", guide: "10-15 min. One hint (topic, then pattern), code your best attempt; only then read a solution.", target: 10 },
  { key: "pass3Min", label: "Pass 3: Rewrite", guide: "10 min. Close the solution, re-implement in C#, explain aloud, state complexity, test edge cases.", target: 10 },
] as const;
