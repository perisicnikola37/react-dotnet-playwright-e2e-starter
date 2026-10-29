import assert from "node:assert/strict";
import { test } from "node:test";
import { createAutomatedTestsClient, RunConflictError } from "../src/client.ts";

const reply = (status: number, body: unknown = {}) => new Response(JSON.stringify(body), { status });

test("startRun posts the category to the configured prefix", async () => {
  const calls: { url: string; init?: RequestInit }[] = [];
  const client = createAutomatedTestsClient({
    baseUrl: "https://api.test/",
    fetch: async (url, init) => (calls.push({ url: String(url), init }), reply(202, { id: "1" })),
  });

  await client.startRun("Smoke");

  assert.equal(calls[0].url, "https://api.test/api/automated-tests/runs");
  assert.equal(calls[0].init?.method, "POST");
  assert.deepEqual(JSON.parse(String(calls[0].init?.body)), { category: "Smoke" });
});

test("a 409 becomes a RunConflictError", async () => {
  const client = createAutomatedTestsClient({ fetch: async () => reply(409) });
  await assert.rejects(client.startRun(), RunConflictError);
});

test("viewUrl points at the run dashboard", () => {
  const client = createAutomatedTestsClient({ prefix: "/tests" });
  assert.equal(client.viewUrl("20260101000000"), "/tests/runs/20260101000000/files/index.html");
});
