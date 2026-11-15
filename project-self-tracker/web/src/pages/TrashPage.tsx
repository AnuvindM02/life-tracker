import { useQueryClient } from "@tanstack/react-query";
import { RotateCcw, Trash2 } from "lucide-react";
import { api, call } from "@/api/client";
import { keys, useAction, useTrash } from "@/api/hooks";
import { ConfirmDialog, EmptyState, ErrorBox, Loading, PageHeader } from "@/components/common";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";

export function TrashPage() {
  const trash = useTrash();
  const qc = useQueryClient();
  const refresh = async () => {
    await qc.invalidateQueries();
  };
  const restore = useAction(
    (x: { entityType: string; id: number }) => call(api.POST("/api/v1/trash/{entityType}/{id}/restore", { params: { path: x } })),
    { success: "Restored.", onSuccess: () => void refresh() },
  );
  const purge = useAction(
    (x: { entityType: string; id: number }) => call(api.DELETE("/api/v1/trash/{entityType}/{id}", { params: { path: x } })),
    { invalidate: [keys.trash, keys.fileStats], success: "Deleted permanently." },
  );

  return (
    <div>
      <PageHeader title="Trash" subtitle="Deleted items can be restored until they are purged automatically after the retention period (Settings)." />
      {trash.isPending ? (
        <Loading />
      ) : trash.isError ? (
        <ErrorBox error={trash.error} />
      ) : trash.data.length === 0 ? (
        <EmptyState title="Trash is empty" />
      ) : (
        <ul className="grid gap-2">
          {trash.data.map((t) => (
            <li key={`${t.entityType}-${t.id}`} className="flex flex-wrap items-center gap-3 rounded-md border px-3 py-2 text-sm">
              <Badge variant="secondary">{t.entityType}</Badge>
              <span className="min-w-0 flex-1 truncate">{t.title}</span>
              <span className="text-xs text-muted-foreground">
                deleted {t.deletedAt ? new Date(t.deletedAt).toLocaleString() : ""}
                {t.purgeAfter && ` · purged after ${new Date(t.purgeAfter).toLocaleDateString()}`}
              </span>
              <Button size="sm" variant="outline" onClick={() => restore.mutate({ entityType: t.entityType, id: t.id })}>
                <RotateCcw /> Restore
              </Button>
              <ConfirmDialog
                trigger={<Button size="sm" variant="ghost" aria-label={`Delete ${t.title} permanently`}><Trash2 /></Button>}
                title="Delete permanently?"
                description={t.entityType === "FileAsset" ? "The file is removed from disk. This cannot be undone." : "This cannot be undone."}
                confirmLabel="Delete permanently"
                onConfirm={() => purge.mutate({ entityType: t.entityType, id: t.id })}
              />
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
