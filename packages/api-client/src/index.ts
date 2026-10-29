export interface Item {
  id: string;
  name: string;
  createdAt: string;
}

export interface ItemsClientOptions {
  /** API origin. Empty means same origin (use with a dev proxy). */
  baseUrl?: string;
  /** Custom fetch, e.g. for tests or to add authentication. */
  fetch?: typeof fetch;
}

export class ApiError extends Error {
  readonly status: number;

  constructor(message: string, status: number) {
    super(message);
    this.name = "ApiError";
    this.status = status;
  }
}

export interface ItemsClient {
  list(): Promise<Item[]>;
  create(name: string): Promise<Item>;
  remove(id: string): Promise<void>;
}

export function createItemsClient(options: ItemsClientOptions = {}): ItemsClient {
  const baseUrl = (options.baseUrl ?? "").replace(/\/$/, "");
  const doFetch = options.fetch ?? ((input, init) => fetch(input, init));

  async function send(path: string, init?: RequestInit): Promise<Response> {
    const response = await doFetch(`${baseUrl}/api/items${path}`, init);
    if (!response.ok) {
      const body = await response.json().catch(() => null);
      const message = body?.error ?? body?.errors?.name?.[0] ?? `Request failed (${response.status})`;
      throw new ApiError(message, response.status);
    }
    return response;
  }

  return {
    list: async () => (await send("/")).json(),
    create: async (name) =>
      (
        await send("/", {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ name }),
        })
      ).json(),
    remove: async (id) => {
      await send(`/${id}`, { method: "DELETE" });
    },
  };
}
