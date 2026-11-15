import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { api, call, errorText, type S } from "./client";

export const keys = {
  settings: ["settings"] as const,
  today: (date?: string) => ["today", date ?? "now"] as const,
  log: (date: string) => ["log", date] as const,
  logs: (params: object) => ["logs", params] as const,
  heatmap: (from?: string, to?: string) => ["heatmap", from, to] as const,
  phases: ["phases"] as const,
  tasks: (params: object) => ["tasks", params] as const,
  pauses: ["pauses"] as const,
  travel: ["travel"] as const,
  blocks: (phase?: string) => ["blocks", phase] as const,
  gym: (phase?: string) => ["gym", phase] as const,
  files: (params: object) => ["files", params] as const,
  fileStats: ["fileStats"] as const,
  trash: ["trash"] as const,
  backups: ["backups"] as const,
};

export function useSettings() {
  return useQuery({ queryKey: keys.settings, queryFn: () => call(api.GET("/api/v1/settings")) });
}

export function useToday(date?: string) {
  return useQuery({
    queryKey: keys.today(date),
    queryFn: () => call(api.GET("/api/v1/views/today", { params: { query: { date } } })),
  });
}

/** Log for a date, or null when none exists yet. */
export function useLogByDate(date: string) {
  return useQuery({
    queryKey: keys.log(date),
    queryFn: async () => {
      const res = await api.GET("/api/v1/daily-logs/by-date/{date}", { params: { path: { date } } });
      if (res.response.status === 404) return null;
      return call(Promise.resolve(res));
    },
  });
}

export function useLogs(params: { from?: string; to?: string; type?: S["DayType"]; page?: number; pageSize?: number }) {
  return useQuery({
    queryKey: keys.logs(params),
    queryFn: () =>
      call(
        api.GET("/api/v1/daily-logs", {
          params: { query: { ...params, type: params.type ?? undefined, sort: "-date" } },
        }),
      ),
    placeholderData: keepPreviousData,
  });
}

export function useHeatmap(from?: string, to?: string, enabled = true) {
  return useQuery({
    queryKey: keys.heatmap(from, to),
    queryFn: () => call(api.GET("/api/v1/views/heatmap", { params: { query: { from, to } } })),
    enabled,
  });
}

export function usePhases() {
  return useQuery({ queryKey: keys.phases, queryFn: () => call(api.GET("/api/v1/plan/phases")) });
}

export function usePlanTasks(params: { week?: number; phase?: number }) {
  return useQuery({
    queryKey: keys.tasks(params),
    queryFn: () => call(api.GET("/api/v1/plan/tasks", { params: { query: { ...params, pageSize: 200 } } })),
  });
}

export function usePauses() {
  return useQuery({
    queryKey: keys.pauses,
    queryFn: () => call(api.GET("/api/v1/plan/pause-ranges", { params: { query: { pageSize: 200, sort: "-startDate" } } })),
  });
}

export function useTravelOverrides() {
  return useQuery({
    queryKey: keys.travel,
    queryFn: () => call(api.GET("/api/v1/plan/travel-overrides", { params: { query: { pageSize: 200, sort: "-weekFriday" } } })),
  });
}

export function useBlocks() {
  return useQuery({
    queryKey: keys.blocks(),
    queryFn: () => call(api.GET("/api/v1/schedule/blocks", { params: { query: { pageSize: 200, sort: "sortOrder" } } })),
  });
}

export function useGymPlan() {
  return useQuery({
    queryKey: keys.gym(),
    queryFn: () => call(api.GET("/api/v1/schedule/gym-plan", { params: { query: { pageSize: 200 } } })),
  });
}

export function useFiles(params: { q?: string; kind?: S["FileKind"]; entityType?: string; entityId?: number; unattached?: boolean; page?: number }) {
  return useQuery({
    queryKey: keys.files(params),
    queryFn: () => call(api.GET("/api/v1/files", { params: { query: { ...params, pageSize: 60 } } })),
    placeholderData: keepPreviousData,
  });
}

export function useFileStats() {
  return useQuery({ queryKey: keys.fileStats, queryFn: () => call(api.GET("/api/v1/files/stats")) });
}

export function useTrash() {
  return useQuery({ queryKey: keys.trash, queryFn: () => call(api.GET("/api/v1/trash")) });
}

/** Mutation wrapper: toasts errors, invalidates the given keys on success. */
export function useAction<TArgs, TResult>(
  fn: (args: TArgs) => Promise<TResult>,
  options: { invalidate?: readonly (readonly unknown[])[]; success?: string | ((r: TResult) => string); onSuccess?: (r: TResult) => void } = {},
) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: fn,
    onSuccess: async (result) => {
      await Promise.all((options.invalidate ?? []).map((k) => qc.invalidateQueries({ queryKey: k })));
      if (options.success) toast.success(typeof options.success === "function" ? options.success(result) : options.success);
      options.onSuccess?.(result);
    },
    onError: (e) => toast.error(errorText(e)),
  });
}

// ---- DSA (M3) ----

export const dsaKeys = {
  all: ["dsa"] as const,
  topics: ["dsa", "topics"] as const,
  problems: (p: object) => ["dsa", "problems", p] as const,
  attempt: (id: number) => ["dsa", "attempt", id] as const,
  attempts: (p: object) => ["dsa", "attempts", p] as const,
  reviews: (bucket: string) => ["dsa", "reviews", bucket] as const,
  progress: ["dsa", "progress"] as const,
  weakest: ["dsa", "weakest"] as const,
  tags: ["dsa", "tags"] as const,
};

export function useDsaTopics() {
  return useQuery({
    queryKey: dsaKeys.topics,
    queryFn: () => call(api.GET("/api/v1/dsa/topics", { params: { query: { pageSize: 200, sort: "sortOrder" } } })),
  });
}

export function useDsaProblems(params: { topic?: number; status?: string; week?: number; q?: string } = {}) {
  return useQuery({
    queryKey: dsaKeys.problems(params),
    queryFn: () => {
      const query: Record<string, string | number> = { pageSize: 200, sort: "sortOrder" };
      Object.entries(params).forEach(([k, v]) => v !== undefined && v !== "" && (query[k] = v));
      return call(api.GET("/api/v1/dsa/problems", { params: { query } }));
    },
  });
}

export function useDsaAttempt(id: number | undefined) {
  return useQuery({
    queryKey: dsaKeys.attempt(id ?? 0),
    queryFn: () => call(api.GET("/api/v1/dsa/attempts/{id}", { params: { path: { id: id! } } })),
    enabled: Boolean(id),
  });
}

export function useDsaAttempts(params: { from?: string; to?: string; type?: S["AttemptType"]; counted?: boolean; tag?: string; problemId?: number; page?: number }) {
  return useQuery({
    queryKey: dsaKeys.attempts(params),
    queryFn: () => call(api.GET("/api/v1/dsa/attempts", { params: { query: { ...params, pageSize: 50, sort: "-date" } } })),
    placeholderData: keepPreviousData,
  });
}

export function useDsaReviews(bucket: NonNullable<S["ReviewBucket"]>) {
  return useQuery({
    queryKey: dsaKeys.reviews(bucket),
    queryFn: () => call(api.GET("/api/v1/dsa/reviews", { params: { query: { bucket } } })),
  });
}

export function useDsaProgress() {
  return useQuery({ queryKey: dsaKeys.progress, queryFn: () => call(api.GET("/api/v1/dsa/progress")) });
}

export function useDsaWeakest() {
  return useQuery({ queryKey: dsaKeys.weakest, queryFn: () => call(api.GET("/api/v1/dsa/weakest", { params: { query: { take: 20 } } })) });
}

export function useMistakeTags() {
  return useQuery({ queryKey: dsaKeys.tags, queryFn: () => call(api.GET("/api/v1/dsa/tags", { params: { query: { kind: "Mistake" } } })) });
}

// ---- DDIA and design (M4) ----

export const ddiaKeys = {
  all: ["ddia"] as const,
  summary: ["ddia", "summary"] as const,
  chapters: (edition: string) => ["ddia", "chapters", edition] as const,
  chapter: (id: number) => ["ddia", "chapter", id] as const,
  reading: (chapterId?: number) => ["ddia", "reading", chapterId ?? "all"] as const,
  note: (chapterId: number) => ["ddia", "note", chapterId] as const,
  drills: ["ddia", "drills"] as const,
  drill: (id: number) => ["ddia", "drill", id] as const,
  articulation: (kind?: string) => ["ddia", "articulation", kind ?? "all"] as const,
  prompts: ["ddia", "prompts"] as const,
  artifacts: (date: string) => ["ddia", "artifacts", date] as const,
};

export function useDdiaSummary() {
  return useQuery({ queryKey: ddiaKeys.summary, queryFn: () => call(api.GET("/api/v1/ddia/summary")) });
}

export function useDdiaChapters(edition: S["DdiaEdition"] | undefined) {
  return useQuery({
    queryKey: ddiaKeys.chapters(edition ?? ""),
    queryFn: () => call(api.GET("/api/v1/ddia/chapters", { params: { query: { edition, pageSize: 50, sort: "number" } } })),
    enabled: Boolean(edition),
  });
}

export function useDdiaChapter(id: number) {
  return useQuery({ queryKey: ddiaKeys.chapter(id), queryFn: () => call(api.GET("/api/v1/ddia/chapters/{id}", { params: { path: { id } } })) });
}

export function useReadingLog(chapterId?: number) {
  return useQuery({
    queryKey: ddiaKeys.reading(chapterId),
    queryFn: () => call(api.GET("/api/v1/ddia/reading-log", { params: { query: { chapterId, pageSize: 100 } } })),
  });
}

/** The chapter's note, or null when none exists yet. */
export function useDdiaNote(chapterId: number) {
  return useQuery({
    queryKey: ddiaKeys.note(chapterId),
    queryFn: async () => {
      const res = await api.GET("/api/v1/ddia/notes/by-chapter/{chapterId}", { params: { path: { chapterId } } });
      if (res.response.status === 404) return null;
      return (await call(Promise.resolve(res))) as unknown as S["DdiaNoteDto"];
    },
  });
}

export function useDrills() {
  return useQuery({ queryKey: ddiaKeys.drills, queryFn: () => call(api.GET("/api/v1/ddia/drills")) });
}

export function useDrill(id: number) {
  return useQuery({ queryKey: ddiaKeys.drill(id), queryFn: () => call(api.GET("/api/v1/ddia/drills/{id}", { params: { path: { id } } })) });
}

export function useArticulation(kind?: S["ArticulationKind"]) {
  return useQuery({
    queryKey: ddiaKeys.articulation(kind ?? undefined),
    queryFn: () => call(api.GET("/api/v1/ddia/articulation", { params: { query: { kind: kind ?? undefined, pageSize: 100 } } })),
  });
}

export function useArticulationPrompts() {
  return useQuery({
    queryKey: ddiaKeys.prompts,
    queryFn: () => call(api.GET("/api/v1/ddia/articulation-prompts", { params: { query: { pageSize: 100 } } })),
  });
}

export function useLinkableArtifacts(date: string, enabled: boolean) {
  return useQuery({
    queryKey: ddiaKeys.artifacts(date),
    queryFn: () => call(api.GET("/api/v1/ddia/artifacts", { params: { query: { date } } })),
    enabled,
  });
}

// ---- Plan, reviews, someday (M5) ----

export const m5Keys = {
  tasks: ["tasks"] as const,
  exits: (phase?: number) => ["exits", phase ?? "all"] as const,
  checkpoints: ["checkpoints"] as const,
  replans: ["replans"] as const,
  weekly: (weekStart: string) => ["weekly", weekStart] as const,
  weeklyPrefill: (weekStart: string) => ["weekly-prefill", weekStart] as const,
  weeklyList: ["weekly-list"] as const,
  monthly: (month: string) => ["monthly", month] as const,
  monthlyPrefill: (month: string) => ["monthly-prefill", month] as const,
  monthlyList: ["monthly-list"] as const,
  someday: (status?: string) => ["someday", status ?? "all"] as const,
  hours: (weekStart?: string) => ["hours", weekStart ?? "now"] as const,
};

export function useAllTasks() {
  return useQuery({
    queryKey: [...m5Keys.tasks, "all"],
    queryFn: () => call(api.GET("/api/v1/plan/tasks", { params: { query: { pageSize: 200, sort: "dayFrom" } } })),
  });
}

export function useExitCriteria(phase?: number) {
  return useQuery({ queryKey: m5Keys.exits(phase), queryFn: () => call(api.GET("/api/v1/plan/exit-criteria", { params: { query: { phase } } })) });
}

export function useCheckpoints() {
  return useQuery({ queryKey: m5Keys.checkpoints, queryFn: () => call(api.GET("/api/v1/plan/checkpoints")) });
}

export function useReplans() {
  return useQuery({ queryKey: m5Keys.replans, queryFn: () => call(api.GET("/api/v1/plan/replans", { params: { query: { pageSize: 100 } } })) });
}

export function useWeeklyReview(weekStart: string) {
  return useQuery({
    queryKey: m5Keys.weekly(weekStart),
    queryFn: async () => {
      const res = await api.GET("/api/v1/reviews/weekly/{weekStart}", { params: { path: { weekStart } } });
      if (res.response.status === 404) return null;
      return (await call(Promise.resolve(res))) as unknown as S["WeeklyReviewDto"];
    },
  });
}

export function useWeeklyPrefill(weekStart: string) {
  return useQuery({ queryKey: m5Keys.weeklyPrefill(weekStart), queryFn: () => call(api.GET("/api/v1/reviews/weekly/prefill", { params: { query: { weekStart } } })) });
}

export function useWeeklyList() {
  return useQuery({ queryKey: m5Keys.weeklyList, queryFn: () => call(api.GET("/api/v1/reviews/weekly")) });
}

export function useMonthlyReview(month: string) {
  return useQuery({
    queryKey: m5Keys.monthly(month),
    queryFn: async () => {
      const res = await api.GET("/api/v1/reviews/monthly/{month}", { params: { path: { month } } });
      if (res.response.status === 404) return null;
      return (await call(Promise.resolve(res))) as unknown as S["MonthlyReviewDto"];
    },
  });
}

export function useMonthlyPrefill(month: string) {
  return useQuery({ queryKey: m5Keys.monthlyPrefill(month), queryFn: () => call(api.GET("/api/v1/reviews/monthly/prefill", { params: { query: { month } } })) });
}

export function useMonthlyList() {
  return useQuery({ queryKey: m5Keys.monthlyList, queryFn: () => call(api.GET("/api/v1/reviews/monthly")) });
}

export function useSomeday(status?: S["SomedayStatus"]) {
  return useQuery({
    queryKey: m5Keys.someday(status ?? undefined),
    queryFn: () => call(api.GET("/api/v1/someday", { params: { query: { status: status ?? undefined, pageSize: 200 } } })),
  });
}

export function useWeeklyHours(weekStart?: string) {
  return useQuery({ queryKey: m5Keys.hours(weekStart), queryFn: () => call(api.GET("/api/v1/views/weekly-hours", { params: { query: { weekStart } } })) });
}
