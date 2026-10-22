import { useEffect, useState } from "react";
import { command } from "../connection";
import type { ItemMessage, LayoutEntry, PartMessage } from "../protocol";
import { useReorder } from "../reorder";
import { GripIcon } from "./Icons";
import { Lines } from "./Lines";
import { Menu, type MenuAction } from "./Menu";

const kindTitle: Record<string, string> = {
  song: "Pieśń", text: "Tekst", presentation: "Prezentacja", video: "Film", kiosk: "Kiosk",
};

/**
 * Kolumna „Pieśń”: wybrana pozycja planu. Pieśń i tekst — części w układzie tego planu: pole wyboru = śpiewana,
 * przeciąganie i „Powtórz” (tylko pieśń; tekst ma kolejność z edytora), „Usuń z układu”, „Przywróć usuniętą część”.
 * Każda zmiana od razu w planie (dla granej pieśni — na żywo na ekranie). Treść pieśni edytuje się w aplikacji.
 */
export function ItemColumn({ itemId, planVersion, chords }: { itemId: string | null; planVersion: number; chords: boolean }) {
  const [item, setItem] = useState<ItemMessage | null>(null);

  useEffect(() => {
    if (!itemId) { setItem(null); return; }
    let current = true;
    void command("item", { itemId }).then((reply) => { if (current) setItem(reply?.item ?? null); });
    return () => { current = false; };
  }, [itemId, planVersion]);

  const parts = item?.parts ?? [];
  const saveLayout = async (layout: LayoutEntry[]) => {
    if (!item) return;
    // od razu w panelu; odpowiedź aplikacji (układ uzgodniony z treścią pieśni) go zastępuje —
    // przywróconej części panel jeszcze nie zna (wersy przyjdą w odpowiedzi), więc do niej nie wchodzi
    const byCode = new Map(parts.map((p) => [p.code, p] as [string, PartMessage]));
    setItem({
      ...item,
      parts: layout.flatMap((e) => { const part = byCode.get(e.code); return part ? [{ ...part, sung: e.sung }] : []; }),
    });
    const reply = await command("itemLayout", { itemId: item.itemId, layout });
    if (reply?.item) setItem(reply.item);
  };
  const change = (edit: (layout: LayoutEntry[]) => void) => {
    const layout = parts.map((p) => ({ code: p.code, sung: p.sung }));
    edit(layout);
    void saveLayout(layout);
  };
  const { listRef, start, rowClass } = useReorder((from, to) =>
    change((layout) => { const [moved] = layout.splice(from, 1); layout.splice(to, 0, moved); }));

  if (!item) {
    return (
      <section className="column item-column">
        <header className="column-head"><h2>Pieśń</h2></header>
        <p className="muted empty">{itemId ? "Wczytywanie…" : "Wybierz pozycję w planie."}</p>
      </section>
    );
  }

  const origin = item.origin === "local" ? (item.kind === "text" ? "własny tekst" : "własna pieśń")
    : item.origin === "modified" ? "zmieniona na tym komputerze" : null;
  const partActions = (part: PartMessage, index: number): MenuAction[] => [
    ...(item.canReorder ? [{
      label: "Powtórz",
      onSelect: () => change((layout) => layout.splice(index + 1, 0, { code: part.code, sung: part.sung })),
    }] : []),
    { label: "Usuń z układu", danger: true, onSelect: () => change((layout) => layout.splice(index, 1)) },
  ];

  return (
    <section className="column item-column">
      <header className="column-head">
        <h2>{kindTitle[item.kind] ?? "Pozycja"}</h2>
        {item.slideCount > 0 ? (
          <button onClick={() => void command("showItem", { itemId: item.itemId })}>Pokaż na ekranie</button>
        ) : null}
      </header>
      <div className="scroll">
        <div className="item-title">
          {item.number ? <span className="item-number">{item.number}</span> : null}
          <h3>{item.title}</h3>
        </div>
        {item.category || origin ? (
          <p className="item-meta">
            {item.category ? <span>{item.category}</span> : null}
            {origin ? <span className="badge">{origin}</span> : null}
          </p>
        ) : null}
        {item.note ? <p className="note">{item.note}</p> : null}
        {!item.parts ? (
          <p className="muted">{item.slideCount === 1 ? "1 slajd" : `Slajdy: ${item.slideCount}`}</p>
        ) : null}
        <div className="parts" ref={listRef}>
          {parts.map((part, index) => (
            <article key={`${part.code}-${index}`} data-row
                     className={`part${part.sung ? "" : " skipped"} ${rowClass(index, parts.length)}`}>
              <header className="part-head">
                {item.canReorder ? (
                  <span className="grip" onPointerDown={start(index)} aria-hidden="true"><GripIcon /></span>
                ) : null}
                <label className="part-name" htmlFor={`part-${index}`}>{part.name}</label>
                <Menu actions={partActions(part, index)} />
                <input id={`part-${index}`} type="checkbox" checked={part.sung} title={part.sung ? "Śpiewana" : "Pominięta"}
                       onChange={(e) => change((layout) => { layout[index].sung = e.target.checked; })} />
              </header>
              <Lines lines={part.lines} chords={chords && item.kind === "song"} />
            </article>
          ))}
        </div>
        {item.removed && item.removed.length > 0 ? (
          <div className="restore">
            <span className="muted small">Przywróć usuniętą część:</span>
            {item.removed.map((s) => (
              <button key={s.code} onClick={() => change((layout) => { layout.push({ code: s.code, sung: true }); })}>
                {s.name}
              </button>
            ))}
          </div>
        ) : null}
      </div>
    </section>
  );
}
