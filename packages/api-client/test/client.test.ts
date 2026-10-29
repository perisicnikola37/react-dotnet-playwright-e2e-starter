import assert from "node:assert/strict";
import { test } from "node:test";
import { ApiError, createItemsClient } from "../src/index.ts";

const json = (status: number, body?: unknown) =>
  new Response(body === undefined ? null : JSON.stringify(body), { status });

test("create posts the name and returns the created item", async () => {
  let request: { url: string; init?: RequestInit } | undefined;
  const client = createItemsClient({
    baseUrl: "http://api.test/",
    fetch: async (url, init) => {
      request = { url: String(url), init };
      return json(201, { id: "1", name: "A", createdAt: "2026-01-01" });
    },
  });

  const item = await client.create("A");

  assert.equal(request?.url, "http://api.test/api/items/");
  assert.equal(request?.init?.method, "POST");
  assert.equal(item.name, "A");
});

test("errors carry the API message and status", async () => {
  const client = createItemsClient({ fetch: async () => json(409, { error: "already exists" }) });

  await assert.rejects(client.create("A"), (e: unknown) => {
    assert.ok(e instanceof ApiError);
    assert.equal(e.status, 409);
    assert.equal(e.message, "already exists");
    return true;
  });
});

test("validation errors use the first message of the name field", async () => {
  const client = createItemsClient({ fetch: async () => json(400, { errors: { name: ["Name is required."] } }) });

  await assert.rejects(client.create(" "), /Name is required/);
});
