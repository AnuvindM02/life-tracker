import type { S } from "@/api/client";

/** Local calendar date as yyyy-MM-dd (no UTC shift). */
export function isoDate(d: Date = new Date()): string {
  const y = d.getFullYear();
  const m = String(d.getMonth() + 1).padStart(2, "0");
  const day = String(d.getDate()).padStart(2, "0");
  return `${y}-${m}-${day}`;
}

export function parseIso(s: string): Date {
  const [y, m, d] = s.split("-").map(Number);
  return new Date(y, m - 1, d);
}

export function addDays(s: string, n: number): string {
  const d = parseIso(s);
  d.setDate(d.getDate() + n);
  return isoDate(d);
}

export function prettyDate(s: string, opts: Intl.DateTimeFormatOptions = { weekday: "short", day: "numeric", month: "short", year: "numeric" }) {
  return parseIso(s).toLocaleDateString(undefined, opts);
}

export function minutes(total: number): string {
  if (total < 60) return `${total} min`;
  const h = Math.floor(total / 60);
  const m = total % 60;
  return m ? `${h} h ${m} min` : `${h} h`;
}

export const categoryLabel: Record<S["TimeCategory"], string> = {
  Dsa: "DSA",
  DdiaDesign: "DDIA / design",
  MainTrack: "Main track",
  Review: "Review",
  Mocks: "Mocks",
  Applications: "Applications",
  Other: "Other",
};

export const dayTypeLabel: Record<NonNullable<S["DayType"]>, string> = { Full: "Full", Mvd: "MVD", Rest: "Rest" };

export const travelRoleLabel: Record<S["TravelRole"], string> = {
  None: "",
  PreplannedMvd: "Travel: pre-planned MVD",
  TimedAttempt: "Travel: timed attempt",
  Review: "Travel: review",
  ShortReview: "Travel: short review",
};

export const trackLabel: Record<S["PlanTrack"], string> = {
  Dsa: "DSA",
  Ddia: "DDIA",
  React: "React",
  MeaiMaf: "MEAI/MAF",
  Design: "Design",
  Flagship: "Flagship",
  Scale: "Scale",
  Interview: "Interview",
  Review: "Review",
  Other: "Other",
};

export const pauseReasonLabel: Record<NonNullable<S["PauseReason"]>, string> = {
  Illness: "Illness",
  Travel: "Travel",
  WorkSpike: "Work spike",
  FamilyEvent: "Family event",
  Other: "Other",
};

/** IANA zones for pickers; Chrome reports Asia/Calcutta, so the canonical default is added explicitly. */
export function timeZones(): string[] {
  return Array.from(new Set(["Asia/Kolkata", "UTC", ...Intl.supportedValuesOf("timeZone")]));
}
