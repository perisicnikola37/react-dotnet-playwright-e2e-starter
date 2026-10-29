export interface Item {
  id: string;
  name: string;
  createdAt: string;
}

async function send(path: string, init?: RequestInit): Promise<Response> {
  const response = await fetch(`/api/items${path}`, init);
  if (!response.ok) {
    const body = await response.json().catch(() => null);
    const message = body?.error ?? body?.errors?.name?.[0] ?? `Request failed (${response.status})`;
    throw new Error(message);
  }
  return response;
}

export const listItems = async (): Promise<Item[]> => (await send("/")).json();

export const createItem = async (name: string): Promise<Item> =>
  (
    await send("/", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ name }),
    })
  ).json();

export const deleteItem = async (id: string): Promise<void> => {
  await send(`/${id}`, { method: "DELETE" });
};
