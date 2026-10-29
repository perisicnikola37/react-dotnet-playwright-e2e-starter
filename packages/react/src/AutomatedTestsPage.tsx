import { useCallback, useEffect, useMemo, useState } from "react";
import { createAutomatedTestsClient, RunConflictError, type AutomatedTestsClient, type RunInfo } from "./client";

export interface AutomatedTestsPageProps {
  /** Use your own client (custom fetch / auth); otherwise one is created from `baseUrl`. */
  client?: AutomatedTestsClient;
  baseUrl?: string;
  /** Categories that get a "Run" button next to "Run all". */
  categories?: string[];
  /** How often to refresh while a run is in progress. */
  pollMs?: number;
}

const colors = { passed: "#16a34a", failed: "#dc2626", error: "#dc2626", running: "#2563eb" } as const;

export function AutomatedTestsPage({ client, baseUrl, categories = ["Smoke", "Regression"], pollMs = 2000 }: AutomatedTestsPageProps) {
  const api = useMemo(() => client ?? createAutomatedTestsClient({ baseUrl }), [client, baseUrl]);
  const [runs, setRuns] = useState<RunInfo[]>([]);
  const [selected, setSelected] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);

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
      <h1>Automated tests</h1>
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
            </option>
          ))}
        </select>
      </div>

      {message && <p role="alert" style={{ color: colors.failed }}>{message}</p>}
      {current && (
        <p data-testid="run-status" style={{ color: colors[current.status] }}>
          {current.status === "running"
            ? "Running…"
            : `${current.status}: ${current.passed ?? 0} passed, ${current.failed ?? 0} failed, ${current.skipped ?? 0} skipped`}
          {current.status === "error" && (
            <>
              {" "}
              <a href={api.logUrl(current.id)} target="_blank" rel="noreferrer">runner log</a>
            </>
          )}
        </p>
      )}

      {current && current.status !== "running" && current.status !== "error" ? (
        <iframe
          key={`${current.id}-${current.finishedAt}`}
          title="Test run dashboard"
          src={api.viewUrl(current.id)}
          style={{ width: "100%", height: "80vh", border: "1px solid #dde1e7", borderRadius: 8 }}
        />
      ) : (
        !current && <p>No runs yet. Start one above.</p>
      )}
    </section>
  );
}
