import { useEffect, useState } from "react";
import icon from "../../remote/icon.png";

const RELEASES = "https://github.com/b-y-t-e/uwielbienia/releases/latest";

interface Line {
  text: string;
  /** Słowa powtarzające tytuł pieśni — na rzutniku w kolorze tytułu. */
  title?: string;
  repeat?: number;
}

// Pieśń 47 ze śpiewnika, w kolejności z pliku (refren, zwrotka, refren) — tak, jak pokazuje ją rzutnik.
const SLIDES: Line[][] = [
  [{ title: "Jezus mój Pan", text: "" }, { text: "Jezus mój Król" }, { text: "Jezus Zbawiciel mój", repeat: 2 }],
  [
    { text: "On mój przyjaciel, On wierny Bóg" },
    { text: "umarł na krzyżu, zmartwychwstał, to wielki cud" },
    { text: "On mą Opoką, On zbawił mnie" },
    { text: "On mą pomocą, prowadzi mnie" },
    { text: "do nieba, do nieba, do nieba bram", repeat: 2 },
  ],
  [{ text: "Jezus mój Pan" }, { text: "Jezus mój Król" }, { text: "Jezus Zbawiciel mój", repeat: 2 }],
];

/** Kolejne slajdy z przenikaniem, jak na rzutniku; bez ruchu, gdy system prosi o ograniczenie animacji. */
function useSlide(count: number, interval: number) {
  const [index, setIndex] = useState(0);
  useEffect(() => {
    if (matchMedia("(prefers-reduced-motion: reduce)").matches) return;
    const timer = setInterval(() => setIndex((i) => (i + 1) % count), interval);
    return () => clearInterval(timer);
  }, [count, interval]);
  return index;
}

function Projection() {
  const current = useSlide(SLIDES.length, 5200);
  return (
    <figure className="projection" aria-label="Tak wygląda pieśń na rzutniku">
      {SLIDES.map((lines, i) => (
        <div key={i} className={`slide${i === current ? " shown" : ""}`} aria-hidden={i !== current}>
          {lines.map((line, j) => (
            <p key={j}>
              {line.title ? <span className="slide-title-words">{line.title}</span> : null}
              {line.text}
              {line.repeat ? <span className="repeat">×{line.repeat}</span> : null}
            </p>
          ))}
        </div>
      ))}
    </figure>
  );
}

/** Strona startowa: czym jest program, dwie strony do połączenia z nim (pilot, panel) i pobranie. */
export function Landing() {
  return (
    <div className="page">
      <header className="bar">
        <a className="brand" href="./">
          <img src={icon} alt="" width="36" height="36" />
          Uwielbienia
        </a>
        <a className="download" href={`${RELEASES}/download/Uwielbienia-win-Setup.exe`}>Pobierz<span className="download-long"> dla Windows</span></a>
      </header>

      <main className="hero">
        <div className="intro">
          <h1>Pieśni na rzutniku, prowadzone z jednego miejsca</h1>
          <p className="lead">
            Śpiewnik z akordami, plan nabożeństwa, prezentacje i filmy w programie na komputerze przy rzutniku.
            Telefon muzyka i drugi komputer łączą się z nim przez tę stronę.
          </p>
        </div>
        <Projection />
      </main>

      <nav className="routes" aria-label="Połącz się z programem">
        <a className="route" href="telefon/">
          <svg className="glyph" viewBox="0 0 24 24" aria-hidden="true"><rect x="6.5" y="2.5" width="11" height="19" rx="2.5" /><path d="M10.5 18.5h3" /></svg>
          <span className="route-name">Pilot w telefonie</span>
          <span className="route-text">Bieżący i następny slajd z akordami, Dalej i Wstecz pod kciukiem.</span>
        </a>
        <a className="route" href="panel/">
          <svg className="glyph" viewBox="0 0 24 24" aria-hidden="true"><rect x="2.5" y="4" width="19" height="13" rx="2" /><path d="M8 20.5h8M12 17v3.5" /></svg>
          <span className="route-name">Panel na komputerze lub tablecie</span>
          <span className="route-text">Plany, kolejność pieśni, układ zwrotek i to, co widzi sala.</span>
        </a>
      </nav>

      <footer className="foot">
        <p>
          Pierwsze połączenie: w programie otwórz „Urządzenia” i zeskanuj kod albo wpisz krótki kod.
          Potem łączy się samo.
        </p>
        <a href={RELEASES}>Wersja dla Linuksa i poprzednie wydania</a>
      </footer>
    </div>
  );
}
