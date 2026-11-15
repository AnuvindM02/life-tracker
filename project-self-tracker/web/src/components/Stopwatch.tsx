import { Pause, Play, RotateCcw, Square } from "lucide-react";
import { useEffect, useState } from "react";
import { Button } from "@/components/ui/button";
import { cn } from "@/lib/utils";

interface TimerState {
  startedAt: number | null;
  accumulatedMs: number;
}

const read = (key: string): TimerState => {
  try {
    const raw = localStorage.getItem(key);
    if (raw) return JSON.parse(raw) as TimerState;
  } catch {
    /* storage unavailable */
  }
  return { startedAt: null, accumulatedMs: 0 };
};

const write = (key: string, s: TimerState) => {
  try {
    localStorage.setItem(key, JSON.stringify(s));
  } catch {
    /* storage unavailable */
  }
};

/** Elapsed milliseconds for a timer state at `now`. */
export const elapsedMs = (s: TimerState, now: number) => s.accumulatedMs + (s.startedAt ? now - s.startedAt : 0);

export const clock = (ms: number) => {
  const total = Math.floor(ms / 1000);
  return `${String(Math.floor(total / 3600)).padStart(2, "0")}:${String(Math.floor((total % 3600) / 60)).padStart(2, "0")}:${String(total % 60).padStart(2, "0")}`;
};

/** A timer whose state lives in localStorage, so it survives a page refresh (spec 4.4). */
export function usePersistedTimer(storageKey: string) {
  const key = `selftracker.timer.${storageKey}`;
  const [state, setState] = useState<TimerState>(() => read(key));
  const [now, setNow] = useState(() => Date.now());

  useEffect(() => {
    if (!state.startedAt) return;
    const t = setInterval(() => setNow(Date.now()), 1000);
    return () => clearInterval(t);
  }, [state.startedAt]);

  const update = (s: TimerState) => {
    setState(s);
    write(key, s);
  };
  const ms = elapsedMs(state, now);
  return {
    ms,
    running: state.startedAt !== null,
    start: () => update({ ...state, startedAt: Date.now() }),
    pause: () => update({ startedAt: null, accumulatedMs: ms }),
    reset: () => update({ startedAt: null, accumulatedMs: 0 }),
  };
}

/**
 * Stopwatch that survives a page refresh.
 * Stopping reports whole minutes (at least 1) to `onStop` and resets.
 */
export function Stopwatch({ storageKey, onStop, label = "Timer" }: { storageKey: string; onStop: (minutes: number) => void; label?: string }) {
  const t = usePersistedTimer(storageKey);
  return (
    <div className="flex items-center gap-2" aria-label={label}>
      <span className="font-mono text-lg tabular-nums" aria-live="off">{clock(t.ms)}</span>
      <TimerButtons t={t} onStop={onStop} />
    </div>
  );
}

function TimerButtons({ t, onStop }: { t: ReturnType<typeof usePersistedTimer>; onStop: (minutes: number) => void }) {
  return (
    <>
      {t.running ? (
        <Button type="button" size="icon" variant="outline" onClick={t.pause} aria-label="Pause timer"><Pause /></Button>
      ) : (
        <Button type="button" size="icon" variant="outline" onClick={t.start} aria-label="Start timer"><Play /></Button>
      )}
      <Button
        type="button"
        size="icon"
        variant="outline"
        disabled={t.ms < 1000}
        onClick={() => {
          onStop(Math.max(1, Math.round(t.ms / 60000)));
          t.reset();
        }}
        aria-label="Stop and record minutes"
      >
        <Square />
      </Button>
      <Button type="button" size="icon" variant="ghost" disabled={t.ms === 0} onClick={t.reset} aria-label="Reset timer"><RotateCcw /></Button>
    </>
  );
}

/** Which segment an elapsed time falls in, for a list of segment lengths in minutes. */
export function segmentAt(ms: number, minutes: readonly number[]): { index: number; remainingMs: number; over: boolean } {
  let start = 0;
  for (let i = 0; i < minutes.length; i++) {
    const end = start + minutes[i] * 60_000;
    if (ms < end) return { index: i, remainingMs: end - ms, over: false };
    start = end;
  }
  return { index: minutes.length - 1, remainingMs: 0, over: true };
}

/** One clock for a framework of timed segments (the 45-minute design drill): highlights the current segment. */
export function SegmentTimer({
  storageKey,
  segments,
  onStop,
}: {
  storageKey: string;
  segments: readonly { label: string; minutes: number }[];
  onStop: (minutes: number) => void;
}) {
  const t = usePersistedTimer(storageKey);
  const at = segmentAt(t.ms, segments.map((s) => s.minutes));
  return (
    <div className="grid gap-3">
      <div className="flex flex-wrap items-center gap-2">
        <span className="font-mono text-lg tabular-nums">{clock(t.ms)}</span>
        <TimerButtons t={t} onStop={onStop} />
        <span className="text-sm text-muted-foreground" aria-live="polite">
          {t.ms === 0 ? "Start when ready." : at.over ? "Time is up: wrap up trade-offs." : `Now: ${segments[at.index].label}, ${Math.ceil(at.remainingMs / 60000)} min left`}
        </span>
      </div>
      <ol className="flex flex-wrap gap-1 text-xs">
        {segments.map((s, i) => (
          <li
            key={s.label}
            className={cn(
              "rounded-md border px-2 py-1",
              t.ms > 0 && i === at.index && !at.over && "border-primary bg-primary/10 text-primary",
              t.ms > 0 && (i < at.index || at.over) && "text-muted-foreground line-through",
            )}
          >
            {s.label} · {s.minutes}
          </li>
        ))}
      </ol>
    </div>
  );
}
