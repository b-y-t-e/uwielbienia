import { useEffect, useState } from "react";
import { command, connect, useConnection, type Snapshot } from "./connection";
import { useStored, useTheme } from "./settings";
import { ItemColumn } from "./components/ItemColumn";
import { LiveColumn } from "./components/LiveColumn";
import { MoonIcon, SunIcon } from "./components/Icons";
import { PlanColumn } from "./components/PlanColumn";
import { PlansDialog } from "./components/PlansDialog";

type Column = "plan" | "item" | "live";

const statusText: Record<string, string> = {
  connecting: "Łączenie…", connected: "Połączono", failed: "Brak połączenia", pairing: "Niesparowany",
};

/**
 * Panel: okno operatora na drugim komputerze lub tablecie — Plan · Pieśń · Na ekranie (wąski ekran: jedna kolumna
 * naraz, przełączana na dole). Stan zawsze z aplikacji; panel tylko wysyła polecenia.
 */
export function App() {
  const snapshot = useConnection();
  if (snapshot.status === "pairing") return <Pairing error={snapshot.pairingError} />;
  if (snapshot.device && !snapshot.device.isPanel) return <NotPanel />;
  return <Panel snapshot={snapshot} />;
}

function Panel({ snapshot }: { snapshot: Snapshot }) {
  const { plan, state } = snapshot;
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [plansOpen, setPlansOpen] = useState(false);
  const [column, setColumn] = useState<Column>("plan");
  const [chords, setChords] = useStored<boolean>("panel-akordy", true);
  const [theme, toggleTheme] = useTheme();

  // zaznaczenie znikło z planu (usunięte, inny plan) — pierwsza pozycja
  const items = plan?.items ?? [];
  useEffect(() => {
    if (!items.some((i) => i.id === selectedId)) setSelectedId(items[0]?.id ?? null);
  }, [plan?.id, items.map((i) => i.id).join()]); // eslint-disable-line react-hooks/exhaustive-deps

  // klawiatura jak w aplikacji: strzałki, PageUp / PageDown, spacja; nie w polach tekstowych i oknach
  useEffect(() => {
    const key = (e: KeyboardEvent) => {
      if (plansOpen || e.target instanceof HTMLInputElement || document.querySelector(".scrim")) return;
      if (["ArrowRight", "ArrowDown", "PageDown", " "].includes(e.key)) void command(e.ctrlKey ? "nextItem" : "next");
      else if (["ArrowLeft", "ArrowUp", "PageUp"].includes(e.key)) void command(e.ctrlKey ? "prevItem" : "prev");
      else if (e.key === ".") void command("blank");
      else return;
      e.preventDefault();
    };
    document.addEventListener("keydown", key);
    return () => document.removeEventListener("keydown", key);
  }, [plansOpen]);

  const date = plan?.date
    ? new Date(`${plan.date}T12:00`).toLocaleDateString("pl-PL", { day: "numeric", month: "long", year: "numeric" })
    : null;

  return (
    <div className="app">
      <header className="topbar">
        <button className="plan-name" onClick={() => setPlansOpen(true)} title="Plany — wydarzenia i szablony">
          <span className="plan-name-text">{plan?.name ?? "Brak planu"}</span>
          {date && !plan?.name?.includes(date) ? <span className="muted">{date}</span> : null}
          {plan?.planKind === "template" ? <span className="badge">szablon</span> : null}
          <span className="chevron">▾</span>
        </button>
        <span className="spacer" />
        <span className="status" data-state={snapshot.status}>{statusText[snapshot.status]}</span>
        <button className={`toggle${chords ? " on" : ""}`} aria-pressed={chords} onClick={() => setChords(!chords)}>Akordy</button>
        <button className="icon-button" aria-label={theme === "dark" ? "Jasny wygląd" : "Ciemny wygląd"} onClick={toggleTheme}>
          {theme === "dark" ? <SunIcon /> : <MoonIcon />}
        </button>
      </header>

      <main className={`columns show-${column}`}>
        <PlanColumn plan={plan} liveItemId={state?.itemId ?? null} selectedId={selectedId}
                    onSelect={(id) => setSelectedId(id)} />
        <ItemColumn itemId={selectedId} planVersion={snapshot.planVersion} chords={chords} />
        <LiveColumn snapshot={snapshot} chords={chords} />
      </main>

      <nav className="tabs" aria-label="Kolumny">
        <button className={column === "plan" ? "active" : ""} onClick={() => setColumn("plan")}>Plan</button>
        <button className={column === "item" ? "active" : ""} onClick={() => setColumn("item")}>Pieśń</button>
        <button className={column === "live" ? "active" : ""} onClick={() => setColumn("live")}>Na ekranie</button>
      </nav>

      {snapshot.error ? <div className="toast" role="alert">{snapshot.error}</div> : null}
      {plansOpen ? <PlansDialog plans={snapshot.plans} onClose={() => setPlansOpen(false)} /> : null}
    </div>
  );
}

function Pairing({ error }: { error: string | null }) {
  const [code, setCode] = useState("");
  return (
    <main className="pairing">
      <h1 className="brand">Uwielbienia — panel</h1>
      <p className="muted">
        W aplikacji otwórz „Telefon”, wybierz „Panel” i zeskanuj kod albo otwórz adres spod kodu na tym urządzeniu.
      </p>
      <form onSubmit={(e) => { e.preventDefault(); if (code.trim()) void connect(code.trim()); }}>
        <label htmlFor="code">Albo wklej kod parowania</label>
        <input id="code" value={code} autoComplete="off" spellCheck={false} placeholder="kod z okna „Telefon” → „Panel”"
               onChange={(e) => setCode(e.target.value)} />
        <button type="submit" className="primary">Połącz</button>
      </form>
      {error ? <p className="error" role="alert">{error}</p> : null}
    </main>
  );
}

function NotPanel() {
  return (
    <main className="pairing">
      <h1 className="brand">Uwielbienia — panel</h1>
      <p>To urządzenie jest połączone, ale nie ma uprawnienia „Panel”.</p>
      <p className="muted">W aplikacji otwórz „Telefon” i zaznacz „Panel” przy tym urządzeniu (nazwa „panel”).</p>
    </main>
  );
}
