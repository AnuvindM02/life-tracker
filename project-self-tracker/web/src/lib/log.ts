import { api, call, type S } from "@/api/client";

export type LogInput = S["DailyLogInput"];

/** Map a saved log to the PUT body (full update with its rowVersion, BR-16). */
export function toInput(l: S["DailyLogDto"]): LogInput {
  return {
    dayType: l.dayType,
    isTravelDay: l.isTravelDay,
    energy: l.energy,
    sleepHours: l.sleepHours,
    wakeTime: l.wakeTime,
    lightsOutTime: l.lightsOutTime,
    dsaLine: l.dsaLine,
    coreLine: l.coreLine,
    buildLine: l.buildLine,
    tomorrowDsa: l.tomorrowDsa,
    tomorrowBuild: l.tomorrowBuild,
    notesMd: l.notesMd,
    ritualOpenTracker: l.ritualOpenTracker,
    ritualReadWeekTarget: l.ritualReadWeekTarget,
    ritualWriteTargets: l.ritualWriteTargets,
    ritualPhoneAway: l.ritualPhoneAway,
    ritualStartTimer: l.ritualStartTimer,
    closeUpdateTracker: l.closeUpdateTracker,
    closeTomorrowTargets: l.closeTomorrowTargets,
    closePrep: l.closePrep,
    closeScreensOff: l.closeScreensOff,
    blocksDone: l.blocksDone,
    gymStatus: l.gymStatus,
    gymSession: l.gymSession,
    gymNote: l.gymNote,
    rowVersion: l.rowVersion,
  };
}

/**
 * Apply a small change (checkbox, block tick) to the date's log, creating an In progress log first
 * if none exists (spec v1.1, 4.3). Returns the updated log.
 */
export async function patchLog(date: string, change: (input: LogInput) => LogInput): Promise<S["DailyLogDto"]> {
  const log = await call(api.POST("/api/v1/daily-logs/by-date/{date}/ensure", { params: { path: { date } } }));
  return call(api.PUT("/api/v1/daily-logs/{id}", { params: { path: { id: log.id } }, body: change(toInput(log)) }));
}

export const START_RITUAL: [keyof LogInput, string][] = [
  ["ritualOpenTracker", "Open the tracker"],
  ["ritualReadWeekTarget", "Read the current week target"],
  ["ritualWriteTargets", "Write one DSA target and one build target"],
  ["ritualPhoneAway", "Put the phone away"],
  ["ritualStartTimer", "Start the timer"],
];

export const CLOSE_DAY: [keyof LogInput, string][] = [
  ["closeUpdateTracker", "Update the tracker"],
  ["closeTomorrowTargets", "Set tomorrow's targets"],
  ["closePrep", "Breakfast / lunch prep"],
  ["closeScreensOff", "Screens off"],
];

/** Tick criteria shown beside each tick (Appendix L.2). */
export const TICK_CRITERIA = {
  Dsa: {
    title: "DSA reps",
    counts: "One genuine attempt for 25+ minutes, followed by a mistake-log line; a scheduled cold review also counts.",
    doesNot: "Watching a solution, reading an editorial or copying code.",
  },
  Core: {
    title: "System design / core skill",
    counts: "A focused DDIA, design, React or MEAI/MAF block ends with a note, diagram or code artifact.",
    doesNot: "Scrolling or passive reading with no output.",
  },
  Build: {
    title: "Build / ship",
    counts: "A meaningful commit, test, design/ADR change, PR or deployed artifact on the current target.",
    doesNot: "Planning, or changing config for an hour without a durable result.",
  },
} as const;
