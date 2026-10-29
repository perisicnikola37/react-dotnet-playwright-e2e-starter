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
  /** Fully qualified test methods when only a selection ran (e.g. "Re-run failed"). */
  tests?: string[] | null;
}

export type RerunMode = "all" | "failed" | "same";

export interface DashboardToken {
  token: string;
  expiresAt: string;
}

export interface AutomatedTestsClientOptions {
  /** API origin. Empty means same origin (use with a dev proxy). */
  baseUrl?: string;
  /** Route prefix used by MapAutomatedTests(). */
  prefix?: string;
  /** Custom fetch, e.g. to add cookies or headers. */
  fetch?: typeof fetch;
  /** Returns a bearer token for every API call, e.g. from MSAL or Auth0. */
  getAccessToken?: () => string | null | undefined | Promise<string | null | undefined>;
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
  /** Starts the whole suite, one category, or the given fully qualified test methods. */
  startRun(category?: string, tests?: string[]): Promise<RunInfo>;
  rerun(id: string, mode?: RerunMode): Promise<RunInfo>;
  /** Short-lived token that authorizes the dashboard iframe (an iframe cannot send an Authorization header). */
  issueDashboardToken(): Promise<DashboardToken>;
  /** URL of a run's dashboard (or another file of the run folder) for an iframe or a link. */
  dashboardUrl(id: string, path?: string): Promise<string>;
  /** URL of the runner log, readable without an Authorization header. */
  logUrl(id: string): Promise<string>;
}

// A token is reused until a minute before it expires.
const TOKEN_MARGIN_MS = 60_000;

export function createAutomatedTestsClient(options: AutomatedTestsClientOptions = {}): AutomatedTestsClient {
  const base = (options.baseUrl ?? "").replace(/\/$/, "") + (options.prefix ?? "/api/automated-tests");
  const doFetch = options.fetch ?? ((input, init) => fetch(input, init));
  let token: Promise<DashboardToken> | null = null;

  async function json<T>(path: string, init: RequestInit = {}): Promise<T> {
    const headers = new Headers(init.headers);
    const accessToken = await options.getAccessToken?.();
    if (accessToken) headers.set("Authorization", `Bearer ${accessToken}`);
    if (init.body) headers.set("Content-Type", "application/json");

    const response = await doFetch(`${base}${path}`, { ...init, headers });
    if (response.status === 409) throw new RunConflictError();
    if (!response.ok) {
      const body = (await response.json().catch(() => null)) as { error?: string } | null;
      throw new Error(body?.error ?? `Request failed (${response.status})`);
    }
    return (await response.json()) as T;
  }

  const issueDashboardToken = () => json<DashboardToken>("/dashboard-token", { method: "POST" });

  async function currentToken(): Promise<string> {
    const cached = token;
    if (cached) {
      try {
        const value = await cached;
        if (Date.parse(value.expiresAt) - Date.now() > TOKEN_MARGIN_MS) return value.token;
      } catch {
        // A failed request is not cached; fall through and ask again.
      }
    }
    if (token === cached) token = issueDashboardToken();
    try {
      return (await token!).token;
    } catch (e) {
      token = null;
      throw e;
    }
  }

  const runPath = (id: string) => `/runs/${encodeURIComponent(id)}`;
  const dashboardPath = async (id: string, path: string) =>
    `${base}/dashboard/${await currentToken()}/${encodeURIComponent(id)}/${path.replace(/^\//, "")}`;

  return {
    listRuns: () => json<RunInfo[]>("/runs"),
    getRun: (id) => json<RunInfo>(runPath(id)),
    startRun: (category, tests) =>
      json<RunInfo>("/runs", { method: "POST", body: JSON.stringify({ category: category ?? null, tests: tests ?? null }) }),
    rerun: (id, mode = "all") => json<RunInfo>(`${runPath(id)}/rerun`, { method: "POST", body: JSON.stringify({ mode }) }),
    issueDashboardToken,
    dashboardUrl: (id, path = "index.html") => dashboardPath(id, path),
    logUrl: (id) => dashboardPath(id, "api/log"),
  };
}
