import type { Item, ItemsClientOptions } from "@perisicnikola37/e2e-starter-client";

// Static GitHub Pages demo: an in-memory fake of the Items API, enabled with VITE_DEMO=true.
let items: Item[] = [];

const json = (body: unknown, status = 200) =>
  new Response(JSON.stringify(body), { status, headers: { "Content-Type": "application/json" } });

const demoFetch: typeof fetch = async (input, init) => {
  const url = String(input);
  const method = init?.method ?? "GET";
  const id = url.split("/api/items/")[1];
  if (method === "GET") return json(items);
  if (method === "POST") {
    const { name } = JSON.parse(String(init?.body)) as { name?: string };
    if (!name?.trim()) return json({ error: "Name is required." }, 400);
    const item = { id: crypto.randomUUID(), name: name.trim(), createdAt: new Date().toISOString() };
    items = [...items, item];
    return json(item, 201);
  }
  items = items.filter((i) => i.id !== id);
  return new Response(null, { status: 204 });
};

export const demoOptions: ItemsClientOptions = import.meta.env.VITE_DEMO ? { fetch: demoFetch } : {};
