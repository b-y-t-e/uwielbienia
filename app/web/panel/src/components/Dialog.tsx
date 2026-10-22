import { useEffect } from "react";
import { CloseIcon } from "./Icons";

/** Nakładka jak w aplikacji: przyciemnione tło, panel na środku, Esc i ✕ zamykają. */
export function Dialog({ title, onClose, children, wide }: {
  title: string; onClose: () => void; children: React.ReactNode; wide?: boolean;
}) {
  useEffect(() => {
    const key = (e: KeyboardEvent) => { if (e.key === "Escape") onClose(); };
    document.addEventListener("keydown", key);
    return () => document.removeEventListener("keydown", key);
  }, [onClose]);
  return (
    <div className="scrim" onPointerDown={(e) => { if (e.target === e.currentTarget) onClose(); }}>
      <section className={`dialog${wide ? " wide" : ""}`} role="dialog" aria-label={title}>
        <header className="dialog-head">
          <h2>{title}</h2>
          <button className="icon-button" aria-label="Zamknij" onClick={onClose}><CloseIcon /></button>
        </header>
        {children}
      </section>
    </div>
  );
}

/** Potwierdzenie usuwania — jak w aplikacji, z czerwonym przyciskiem. */
export function Confirm({ text, action, onConfirm, onCancel }: {
  text: string; action: string; onConfirm: () => void; onCancel: () => void;
}) {
  return (
    <Dialog title={action} onClose={onCancel}>
      <p>{text}</p>
      <div className="dialog-buttons">
        <button onClick={onCancel}>Anuluj</button>
        <button className="danger" onClick={onConfirm} autoFocus>{action}</button>
      </div>
    </Dialog>
  );
}
