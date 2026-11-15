import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import type { ReactNode } from "react";
import { MemoryRouter } from "react-router";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { Attachments } from "@/components/Attachments";
import { Heatmap } from "@/components/Heatmap";
import { Stopwatch } from "@/components/Stopwatch";
import { NudgeCard } from "@/pages/TodayPage";
import * as upload from "@/lib/upload";

function wrap(ui: ReactNode) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={qc}>
      <MemoryRouter>{ui}</MemoryRouter>
    </QueryClientProvider>,
  );
}

const emptyPage = { items: [], total: 0, page: 1, pageSize: 60 };

describe("NudgeCard", () => {
  it("is calm: no debt or catch-up wording, offers one action", () => {
    wrap(<NudgeCard nudge={{ key: "missed-two", title: "Never miss twice", message: "Do an MVD now and ramp back over 2-3 days. Nothing needs to be repaid.", actionLabel: "Log an MVD", actionLink: "/logs/today?type=Mvd" }} />);
    expect(screen.getByRole("status")).toHaveTextContent("Never miss twice");
    expect(screen.getByRole("button", { name: "Log an MVD" })).toBeInTheDocument();
    expect(document.body.textContent?.toLowerCase()).not.toMatch(/owed|catch up|make up hours|debt/);
  });
});

describe("Attachments", () => {
  beforeEach(() => {
    vi.spyOn(globalThis, "fetch").mockResolvedValue(new Response(JSON.stringify(emptyPage), { status: 200, headers: { "Content-Type": "application/json" } }));
  });
  afterEach(() => vi.restoreAllMocks());

  it("shows the employer-material notice and uploads to the record", async () => {
    const spy = vi.spyOn(upload, "uploadFiles").mockResolvedValue([
      { originalName: "notes.md", status: "Stored", message: null, file: { id: 7 } as never },
    ]);
    const onUploaded = vi.fn();
    wrap(<Attachments entityType="DailyLog" entityId={3} onUploaded={onUploaded} />);
    expect(screen.getByText(/Do not upload employer code/)).toBeInTheDocument();

    const input = screen.getByLabelText("Choose files to upload");
    await userEvent.upload(input, new File(["# hi"], "notes.md", { type: "text/markdown" }));
    await waitFor(() => expect(spy).toHaveBeenCalledOnce());
    expect(spy.mock.calls[0][1]).toEqual({ entityType: "DailyLog", entityId: 3, label: undefined });
    await waitFor(() => expect(onUploaded).toHaveBeenCalledWith([{ id: 7 }]));
  });

  it("does not treat a rejected file as uploaded", async () => {
    vi.spyOn(upload, "uploadFiles").mockResolvedValue([{ originalName: "run.ps1", status: "Rejected", message: "blocked", file: null }]);
    const onUploaded = vi.fn();
    wrap(<Attachments onUploaded={onUploaded} />);
    await userEvent.upload(screen.getByLabelText("Choose files to upload"), new File(["x"], "run.ps1"));
    await waitFor(() => expect(upload.uploadFiles).toHaveBeenCalled());
    expect(onUploaded).not.toHaveBeenCalled();
  });
});

describe("Heatmap", () => {
  it("never relies on colour alone: every logged state carries a letter", () => {
    wrap(
      <Heatmap
        cells={[
          { date: "2026-11-02", state: "Full", planDay: 1, isTravel: false },
          { date: "2026-11-03", state: "Mvd", planDay: 2, isTravel: false },
          { date: "2026-11-04", state: "Missed", planDay: 3, isTravel: false },
          { date: "2026-11-05", state: "Paused", planDay: 3, isTravel: false },
        ]}
      />,
    );
    expect(screen.getByLabelText(/Mon, 2 Nov 2026: Full|2 Nov.*Full/)).toHaveTextContent("F");
    expect(screen.getByRole("gridcell", { name: /MVD/ })).toHaveTextContent("M");
    expect(screen.getByRole("gridcell", { name: /Paused/ })).toHaveTextContent("P");
    fireEvent.click(screen.getByText("Show as table"));
    expect(screen.getByRole("table")).toHaveTextContent("Not logged");
  });
});

describe("Stopwatch", () => {
  beforeEach(() => localStorage.clear());

  it("survives a remount (page refresh) and reports minutes on stop", () => {
    vi.useFakeTimers();
    const onStop = vi.fn();
    const first = render(<Stopwatch storageKey="t1" onStop={onStop} />);
    fireEvent.click(screen.getByLabelText("Start timer"));
    first.unmount();
    vi.advanceTimersByTime(5 * 60_000);
    render(<Stopwatch storageKey="t1" onStop={onStop} />);
    fireEvent.click(screen.getByLabelText("Stop and record minutes"));
    expect(onStop).toHaveBeenCalledWith(5);
    vi.useRealTimers();
  });
});
