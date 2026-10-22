import { useEffect, useState } from "react";

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

/** Pełny ekran przeglądarki w panelu: stan faktyczny i przełącznik; `canToggle` = da się go zmienić z panelu. */
export type FullscreenControl = { active: boolean; canToggle: boolean; toggle: () => void };

/**
 * Pełny ekran przeglądarki (panel bez pasków przeglądarki — jak okno operatora). Stan bierzemy z przeglądarki,
 * bo z pełnego ekranu wychodzi się też Esc i F11: `fullscreenElement` widzi tylko tryb z tego przycisku, a F11
 * (osobny tryb przeglądarki) — zapytanie `display-mode: fullscreen`. Trybu F11 nie da się wyłączyć kodem, więc
 * wtedy przycisk pokazuje stan, ale nic nie robi (`canToggle: false`) — żeby opis nie obiecywał czegoś innego
 * niż czynność. `null` = przeglądarka nie daje pełnego ekranu (nie umie albo zabroniła go polityką) i wtedy
 * nie pokazujemy przycisku.
 */
export function useFullscreen(): FullscreenControl | null {
  const query = window.matchMedia("(display-mode: fullscreen)");
  const shown = () => document.fullscreenElement != null || query.matches;
  const [active, setActive] = useState(shown);
  useEffect(() => {
    const change = () => setActive(shown());
    document.addEventListener("fullscreenchange", change);
    query.addEventListener("change", change);
    return () => {
      document.removeEventListener("fullscreenchange", change);
      query.removeEventListener("change", change);
    };
  }, []);
  if (!document.fullscreenEnabled) return null;
  const ownFullscreen = document.fullscreenElement != null;
  return {
    active,
    canToggle: !active || ownFullscreen,
    toggle: () => {
      if (ownFullscreen) void document.exitFullscreen().catch(() => {});
      else if (!active) void document.documentElement.requestFullscreen().catch(() => {});
    },
  };
}
