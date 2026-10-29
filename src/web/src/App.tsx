import { createItemsClient, type Item } from "@perisicnikola37/e2e-starter-client";
import { FormEvent, useEffect, useState } from "react";

const client = createItemsClient();
const listItems = () => client.list();
const createItem = (name: string) => client.create(name);
const deleteItem = (id: string) => client.remove(id);

export function App() {
  const [items, setItems] = useState<Item[] | null>(null);
  const [name, setName] = useState("");
  const [error, setError] = useState<string | null>(null);

  const load = () => listItems().then(setItems).catch((e: Error) => setError(e.message));

  useEffect(() => {
    void load();
  }, []);

  const onSubmit = async (event: FormEvent) => {
    event.preventDefault();
    setError(null);
    try {
      await createItem(name);
      setName("");
      await load();
    } catch (e) {
      setError((e as Error).message);
    }
  };

  const onDelete = async (id: string) => {
    await deleteItem(id);
    await load();
  };

  return (
    <main>
      <h1 data-testid="page-heading">Items</h1>

      <form onSubmit={onSubmit} aria-label="Add item">
        <label htmlFor="item-name">Item name</label>
        <input id="item-name" data-testid="item-name" value={name} onChange={(e) => setName(e.target.value)} />
        <button type="submit" data-testid="add-item">
          Add item
        </button>
      </form>

      {error && (
        <p role="alert" data-testid="error-message">
          {error}
        </p>
      )}

      {items === null ? (
        <p data-testid="loading">Loading…</p>
      ) : items.length === 0 ? (
        <p data-testid="empty-state">No items yet.</p>
      ) : (
        <ul data-testid="item-list">
          {items.map((item) => (
            <li key={item.id} data-testid="item-row">
              <span data-testid="item-label">{item.name}</span>
              <button type="button" aria-label={`Delete ${item.name}`} onClick={() => onDelete(item.id)}>
                Delete
              </button>
            </li>
          ))}
        </ul>
      )}
    </main>
  );
}
