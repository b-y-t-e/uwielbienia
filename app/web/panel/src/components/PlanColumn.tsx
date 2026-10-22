import { useState } from "react";
import { command } from "../connection";
import type { PlanItemMessage, PlanMessage, SongHit } from "../protocol";
import { useReorder } from "../reorder";
import { Confirm } from "./Dialog";
import { GripIcon, KindMark, PlusIcon } from "./Icons";
import { Menu } from "./Menu";
import { SearchDialog } from "./SearchDialog";

/**
 * Kolumna „Plan”: klik wybiera pozycję do kolumny „Pieśń”, dwuklik (albo „Pokaż na ekranie”) pokazuje ją na ekranie,
 * uchwyt ⠿ zmienia kolejność, „+” i „Dodaj powyżej / poniżej” dodają pieśń lub tekst.
 */
export function PlanColumn({ plan, liveItemId, selectedId, onSelect }: {
  plan: PlanMessage | null;
  liveItemId: string | null;
  selectedId: string | null;
  onSelect: (id: string) => void;
}) {
  const items = plan?.items ?? [];
  const [addAt, setAddAt] = useState<number | null>(null);
  const [removing, setRemoving] = useState<PlanItemMessage | null>(null);
  const { listRef, start, rowClass } = useReorder((from, to) =>
    void command("planMove", { itemId: items[from].id, index: to }));

  const show = (item: PlanItemMessage) => {
    onSelect(item.id);
    if (item.slideCount > 0) void command("showItem", { itemId: item.id });
  };
  const add = async (hit: SongHit) => {
    const index = addAt ?? items.length;
    setAddAt(null);
    const reply = await command("planAdd", { songId: hit.songId, index });
    if (reply?.added) onSelect(reply.added.id);
  };
  const selectedIndex = items.findIndex((i) => i.id === selectedId);

  return (
    <section className="column plan-column">
      <header className="column-head">
        <h2>Plan</h2>
        <button className="icon-button" aria-label="Dodaj do planu" title="Dodaj do planu (za zaznaczoną pozycją)"
                onClick={() => setAddAt(selectedIndex >= 0 ? selectedIndex + 1 : items.length)}>
          <PlusIcon />
        </button>
      </header>
      <div className="scroll">
        <div className="plan-list" ref={listRef}>
          {items.map((item, index) => (
            <div key={item.id} data-row
                 className={`plan-item kind-${item.kind}${item.id === selectedId ? " selected" : ""}${item.id === liveItemId ? " live" : ""} ${rowClass(index, items.length)}`}
                 onClick={() => onSelect(item.id)} onDoubleClick={() => show(item)}>
              <span className="grip" onPointerDown={start(index)} onClick={(e) => e.stopPropagation()} aria-hidden="true">
                <GripIcon />
              </span>
              <KindMark kind={item.kind} number={item.number} />
              <span className="plan-item-text">
                <span className="plan-item-title">{item.title}</span>
                {item.note ? <span className="note">{item.note}</span> : null}
              </span>
              <Menu actions={[
                { label: "Pokaż na ekranie", onSelect: () => show(item), disabled: item.slideCount === 0 },
                { label: "Dodaj powyżej", onSelect: () => setAddAt(index) },
                { label: "Dodaj poniżej", onSelect: () => setAddAt(index + 1) },
                { label: "Duplikuj", onSelect: () => void command("planDuplicate", { itemId: item.id }) },
                { label: "Usuń z planu", onSelect: () => setRemoving(item), danger: true },
              ]} />
            </div>
          ))}
        </div>
        {plan && items.length === 0 ? <p className="muted empty">Plan jest pusty. Dodaj pieśń przyciskiem „+”.</p> : null}
      </div>
      {addAt !== null ? <SearchDialog title="Dodaj do planu" onPick={add} onClose={() => setAddAt(null)} /> : null}
      {removing ? (
        <Confirm text={`Usunąć „${removing.title}” z planu?`} action="Usuń z planu"
                 onConfirm={() => { void command("planRemove", { itemId: removing.id }); setRemoving(null); }}
                 onCancel={() => setRemoving(null)} />
      ) : null}
    </section>
  );
}
