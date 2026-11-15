import { useEffect, useState } from "react";
import type { S } from "@/api/client";
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { formatBytes } from "@/lib/upload";

/** Inline preview for the types the server allows inline: images (not SVG), PDF, audio, video, text and Markdown. */
export function FilePreviewDialog({ file, onClose }: { file: S["FileAssetDto"] | null; onClose: () => void }) {
  return (
    <Dialog open={file !== null} onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="max-h-[90vh] overflow-auto sm:max-w-4xl">
        {file && (
          <>
            <DialogHeader>
              <DialogTitle>{file.displayTitle}</DialogTitle>
              <DialogDescription>
                {file.originalName} · {formatBytes(file.sizeBytes)} · {file.contentType}
              </DialogDescription>
            </DialogHeader>
            <PreviewBody file={file} />
          </>
        )}
      </DialogContent>
    </Dialog>
  );
}

function PreviewBody({ file }: { file: S["FileAssetDto"] }) {
  const src = `/api/v1/files/${file.id}/content`;
  const type = file.contentType;
  if (type.startsWith("image/")) return <img src={src} alt={file.displayTitle} className="mx-auto max-h-[70vh] rounded-md" />;
  if (type.startsWith("video/")) return <video src={src} controls preload="metadata" className="w-full rounded-md" />;
  if (type.startsWith("audio/")) return <audio src={src} controls preload="metadata" className="w-full" />;
  if (type === "application/pdf") return <iframe src={src} title={file.displayTitle} className="h-[70vh] w-full rounded-md border" />;
  return <TextPreview src={src} />;
}

function TextPreview({ src }: { src: string }) {
  const [text, setText] = useState<string>("Loading...");
  useEffect(() => {
    let cancelled = false;
    fetch(src)
      .then((r) => r.text())
      .then((t) => !cancelled && setText(t.length > 200_000 ? `${t.slice(0, 200_000)}\n\n[truncated]` : t))
      .catch(() => !cancelled && setText("Could not load the file."));
    return () => {
      cancelled = true;
    };
  }, [src]);
  return <pre className="max-h-[70vh] overflow-auto whitespace-pre-wrap rounded-md bg-muted p-3 text-sm">{text}</pre>;
}
