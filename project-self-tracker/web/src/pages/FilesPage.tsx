import { AlertTriangle, Download, Eye, FileArchive, FileAudio, FileCode, FileIcon, FileImage, FileText, FileVideo, Pencil, Trash2 } from "lucide-react";
import { useState } from "react";
import { useSearchParams } from "react-router";
import { toast } from "sonner";
import { api, call, type S } from "@/api/client";
import { keys, useAction, useFiles, useFileStats } from "@/api/hooks";
import { Attachments } from "@/components/Attachments";
import { ConfirmDialog, EmptyState, ErrorBox, Field, Loading, PageHeader, selectClass } from "@/components/common";
import { FilePreviewDialog } from "@/components/FilePreview";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Checkbox } from "@/components/ui/checkbox";
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { formatBytes } from "@/lib/upload";
import { cn } from "@/lib/utils";

const KIND_ICON: Record<S["FileKind"], typeof FileIcon> = {
  Document: FileText,
  Image: FileImage,
  Audio: FileAudio,
  Video: FileVideo,
  Code: FileCode,
  Archive: FileArchive,
  Other: FileIcon,
};

export function FilesPage() {
  const [search] = useSearchParams();
  const [q, setQ] = useState("");
  const [kind, setKind] = useState<S["FileKind"] | "">("");
  const [unattached, setUnattached] = useState(false);
  const [page, setPage] = useState(1);
  const files = useFiles({ q: q || undefined, kind: kind || undefined, unattached: unattached || undefined, page });
  const stats = useFileStats();
  const [preview, setPreview] = useState<S["FileAssetDto"] | null>(null);
  const [editing, setEditing] = useState<S["FileAssetDto"] | null>(null);
  const del = useAction((id: number) => call(api.DELETE("/api/v1/files/{id}", { params: { path: { id } } })), {
    invalidate: [["files"], keys.fileStats, keys.trash],
    onSuccess: () => toast.success("File moved to Trash. It is removed from disk only when the Trash is purged."),
  });

  return (
    <div className="grid gap-6">
      <PageHeader title="Files" subtitle="Notes, diagrams, design docs, reports, resumes and recordings. Stored on this laptop only." />
      <div className="grid gap-6 lg:grid-cols-3">
        <Card className="lg:col-span-2">
          <CardHeader>
            <CardTitle className="text-base">Upload</CardTitle>
          </CardHeader>
          <CardContent>
            <Attachments key={search.get("upload") ?? "u"} />
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Storage</CardTitle>
          </CardHeader>
          <CardContent className="grid gap-1 text-sm">
            {stats.data && (
              <>
                <p>
                  <span className="font-medium">{stats.data.totalFiles}</span> files · {formatBytes(stats.data.totalBytes)}
                </p>
                <p className="text-muted-foreground">{stats.data.unattachedCount} unattached</p>
                <p className="text-muted-foreground">{stats.data.byKind.map((k) => `${k.kind} ${k.count}`).join(" · ")}</p>
                {stats.data.freeDiskBytes != null && <p className="text-muted-foreground">{formatBytes(stats.data.freeDiskBytes)} free on disk</p>}
                {stats.data.lowDiskWarning && (
                  <p className="flex items-center gap-1 text-primary"><AlertTriangle className="size-4" /> Less than 5 GB free.</p>
                )}
                <p className="truncate text-xs text-muted-foreground" title={stats.data.fileRoot}>{stats.data.fileRoot}</p>
              </>
            )}
          </CardContent>
        </Card>
      </div>

      <div className="flex flex-wrap items-center gap-3">
        <Input className="w-64" placeholder="Search title, name, tags" value={q} onChange={(e) => { setQ(e.target.value); setPage(1); }} aria-label="Search files" />
        <select className={cn(selectClass, "w-40")} value={kind} onChange={(e) => { setKind(e.target.value as S["FileKind"]); setPage(1); }} aria-label="Kind">
          <option value="">All kinds</option>
          {Object.keys(KIND_ICON).map((k) => <option key={k}>{k}</option>)}
        </select>
        <label className="flex items-center gap-2 text-sm">
          <Checkbox checked={unattached} onCheckedChange={(c) => setUnattached(c === true)} /> Unattached only
        </label>
      </div>

      {files.isPending ? (
        <Loading />
      ) : files.isError ? (
        <ErrorBox error={files.error} />
      ) : files.data.items.length === 0 ? (
        <EmptyState title="No files yet" />
      ) : (
        <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
          {files.data.items.map((f) => {
            const Icon = KIND_ICON[f.kind];
            return (
              <Card key={f.id} className="gap-2 py-4">
                <CardContent className="grid gap-2 px-4">
                  <div className="flex items-start gap-2">
                    <Icon className="mt-0.5 size-5 shrink-0 text-muted-foreground" aria-hidden />
                    <div className="min-w-0 flex-1">
                      <p className="truncate font-medium" title={f.displayTitle}>{f.displayTitle}</p>
                      <p className="truncate text-xs text-muted-foreground" title={f.originalName}>
                        {f.originalName} · {formatBytes(f.sizeBytes)} · {new Date(f.uploadedAt).toLocaleDateString()}
                      </p>
                    </div>
                  </div>
                  <div className="flex flex-wrap gap-1">
                    {f.links.length === 0 ? <Badge variant="outline">Unattached</Badge> : f.links.map((l) => <Badge key={l.id} variant="secondary">{l.entityType} #{l.entityId}</Badge>)}
                    {f.tags?.split(",").map((t) => <Badge key={t} variant="outline">#{t}</Badge>)}
                  </div>
                  <div className="flex gap-1">
                    {f.inlineAllowed && <Button size="sm" variant="ghost" onClick={() => setPreview(f)}><Eye /> Preview</Button>}
                    <Button size="sm" variant="ghost" asChild><a href={`/api/v1/files/${f.id}/download`}><Download /> Download</a></Button>
                    <Button size="icon" variant="ghost" className="ml-auto" onClick={() => setEditing(f)} aria-label={`Rename or retag ${f.displayTitle}`}><Pencil /></Button>
                    <ConfirmDialog
                      trigger={<Button size="icon" variant="ghost" aria-label={`Delete ${f.displayTitle}`}><Trash2 /></Button>}
                      title={`Delete "${f.displayTitle}"?`}
                      description="It moves to the Trash and is removed from every record it is attached to after it is purged."
                      onConfirm={() => del.mutate(f.id)}
                    />
                  </div>
                </CardContent>
              </Card>
            );
          })}
        </div>
      )}
      {files.data && files.data.total > files.data.pageSize && (
        <div className="flex items-center gap-2 text-sm">
          <Button size="sm" variant="outline" disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>Previous</Button>
          <span>Page {page} of {Math.ceil(files.data.total / files.data.pageSize)}</span>
          <Button size="sm" variant="outline" disabled={page * files.data.pageSize >= files.data.total} onClick={() => setPage((p) => p + 1)}>Next</Button>
        </div>
      )}
      <FilePreviewDialog file={preview} onClose={() => setPreview(null)} />
      <EditFileDialog file={editing} onClose={() => setEditing(null)} />
    </div>
  );
}

function EditFileDialog({ file, onClose }: { file: S["FileAssetDto"] | null; onClose: () => void }) {
  const [loadedFor, setLoadedFor] = useState<unknown>(null);
  const [title, setTitle] = useState("");
  const [desc, setDesc] = useState("");
  const [tags, setTags] = useState("");
  if (file !== loadedFor) {
    setLoadedFor(file);
    setTitle(file?.displayTitle ?? "");
    setDesc(file?.description ?? "");
    setTags(file?.tags ?? "");
  }
  const save = useAction(
    () => call(api.PATCH("/api/v1/files/{id}", { params: { path: { id: file!.id } }, body: { displayTitle: title, description: desc || null, tags: tags || null, rowVersion: file!.rowVersion } })),
    { invalidate: [["files"]], success: "Saved.", onSuccess: onClose },
  );
  return (
    <Dialog open={file !== null} onOpenChange={(o) => !o && onClose()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Edit file details</DialogTitle>
        </DialogHeader>
        <div className="grid gap-3">
          <Field label="Title">{(id) => <Input id={id} value={title} onChange={(e) => setTitle(e.target.value)} />}</Field>
          <Field label="Description">{(id) => <Textarea id={id} value={desc} onChange={(e) => setDesc(e.target.value)} />}</Field>
          <Field label="Tags" hint="Comma-separated.">{(id) => <Input id={id} value={tags} onChange={(e) => setTags(e.target.value)} />}</Field>
        </div>
        <DialogFooter>
          <Button disabled={!title || save.isPending} onClick={() => save.mutate(undefined)}>Save</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
