import { useState } from "react";
import { Link } from "react-router";
import type { S } from "@/api/client";
import { parseIso, prettyDate } from "@/lib/format";
import { cn } from "@/lib/utils";

/** Each state has a letter as well as a colour, so the heatmap never relies on colour alone (spec 8). */
export const stateStyle: Record<S["DayState"], { letter: string; label: string; className: string }> = {
  Full: { letter: "F", label: "Full", className: "bg-success text-background" },
  Mvd: { letter: "M", label: "MVD", className: "bg-info text-background" },
  Rest: { letter: "R", label: "Rest", className: "bg-secondary text-secondary-foreground" },
  Missed: { letter: "·", label: "Not logged", className: "bg-transparent text-muted-foreground border border-border" },
  Paused: { letter: "P", label: "Paused", className: "bg-muted text-muted-foreground [background-image:repeating-linear-gradient(45deg,transparent_0_3px,rgb(255_255_255/0.08)_3px_6px)]" },
  Future: { letter: "", label: "Future", className: "bg-transparent border border-dashed border-border/60" },
  Today: { letter: "T", label: "Today", className: "border-2 border-primary text-primary" },
  Unclassified: { letter: "?", label: "Unclassified (choose a type)", className: "bg-transparent border border-primary/60 text-primary" },
  NotStarted: { letter: "", label: "Before start", className: "bg-transparent" },
};

/** Calendar-week columns (Mon-Sun rows), GitHub-style; each cell links to its daily log. */
export function Heatmap({ cells }: { cells: S["HeatmapCell"][] }) {
  const [table, setTable] = useState(false);
  if (cells.length === 0) return null;
  const first = parseIso(cells[0].date);
  const offset = (first.getDay() + 6) % 7; // Monday = 0
  const padded: (S["HeatmapCell"] | null)[] = [...Array(offset).fill(null), ...cells];
  const weeks: (S["HeatmapCell"] | null)[][] = [];
  for (let i = 0; i < padded.length; i += 7) weeks.push(padded.slice(i, i + 7));

  const counts = cells.reduce<Record<string, number>>((acc, c) => ({ ...acc, [c.state]: (acc[c.state] ?? 0) + 1 }), {});

  return (
    <div className="grid gap-3">
      <div className="flex flex-wrap items-center gap-3 text-xs text-muted-foreground">
        {(["Full", "Mvd", "Rest", "Paused", "Missed", "Today"] as const).map((s) => (
          <span key={s} className="flex items-center gap-1">
            <span className={cn("grid size-4 place-items-center rounded-sm text-[9px] font-bold", stateStyle[s].className)}>{stateStyle[s].letter}</span>
            {stateStyle[s].label} {counts[s] ? `(${counts[s]})` : ""}
          </span>
        ))}
        <button type="button" className="ml-auto underline" onClick={() => setTable((t) => !t)}>
          {table ? "Show heatmap" : "Show as table"}
        </button>
      </div>
      {table ? (
        <div className="max-h-80 overflow-auto rounded-md border">
          <table className="w-full text-sm">
            <thead className="sticky top-0 bg-card">
              <tr className="text-left">
                <th className="p-2">Date</th>
                <th className="p-2">Plan day</th>
                <th className="p-2">State</th>
                <th className="p-2">Travel</th>
              </tr>
            </thead>
            <tbody>
              {cells.map((c) => (
                <tr key={c.date} className="border-t">
                  <td className="p-2">{prettyDate(c.date)}</td>
                  <td className="p-2">{c.planDay || ""}</td>
                  <td className="p-2">{stateStyle[c.state].label}</td>
                  <td className="p-2">{c.isTravel ? "Yes" : ""}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ) : (
        <div className="overflow-x-auto pb-1">
          <div className="flex gap-[3px]" role="grid" aria-label="Plan-year heatmap">
            {weeks.map((week, wi) => (
              <div key={wi} className="flex flex-col gap-[3px]" role="row">
                {week.map((c, di) =>
                  c ? (
                    <Link
                      key={c.date}
                      to={c.state === "Future" ? "#" : `/logs/${c.date}`}
                      role="gridcell"
                      title={`${prettyDate(c.date)}${c.planDay ? ` · Day ${c.planDay}` : ""} · ${stateStyle[c.state].label}${c.isTravel ? " · travel" : ""}`}
                      aria-label={`${prettyDate(c.date)}: ${stateStyle[c.state].label}`}
                      className={cn(
                        "grid size-4 place-items-center rounded-sm text-[9px] font-bold leading-none",
                        stateStyle[c.state].className,
                        c.isTravel && "ring-1 ring-primary/50",
                      )}
                    >
                      {stateStyle[c.state].letter}
                    </Link>
                  ) : (
                    <span key={`pad-${di}`} className="size-4" />
                  ),
                )}
              </div>
            ))}
          </div>
        </div>
      )}
      <p className="text-xs text-muted-foreground">A tick measures behaviour, not mastery.</p>
    </div>
  );
}
