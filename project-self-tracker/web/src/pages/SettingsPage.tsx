import { useQuery, useQueryClient } from "@tanstack/react-query";
import { Download, HardDriveDownload, ShieldCheck } from "lucide-react";
import { useState } from "react";
import { api, call, type S } from "@/api/client";
import { keys, useAction, useSettings } from "@/api/hooks";
import { ErrorBox, Field, Loading, PageHeader, selectClass } from "@/components/common";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Checkbox } from "@/components/ui/checkbox";
import { Input } from "@/components/ui/input";
import { prettyDate, timeZones } from "@/lib/format";
import { formatBytes } from "@/lib/upload";

export function SettingsPage() {
  const settings = useSettings();
  if (settings.isPending) return <Loading />;
  if (settings.isError) return <ErrorBox error={settings.error} />;
  return (
    <div className="grid gap-6">
      <PageHeader title="Settings & data" />
      <SettingsForm key={settings.data.rowVersion} s={settings.data} />
      <DdiaEdition s={settings.data} />
      <DataCard />
    </div>
  );
}

function SettingsForm({ s }: { s: S["SettingsDto"] }) {
  const qc = useQueryClient();
  const [f, setF] = useState({ ...s });
  const save = useAction(
    () =>
      call(
        api.PUT("/api/v1/settings", {
          body: {
            planStartDate: f.planStartDate,
            timeZone: f.timeZone,
            dsaUnaidedMinMinutes: f.dsaUnaidedMinMinutes,
            reviewIntervalsCsv: f.reviewIntervalsCsv,
            highConfidenceReviewDays: f.highConfidenceReviewDays,
            travelAnchorFriday: f.travelAnchorFriday || null,
            travelEnabled: f.travelEnabled,
            maxUploadMb: f.maxUploadMb,
            trashRetentionDays: f.trashRetentionDays,
            nightlyBackupEnabled: f.nightlyBackupEnabled,
            allowDangerousExtensions: f.allowDangerousExtensions,
            rowVersion: s.rowVersion,
          },
        }),
      ),
    {
      success: "Settings saved.",
      onSuccess: async () => {
        await qc.invalidateQueries();
      },
    },
  );
  const num = (k: keyof typeof f) => (e: React.ChangeEvent<HTMLInputElement>) => setF({ ...f, [k]: Number(e.target.value) });

  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base">Plan</CardTitle>
        <CardDescription>Changing the start date or pauses recomputes plan days and checkpoint dates; stored logs never change.</CardDescription>
      </CardHeader>
      <CardContent>
        <form
          className="grid gap-4 md:grid-cols-3"
          onSubmit={(e) => {
            e.preventDefault();
            save.mutate(undefined);
          }}
        >
          <Field label="Plan start date (Day 1)">{(id) => <Input id={id} type="date" value={f.planStartDate ?? ""} onChange={(e) => setF({ ...f, planStartDate: e.target.value })} />}</Field>
          <Field label="Time zone">
            {(id) => (
              <select id={id} className={selectClass} value={f.timeZone} onChange={(e) => setF({ ...f, timeZone: e.target.value })}>
                {timeZones().map((z) => <option key={z}>{z}</option>)}
              </select>
            )}
          </Field>
          <Field label="DSA unaided minimum (min)" hint="A counted attempt needs at least this much Pass 1 time.">
            {(id) => <Input id={id} type="number" min={5} max={120} value={f.dsaUnaidedMinMinutes} onChange={num("dsaUnaidedMinMinutes")} />}
          </Field>
          <Field label="Review intervals (days)" hint="Confidence 1-3 ladder, comma-separated.">
            {(id) => <Input id={id} value={f.reviewIntervalsCsv} onChange={(e) => setF({ ...f, reviewIntervalsCsv: e.target.value })} />}
          </Field>
          <Field label="High-confidence review after (days)">{(id) => <Input id={id} type="number" min={1} value={f.highConfidenceReviewDays} onChange={num("highConfidenceReviewDays")} />}</Field>
          <div className="grid gap-2">
            <label className="flex items-center gap-2 text-sm font-medium">
              <Checkbox checked={f.travelEnabled} onCheckedChange={(c) => setF({ ...f, travelEnabled: c === true })} /> Alternate-week travel
            </label>
            <Input type="date" aria-label="Travel anchor Friday" value={f.travelAnchorFriday ?? ""} onChange={(e) => setF({ ...f, travelAnchorFriday: e.target.value })} />
          </div>
          <Field label="Max upload size (MB)">{(id) => <Input id={id} type="number" min={1} value={f.maxUploadMb} onChange={num("maxUploadMb")} />}</Field>
          <Field label="Trash retention (days)">{(id) => <Input id={id} type="number" min={1} value={f.trashRetentionDays} onChange={num("trashRetentionDays")} />}</Field>
          <div className="grid content-start gap-2 text-sm">
            <label className="flex items-center gap-2">
              <Checkbox checked={f.nightlyBackupEnabled} onCheckedChange={(c) => setF({ ...f, nightlyBackupEnabled: c === true })} /> Automatic daily backup (M8)
            </label>
            <label className="flex items-center gap-2">
              <Checkbox checked={f.allowDangerousExtensions} onCheckedChange={(c) => setF({ ...f, allowDangerousExtensions: c === true })} /> Allow blocked file types (.exe, .ps1, ...)
            </label>
            {f.allowDangerousExtensions && <p className="text-xs text-primary">Only enable this if you need to keep a script; executables are never run by the app.</p>}
          </div>
          <div className="md:col-span-3">
            <p className="mb-3 text-xs text-muted-foreground">File folder: {s.fileRoot} (fixed after setup).</p>
            <Button type="submit" disabled={save.isPending}>Save settings</Button>
          </div>
        </form>
      </CardContent>
    </Card>
  );
}

function DdiaEdition({ s }: { s: S["SettingsDto"] }) {
  const confirm = useAction(
    (edition: S["DdiaEdition"]) => call(api.POST("/api/v1/settings/confirm-ddia-edition", { body: { edition } })),
    { invalidate: [keys.settings, ["today"]], success: "Edition confirmed." },
  );
  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2 text-base">
          DDIA edition <Badge variant={s.ddiaEdition === "Unconfirmed" ? "outline" : "secondary"}>{s.ddiaEdition}</Badge>
        </CardTitle>
        <CardDescription>
          Check the title/copyright page and ISBN. Chapter numbers differ between editions; the app never applies 2nd-edition numbers to a 1st-edition book.
          {s.ddiaEditionConfirmedOn && ` Confirmed ${prettyDate(s.ddiaEditionConfirmedOn)}.`}
        </CardDescription>
      </CardHeader>
      <CardContent className="flex gap-2">
        <Button variant="outline" onClick={() => confirm.mutate("Second")}>I own the 2nd edition (2026)</Button>
        <Button variant="outline" onClick={() => confirm.mutate("First")}>I own the 1st edition</Button>
      </CardContent>
    </Card>
  );
}

function DataCard() {
  const backups = useQuery({ queryKey: keys.backups, queryFn: () => call(api.GET("/api/v1/data/backups")) });
  const [report, setReport] = useState<S["VerifyReport"] | null>(null);
  const [inspect, setInspect] = useState<S["ImportSummary"] | null>(null);
  const exportNow = useAction(() => call(api.POST("/api/v1/data/export")), {
    invalidate: [keys.backups],
    success: (r) => `Backup written: ${r.fileName} (${formatBytes(r.sizeBytes)}, ${r.fileCount} files).`,
  });
  const verify = useAction(() => call(api.POST("/api/v1/files/maintenance/verify")), { onSuccess: setReport });
  const dryRun = useAction(
    async (file: File) => {
      const form = new FormData();
      form.append("file", file);
      const res = await fetch("/api/v1/data/import?dryRun=true", { method: "POST", body: form, headers: { "X-SelfTracker": "1" } });
      if (!res.ok) throw new Error(((await res.json()) as { title?: string }).title ?? "Import check failed");
      return (await res.json()) as S["ImportSummary"];
    },
    { onSuccess: setInspect },
  );

  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base">Data</CardTitle>
        <CardDescription>A full backup is one .zip with every table as JSON plus the files folder. Backups stay in data/backups on this laptop.</CardDescription>
      </CardHeader>
      <CardContent className="grid gap-4 text-sm">
        <div className="flex flex-wrap gap-2">
          <Button onClick={() => exportNow.mutate(undefined)} disabled={exportNow.isPending}><HardDriveDownload /> Back up now</Button>
          <Button variant="outline" onClick={() => verify.mutate(undefined)} disabled={verify.isPending}><ShieldCheck /> Verify files</Button>
          <label className="inline-flex cursor-pointer items-center gap-2 rounded-md border px-3 py-1.5 hover:bg-accent">
            Check a backup (dry run)
            <input type="file" accept=".zip" className="sr-only" onChange={(e) => e.target.files?.[0] && dryRun.mutate(e.target.files[0])} />
          </label>
        </div>
        {report && (
          <p className={report.issues.length ? "text-primary" : "text-muted-foreground"}>
            Checked {report.filesChecked} files: {report.missing} missing, {report.hashMismatches} changed on disk, {report.orphans} orphaned.
            {report.issues.slice(0, 5).map((i) => <span key={i.path} className="block text-xs">{i.kind}: {i.path}</span>)}
          </p>
        )}
        {inspect && (
          <p className="text-muted-foreground">
            Backup contains {Object.values(inspect.rowCounts).reduce((a, b) => a + b, 0)} rows and {inspect.fileCount} files; {inspect.hashMismatches} hash problems.
            {inspect.problems.map((p) => <span key={p} className="block text-xs">{p}</span>)}
          </p>
        )}
        <div className="grid gap-1">
          {backups.data?.map((b) => (
            <div key={b.fileName} className="flex items-center gap-3 rounded-md border px-3 py-1.5">
              <span className="font-mono text-xs">{b.fileName}</span>
              <span className="text-xs text-muted-foreground">{formatBytes(b.sizeBytes)}</span>
              <Button size="sm" variant="ghost" className="ml-auto" asChild>
                <a href={`/api/v1/data/backups/${encodeURIComponent(b.fileName)}`}><Download /> Download</a>
              </Button>
            </div>
          ))}
        </div>
        <p className="text-xs text-muted-foreground">Full restore into an empty database, CSV/Markdown exports and the automatic daily backup arrive with milestone M8.</p>
      </CardContent>
    </Card>
  );
}
