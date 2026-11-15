import { GUARD_HEADER, type S } from "@/api/client";

export interface UploadTarget {
  entityType?: string;
  entityId?: number;
  label?: string;
}

/**
 * Multipart upload with progress. fetch() cannot report upload progress, so this uses XMLHttpRequest.
 * The server streams each file to disk, so large recordings never sit in memory.
 */
export function uploadFiles(
  files: File[],
  target: UploadTarget,
  onProgress: (fraction: number) => void,
): Promise<S["UploadItemResult"][]> {
  return new Promise((resolve, reject) => {
    const form = new FormData();
    files.forEach((f) => form.append("files", f, f.name));
    const query = new URLSearchParams();
    if (target.entityType && target.entityId) {
      query.set("entityType", target.entityType);
      query.set("entityId", String(target.entityId));
    }
    if (target.label) query.set("label", target.label);

    const xhr = new XMLHttpRequest();
    xhr.open("POST", `/api/v1/files${query.size ? `?${query}` : ""}`);
    Object.entries(GUARD_HEADER).forEach(([k, v]) => xhr.setRequestHeader(k, v));
    xhr.upload.onprogress = (e) => e.lengthComputable && onProgress(e.loaded / e.total);
    xhr.onload = () => {
      let body: unknown = null;
      try {
        body = JSON.parse(xhr.responseText);
      } catch {
        /* non-JSON error */
      }
      if (xhr.status >= 200 && xhr.status < 300) resolve(body as S["UploadItemResult"][]);
      else reject(new Error((body as { title?: string } | null)?.title ?? `Upload failed (${xhr.status})`));
    };
    xhr.onerror = () => reject(new Error("Upload failed: the API is not reachable."));
    xhr.send(form);
  });
}

export function formatBytes(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  const units = ["KB", "MB", "GB", "TB"];
  let v = bytes / 1024;
  let i = 0;
  while (v >= 1024 && i < units.length - 1) {
    v /= 1024;
    i++;
  }
  return `${v.toFixed(v < 10 ? 1 : 0)} ${units[i]}`;
}
