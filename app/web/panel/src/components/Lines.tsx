import type { LineMessage } from "../protocol";

/** Wersy jak w śpiewniku: akordy nad wersem (gdy włączone), refren kursywą, „×N” przy powtarzanych. */
export function Lines({ lines, chords, className }: { lines: LineMessage[]; chords: boolean; className?: string }) {
  return (
    <div className={`lines ${className ?? ""}`}>
      {lines.map((line, i) => (
        <div key={i} className={`line${line.chorus ? " chorus" : ""}${line.partStart ? " part-start" : ""}`}>
          {chords && line.chords ? <div className="chords">{line.chords}</div> : null}
          {line.text || (chords && line.chords) ? (
            <div className="text">
              {line.text}
              {line.repeat > 1 ? <span className="rep"> ×{line.repeat}</span> : null}
            </div>
          ) : null}
        </div>
      ))}
    </div>
  );
}
