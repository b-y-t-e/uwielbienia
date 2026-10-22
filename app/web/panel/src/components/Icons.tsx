// Ikony jak w aplikacji: proste kreski w kolorze tekstu.
const svg = (path: React.ReactNode, filled = false) => (
  <svg viewBox="0 0 24 24" aria-hidden="true" fill={filled ? "currentColor" : "none"} stroke={filled ? "none" : "currentColor"}
       strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">{path}</svg>
);

export const PlusIcon = () => svg(<path d="M12 5v14M5 12h14" />);
export const MoreIcon = () => svg(<><circle cx="5" cy="12" r="1.6" /><circle cx="12" cy="12" r="1.6" /><circle cx="19" cy="12" r="1.6" /></>, true);
export const GripIcon = () => svg(<><circle cx="9" cy="6" r="1.4" /><circle cx="15" cy="6" r="1.4" /><circle cx="9" cy="12" r="1.4" /><circle cx="15" cy="12" r="1.4" /><circle cx="9" cy="18" r="1.4" /><circle cx="15" cy="18" r="1.4" /></>, true);
export const SunIcon = () => svg(<><circle cx="12" cy="12" r="4.5" /><path d="M12 2v2M12 20v2M2 12h2M20 12h2M4.9 4.9l1.4 1.4M17.7 17.7l1.4 1.4M4.9 19.1l1.4-1.4M17.7 6.3l1.4-1.4" /></>);
export const MoonIcon = () => svg(<path d="M20 14.5A8 8 0 0 1 9.5 4a8 8 0 1 0 10.5 10.5z" />);
export const CloseIcon = () => svg(<path d="M6 6l12 12M18 6L6 18" />);
export const PlayIcon = () => <svg viewBox="0 0 16 16" aria-hidden="true" fill="currentColor"><path d="M4.5,2 L14,8 L4.5,14 Z" /></svg>;
export const PauseIcon = () => <svg viewBox="0 0 16 16" aria-hidden="true" fill="currentColor"><path d="M3,2 L5.6,2 L5.6,14 L3,14 Z M10.4,2 L13,2 L13,14 L10.4,14 Z" /></svg>;
export const SlideIcon = () => svg(<><rect x="3.5" y="5" width="17" height="12" rx="1.5" /><path d="M9 21h6M12 17v4" /></>);
export const KioskIcon = () => svg(<><rect x="3" y="7" width="14" height="11" rx="1.5" /><path d="M7 4h12.5A1.5 1.5 0 0 1 21 5.5V15" /></>);

/** Znacznik pozycji jak w planie aplikacji: numer pieśni, kropka tekstu, znak slajdu / filmu / kiosku. */
export function KindMark({ kind, number }: { kind: string; number?: number | null }) {
  switch (kind) {
    case "song": return <span className="mark number">{number ?? ""}</span>;
    case "text": return <span className="mark text-dot">•</span>;
    case "presentation": return <span className="mark media"><SlideIcon /></span>;
    case "video": return <span className="mark media"><PlayIcon /></span>;
    case "kiosk": return <span className="mark media"><KioskIcon /></span>;
    default: return <span className="mark" />;
  }
}
