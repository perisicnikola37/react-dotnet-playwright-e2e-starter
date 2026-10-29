import { useEffect, useState } from "react";

interface TestCase {
  name: string;
  category: "Smoke" | "Regression";
  status: "passed" | "failed" | "skipped";
  durationMs: number;
}

interface TestRun {
  commit: string;
  startedAt: string;
  browser: string;
  tests: TestCase[];
}

export function AutomatedTestsPage() {
  const [run, setRun] = useState<TestRun | null>(null);
  const [filter, setFilter] = useState<"all" | TestCase["category"]>("all");
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    fetch(`${import.meta.env.BASE_URL}test-results.json`)
      .then((r) => (r.ok ? (r.json() as Promise<TestRun>) : Promise.reject(new Error("No test results published."))))
      .then(setRun)
      .catch((e: Error) => setError(e.message));
  }, []);

  if (error) return <p role="alert">{error}</p>;
  if (!run) return <p>Loading…</p>;

  const count = (s: TestCase["status"]) => run.tests.filter((t) => t.status === s).length;
  const visible = run.tests.filter((t) => filter === "all" || t.category === filter);

  return (
    <section data-testid="automated-tests">
      <h1>Automated tests</h1>
      <p className="muted">
        {run.browser} · commit {run.commit} · {new Date(run.startedAt).toLocaleString()}
      </p>
      <div className="summary">
        <div className="card passed"><strong>{count("passed")}</strong> passed</div>
        <div className="card failed"><strong>{count("failed")}</strong> failed</div>
        <div className="card skipped"><strong>{count("skipped")}</strong> skipped</div>
      </div>
      <div className="filters" role="group" aria-label="Category">
        {(["all", "Smoke", "Regression"] as const).map((c) => (
          <button key={c} type="button" aria-pressed={filter === c} onClick={() => setFilter(c)}>
            {c === "all" ? "All" : c}
          </button>
        ))}
      </div>
      <table>
        <thead>
          <tr><th>Test</th><th>Category</th><th>Status</th><th>Duration</th></tr>
        </thead>
        <tbody>
          {visible.map((t) => (
            <tr key={t.name}>
              <td>{t.name}</td>
              <td>{t.category}</td>
              <td><span className={`badge ${t.status}`}>{t.status}</span></td>
              <td>{(t.durationMs / 1000).toFixed(1)} s</td>
            </tr>
          ))}
        </tbody>
      </table>
    </section>
  );
}
