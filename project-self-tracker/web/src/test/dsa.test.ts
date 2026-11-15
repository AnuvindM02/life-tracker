import { describe, expect, it } from "vitest";
import { countPreview } from "@/lib/dsa";
import { toAttemptBody, type AttemptForm } from "@/pages/dsa/AttemptPage";

const form: AttemptForm = {
  date: "2026-11-15",
  problemId: 3,
  titleFreeText: "",
  type: "New",
  pass1Min: 28,
  hintLevel: "None",
  pass2Min: 5,
  pass3Min: 10,
  solvedUnaided: false,
  pattern: " hashing ",
  keyInsight: "",
  whatIMissed: "checked complement too late",
  timeComplexity: "O(n)",
  spaceComplexity: "",
  confidence: 2,
  codeMd: "",
  company: "",
  mistakeTags: ["off-by-one"],
};

describe("BR-08 counted preview (mirrors the server)", () => {
  it("counts a 25+ minute unaided attempt with a mistake line", () => {
    expect(countPreview({ type: "New", pass1Min: 25, whatIMissed: "x", hasProblem: true, unaidedMin: 25 }).counted).toBe(true);
  });
  it("explains a short attempt without a mistake line", () => {
    const r = countPreview({ type: "Timed", pass1Min: 20, whatIMissed: " ", hasProblem: true, unaidedMin: 25 });
    expect(r.counted).toBe(false);
    expect(r.reason).toMatch(/25\+/);
    expect(r.reason).toMatch(/what you missed/);
  });
  it("counts any cold review of a library problem, never a free-text one", () => {
    expect(countPreview({ type: "ColdReview", pass1Min: 5, whatIMissed: "", hasProblem: true, unaidedMin: 25 }).counted).toBe(true);
    expect(countPreview({ type: "ColdReview", pass1Min: 30, whatIMissed: "x", hasProblem: false, unaidedMin: 25 }).counted).toBe(false);
  });
});

describe("attempt form body", () => {
  it("trims text, nulls empties, lets the server total the passes and passes the review item", () => {
    const body = toAttemptBody(form, 42, null);
    expect(body).toMatchObject({ problemId: 3, titleFreeText: null, pattern: "hashing", keyInsight: null, spaceComplexity: null, totalMin: null, reviewItemId: 42 });
    expect(body.mistakeTags).toEqual(["off-by-one"]);
  });
  it("keeps the free-text title only when no library problem is chosen", () => {
    expect(toAttemptBody({ ...form, problemId: null, titleFreeText: " Contest Q2 " }, null, null).titleFreeText).toBe("Contest Q2");
  });
});

import { segmentAt } from "@/components/Stopwatch";

describe("design drill segment timer", () => {
  const minutes = [5, 5, 8, 10, 10, 7];
  it("finds the current segment and time left", () => {
    expect(segmentAt(0, minutes)).toEqual({ index: 0, remainingMs: 5 * 60_000, over: false });
    expect(segmentAt(12 * 60_000, minutes)).toMatchObject({ index: 2, remainingMs: 6 * 60_000 });
    expect(segmentAt(45 * 60_000, minutes).over).toBe(true);
  });
});
