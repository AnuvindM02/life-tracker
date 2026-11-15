import { CheckCircle2, ChevronLeft, ChevronRight, Circle, Plane } from "lucide-react";
import { useState } from "react";
import { Link, useNavigate } from "react-router";
import type { S } from "@/api/client";
import { useHeatmap, useLogs, useSettings } from "@/api/hooks";
import { EmptyState, ErrorBox, Loading, PageHeader, selectClass } from "@/components/common";
import { Heatmap, stateStyle } from "@/components/Heatmap";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { dayTypeLabel, isoDate, minutes, prettyDate } from "@/lib/format";
import { cn } from "@/lib/utils";

export function LogsPage() {
  const navigate = useNavigate();
  return (
    <div>
      <PageHeader
        title="Daily logs"
        subtitle="One log per date. Backfilling is allowed and marked as logged later."
        actions={<Button onClick={() => navigate(`/logs/${isoDate()}`)}>Log today</Button>}
      />
      <Tabs defaultValue="calendar">
        <TabsList>
          <TabsTrigger value="calendar">Calendar</TabsTrigger>
          <TabsTrigger value="list">List</TabsTrigger>
          <TabsTrigger value="year">365-day heatmap</TabsTrigger>
        </TabsList>
        <TabsContent value="calendar" className="mt-4">
          <MonthCalendar />
        </TabsContent>
        <TabsContent value="list" className="mt-4">
          <LogList />
        </TabsContent>
        <TabsContent value="year" className="mt-4">
          <YearHeatmap />
        </TabsContent>
      </Tabs>
    </div>
  );
}

function LogList() {
  const [month, setMonth] = useState(isoDate().slice(0, 7));
  const [type, setType] = useState<S["DayType"] | "">("");
  const [page, setPage] = useState(1);
  const from = month ? `${month}-01` : undefined;
  const to = month ? isoDate(new Date(Number(month.slice(0, 4)), Number(month.slice(5, 7)), 0)) : undefined;
  const logs = useLogs({ from, to, type: type || undefined, page, pageSize: 31 });

  return (
    <div className="grid gap-4">
      <div className="flex flex-wrap gap-3">
        <Input type="month" value={month} onChange={(e) => { setMonth(e.target.value); setPage(1); }} className="w-44" aria-label="Month" />
        <select className={cn(selectClass, "w-40")} value={type ?? ""} onChange={(e) => { setType(e.target.value as S["DayType"]); setPage(1); }} aria-label="Day type">
          <option value="">All types</option>
          <option value="Full">Full</option>
          <option value="Mvd">MVD</option>
          <option value="Rest">Rest</option>
        </select>
      </div>
      {logs.isPending ? (
        <Loading />
      ) : logs.isError ? (
        <ErrorBox error={logs.error} />
      ) : logs.data.items.length === 0 ? (
        <EmptyState title="No logs for this filter" />
      ) : (
        <div className="overflow-x-auto rounded-md border">
          <table className="w-full text-sm">
            <thead className="bg-muted/40 text-left text-xs text-muted-foreground">
              <tr>
                <th className="p-2">Date</th>
                <th className="p-2">Type</th>
                <th className="p-2">DSA</th>
                <th className="p-2">Core</th>
                <th className="p-2">Build</th>
                <th className="p-2">Time</th>
                <th className="p-2">Energy</th>
                <th className="p-2">Sleep</th>
                <th className="p-2">Gym</th>
              </tr>
            </thead>
            <tbody>
              {logs.data.items.map((l) => (
                <tr key={l.id} className="border-t hover:bg-accent/40">
                  <td className="p-2">
                    <Link to={`/logs/${l.date}`} className="font-medium text-primary underline-offset-2 hover:underline">
                      {prettyDate(l.date)}
                    </Link>
                    {l.isTravelDay && <Plane className="ml-1 inline size-3 text-primary" aria-label="travel day" />}
                    {l.loggedLate && <span className="ml-1 text-xs text-muted-foreground">(later)</span>}
                  </td>
                  <td className="p-2">{l.dayType ? <Badge variant="secondary">{dayTypeLabel[l.dayType]}</Badge> : <span className="text-xs text-muted-foreground">In progress</span>}</td>
                  {[l.dsaTick, l.coreTick, l.buildTick].map((on, i) => (
                    <td key={i} className="p-2">
                      {on ? <CheckCircle2 className="size-4 text-success" aria-label="ticked" /> : <Circle className="size-4 text-muted-foreground/50" aria-label="not ticked" />}
                    </td>
                  ))}
                  <td className="p-2">{l.totalMinutes ? minutes(l.totalMinutes) : ""}</td>
                  <td className="p-2">{l.energy ?? ""}</td>
                  <td className="p-2">{l.sleepHours ?? ""}</td>
                  <td className="p-2">{l.gymStatus ?? ""}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      {logs.data && logs.data.total > logs.data.pageSize && (
        <div className="flex items-center gap-2 text-sm">
          <Button size="sm" variant="outline" disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>Previous</Button>
          <span>Page {page}</span>
          <Button size="sm" variant="outline" disabled={page * logs.data.pageSize >= logs.data.total} onClick={() => setPage((p) => p + 1)}>Next</Button>
        </div>
      )}
    </div>
  );
}

function MonthCalendar() {
  const [cursor, setCursor] = useState(() => {
    const d = new Date();
    return new Date(d.getFullYear(), d.getMonth(), 1);
  });
  const from = isoDate(cursor);
  const to = isoDate(new Date(cursor.getFullYear(), cursor.getMonth() + 1, 0));
  const heat = useHeatmap(from, to);
  const offset = (cursor.getDay() + 6) % 7;

  return (
    <Card>
      <CardContent className="grid gap-3 pt-6">
        <div className="flex items-center gap-2">
          <Button size="icon" variant="ghost" onClick={() => setCursor(new Date(cursor.getFullYear(), cursor.getMonth() - 1, 1))} aria-label="Previous month">
            <ChevronLeft />
          </Button>
          <h2 className="w-44 text-center font-medium">{cursor.toLocaleDateString(undefined, { month: "long", year: "numeric" })}</h2>
          <Button size="icon" variant="ghost" onClick={() => setCursor(new Date(cursor.getFullYear(), cursor.getMonth() + 1, 1))} aria-label="Next month">
            <ChevronRight />
          </Button>
        </div>
        {heat.isError ? (
          <ErrorBox error={heat.error} />
        ) : (
          <div className="grid grid-cols-7 gap-1 text-sm" role="grid" aria-label="Month calendar">
            {["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"].map((d) => (
              <div key={d} className="p-1 text-center text-xs text-muted-foreground" role="columnheader">{d}</div>
            ))}
            {Array.from({ length: offset }).map((_, i) => (
              <div key={`pad-${i}`} />
            ))}
            {(heat.data?.cells ?? []).map((c) => (
              <Link
                key={c.date}
                to={c.state === "Future" ? "#" : `/logs/${c.date}`}
                role="gridcell"
                aria-label={`${prettyDate(c.date)}: ${stateStyle[c.state].label}`}
                className="flex min-h-16 flex-col rounded-md border p-1.5 hover:bg-accent/40"
              >
                <span className="flex items-center justify-between text-xs">
                  <span className={cn(c.date === isoDate() && "font-bold text-primary")}>{Number(c.date.slice(8))}</span>
                  {c.isTravel && <Plane className="size-3 text-primary" aria-label="travel" />}
                </span>
                {c.planDay > 0 && <span className="text-[10px] text-muted-foreground">Day {c.planDay}</span>}
                <span className={cn("mt-auto self-start rounded px-1 text-[10px] font-semibold", stateStyle[c.state].className)}>
                  {c.state === "Future" || c.state === "NotStarted" ? "" : stateStyle[c.state].label}
                </span>
              </Link>
            ))}
          </div>
        )}
      </CardContent>
    </Card>
  );
}

function YearHeatmap() {
  const settings = useSettings();
  const heat = useHeatmap(undefined, undefined, Boolean(settings.data?.planStartDate));
  if (heat.isPending) return <Loading />;
  if (heat.isError) return <ErrorBox error={heat.error} />;
  return (
    <Card>
      <CardContent className="pt-6">
        <p className="mb-3 text-sm text-muted-foreground">
          Plan year {prettyDate(heat.data.from)} to {prettyDate(heat.data.to)} (pauses extend it).
        </p>
        <Heatmap cells={heat.data.cells} />
      </CardContent>
    </Card>
  );
}
