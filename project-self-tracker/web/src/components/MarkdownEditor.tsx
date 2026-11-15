import { useState } from "react";
import ReactMarkdown from "react-markdown";
import { Textarea } from "@/components/ui/textarea";
import { cn } from "@/lib/utils";

/** Textarea with a Write/Preview toggle. Markdown is rendered without raw HTML (react-markdown default). */
export function MarkdownEditor({
  id,
  value,
  onChange,
  placeholder,
  rows = 6,
}: {
  id?: string;
  value: string;
  onChange: (v: string) => void;
  placeholder?: string;
  rows?: number;
}) {
  const [preview, setPreview] = useState(false);
  return (
    <div className="grid gap-1">
      <div className="flex gap-1 text-xs" role="tablist" aria-label="Editor mode">
        {(["Write", "Preview"] as const).map((m) => (
          <button
            key={m}
            type="button"
            role="tab"
            aria-selected={(m === "Preview") === preview}
            onClick={() => setPreview(m === "Preview")}
            className={cn(
              "rounded px-2 py-0.5",
              (m === "Preview") === preview ? "bg-secondary text-secondary-foreground" : "text-muted-foreground hover:text-foreground",
            )}
          >
            {m}
          </button>
        ))}
      </div>
      {preview ? (
        <div className="prose-sm min-h-24 rounded-md border p-3 text-sm [&_a]:text-primary [&_a]:underline [&_code]:rounded [&_code]:bg-muted [&_code]:px-1 [&_h1]:text-lg [&_h1]:font-semibold [&_h2]:font-semibold [&_li]:ml-4 [&_ol]:list-decimal [&_p]:my-1 [&_ul]:list-disc">
          {value.trim() ? <ReactMarkdown>{value}</ReactMarkdown> : <span className="text-muted-foreground">Nothing to preview.</span>}
        </div>
      ) : (
        <Textarea id={id} value={value} onChange={(e) => onChange(e.target.value)} placeholder={placeholder} rows={rows} />
      )}
    </div>
  );
}
