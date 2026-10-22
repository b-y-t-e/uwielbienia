import { useEffect, useRef, useState } from "react";
import { MoreIcon } from "./Icons";

export interface MenuAction {
  label: string;
  onSelect: () => void;
  danger?: boolean;
  disabled?: boolean;
}

/** Przycisk „⋯” z listą czynności (też prawy klik na wierszu — <c>openAt</c>). Zamyka się kliknięciem obok i Esc. */
export function Menu({ actions, label = "Więcej" }: { actions: MenuAction[]; label?: string }) {
  const [open, setOpen] = useState(false);
  const ref = useRef<HTMLDivElement>(null);
  useEffect(() => {
    if (!open) return;
    const close = (e: Event) => {
      if (e instanceof KeyboardEvent ? e.key === "Escape" : !ref.current?.contains(e.target as Node)) setOpen(false);
    };
    document.addEventListener("pointerdown", close);
    document.addEventListener("keydown", close);
    return () => { document.removeEventListener("pointerdown", close); document.removeEventListener("keydown", close); };
  }, [open]);
  return (
    <div className="menu" ref={ref}>
      <button className="icon-button" aria-label={label} aria-expanded={open}
              onClick={(e) => { e.stopPropagation(); setOpen(!open); }}>
        <MoreIcon />
      </button>
      {open ? (
        <div className="menu-list" role="menu">
          {actions.map((a) => (
            <button key={a.label} role="menuitem" className={a.danger ? "danger-text" : ""} disabled={a.disabled}
                    onClick={(e) => { e.stopPropagation(); setOpen(false); a.onSelect(); }}>
              {a.label}
            </button>
          ))}
        </div>
      ) : null}
    </div>
  );
}
