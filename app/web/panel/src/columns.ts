import { useState } from "react";

const KEY = "panel-szerokosci";
const DEFAULT = [0.8, 1.25, 1.15];
/** Najwęższa czytelna kolumna; to samo minimum pilnuje CSS (`minmax`), więc trzyma się też po zwężeniu okna. */
const MIN_PX = 220;

const clamp = (v: number, lo: number, hi: number) => Math.min(Math.max(v, lo), hi);

/** Łączna szerokość siatki w frakcjach — po niej normalizujemy zapamiętany układ, żeby arytmetyka gestu nie przepełniała. */
const TOTAL = DEFAULT.reduce((sum, fraction) => sum + fraction, 0);

/**
 * Zapamiętany układ sprowadzony do tej samej skali co domyślny (suma frakcji = `TOTAL`): trzy skończone,
 * dodatnie frakcje o skończonej sumie, z których żadna po normalizacji nie znika. Inna wartość (stara wersja,
 * ręczna zmiana, liczby na granicy zakresu) daje `null`, czyli układ domyślny — o czytelność kolumn dba
 * `minmax(220px, …)` w CSS, więc skrajna proporcja nie zwęża żadnej poniżej minimum.
 */
function normalized(widths: number[]): number[] | null {
  if (widths.length !== 3 || !widths.every((n) => Number.isFinite(n) && n > 0)) return null;
  const sum = widths.reduce((total, fraction) => total + fraction, 0);
  if (!Number.isFinite(sum)) return null;
  const scaled = widths.map((fraction) => (fraction / sum) * TOTAL);
  return scaled.every((fraction) => fraction > 0) ? scaled : null;
}

function read(): number[] {
  const saved = (localStorage.getItem(KEY) ?? "").split(",").map(Number);
  return normalized(saved) ?? DEFAULT;
}

/**
 * Szerokości kolumn Plan · Pieśń · Na ekranie (frakcje siatki): uchwyt między kolumnami przeciąga się myszą
 * albo palcem, dwuklik przywraca domyślne. Zapamiętane w przeglądarce — aplikacja ma własne w ustawieniach.
 * Na wąskim ekranie kolumny są jedna naraz, więc uchwytów nie ma (CSS) i nic się nie liczy.
 */
export function useColumnWidths() {
  const [widths, setWidths] = useState<number[]>(read);

  const save = (next: number[]) => {
    localStorage.setItem(KEY, next.join(","));
    setWidths(next);
  };

  const drag = (index: number, e: React.PointerEvent<HTMLDivElement>) => {
    if (e.button !== 0) return; // prawy / środkowy przycisk otwiera menu, nie przestawia kolumn
    const grid = e.currentTarget.parentElement;
    const columns = grid ? Array.from(grid.querySelectorAll<HTMLElement>(":scope > .column")) : [];
    const measured = columns.map((column) => column.getBoundingClientRect().width);
    if (measured.length !== 3 || measured.some((width) => width <= 0)) return;

    // zaczynamy od tego, co widać: po zwężeniu okna CSS mógł przyciąć kolumny do minimum, więc zapamiętane
    // frakcje bywają inne niż faktyczne szerokości. Wszystkie trzy frakcje liczymy w jednej skali z pomiaru,
    // żeby zmiana pary nie przesuwała trzeciej kolumny — granica ma iść za kursorem
    const total = measured.reduce((sum, width) => sum + width, 0);
    const scale = widths.reduce((sum, fraction) => sum + fraction, 0) / total; // frakcje na piksel
    const start = measured.map((width) => width * scale);
    const pair = start[index] + start[index + 1];
    const min = Math.min(MIN_PX * scale, pair / 2);
    const startX = e.clientX;
    const pointer = e.pointerId;
    let current = start;

    const move = (ev: PointerEvent) => {
      if (ev.pointerId !== pointer) return; // drugi palec nad kolumną nie przejmuje przeciągania
      current = [...start];
      current[index] = clamp(start[index] + (ev.clientX - startX) * scale, min, pair - min);
      current[index + 1] = pair - current[index];
      setWidths(current);
    };
    const up = (ev: PointerEvent) => {
      if (ev.pointerId !== pointer) return;
      document.removeEventListener("pointermove", move);
      document.removeEventListener("pointerup", up);
      document.removeEventListener("pointercancel", up);
      save(normalized(current) ?? DEFAULT);
    };
    e.currentTarget.setPointerCapture(e.pointerId);
    document.addEventListener("pointermove", move);
    document.addEventListener("pointerup", up);
    document.addEventListener("pointercancel", up);
    e.preventDefault();
  };

  const style = {
    "--w-plan": `${widths[0]}fr`, "--w-item": `${widths[1]}fr`, "--w-live": `${widths[2]}fr`,
  } as React.CSSProperties;

  /** Uchwyt po kolumnie o numerze `index` (0 = między Plan i Pieśń, 1 = między Pieśń i Na ekranie). */
  const splitter = (index: number, label: string) => ({
    className: "splitter", role: "separator", "aria-orientation": "vertical" as const, "aria-label": label,
    onPointerDown: (e: React.PointerEvent<HTMLDivElement>) => drag(index, e),
    onDoubleClick: () => save(DEFAULT),
  });

  return { style, splitter };
}
