import {
  BookOpen,
  Briefcase,
  CalendarDays,
  ClipboardList,
  Code2,
  FolderOpen,
  Gauge,
  Hammer,
  Home,
  Lightbulb,
  ListChecks,
  Moon,
  Plus,
  Search,
  Settings,
  Sun,
  Trash2,
  TrendingUp,
} from "lucide-react";
import { type ReactNode, useEffect, useState } from "react";
import { NavLink, Outlet, useNavigate } from "react-router";
import { Button } from "@/components/ui/button";
import {
  CommandDialog,
  CommandEmpty,
  CommandGroup,
  CommandInput,
  CommandItem,
  CommandList,
  CommandSeparator,
} from "@/components/ui/command";
import { isoDate } from "@/lib/format";
import { cn } from "@/lib/utils";
import { EditionBanner } from "@/pages/ddia/DdiaShared";
import { SomedayQuickAdd } from "@/pages/SomedayPage";

interface NavItem {
  to: string;
  label: string;
  icon: ReactNode;
  milestone?: string;
}

const NAV: NavItem[] = [
  { to: "/", label: "Today", icon: <Home /> },
  { to: "/logs", label: "Daily logs", icon: <CalendarDays /> },
  { to: "/plan", label: "Plan", icon: <ClipboardList /> },
  { to: "/plan/schedule", label: "Schedule", icon: <ListChecks /> },
  { to: "/files", label: "Files", icon: <FolderOpen /> },
  { to: "/dsa", label: "DSA", icon: <Code2 /> },
  { to: "/ddia", label: "DDIA & design", icon: <BookOpen /> },
  { to: "/reviews/weekly", label: "Reviews", icon: <TrendingUp /> },
  { to: "/someday", label: "Someday", icon: <Lightbulb /> },
  { to: "/flagship", label: "Flagship", icon: <Hammer />, milestone: "M6" },
  { to: "/interviews", label: "Market & interviews", icon: <Briefcase />, milestone: "M7" },
  { to: "/dashboard", label: "Dashboard", icon: <Gauge />, milestone: "M8" },
  { to: "/search", label: "Search", icon: <Search />, milestone: "M8" },
];

function useTheme() {
  const [dark, setDark] = useState(() => {
    try {
      return localStorage.getItem("selftracker.theme") !== "light";
    } catch {
      return true;
    }
  });
  useEffect(() => {
    document.documentElement.classList.toggle("dark", dark);
    try {
      localStorage.setItem("selftracker.theme", dark ? "dark" : "light");
    } catch {
      /* ignore */
    }
  }, [dark]);
  return [dark, setDark] as const;
}

export function AppShell() {
  const [dark, setDark] = useTheme();
  const [open, setOpen] = useState(false);
  const [someday, setSomeday] = useState(false);
  const navigate = useNavigate();

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === "k") {
        e.preventDefault();
        setOpen((o) => !o);
      }
    };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, []);

  const go = (to: string) => {
    setOpen(false);
    navigate(to);
  };
  const today = isoDate();

  return (
    <div className="flex min-h-screen">
      <a href="#main" className="sr-only focus:not-sr-only focus:absolute focus:left-2 focus:top-2 focus:z-50 focus:rounded focus:bg-primary focus:px-3 focus:py-1 focus:text-primary-foreground">
        Skip to content
      </a>
      <aside className="sticky top-0 hidden h-screen w-56 shrink-0 flex-col border-r bg-card/40 p-3 md:flex">
        <div className="mb-4 flex items-center gap-2 px-2">
          <span className="grid size-7 place-items-center rounded-md bg-primary text-sm font-bold text-primary-foreground">S</span>
          <div className="leading-tight">
            <div className="text-sm font-semibold">Project Self</div>
            <div className="text-xs text-muted-foreground">365-day tracker</div>
          </div>
        </div>
        <nav className="grid gap-0.5 text-sm" aria-label="Main">
          {NAV.map((n) =>
            n.milestone ? (
              <span key={n.to} className="flex items-center gap-2 rounded-md px-2 py-1.5 text-muted-foreground/60 [&_svg]:size-4" title={`Arrives with milestone ${n.milestone}`}>
                {n.icon}
                {n.label}
                <span className="ml-auto text-[10px] uppercase">{n.milestone}</span>
              </span>
            ) : (
              <NavLink
                key={n.to}
                to={n.to}
                end={n.to === "/" || n.to === "/plan"}
                className={({ isActive }) =>
                  cn("flex items-center gap-2 rounded-md px-2 py-1.5 [&_svg]:size-4 hover:bg-accent", isActive && "bg-accent font-medium text-foreground")
                }
              >
                {n.icon}
                {n.label}
              </NavLink>
            ),
          )}
        </nav>
        <div className="mt-auto grid gap-0.5 text-sm">
          <NavLink to="/settings" className={({ isActive }) => cn("flex items-center gap-2 rounded-md px-2 py-1.5 hover:bg-accent [&_svg]:size-4", isActive && "bg-accent")}>
            <Settings /> Settings & data
          </NavLink>
          <NavLink to="/trash" className={({ isActive }) => cn("flex items-center gap-2 rounded-md px-2 py-1.5 hover:bg-accent [&_svg]:size-4", isActive && "bg-accent")}>
            <Trash2 /> Trash
          </NavLink>
        </div>
      </aside>

      <div className="flex min-w-0 flex-1 flex-col">
        <header className="sticky top-0 z-30 flex items-center gap-2 border-b bg-background/85 px-4 py-2 backdrop-blur">
          <nav className="flex gap-1 overflow-x-auto text-sm md:hidden" aria-label="Main (compact)">
            {NAV.filter((n) => !n.milestone).map((n) => (
              <NavLink key={n.to} to={n.to} end className={({ isActive }) => cn("rounded px-2 py-1", isActive && "bg-accent")}>
                {n.label}
              </NavLink>
            ))}
          </nav>
          <Button variant="outline" size="sm" className="ml-auto gap-2 text-muted-foreground" onClick={() => setOpen(true)}>
            <Search /> Quick actions <kbd className="rounded border px-1 text-[10px]">Ctrl K</kbd>
          </Button>
          <Button size="sm" onClick={() => navigate(`/logs/${today}`)}>
            <Plus /> Log today
          </Button>
          <Button variant="ghost" size="icon" onClick={() => setDark(!dark)} aria-label={dark ? "Switch to light mode" : "Switch to dark mode"}>
            {dark ? <Sun /> : <Moon />}
          </Button>
        </header>
        <EditionBanner />
        <main id="main" className="mx-auto w-full max-w-6xl flex-1 p-4 md:p-6">
          <Outlet />
        </main>
      </div>

      <CommandDialog open={open} onOpenChange={setOpen} title="Quick actions" description="Log, upload or jump to a page">
        <CommandInput placeholder="Type an action or page..." />
        <CommandList>
          <CommandEmpty>No match.</CommandEmpty>
          <CommandGroup heading="Log">
            <CommandItem onSelect={() => go(`/logs/${today}?type=Full`)}>Log full day</CommandItem>
            <CommandItem onSelect={() => go(`/logs/${today}?type=Mvd`)}>Log MVD</CommandItem>
            <CommandItem onSelect={() => go(`/logs/${today}?type=Rest`)}>Log rest day</CommandItem>
            <CommandItem onSelect={() => go(`/logs/${today}#time`)}>Add time / start timer</CommandItem>
            <CommandItem onSelect={() => go("/files?upload=1")}>Upload file</CommandItem>
            <CommandItem onSelect={() => go("/dsa/attempts/new")}>Add DSA attempt</CommandItem>
            <CommandItem onSelect={() => go("/dsa/reviews")}>DSA review queue</CommandItem>
            <CommandItem onSelect={() => go("/dsa/timed")}>Timed DSA session</CommandItem>
            <CommandItem onSelect={() => go("/ddia")}>Log DDIA reading</CommandItem>
            <CommandItem onSelect={() => go("/ddia/drills")}>Design drill</CommandItem>
            <CommandItem onSelect={() => go("/ddia/articulation")}>Why X over Y drill</CommandItem>
            <CommandItem onSelect={() => { setOpen(false); setSomeday(true); }}>Add someday idea</CommandItem>
            <CommandItem onSelect={() => go("/reviews/weekly")}>Weekly review</CommandItem>
            <CommandItem onSelect={() => go("/reviews/monthly")}>Monthly review</CommandItem>
            <CommandItem onSelect={() => go("/plan/checkpoints")}>Checkpoints</CommandItem>
            <CommandItem onSelect={() => go("/plan/tasks?view=board")}>Task board</CommandItem>
          </CommandGroup>
          <CommandSeparator />
          <CommandGroup heading="Go to">
            {NAV.filter((n) => !n.milestone).map((n) => (
              <CommandItem key={n.to} onSelect={() => go(n.to)}>
                {n.label}
              </CommandItem>
            ))}
            <CommandItem onSelect={() => go("/plan/pauses")}>Pause ranges & travel</CommandItem>
            <CommandItem onSelect={() => go("/settings")}>Settings & data</CommandItem>
            <CommandItem onSelect={() => go("/trash")}>Trash</CommandItem>
          </CommandGroup>
        </CommandList>
      </CommandDialog>
      <SomedayQuickAdd open={someday} onOpenChange={setSomeday} />
    </div>
  );
}
