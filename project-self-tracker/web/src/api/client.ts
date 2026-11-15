import createClient from "openapi-fetch";
import type { components, paths } from "./schema";

export type S = components["schemas"];

/** Header required by the API's local-request guard on every state-changing request (spec 6.2). */
export const GUARD_HEADER = { "X-SelfTracker": "1" } as const;

export interface Problem {
  status?: number;
  title?: string;
  detail?: string;
  errors?: Record<string, string[]>;
  type?: string;
}

export class ApiError extends Error {
  readonly status: number;
  readonly problem: Problem;

  constructor(status: number, problem: Problem) {
    super(problem.title ?? `Request failed (${status})`);
    this.status = status;
    this.problem = problem;
  }

  get isConflict() {
    return this.status === 409;
  }

  /** Flattened list of field errors for display. */
  get messages(): string[] {
    const fromFields = Object.values(this.problem.errors ?? {}).flat();
    return fromFields.length > 0 ? fromFields : [this.message];
  }
}

export const api = createClient<paths>({ baseUrl: "", headers: GUARD_HEADER });

/** Unwrap an openapi-fetch result: return data or throw ApiError with the ProblemDetails body. */
export async function call<T>(promise: Promise<{ data?: T; error?: unknown; response: Response }>): Promise<T> {
  const { data, error, response } = await promise;
  if (!response.ok) {
    const problem = (error ?? {}) as Problem;
    throw new ApiError(response.status, typeof problem === "object" ? problem : { title: String(problem) });
  }
  return data as T;
}

export function errorText(e: unknown): string {
  if (e instanceof ApiError) return e.messages.join(" ");
  if (e instanceof Error) return e.message;
  return String(e);
}
