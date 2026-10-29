import assert from "node:assert/strict";
import { test } from "node:test";
import { createAutomatedTestsClient, RunConflictError } from "../src/client.ts";

const reply = (status: number, body: unknown = {}) => new Response(JSON.stringify(body), { status });
const inAnHour = () => new Date(Date.now() + 3_600_000).toISOString();

test("startRun posts the category and selected tests to the configured prefix", async () => {
  const calls: { url: string; init?: RequestInit }[] = [];
  const client = createAutomatedTestsClient({
    baseUrl: "https://api.test/",
    fetch: async (url, init) => (calls.push({ url: String(url), init }), reply(202, { id: "1" })),
  });

  await client.startRun("Smoke", ["App.Tests.A"]);

  assert.equal(calls[0].url, "https://api.test/api/automated-tests/runs");
  assert.equal(calls[0].init?.method, "POST");
  assert.deepEqual(JSON.parse(String(calls[0].init?.body)), { category: "Smoke", tests: ["App.Tests.A"] });
});

test("a 409 becomes a RunConflictError and other errors carry the API message", async () => {
  const conflict = createAutomatedTestsClient({ fetch: async () => reply(409) });
  await assert.rejects(conflict.startRun(), RunConflictError);

  const bad = createAutomatedTestsClient({ fetch: async () => reply(400, { error: "That run has no failed tests." }) });
  await assert.rejects(bad.rerun("1", "failed"), /no failed tests/);
});

test("getAccessToken adds a bearer header to every call", async () => {
  const headers: Headers[] = [];
  const client = createAutomatedTestsClient({
    getAccessToken: async () => "abc",
    fetch: async (_url, init) => (headers.push(new Headers(init?.headers)), reply(200, [])),
  });

  await client.listRuns();

  assert.equal(headers[0].get("Authorization"), "Bearer abc");
});

test("dashboardUrl puts a dashboard token in the path and reuses it until it nearly expires", async () => {
  let issued = 0;
  const client = createAutomatedTestsClient({
    prefix: "/tests",
    fetch: async () => reply(200, { token: `t${++issued}`, expiresAt: inAnHour() }),
  });

  assert.equal(await client.dashboardUrl("20260101000000000"), "/tests/dashboard/t1/20260101000000000/index.html");
  assert.equal(await client.logUrl("20260101000000000"), "/tests/dashboard/t1/20260101000000000/api/log");
  assert.equal(issued, 1);
});

test("an expired dashboard token is replaced", async () => {
  let issued = 0;
  const client = createAutomatedTestsClient({
    fetch: async () => reply(200, { token: `t${++issued}`, expiresAt: issued === 1 ? new Date().toISOString() : inAnHour() }),
  });

  await client.dashboardUrl("1");
  assert.equal(await client.dashboardUrl("1"), "/api/automated-tests/dashboard/t2/1/index.html");
});
