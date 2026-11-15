import { describe, expect, it } from "vitest";

/**
 * BR-14: no UI text may express hours owed, catch-up or debt. Quoted roadmap rules that negate these
 * ideas ("nothing needs to be repaid") are fine; the forbidden patterns below are app-authored framings.
 */
const FORBIDDEN = [/hours? owed/i, /\bowed\b/i, /catch[- ]?up/i, /make[- ]up hours/i, /\bdebt\b/i, /behind by \d+ hours?/i, /backlog hours/i];

const sources = import.meta.glob("../**/*.{ts,tsx}", { query: "?raw", import: "default", eager: true }) as Record<string, string>;

describe("BR-14 no-debt wording", () => {
  it("scans every UI source file", () => {
    const offenders: string[] = [];
    for (const [file, text] of Object.entries(sources)) {
      if (file.includes("/test/") || /\.test\.tsx?$/.test(file) || file.endsWith("schema.d.ts")) continue;
      for (const re of FORBIDDEN) if (re.test(text)) offenders.push(`${file}: ${re}`);
    }
    expect(offenders, offenders.join(", ")).toEqual([]);
  });
});
