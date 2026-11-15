import { NavLink } from "react-router";
import type { S } from "@/api/client";
import { Badge } from "@/components/ui/badge";
import { cn } from "@/lib/utils";

const LINKS = [
  ["/plan", "Overview"],
  ["/plan/tasks", "Tasks"],
  ["/plan/checkpoints", "Checkpoints"],
  ["/plan/replans", "Re-plan log"],
  ["/plan/pauses", "Pauses & travel"],
  ["/plan/schedule", "Schedule"],
] as const;

export function PlanNav() {
  return (
    <nav className="mb-6 flex flex-wrap gap-1 border-b pb-2 text-sm" aria-label="Plan">
      {LINKS.map(([to, label]) => (
        <NavLink key={to} to={to} end className={({ isActive }) => cn("rounded-md px-3 py-1.5 hover:bg-accent", isActive && "bg-accent font-medium")}>
          {label}
        </NavLink>
      ))}
    </nav>
  );
}

export const taskStatusLabel: Record<S["PlanTaskStatus"], string> = {
  NotStarted: "Not started",
  InProgress: "In progress",
  Done: "Done",
  Deferred: "Deferred",
  Skipped: "Skipped",
};

export function TaskStatusBadge({ status }: { status: S["PlanTaskStatus"] }) {
  return <Badge variant={status === "Done" ? "default" : status === "InProgress" ? "secondary" : "outline"}>{taskStatusLabel[status]}</Badge>;
}

export const triggerLabel: Record<S["ReplanTrigger"], string> = {
  SkillMismatch: "1. Repeated skill mismatch in postings or interviews",
  Phase1Behind: "2. Phase 1 more than three weeks behind",
  LifeChange: "3. Job, health, finances or family change",
  PackageBlocker: "4. Package/framework blocker over two focused sessions",
  Checkpoint: "Checkpoint evidence",
};

export const checkpointStateLabel: Record<S["CheckpointState"], string> = {
  Upcoming: "Upcoming",
  Soon: "Due soon",
  DueToday: "Due today",
  Overdue: "Past due",
  Answered: "Answered",
};
