import { useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { Navigate, useNavigate } from "react-router";
import { api, call, type S } from "@/api/client";
import { keys, useAction, useSettings } from "@/api/hooks";
import { Field, Loading, selectClass } from "@/components/common";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Checkbox } from "@/components/ui/checkbox";
import { Input } from "@/components/ui/input";
import { isoDate, timeZones } from "@/lib/format";

/** First-run setup (spec 12): plan start date, DDIA edition, travel anchor Friday, file root. */
export function SetupPage() {
  const settings = useSettings();
  const navigate = useNavigate();
  const qc = useQueryClient();
  const [start, setStart] = useState(isoDate());
  const [edition, setEdition] = useState<S["DdiaEdition"]>("Unconfirmed");
  const [travel, setTravel] = useState(true);
  const [anchor, setAnchor] = useState("");
  const [fileRoot, setFileRoot] = useState("data/files");
  const [tz, setTz] = useState("Asia/Kolkata");

  const setup = useAction(
    () =>
      call(
        api.POST("/api/v1/settings/setup", {
          body: {
            planStartDate: start,
            ddiaEdition: edition,
            travelEnabled: travel && Boolean(anchor),
            travelAnchorFriday: travel && anchor ? anchor : null,
            fileRoot,
            timeZone: tz,
          },
        }),
      ),
    {
      success: "Setup complete. Day 1 starts when you say it does.",
      onSuccess: async () => {
        await qc.invalidateQueries({ queryKey: keys.settings });
        navigate("/", { replace: true });
      },
    },
  );

  if (settings.isPending) return <Loading />;
  if (settings.data?.setupCompleted) return <Navigate to="/" replace />;

  const anchorIsFriday = !anchor || new Date(`${anchor}T00:00:00`).getDay() === 5;

  return (
    <main className="mx-auto grid min-h-screen max-w-xl place-items-center p-6">
      <Card className="w-full">
        <CardHeader>
          <CardTitle className="text-xl">Project Self: first-run setup</CardTitle>
          <CardDescription>
            Day 1 is the day you start your tracker. A delayed week shifts the sequence; it never restarts the plan. Everything here can be changed later
            except the file folder.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <form
            className="grid gap-4"
            onSubmit={(e) => {
              e.preventDefault();
              setup.mutate(undefined);
            }}
          >
            <Field label="Plan start date (Day 1)">{(id) => <Input id={id} type="date" required value={start} onChange={(e) => setStart(e.target.value)} />}</Field>

            <fieldset className="grid gap-2">
              <legend className="mb-1 text-sm font-medium">Which DDIA edition do you own?</legend>
              <p className="text-xs text-muted-foreground">Check the title/copyright page and ISBN. The default chapter map is the 2nd edition (2026, 14 chapters).</p>
              <div className="flex flex-wrap gap-4 text-sm">
                {(["Second", "First", "Unconfirmed"] as const).map((e) => (
                  <label key={e} className="flex items-center gap-2">
                    <input type="radio" name="edition" checked={edition === e} onChange={() => setEdition(e)} />
                    {e === "Unconfirmed" ? "Not sure yet" : `${e} edition`}
                  </label>
                ))}
              </div>
            </fieldset>

            <div className="grid gap-2">
              <label className="flex items-center gap-2 text-sm font-medium">
                <Checkbox checked={travel} onCheckedChange={(v) => setTravel(v === true)} /> Alternate-week Edappal trips (Friday to Monday)
              </label>
              {travel && (
                <Field
                  label="A Friday on which a trip starts"
                  hint="Trips then repeat every 14 days; any single week can be overridden later."
                  error={anchorIsFriday ? undefined : "Pick a Friday."}
                >
                  {(id) => <Input id={id} type="date" value={anchor} onChange={(e) => setAnchor(e.target.value)} />}
                </Field>
              )}
            </div>

            <Field label="File folder" hint="Relative to the repository root, or an absolute path. Fixed after setup (moving it is a manual step).">
              {(id) => <Input id={id} value={fileRoot} onChange={(e) => setFileRoot(e.target.value)} />}
            </Field>

            <Field label="Time zone">
              {(id) => (
                <select id={id} className={selectClass} value={tz} onChange={(e) => setTz(e.target.value)}>
                  {timeZones().map((z) => (
                    <option key={z} value={z}>
                      {z}
                    </option>
                  ))}
                </select>
              )}
            </Field>

            <Button type="submit" disabled={setup.isPending || !anchorIsFriday}>
              {setup.isPending ? "Saving..." : "Start tracking"}
            </Button>
          </form>
        </CardContent>
      </Card>
    </main>
  );
}
