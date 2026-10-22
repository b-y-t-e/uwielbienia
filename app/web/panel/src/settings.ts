import { useState } from "react";

/** Ustawienie widoku zapamiętane w przeglądarce (akordy, wygląd) — nie dotyczy aplikacji. */
export function useStored<T extends string | boolean>(key: string, initial: T): [T, (value: T) => void] {
  const [value, setValue] = useState<T>(() => {
    const saved = localStorage.getItem(key);
    if (saved === null) return initial;
    return (typeof initial === "boolean" ? saved === "true" : saved) as T;
  });
  return [value, (next: T) => { localStorage.setItem(key, String(next)); setValue(next); }];
}

export function useTheme(): [string, () => void] {
  const [theme, setTheme] = useStored<string>("panel-wyglad", document.documentElement.dataset.theme ?? "dark");
  document.documentElement.dataset.theme = theme;
  return [theme, () => setTheme(theme === "dark" ? "light" : "dark")];
}
