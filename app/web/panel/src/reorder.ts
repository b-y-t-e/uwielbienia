import { useRef, useState } from "react";

/** Miejsce upuszczenia: przeciągany wiersz i szczelina 0…count, przed którą trafi. */
export interface Drag {
  from: number;
  slot: number;
}

/**
 * Przeciąganie wierszy za uchwyt — mysz i dotyk (zdarzenia wskaźnika, nie HTML5 drag and drop, które nie działa
 * na tabletach). Wiersze listy mają atrybut <c>data-row</c>; przy krawędzi przewijanego kontenera lista się przewija.
 */
export function useReorder(onMove: (from: number, to: number) => void) {
  const listRef = useRef<HTMLDivElement>(null);
  const [drag, setDrag] = useState<Drag | null>(null);

  const start = (from: number) => (e: React.PointerEvent<HTMLElement>) => {
    if (e.button !== 0 || !listRef.current) return;
    e.preventDefault();
    const handle = e.currentTarget;
    handle.setPointerCapture(e.pointerId);
    const list = listRef.current;
    const scroller = list.closest<HTMLElement>(".scroll");
    let slot = from;
    let lastY = e.clientY;
    let scrollTimer = 0;

    const locate = () => {
      const rows = Array.from(list.querySelectorAll<HTMLElement>("[data-row]"));
      slot = rows.filter((row) => {
        const box = row.getBoundingClientRect();
        return lastY > box.top + box.height / 2;
      }).length;
      setDrag({ from, slot });
    };
    const autoscroll = () => {
      if (!scroller) return;
      const box = scroller.getBoundingClientRect();
      const edge = 48;
      const step = lastY < box.top + edge ? -12 : lastY > box.bottom - edge ? 12 : 0;
      if (step) { scroller.scrollBy(0, step); locate(); }
    };
    const move = (ev: PointerEvent) => { lastY = ev.clientY; locate(); };
    const end = (commit: boolean) => () => {
      handle.removeEventListener("pointermove", move);
      handle.removeEventListener("pointerup", up);
      handle.removeEventListener("pointercancel", cancel);
      clearInterval(scrollTimer);
      setDrag(null);
      const to = slot > from ? slot - 1 : slot;
      if (commit && to !== from) onMove(from, to);
    };
    const up = end(true);
    const cancel = end(false);
    handle.addEventListener("pointermove", move);
    handle.addEventListener("pointerup", up);
    handle.addEventListener("pointercancel", cancel);
    scrollTimer = window.setInterval(autoscroll, 30);
    setDrag({ from, slot: from });
  };

  /** Klasy wiersza: przeciągany, znacznik nad nim albo (ostatni wiersz) pod nim. */
  const rowClass = (index: number, count: number) => {
    if (!drag) return "";
    const marks = [];
    if (drag.from === index) marks.push("dragged");
    const moves = drag.slot !== drag.from && drag.slot !== drag.from + 1;
    if (moves && drag.slot === index) marks.push("drop-before");
    if (moves && drag.slot === count && index === count - 1) marks.push("drop-after");
    return marks.join(" ");
  };

  return { listRef, start, rowClass, dragging: drag !== null };
}
