import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { createAutomatedTestsClient, RunConflictError, type AutomatedTestsClient, type RunInfo } from "./client";

export interface AutomatedTestsPageProps {
  /** Use your own client (auth, custom origin); otherwise one is created from `baseUrl`. */
  client?: AutomatedTestsClient;
  baseUrl?: string;
  /** Page heading. */
  title?: string;
  /** Categories that get a "Run" button next to "Run all". */
  categories?: string[];
  /** How often the run list refreshes while a run is in progress. The dashboard itself updates live. */
  pollMs?: number;
  /** Height of the dashboard iframe. */
  height?: string | number;
}

const colors = { passed: "#16a34a", failed: "#dc2626", error: "#dc2626", running: "#2563eb" } as const;

function summary(run: RunInfo) {
  if (run.status === "running") return "Running… the dashboard below updates live.";
  if (run.status === "error") return "The run ended without results. Check the runner log.";
  return `${run.status}: ${run.passed ?? 0} passed, ${run.failed ?? 0} failed, ${run.skipped ?? 0} skipped`;
}

export function AutomatedTestsPage({
  client,
  baseUrl,
  title = "Automated tests",
  categories = ["Smoke", "Regression"],
  pollMs = 3000,
  height = "80vh",
}: AutomatedTestsPageProps) {
  const api = useMemo(() => client ?? createAutomatedTestsClient({ baseUrl }), [client, baseUrl]);
  const [runs, setRuns] = useState<RunInfo[]>([]);
  const [selected, setSelected] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [frameUrl, setFrameUrl] = useState<string | null>(null);
  const [logUrl, setLogUrl] = useState<string | null>(null);
  const frame = useRef<HTMLIFrameElement>(null);

  const refresh = useCallback(async () => {
    try {
      const list = await api.listRuns();
      setRuns(list);
      setSelected((current) => current ?? list[0]?.id ?? null);
    } catch (e) {
      setMessage((e as Error).message);
    }
  }, [api]);

  useEffect(() => {
    void refresh();
  }, [refresh]);

  const running = runs.some((r) => r.status === "running");
  useEffect(() => {
    if (!running) return;
    const timer = setInterval(() => void refresh(), pollMs);
    return () => clearInterval(timer);
  }, [running, refresh, pollMs]);

  // The dashboard follows a run live, so it is shown from the moment the run starts.
  useEffect(() => {
    if (!selected) return;
    let cancelled = false;
    setFrameUrl(null);
    setLogUrl(null);
    Promise.all([api.dashboardUrl(selected), api.logUrl(selected)]).then(
      ([dashboard, log]) => {
        if (cancelled) return;
        setFrameUrl(dashboard);
        setLogUrl(log);
      },
      (e: Error) => !cancelled && setMessage(e.message),
    );
    return () => {
      cancelled = true;
    };
  }, [api, selected]);

  // "Re-run" inside the dashboard starts a run and tells this page, so the list and the selection follow.
  useEffect(() => {
    const onMessage = (event: MessageEvent) => {
      if (event.source !== frame.current?.contentWindow) return;
      const data = event.data as { source?: string; type?: string; runId?: string } | null;
      if (data?.source !== "e2e-dashboard" || data.type !== "run-started" || !data.runId) return;
      setSelected(data.runId);
      void refresh();
    };
    window.addEventListener("message", onMessage);
    return () => window.removeEventListener("message", onMessage);
  }, [refresh]);

  const start = async (category?: string) => {
    setMessage(null);
    try {
      const run = await api.startRun(category);
      setSelected(run.id);
      await refresh();
    } catch (e) {
      setMessage(e instanceof RunConflictError ? e.message : (e as Error).message);
    }
  };

  const current = runs.find((r) => r.id === selected);

  return (
    <section data-testid="automated-tests" style={{ fontFamily: "system-ui, sans-serif" }}>
      <h1>{title}</h1>
      <div style={{ display: "flex", gap: 8, flexWrap: "wrap", margin: "12px 0" }}>
        <button type="button" disabled={running} onClick={() => void start()}>
          Run all
        </button>
        {categories.map((c) => (
          <button key={c} type="button" disabled={running} onClick={() => void start(c)}>
            Run {c}
          </button>
        ))}
        <select aria-label="Run" value={selected ?? ""} onChange={(e) => setSelected(e.target.value)} disabled={runs.length === 0}>
          {runs.map((r) => (
            <option key={r.id} value={r.id}>
              {r.id} · {r.status}
              {r.category ? ` · ${r.category}` : ""}
              {r.tests?.length ? ` · ${r.tests.length} selected` : ""}
            </option>
          ))}
        </select>
      </div>

      {message && (
        <p role="alert" style={{ color: colors.failed }}>
          {message}
        </p>
      )}
      {current && (
        <p data-testid="run-status" style={{ color: colors[current.status] }}>
          {summary(current)}
          {current.status === "error" && logUrl && (
            <>
              {" "}
              <a href={logUrl} target="_blank" rel="noreferrer">
                runner log
              </a>
            </>
          )}
        </p>
      )}

      {current && frameUrl ? (
        <iframe
          ref={frame}
          key={current.id}
          title="Test run dashboard"
          src={frameUrl}
          referrerPolicy="no-referrer"
          style={{ width: "100%", height, border: "1px solid #dde1e7", borderRadius: 8 }}
        />
      ) : (
        !current && <p>No runs yet. Start one above.</p>
      )}
    </section>
  );
}
