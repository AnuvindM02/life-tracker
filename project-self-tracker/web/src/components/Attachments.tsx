import { useQueryClient } from "@tanstack/react-query";
import { Download, Eye, FileIcon, Link2Off, Paperclip, Upload } from "lucide-react";
import { useRef, useState } from "react";
import { toast } from "sonner";
import { api, call, errorText, type S } from "@/api/client";
import { keys, useFiles } from "@/api/hooks";
import { Button } from "@/components/ui/button";
import { Progress } from "@/components/ui/progress";
import { formatBytes, uploadFiles } from "@/lib/upload";
import { cn } from "@/lib/utils";
import { FilePreviewDialog } from "./FilePreview";

export const UPLOAD_NOTICE =
  "Do not upload employer code, data, credentials or confidential architecture. The roadmap forbids moving company material into personal repositories and tools.";

/**
 * Attachments for one record: list, drag-and-drop upload with progress, duplicate handling, preview, unlink.
 * Without a target it uploads unattached files (used by the Files library).
 */
export function Attachments({
  entityType,
  entityId,
  label,
  onUploaded,
  compact,
}: {
  entityType?: string;
  entityId?: number;
  label?: string;
  onUploaded?: (files: S["FileAssetDto"][]) => void;
  compact?: boolean;
}) {
  const qc = useQueryClient();
  const input = useRef<HTMLInputElement>(null);
  const [progress, setProgress] = useState<number | null>(null);
  const [drag, setDrag] = useState(false);
  const [preview, setPreview] = useState<S["FileAssetDto"] | null>(null);
  const attached = useFiles({ entityType, entityId });
  const hasTarget = Boolean(entityType && entityId);

  async function handle(files: File[]) {
    if (files.length === 0) return;
    setProgress(0);
    try {
      const results = await uploadFiles(files, { entityType, entityId, label }, setProgress);
      const stored: S["FileAssetDto"][] = [];
      for (const r of results) {
        if (r.status === "Stored" && r.file) {
          stored.push(r.file);
          toast.success(`Stored ${r.originalName}`);
        } else if (r.status === "Duplicate" && r.file) {
          const existing = r.file;
          toast.info(r.message ?? "Already stored.", {
            action: hasTarget
              ? {
                  label: "Link existing",
                  onClick: async () => {
                    try {
                      await call(
                        api.POST("/api/v1/files/{id}/links", {
                          params: { path: { id: existing.id } },
                          body: { entityType: entityType!, entityId: entityId!, label: label ?? null },
                        }),
                      );
                      await qc.invalidateQueries({ queryKey: ["files"] });
                      onUploaded?.([existing]);
                    } catch (e) {
                      toast.error(errorText(e));
                    }
                  },
                }
              : undefined,
          });
          if (!hasTarget) stored.push(existing);
        } else {
          toast.error(`${r.originalName}: ${r.message ?? "rejected"}`);
        }
      }
      await qc.invalidateQueries({ queryKey: ["files"] });
      await qc.invalidateQueries({ queryKey: keys.fileStats });
      if (stored.length) onUploaded?.(stored);
    } catch (e) {
      toast.error(errorText(e));
    } finally {
      setProgress(null);
      if (input.current) input.current.value = "";
    }
  }

  async function unlink(file: S["FileAssetDto"]) {
    try {
      await call(
        api.DELETE("/api/v1/files/{id}/links", {
          params: { path: { id: file.id }, query: { entityType: entityType!, entityId: entityId! } },
        }),
      );
      await qc.invalidateQueries({ queryKey: ["files"] });
      toast.success("Removed from this record (the file stays in the library).");
    } catch (e) {
      toast.error(errorText(e));
    }
  }

  const items = hasTarget ? (attached.data?.items ?? []) : [];

  return (
    <div className="grid gap-2">
      <div
        onDragOver={(e) => {
          e.preventDefault();
          setDrag(true);
        }}
        onDragLeave={() => setDrag(false)}
        onDrop={(e) => {
          e.preventDefault();
          setDrag(false);
          void handle(Array.from(e.dataTransfer.files));
        }}
        className={cn(
          "flex flex-wrap items-center gap-3 rounded-md border border-dashed p-3 text-sm",
          drag && "border-primary bg-primary/5",
        )}
      >
        <Paperclip className="size-4 text-muted-foreground" aria-hidden />
        <span className="text-muted-foreground">{compact ? "Attach files" : "Drop files here or"}</span>
        <Button type="button" size="sm" variant="outline" onClick={() => input.current?.click()} disabled={progress !== null}>
          <Upload /> Choose files
        </Button>
        <input
          ref={input}
          type="file"
          multiple
          className="sr-only"
          aria-label="Choose files to upload"
          onChange={(e) => void handle(Array.from(e.target.files ?? []))}
        />
        {progress !== null && <Progress value={progress * 100} className="h-2 w-40" aria-label="Upload progress" />}
      </div>
      {!compact && <p className="text-xs text-muted-foreground">{UPLOAD_NOTICE}</p>}
      {items.length > 0 && (
        <ul className="grid gap-1">
          {items.map((f) => (
            <li key={f.id} className="flex items-center gap-2 rounded-md border px-2 py-1.5 text-sm">
              <FileIcon className="size-4 shrink-0 text-muted-foreground" aria-hidden />
              <span className="min-w-0 flex-1 truncate" title={f.originalName}>
                {f.displayTitle}
              </span>
              <span className="text-xs text-muted-foreground">{formatBytes(f.sizeBytes)}</span>
              {f.inlineAllowed && (
                <Button size="icon" variant="ghost" className="size-7" onClick={() => setPreview(f)} aria-label={`Preview ${f.displayTitle}`}>
                  <Eye />
                </Button>
              )}
              <Button size="icon" variant="ghost" className="size-7" asChild>
                <a href={`/api/v1/files/${f.id}/download`} aria-label={`Download ${f.displayTitle}`}>
                  <Download />
                </a>
              </Button>
              <Button size="icon" variant="ghost" className="size-7" onClick={() => void unlink(f)} aria-label={`Remove ${f.displayTitle} from this record`}>
                <Link2Off />
              </Button>
            </li>
          ))}
        </ul>
      )}
      <FilePreviewDialog file={preview} onClose={() => setPreview(null)} />
    </div>
  );
}
