export type RunStatus = "running" | "passed" | "failed" | "error";

export interface RunInfo {
  id: string;
  status: RunStatus;
  category: string | null;
  startedAt: string;
  finishedAt: string | null;
  total: number | null;
  passed: number | null;
  failed: number | null;
  skipped: number | null;
  exitCode: number | null;
}

export interface AutomatedTestsClientOptions {
  /** API origin. Empty means same origin (use with a dev proxy). */
  baseUrl?: string;
  /** Route prefix used by MapAutomatedTests(). */
  prefix?: string;
  /** Custom fetch, e.g. to add authentication. */
  fetch?: typeof fetch;
}

export class RunConflictError extends Error {
  constructor() {
    super("A run is already in progress.");
    this.name = "RunConflictError";
  }
}

export interface AutomatedTestsClient {
  listRuns(): Promise<RunInfo[]>;
  getRun(id: string): Promise<RunInfo>;
  startRun(category?: string): Promise<RunInfo>;
  /** URL of the run's dashboard; meant for an iframe. */
  viewUrl(id: string): string;
  logUrl(id: string): string;
}

export function createAutomatedTestsClient(options: AutomatedTestsClientOptions = {}): AutomatedTestsClient {
  const base = (options.baseUrl ?? "").replace(/\/$/, "") + (options.prefix ?? "/api/automated-tests");
  const doFetch = options.fetch ?? ((input, init) => fetch(input, init));

  async function json<T>(path: string, init?: RequestInit): Promise<T> {
    const response = await doFetch(`${base}${path}`, init);
    if (response.status === 409) throw new RunConflictError();
    if (!response.ok) throw new Error(`Request failed (${response.status})`);
    return (await response.json()) as T;
  }

  return {
    listRuns: () => json<RunInfo[]>("/runs"),
    getRun: (id) => json<RunInfo>(`/runs/${encodeURIComponent(id)}`),
    startRun: (category) =>
      json<RunInfo>("/runs", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ category: category ?? null }),
      }),
    viewUrl: (id) => `${base}/runs/${encodeURIComponent(id)}/files/index.html`,
    logUrl: (id) => `${base}/runs/${encodeURIComponent(id)}/log`,
  };
}
