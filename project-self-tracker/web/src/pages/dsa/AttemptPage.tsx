import { zodResolver } from "@hookform/resolvers/zod";
import { CalendarClock, CheckCircle2, Info, Trash2 } from "lucide-react";
import { useMemo } from "react";
import { Controller, useForm, useWatch } from "react-hook-form";
import { Link, useNavigate, useParams, useSearchParams } from "react-router";
import { toast } from "sonner";
import { z } from "zod";
import { api, call, errorText, type S } from "@/api/client";
import { dsaKeys, useAction, useDsaAttempt, useDsaProblems, useMistakeTags, useSettings } from "@/api/hooks";
import { Attachments } from "@/components/Attachments";
import { ConfirmDialog, ErrorBox, Field, Loading, PageHeader, RatingInput, selectClass } from "@/components/common";
import { MarkdownEditor } from "@/components/MarkdownEditor";
import { Stopwatch } from "@/components/Stopwatch";
import { TagInput } from "@/components/TagInput";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Checkbox } from "@/components/ui/checkbox";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { CONFIDENCE_HINT, countPreview } from "@/lib/dsa";
import { isoDate, prettyDate } from "@/lib/format";
import { cn } from "@/lib/utils";
import { attemptTypeLabel, DsaNav, PASSES } from "./DsaNav";

const schema = z
  .object({
    date: z.string().min(10),
    problemId: z.number().int().nullable(),
    titleFreeText: z.string().max(200),
    type: z.enum(["New", "ColdReview", "Timed"]),
    pass1Min: z.number().int().min(0).max(600),
    hintLevel: z.enum(["None", "Topic", "Pattern", "Solution"]),
    pass2Min: z.number().int().min(0).max(600),
    pass3Min: z.number().int().min(0).max(600),
    solvedUnaided: z.boolean(),
    pattern: z.string().max(200),
    keyInsight: z.string().max(2000),
    whatIMissed: z.string().max(2000),
    timeComplexity: z.string().max(60),
    spaceComplexity: z.string().max(60),
    confidence: z.number().int().min(1, "Rate your confidence 1-5.").max(5),
    codeMd: z.string(),
    company: z.string().max(120),
    mistakeTags: z.array(z.string()),
  })
  .refine((v) => v.problemId !== null || v.titleFreeText.trim().length > 0, { message: "Choose a problem or type a title.", path: ["titleFreeText"] });
export type AttemptForm = z.infer<typeof schema>;

export function toAttemptBody(f: AttemptForm, reviewItemId: number | null, rowVersion: string | null): S["DsaAttemptInput"] {
  const t = (s: string) => (s.trim() ? s.trim() : null);
  return {
    date: f.date,
    problemId: f.problemId,
    titleFreeText: f.problemId ? null : t(f.titleFreeText),
    type: f.type,
    pass1Min: f.pass1Min,
    hintLevel: f.hintLevel,
    pass2Min: f.pass2Min,
    pass3Min: f.pass3Min,
    totalMin: null,
    solvedUnaided: f.solvedUnaided,
    pattern: t(f.pattern),
    keyInsight: t(f.keyInsight),
    whatIMissed: t(f.whatIMissed),
    timeComplexity: t(f.timeComplexity),
    spaceComplexity: t(f.spaceComplexity),
    confidence: f.confidence,
    codeMd: t(f.codeMd),
    company: t(f.company),
    mistakeTags: f.mistakeTags,
    reviewItemId,
    rowVersion,
  };
}

export function AttemptPage() {
  const { id } = useParams();
  const attemptId = id ? Number(id) : undefined;
  const attempt = useDsaAttempt(attemptId);
  if (attemptId && attempt.isPending) return <Loading />;
  if (attempt.isError) return <ErrorBox error={attempt.error} />;
  return <AttemptEditor key={attemptId ?? "new"} existing={attempt.data ?? null} />;
}

function AttemptEditor({ existing }: { existing: S["DsaAttemptDto"] | null }) {
  const [search] = useSearchParams();
  const navigate = useNavigate();
  const settings = useSettings();
  const problems = useDsaProblems();
  const tags = useMistakeTags();
  const reviewItemId = search.get("reviewItemId") ? Number(search.get("reviewItemId")) : null;

  const defaults: AttemptForm = useMemo(
    () => ({
      date: existing?.date ?? search.get("date") ?? isoDate(),
      problemId: existing?.problemId ?? (search.get("problemId") ? Number(search.get("problemId")) : null),
      titleFreeText: existing?.titleFreeText ?? "",
      type: existing?.type ?? ((search.get("type") as S["AttemptType"]) || "New"),
      pass1Min: existing?.pass1Min ?? 0,
      hintLevel: existing?.hintLevel ?? "None",
      pass2Min: existing?.pass2Min ?? 0,
      pass3Min: existing?.pass3Min ?? 0,
      solvedUnaided: existing?.solvedUnaided ?? false,
      pattern: existing?.pattern ?? "",
      keyInsight: existing?.keyInsight ?? "",
      whatIMissed: existing?.whatIMissed ?? "",
      timeComplexity: existing?.timeComplexity ?? "",
      spaceComplexity: existing?.spaceComplexity ?? "",
      confidence: existing?.confidence ?? 0,
      codeMd: existing?.codeMd ?? "",
      company: existing?.company ?? "",
      mistakeTags: existing?.mistakeTags ? [...existing.mistakeTags] : [],
    }),
    [existing, search],
  );
  const form = useForm<AttemptForm>({ resolver: zodResolver(schema), defaultValues: defaults });
  const { register, control, handleSubmit, setValue, formState } = form;
  const v = useWatch({ control });
  const preview = countPreview({
    type: v.type ?? "New",
    pass1Min: v.pass1Min ?? 0,
    whatIMissed: v.whatIMissed ?? "",
    hasProblem: Boolean(v.problemId),
    unaidedMin: settings.data?.dsaUnaidedMinMinutes ?? 25,
  });

  const save = useAction(
    (f: AttemptForm) =>
      existing
        ? call(api.PUT("/api/v1/dsa/attempts/{id}", { params: { path: { id: existing.id } }, body: toAttemptBody(f, existing.reviewItemId, existing.rowVersion) }))
        : call(api.POST("/api/v1/dsa/attempts", { body: toAttemptBody(f, reviewItemId, null) })),
    {
      invalidate: [dsaKeys.all, ["today"], ["log"], ["logs"], ["heatmap"]],
      onSuccess: (saved) => {
        const pending = saved.reviews.filter((r) => r.status === "Pending");
        toast.success(
          pending.length ? `Saved. Cold reviews: ${pending.map((r) => prettyDate(r.dueDate, { day: "numeric", month: "short" })).join(", ")}.` : "Saved.",
        );
        if (!existing) navigate(`/dsa/attempts/${saved.id}`, { replace: true });
      },
    },
  );
  const remove = useAction(() => call(api.DELETE("/api/v1/dsa/attempts/{id}", { params: { path: { id: existing!.id } } })), {
    invalidate: [dsaKeys.all, ["today"], ["log"]],
    success: "Attempt moved to Trash; its time entry and pending reviews were removed.",
    onSuccess: () => navigate("/dsa/attempts"),
  });

  const grouped = useMemo(() => {
    const map = new Map<string, S["DsaProblemDto"][]>();
    (problems.data?.items ?? []).forEach((p) => map.set(p.topicName, [...(map.get(p.topicName) ?? []), p]));
    return [...map.entries()];
  }, [problems.data]);

  const locked = Boolean(existing);
  const totalMin = (v.pass1Min ?? 0) + (v.pass2Min ?? 0) + (v.pass3Min ?? 0);
  const storage = existing ? `attempt-${existing.id}` : `attempt-new-${v.problemId ?? "free"}`;

  return (
    <div>
      <DsaNav />
      <PageHeader
        title={existing ? `${attemptTypeLabel[existing.type]}: ${existing.title}` : "Log a DSA attempt"}
        subtitle={existing ? `${prettyDate(existing.date)}${existing.topicName ? ` · ${existing.topicName}` : ""}` : "Attempt unaided first. Watching a solution, reading an editorial or copying code does not count."}
        actions={
          existing && (
            <ConfirmDialog
              trigger={<Button variant="outline" size="icon" aria-label="Delete attempt"><Trash2 /></Button>}
              title="Delete this attempt?"
              description="It moves to the Trash. Its derived time entry and pending review dates are removed; restoring brings the time entry back."
              onConfirm={() => remove.mutate(undefined)}
            />
          )
        }
      />
      <form
        className="grid gap-6 lg:grid-cols-3"
        onSubmit={handleSubmit(
          (f) => save.mutate(f),
          () => toast.error("Check the highlighted fields."),
        )}
      >
        <div className="grid content-start gap-6 lg:col-span-2">
          <Card>
            <CardHeader>
              <CardTitle className="text-base">Problem</CardTitle>
            </CardHeader>
            <CardContent className="grid gap-4 sm:grid-cols-2">
              <Field label="From the library" hint={locked ? "Fixed after logging (it owns the review dates)." : undefined}>
                {(id) => (
                  <Controller
                    control={control}
                    name="problemId"
                    render={({ field }) => (
                      <select id={id} className={selectClass} disabled={locked} value={field.value ?? ""} onChange={(e) => field.onChange(e.target.value ? Number(e.target.value) : null)}>
                        <option value="">Not in the library (type a title)</option>
                        {grouped.map(([topic, list]) => (
                          <optgroup key={topic} label={topic}>
                            {list.map((p) => (
                              <option key={p.id} value={p.id}>
                                {p.planWeek ? `W${p.planWeek} · ` : ""}
                                {p.title}
                              </option>
                            ))}
                          </optgroup>
                        ))}
                      </select>
                    )}
                  />
                )}
              </Field>
              {!v.problemId && (
                <Field label="Title" error={formState.errors.titleFreeText?.message} hint="Free-text attempts get no review dates until the problem is in the library.">
                  {(id) => <Input id={id} disabled={locked} {...register("titleFreeText")} />}
                </Field>
              )}
              <Field label="Date">{(id) => <Input id={id} type="date" max={isoDate()} {...register("date")} />}</Field>
              <Controller
                control={control}
                name="type"
                render={({ field }) => (
                  <div role="radiogroup" aria-label="Attempt type" className="flex flex-wrap items-end gap-2">
                    {(["New", "ColdReview", "Timed"] as const).map((t) => (
                      <button
                        key={t}
                        type="button"
                        role="radio"
                        disabled={locked}
                        aria-checked={field.value === t}
                        onClick={() => field.onChange(t)}
                        className={cn("rounded-md border px-3 py-1.5 text-sm disabled:opacity-60", field.value === t ? "border-primary bg-primary text-primary-foreground" : "hover:bg-accent")}
                      >
                        {attemptTypeLabel[t]}
                      </button>
                    ))}
                  </div>
                )}
              />
              {reviewItemId && !existing && (
                <p className="flex items-center gap-2 text-sm text-primary sm:col-span-2">
                  <CalendarClock className="size-4" /> This cold review completes a scheduled review.
                </p>
              )}
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle className="text-base">Four passes</CardTitle>
              <CardDescription>Stopping a timer writes its minutes into the pass. Timers survive a page refresh.</CardDescription>
            </CardHeader>
            <CardContent className="grid gap-4">
              {PASSES.map((p) => (
                <div key={p.key} className="grid gap-2 rounded-md border p-3 sm:grid-cols-[1fr_auto_6rem] sm:items-center">
                  <div>
                    <p className="text-sm font-medium">{p.label}</p>
                    <p className="text-xs text-muted-foreground">{p.guide}</p>
                  </div>
                  <Stopwatch storageKey={`${storage}-${p.key}`} label={`${p.label} timer`} onStop={(m) => setValue(p.key, (v[p.key] ?? 0) + m, { shouldDirty: true })} />
                  <Input type="number" min={0} aria-label={`${p.label} minutes`} {...register(p.key, { valueAsNumber: true })} />
                </div>
              ))}
              <div className="grid gap-3 sm:grid-cols-3">
                <Field label="Hint used">
                  {(id) => (
                    <select id={id} className={selectClass} {...register("hintLevel")}>
                      <option value="None">None</option>
                      <option value="Topic">Topic</option>
                      <option value="Pattern">Pattern</option>
                      <option value="Solution">Read the solution</option>
                    </select>
                  )}
                </Field>
                <label className="flex items-center gap-2 self-end pb-2 text-sm">
                  <Controller control={control} name="solvedUnaided" render={({ field }) => <Checkbox checked={field.value} onCheckedChange={(c) => field.onChange(c === true)} />} />
                  Solved unaided
                </label>
                <p className="self-end pb-2 text-sm text-muted-foreground">Total {totalMin} min (plus about 2 min to log)</p>
              </div>
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle className="text-base">Pass 4: Log</CardTitle>
              <CardDescription>Pattern, key insight, what you missed, time and confidence.</CardDescription>
            </CardHeader>
            <CardContent className="grid gap-4">
              <div className="grid gap-4 sm:grid-cols-3">
                <Field label="Pattern">{(id) => <Input id={id} placeholder="e.g. hashing, monotonic stack" {...register("pattern")} />}</Field>
                <Field label="Time complexity">{(id) => <Input id={id} placeholder="O(n)" {...register("timeComplexity")} />}</Field>
                <Field label="Space complexity">{(id) => <Input id={id} placeholder="O(n)" {...register("spaceComplexity")} />}</Field>
              </div>
              <Field label="Key insight">{(id) => <Textarea id={id} rows={2} {...register("keyInsight")} />}</Field>
              <Field label="What I missed (mistake-log line)" hint="Required for a counted new attempt.">
                {(id) => <Textarea id={id} rows={2} {...register("whatIMissed")} />}
              </Field>
              <Field label="Mistake tags">
                {(id) => (
                  <Controller
                    control={control}
                    name="mistakeTags"
                    render={({ field }) => <TagInput id={id} value={field.value} onChange={field.onChange} suggestions={tags.data?.map((t) => t.name)} />}
                  />
                )}
              </Field>
              <Field label="Confidence (1-5)" hint={CONFIDENCE_HINT} error={formState.errors.confidence?.message}>
                {(id) => <Controller control={control} name="confidence" render={({ field }) => <RatingInput id={id} label="Confidence" value={field.value || null} onChange={(n) => field.onChange(n ?? 0)} />} />}
              </Field>
              <Field label="C# solution (Markdown code block)">
                {(id) => <Controller control={control} name="codeMd" render={({ field }) => <MarkdownEditor id={id} value={field.value} onChange={field.onChange} rows={8} placeholder={"```csharp\npublic int[] TwoSum(int[] nums, int target) { ... }\n```"} />} />}
              </Field>
              <Field label="Company tag (optional, for genuine target roles)">{(id) => <Input id={id} {...register("company")} />}</Field>
            </CardContent>
          </Card>
        </div>

        <div className="grid content-start gap-6">
          <Card className={cn(preview.counted ? "border-success/50" : "border-primary/40")}>
            <CardHeader>
              <CardTitle className="flex items-center gap-2 text-base">
                {preview.counted ? <CheckCircle2 className="size-4 text-success" /> : <Info className="size-4 text-primary" />}
                {preview.counted ? "Counts toward the marker" : "Saved, not counted"}
              </CardTitle>
              <CardDescription>{preview.reason ?? "A meaningful attempt also earns today's DSA tick."}</CardDescription>
            </CardHeader>
            <CardContent className="grid gap-2">
              <Button type="submit" disabled={save.isPending}>{save.isPending ? "Saving..." : existing ? "Save changes" : "Log attempt"}</Button>
              {existing && !existing.isCounted && existing.notCountedReason && <p className="text-xs text-muted-foreground">Server: {existing.notCountedReason}</p>}
            </CardContent>
          </Card>

          {existing && (
            <Card>
              <CardHeader>
                <CardTitle className="text-base">Review dates</CardTitle>
                <CardDescription>{existing.reviews.length ? CONFIDENCE_HINT : existing.problemId ? "No reviews from this attempt." : "Add the problem to the library to schedule reviews."}</CardDescription>
              </CardHeader>
              <CardContent className="grid gap-1 text-sm">
                {existing.reviews.map((r) => (
                  <div key={r.id} className="flex items-center gap-2">
                    <span className="w-28">{prettyDate(r.dueDate, { day: "numeric", month: "short", year: "numeric" })}</span>
                    <span className="text-muted-foreground">+{r.intervalDays} d</span>
                    <Badge variant={r.status === "Pending" ? "secondary" : "outline"} className="ml-auto">{r.status}</Badge>
                  </div>
                ))}
                <Link to="/dsa/reviews" className="mt-2 text-primary underline">Review queue</Link>
              </CardContent>
            </Card>
          )}

          {existing && (
            <Card>
              <CardHeader>
                <CardTitle className="text-base">Attachments</CardTitle>
              </CardHeader>
              <CardContent>
                <Attachments entityType="DsaAttempt" entityId={existing.id} label="DSA attempt" />
              </CardContent>
            </Card>
          )}
        </div>
      </form>
      {save.isError && <p className="mt-4 text-sm text-destructive">{errorText(save.error)}</p>}
    </div>
  );
}
