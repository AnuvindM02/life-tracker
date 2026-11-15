import type { S } from "@/api/client";

/** Client mirror of BR-08 (v1.1) for a live preview; the server decides. */
export function countPreview(args: {
  type: S["AttemptType"];
  pass1Min: number;
  whatIMissed: string;
  hasProblem: boolean;
  unaidedMin: number;
}): { counted: boolean; reason: string | null } {
  if (args.type === "ColdReview") {
    return args.hasProblem
      ? { counted: true, reason: null }
      : { counted: false, reason: "Cold reviews count only for problems in the library." };
  }
  const reasons: string[] = [];
  if (args.pass1Min < args.unaidedMin) reasons.push(`Pass 1 needs ${args.unaidedMin}+ unaided minutes (now ${args.pass1Min}).`);
  if (!args.whatIMissed.trim()) reasons.push("Fill in what you missed (the mistake-log line).");
  return reasons.length ? { counted: false, reason: reasons.join(" ") } : { counted: true, reason: null };
}

export const CONFIDENCE_HINT = "1-3: re-solve cold at +1, +3, +7 and +21 days. 4-5: one later review.";
