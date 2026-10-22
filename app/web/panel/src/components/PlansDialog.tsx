import { useEffect, useState } from "react";
import { command } from "../connection";
import type { PlanSummaryMessage, PlansMessage } from "../protocol";
import { Confirm, Dialog } from "./Dialog";

const formatDate = (date?: string | null) =>
  date ? new Date(`${date}T12:00`).toLocaleDateString("pl-PL", { weekday: "short", day: "numeric", month: "short", year: "numeric" }) : null;

const isFew = (n: number) => n % 10 >= 2 && n % 10 <= 4 && (n % 100 < 12 || n % 100 > 14);

const countText = (n: number) =>
  n === 0 ? "bez pozycji" : n === 1 ? "1 pozycja" : `${n} ${isFew(n) ? "pozycje" : "pozycji"}`;

const today = () => new Date().toLocaleDateString("sv-SE"); // RRRR-MM-DD

/** Okno „Plany” jak w aplikacji: zakładki Wydarzenia / Szablony, lista po lewej, formularz po prawej. */
export function PlansDialog({ plans, onClose }: { plans: PlansMessage | null; onClose: () => void }) {
  const openPlan = plans ? [...plans.events, ...plans.templates].find((p) => p.isOpen) : undefined;
  const [templates, setTemplates] = useState(openPlan?.kind === "template");
  const [selectedId, setSelectedId] = useState<string | null>(openPlan?.id ?? null);
  const [creating, setCreating] = useState(false);
  const [name, setName] = useState("");
  const [date, setDate] = useState("");
  const [confirmDelete, setConfirmDelete] = useState(false);

  useEffect(() => { void command("plans"); }, []);

  const list = (templates ? plans?.templates : plans?.events) ?? [];
  const selected: PlanSummaryMessage | undefined = list.find((p) => p.id === selectedId) ?? (creating ? undefined : list[0]);

  useEffect(() => {
    if (creating) return;
    setName(selected?.name ?? "");
    setDate(selected?.date ?? "");
  }, [selected?.id, selected?.name, selected?.date, creating]);

  const valid = name.trim().length > 0 && (templates || date.length > 0);
  const dirty = !!selected && (name.trim() !== selected.name || (!templates && date !== (selected.date ?? "")));

  const switchTab = (t: boolean) => { setTemplates(t); setCreating(false); setSelectedId(null); };
  const startNew = () => { setCreating(true); setSelectedId(null); setName(""); setDate(templates ? "" : today()); };

  const create = async () => {
    if (!valid) return;
    const reply = await command("planCreate", { name: name.trim(), date: templates ? null : date, kind: templates ? "template" : "event" });
    if (reply?.ok) onClose();
  };
  const save = () => selected && command("planRename", { planId: selected.id, name: name.trim(), date: templates ? null : date });
  const open = async () => {
    if (!selected) return;
    if (!selected.isOpen) await command("planOpen", { planId: selected.id });
    onClose();
  };
  const copyToToday = async () => {
    if (!selected) return;
    const reply = await command("planCopyToToday", { planId: selected.id });
    if (reply?.ok) onClose();
  };
  const saveAsTemplate = async () => {
    if (!selected) return;
    const reply = await command("planSaveAsTemplate", { planId: selected.id });
    if (reply?.ok) { setTemplates(true); setSelectedId(null); }
  };
  const remove = async () => {
    setConfirmDelete(false);
    if (selected) await command("planDelete", { planId: selected.id });
    setSelectedId(null);
  };

  return (
    <Dialog title="Plany" onClose={onClose} wide>
      <div className="plans-bar">
        <div className="segmented">
          <button className={templates ? "" : "active"} onClick={() => switchTab(false)}>Wydarzenia</button>
          <button className={templates ? "active" : ""} onClick={() => switchTab(true)}>Szablony</button>
        </div>
        <button className="primary" onClick={startNew}>{templates ? "+ Nowy szablon" : "+ Nowe wydarzenie"}</button>
      </div>
      <div className="plans-body">
        <ul className="plans-list scroll">
          {list.map((p) => (
            <li key={p.id}>
              <button className={`plan-row${p.id === selected?.id && !creating ? " selected" : ""}`}
                      onClick={() => { setCreating(false); setSelectedId(p.id); }} onDoubleClick={open}>
                <span className="plan-row-name">{p.name}{p.isOpen ? <span className="badge">otwarty</span> : null}</span>
                <span className="muted small">{[formatDate(p.date), countText(p.count)].filter(Boolean).join(" · ")}</span>
              </button>
            </li>
          ))}
          {list.length === 0 ? (
            <li className="muted">{templates
              ? "Nie ma jeszcze szablonów. Szablon to stały zestaw pieśni bez daty — tworzy się z niego kolejne wydarzenia."
              : "Nie ma jeszcze wydarzeń. Wydarzenie to plan pieśni na konkretny dzień."}</li>
          ) : null}
        </ul>
        {creating || selected ? (
          <form className="plan-form" onSubmit={(e) => { e.preventDefault(); void (creating ? create() : save()); }}>
            <h3>{creating ? (templates ? "Nowy szablon" : "Nowe wydarzenie") : templates ? "Szablon" : "Wydarzenie"}</h3>
            <label>Nazwa<input value={name} placeholder="Wpisz nazwę" onChange={(e) => setName(e.target.value)} autoFocus={creating} /></label>
            {templates ? null : <label>Data<input type="date" value={date} onChange={(e) => setDate(e.target.value)} /></label>}
            {creating ? (
              <div className="form-buttons">
                <button type="button" onClick={() => setCreating(false)}>Anuluj</button>
                <button type="submit" className="primary" disabled={!valid}>Utwórz i otwórz</button>
              </div>
            ) : (
              <>
                <div className="form-buttons">
                  {dirty ? <button type="submit" disabled={!valid}>Zapisz</button> : null}
                  <button type="button" className="primary" onClick={open}>
                    {selected?.isOpen ? "Zamknij okno" : templates ? "Edytuj pieśni" : "Otwórz"}
                  </button>
                </div>
                <div className="form-buttons secondary">
                  <button type="button" onClick={copyToToday}>{templates ? "Utwórz wydarzenie na dziś" : "Kopia na dziś"}</button>
                  {templates ? null : <button type="button" onClick={saveAsTemplate}>Zapisz jako szablon</button>}
                  <button type="button" className="danger-text" disabled={selected?.isOpen}
                          title={selected?.isOpen ? "Otwartego planu nie można usunąć" : undefined}
                          onClick={() => setConfirmDelete(true)}>Usuń</button>
                </div>
              </>
            )}
          </form>
        ) : <p className="muted plan-form">Wybierz plan z listy.</p>}
      </div>
      {confirmDelete && selected ? (
        <Confirm text={`Usunąć „${selected.name}”? Tego nie da się cofnąć.`} action="Usuń plan"
                 onConfirm={remove} onCancel={() => setConfirmDelete(false)} />
      ) : null}
    </Dialog>
  );
}
