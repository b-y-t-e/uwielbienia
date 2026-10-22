import { useEffect, useState } from "react";
import { command, type Snapshot } from "../connection";
import { Lines } from "./Lines";
import { PauseIcon, PlayIcon } from "./Icons";

const formatTime = (seconds: number) => {
  const s = Math.max(0, Math.floor(seconds));
  const h = Math.floor(s / 3600), m = Math.floor((s % 3600) / 60), r = String(s % 60).padStart(2, "0");
  return h > 0 ? `${h}:${String(m).padStart(2, "0")}:${r}` : `${m}:${r}`;
};

/**
 * Kolumna „Na ekranie”: to, co widzi sala. Slajdy pozycji (klik = ten slajd), bieżący z akordami, następny,
 * Wstecz / Dalej, czarny ekran; przy filmie — czas, pauza, ±15 s; przy kiosku — bieżący element.
 */
export function LiveColumn({ snapshot, chords }: { snapshot: Snapshot; chords: boolean }) {
  const { state, video, kiosk } = snapshot;
  const hasItem = !!state?.itemId || !!state?.songId;
  const kioskShown = !!state?.isKiosk && !!kiosk && kiosk.itemId === state.itemId;

  return (
    <section className="column live-column">
      <header className="column-head">
        <h2>Na ekranie</h2>
        {state?.isBlank ? <span className="badge candle">Czarny ekran</span> : null}
      </header>
      <div className="scroll">
        {!hasItem ? <p className="muted empty">Ekran jest pusty. Dwuklik w planie pokazuje pozycję.</p> : (
          <>
            <div className="item-title live-title">
              {state!.number ? <span className="item-number">{state!.number}</span> : null}
              <h3>{state!.title}</h3>
            </div>
            {state!.slideLabels.length > 1 ? (
              <div className="slide-chips">
                {state!.slideLabels.map((label, i) => (
                  <button key={i} className={i === state!.slideIndex ? "active" : ""}
                          onClick={() => void command("goto", { slide: i })}>{label}</button>
                ))}
              </div>
            ) : null}
            <div className={`current-slide${state!.isBlank ? " blank" : ""}`}>
              {state!.label && !state!.isVideo && !state!.isKiosk ? <div className="slide-label">{state!.label}</div> : null}
              {state!.isVideo ? <VideoControls video={video} /> : null}
              {kioskShown ? (
                <div className="kiosk">
                  {kiosk!.image ? <img src={kiosk!.image} alt="" /> : null}
                  {kiosk!.title ? <div className="kiosk-title">{kiosk!.title}</div> : null}
                  {kiosk!.text ? <p>{kiosk!.text}</p> : null}
                  <div className="muted small">Element {kiosk!.index + 1} z {kiosk!.count}{kiosk!.isRunning || kiosk!.count < 2 ? "" : " · zatrzymany"}</div>
                </div>
              ) : null}
              {!state!.isVideo && !state!.isKiosk ? <Lines lines={state!.lines} chords={chords} className="live-lines" /> : null}
            </div>
            {state!.nextLines?.length || state!.next ? (
              <div className="upcoming">
                <div className="slide-label">Następny{state!.next ? `: ${state!.next}` : ""}</div>
                {state!.nextLines ? <Lines lines={state!.nextLines.slice(0, 3)} chords={false} /> : null}
              </div>
            ) : null}
          </>
        )}
      </div>
      {hasItem ? (
        <footer className="live-controls">
          <button onClick={() => void command("prev")}>Wstecz</button>
          <button className={state?.isBlank ? "candle-outline" : ""} onClick={() => void command("blank")}
                  title="Czarny ekran (tekst zostaje, sala go nie widzi)">
            {state?.isBlank ? "Pokaż tekst" : "Czarny ekran"}
          </button>
          <button className="primary" onClick={() => void command("next")}>Dalej</button>
        </footer>
      ) : null}
    </section>
  );
}

function VideoControls({ video }: { video: Snapshot["video"] }) {
  const [, tick] = useState(0);
  // między wiadomościami (co sekundę) czas grającego filmu płynie lokalnie
  useEffect(() => {
    if (!video?.isPlaying) return;
    const timer = setInterval(() => tick((t) => t + 1), 250);
    return () => clearInterval(timer);
  }, [video?.isPlaying]);

  const v = video ?? { isPlaying: false, position: 0, duration: 0, receivedAt: 0, error: null, pictureOffsetMs: 0 };
  const elapsed = v.isPlaying ? (performance.now() - v.receivedAt) / 1000 : 0;
  const position = Math.min(v.duration || Infinity, v.position + elapsed);
  const ms = v.pictureOffsetMs ?? 0;
  const offset = `${ms > 0 ? "+" : ms < 0 ? "−" : ""}${(Math.abs(ms) / 1000).toFixed(1).replace(".", ",")} s`;
  return (
    <div className="video">
      <div className="video-clock">{formatTime(position)}{v.duration ? <span className="muted"> / {formatTime(v.duration)}</span> : null}</div>
      <div className="video-progress"><div style={{ width: v.duration ? `${(100 * position) / v.duration}%` : "0%" }} /></div>
      <div className="video-buttons">
        <button onClick={() => void command("videoSkip", { seconds: -15 })}>−15 s</button>
        <button className="video-toggle" aria-label={v.isPlaying ? "Pauza" : "Odtwórz"} onClick={() => void command("videoToggle")}>
          {v.isPlaying ? <PauseIcon /> : <PlayIcon />}
        </button>
        <button onClick={() => void command("videoSkip", { seconds: 15 })}>+15 s</button>
      </div>
      <div className={`video-offset${ms ? " set" : ""}`}>
        Obraz
        <button aria-label="Obraz wcześniej o 0,1 s" onClick={() => void command("videoOffset", { milliseconds: -100 })}>‹</button>
        <b>{offset}</b>
        <button aria-label="Obraz później o 0,1 s" onClick={() => void command("videoOffset", { milliseconds: 100 })}>›</button>
      </div>
      {v.error ? <p className="error">{v.error}</p> : null}
    </div>
  );
}
