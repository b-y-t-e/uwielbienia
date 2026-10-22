import { useEffect, useRef, useState } from "react";
import { command } from "../connection";
import type { SongHit } from "../protocol";
import { Dialog } from "./Dialog";
import { KindMark } from "./Icons";

/**
 * „Dodaj do planu”: numer albo słowa pieśni (wyszukiwarka aplikacji), Enter / kliknięcie dodaje w wybrane miejsce.
 * Nowe pieśni, prezentacje, filmy i kioski dodaje się w aplikacji (pliki są na tamtym komputerze).
 */
export function SearchDialog({ title, onPick, onClose }: {
  title: string; onPick: (hit: SongHit) => void; onClose: () => void;
}) {
  const [query, setQuery] = useState("");
  const [hits, setHits] = useState<SongHit[]>([]);
  const [active, setActive] = useState(0);
  const seq = useRef(0);

  useEffect(() => {
    const mine = ++seq.current;
    const timer = setTimeout(async () => {
      const reply = await command("search", { query });
      if (mine !== seq.current || !reply?.ok) return; // starsza odpowiedź po nowszym wpisaniu
      setHits(reply.results ?? []);
      setActive(0);
    }, 150);
    return () => clearTimeout(timer);
  }, [query]);

  const key = (e: React.KeyboardEvent) => {
    if (e.key === "ArrowDown") { setActive(Math.min(active + 1, hits.length - 1)); e.preventDefault(); }
    else if (e.key === "ArrowUp") { setActive(Math.max(active - 1, 0)); e.preventDefault(); }
    else if (e.key === "Enter" && hits[active]) { onPick(hits[active]); e.preventDefault(); }
  };

  return (
    <Dialog title={title} onClose={onClose}>
      <input autoFocus type="search" value={query} placeholder="Numer albo słowa pieśni" enterKeyHint="search"
             autoComplete="off" spellCheck={false} onChange={(e) => setQuery(e.target.value)} onKeyDown={key} />
      <ul className="results">
        {hits.map((hit, i) => (
          <li key={hit.songId}>
            <button className={i === active ? "active" : ""} onClick={() => onPick(hit)} onMouseEnter={() => setActive(i)}>
              <KindMark kind={hit.isText ? "text" : "song"} number={hit.number} />
              <span className="result-text">
                <span className="result-title">{hit.title}</span>
                {hit.firstLine && hit.firstLine !== hit.title ? <span className="result-first">{hit.firstLine}</span> : null}
              </span>
            </button>
          </li>
        ))}
      </ul>
      {hits.length === 0 ? <p className="muted">Nic nie znaleziono.</p> : null}
    </Dialog>
  );
}
