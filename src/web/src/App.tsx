import { useEffect, useState } from "react";
import { AutomatedTestsPage } from "./AutomatedTestsPage";
import { ItemsPage } from "./ItemsPage";

const route = () => (location.hash === "#/automated-tests" ? "tests" : "items");

export function App() {
  const [current, setCurrent] = useState(route);

  useEffect(() => {
    const onChange = () => setCurrent(route());
    window.addEventListener("hashchange", onChange);
    return () => window.removeEventListener("hashchange", onChange);
  }, []);

  return (
    <>
      <nav aria-label="Main">
        <a href="#/" aria-current={current === "items" ? "page" : undefined}>Items</a>
        <a href="#/automated-tests" aria-current={current === "tests" ? "page" : undefined}>Automated tests</a>
      </nav>
      <main>{current === "tests" ? <AutomatedTestsPage /> : <ItemsPage />}</main>
    </>
  );
}
