import { createBrowserRouter, Navigate, Outlet } from "react-router";
import { useSettings } from "@/api/hooks";
import { AppShell } from "@/components/AppShell";
import { ErrorBox, Loading } from "@/components/common";
import { FilesPage } from "@/pages/FilesPage";
import { LogPage } from "@/pages/LogPage";
import { LogsPage } from "@/pages/LogsPage";
import { NotFoundPage } from "@/pages/NotFoundPage";
import { PausesPage, PlanPage } from "@/pages/PlanPage";
import { SchedulePage } from "@/pages/SchedulePage";
import { SettingsPage } from "@/pages/SettingsPage";
import { SetupPage } from "@/pages/SetupPage";
import { TodayPage } from "@/pages/TodayPage";
import { TrashPage } from "@/pages/TrashPage";
import { AttemptPage } from "@/pages/dsa/AttemptPage";
import { AttemptsPage, DsaOverviewPage, ReviewsPage } from "@/pages/dsa/DsaPages";
import { LibraryPage } from "@/pages/dsa/LibraryPage";
import { ProgressPage } from "@/pages/dsa/ProgressPage";
import { TimedPage } from "@/pages/dsa/TimedPage";
import { ArticulationPage } from "@/pages/ddia/ArticulationPage";
import { ChapterPage } from "@/pages/ddia/ChapterPage";
import { ChaptersPage } from "@/pages/ddia/ChaptersPage";
import { DrillPage, DrillsPage } from "@/pages/ddia/DrillsPages";
import { CheckpointsPage, PhasePage, ReplansPage } from "@/pages/plan/CheckpointsPages";
import { TasksPage } from "@/pages/plan/TasksPage";
import { MonthlyReviewPage, WeeklyReviewPage } from "@/pages/ReviewsPages";
import { SomedayPage } from "@/pages/SomedayPage";

/** First-run gate (spec 12): until setup is complete every page redirects to /setup. */
function RequireSetup() {
  const settings = useSettings();
  if (settings.isPending) return <Loading />;
  if (settings.isError) return <div className="p-6"><ErrorBox error={settings.error} /></div>;
  if (!settings.data.setupCompleted) return <Navigate to="/setup" replace />;
  return <Outlet />;
}

export const router = createBrowserRouter([
  { path: "/setup", element: <SetupPage /> },
  {
    element: <RequireSetup />,
    children: [
      {
        element: <AppShell />,
        children: [
          { index: true, element: <TodayPage /> },
          { path: "logs", element: <LogsPage /> },
          { path: "logs/today", element: <LogPage today /> },
          { path: "logs/:date", element: <LogPage /> },
          { path: "plan", element: <PlanPage /> },
          { path: "plan/pauses", element: <PausesPage /> },
          { path: "plan/schedule", element: <SchedulePage /> },
          { path: "plan/tasks", element: <TasksPage /> },
          { path: "plan/checkpoints", element: <CheckpointsPage /> },
          { path: "plan/replans", element: <ReplansPage /> },
          { path: "plan/phases/:n", element: <PhasePage /> },
          { path: "reviews", element: <Navigate to="/reviews/weekly" replace /> },
          { path: "reviews/weekly", element: <WeeklyReviewPage /> },
          { path: "reviews/monthly", element: <MonthlyReviewPage /> },
          { path: "someday", element: <SomedayPage /> },
          { path: "files", element: <FilesPage /> },
          { path: "dsa", element: <DsaOverviewPage /> },
          { path: "dsa/problems", element: <LibraryPage /> },
          { path: "dsa/attempts", element: <AttemptsPage /> },
          { path: "dsa/attempts/new", element: <AttemptPage /> },
          { path: "dsa/attempts/:id", element: <AttemptPage /> },
          { path: "dsa/reviews", element: <ReviewsPage /> },
          { path: "dsa/progress", element: <ProgressPage /> },
          { path: "dsa/timed", element: <TimedPage /> },
          { path: "ddia", element: <ChaptersPage /> },
          { path: "ddia/chapters/:id", element: <ChapterPage /> },
          { path: "ddia/drills", element: <DrillsPage /> },
          { path: "ddia/drills/:id", element: <DrillPage /> },
          { path: "ddia/articulation", element: <ArticulationPage /> },
          { path: "settings", element: <SettingsPage /> },
          { path: "settings/data", element: <SettingsPage /> },
          { path: "trash", element: <TrashPage /> },
          { path: "*", element: <NotFoundPage /> },
        ],
      },
    ],
  },
]);
