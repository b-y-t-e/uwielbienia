import icon from "../../remote/icon.png";

const RELEASES = "https://github.com/b-y-t-e/uwielbienia/releases/latest";

/** Strona startowa: dwie strony do sparowania z aplikacją (pilot, panel) i pobranie aplikacji. */
export function Landing() {
  return (
    <main className="landing">
      <header className="hero">
        <img className="logo" src={icon} alt="" width="72" height="72" />
        <h1>Uwielbienia</h1>
        <p className="lead">Teksty pieśni na rzutniku — śpiewnik, plany nabożeństw, prezentacje i filmy.</p>
      </header>

      <section className="cards">
        <a className="card" href="telefon/">
          <span className="card-icon" aria-hidden="true">
            <svg viewBox="0 0 24 24"><rect x="6.5" y="2.5" width="11" height="19" rx="2.5" /><path d="M10.5 18.5h3" /></svg>
          </span>
          <span className="card-title">Pilot w telefonie</span>
          <span className="card-text">Tekst z akordami, Dalej i Wstecz. Muzyk widzi bieżący i następny slajd.</span>
          <span className="card-go">Otwórz pilota →</span>
        </a>
        <a className="card" href="panel/">
          <span className="card-icon" aria-hidden="true">
            <svg viewBox="0 0 24 24"><rect x="2.5" y="4" width="19" height="13" rx="2" /><path d="M8 20.5h8M12 17v3.5M9 4v13M15 4v13" /></svg>
          </span>
          <span className="card-title">Panel na komputerze lub tablecie</span>
          <span className="card-text">Plany, plan, układ pieśni i to, co jest na ekranie — jak w oknie aplikacji.</span>
          <span className="card-go">Otwórz panel →</span>
        </a>
      </section>

      <p className="how">
        Parowanie: w aplikacji otwórz „Telefon”, wybierz „Telefon” albo „Panel” i zeskanuj kod aparatem.
        Sparowane urządzenie łączy się później samo.
      </p>

      <footer className="download">
        <a className="button primary" href={`${RELEASES}/download/Uwielbienia-win-Setup.exe`}>Pobierz aplikację dla Windows</a>
        <a className="button" href={RELEASES}>Linux i wszystkie wersje</a>
      </footer>
    </main>
  );
}
