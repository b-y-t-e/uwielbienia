# Aplikacja „Uwielbienia” — rzutnik tekstów pieśni

Projekt i scenariusze: [`../docs/projekt-aplikacji.md`](../docs/projekt-aplikacji.md).

## Uruchomienie

```
dotnet run --project app/src/Uwielbienia.Desktop   # albo: python run.py
```

Wymaga .NET 10 SDK. Pieśni są wbudowane w aplikację; uruchomiona z repozytorium czyta od razu
folder `Teksty/`, więc poprawki tekstów widać bez przebudowy.

Dane użytkownika (plany, ustawienia, sparowane urządzenia, własne pieśni i teksty w `teksty/`,
prezentacje w `prezentacje/`): `%APPDATA%\Uwielbienia` (Linux: `~/.config/Uwielbienia`).

**Plan, pieśni i teksty:** do planu dodaje się z planu — „+” w nagłówku albo prawy klik na pozycji
(„Dodaj powyżej / poniżej”, „Duplikuj”, „Pokaż na ekranie”, „Usuń z planu”). Okno „Dodaj do planu” wyszukuje
pieśni i teksty i pozwala utworzyć nową pieśń albo tekst (część Mszy, modlitwa, ogłoszenie — bez
akordów). „Edytuj” w kolumnie „Pieśń” zmienia wybraną. Zapis jest tylko na tym komputerze; zmieniona pieśń
śpiewnika ma „Przywróć oryginał”.

**Prezentacje i obrazy:** „Prezentacja” (albo „Obraz” — każdy obraz osobną pozycją) w oknie „Dodaj do planu” (albo upuszczenie pliku na plan) dodaje
plik PowerPoint (`.pptx`, `.pps`, `.ppt`…) albo obrazy slajdów. Plik jest kopiowany do danych
aplikacji, a slajdy przygotowuje w tle zainstalowany PowerPoint (Windows) — postęp widać w planie. Animacje
„po kliknięciu” (`.pptx`) stają się osobnymi slajdami — każde kliknięcie pilota pokazuje następny stan.
Bez PowerPointa można dodać slajdy zapisane jako obrazy.

**Filmy:** „Film” (albo upuszczenie pliku na plan) dodaje film w dowolnym formacie, który zna
VLC (`.mp4`, `.mkv`, `.avi`, `.mov`, `.wmv`, `.webm`…). Plik nie jest kopiowany — plan go tylko
wskazuje, więc musi zostać na swoim miejscu (brak pliku widać w planie). Film gra, gdy jest na
ekranie (sterowanie — rozdz. „Sterowanie” niżej). Dźwięk idzie na domyślne urządzenie systemu,
obraz podlega „Dopasuj obraz”. VLC jest w instalacji Windows; na Linuksie trzeba go zainstalować
(`sudo apt install vlc`).

**Dopasowanie obrazu do ekranu w sali** (jak w pps_viewer) — myszą wprost na ekranie projekcji
(korekcja trapezu, zmniejszenie, przesunięcie); obsługa — rozdz. „Sterowanie” niżej.

## Okno operatora

Trzy kolumny (szerokość regulowana przeciąganiem, zapamiętywana):

| Kolumna | Zawartość | Kod |
|---|---|---|
| **Plan** | pieśni, teksty, prezentacje i filmy: klik wybiera, dwuklik pokazuje, przeciąganie zmienia kolejność | `PlanViewModel` |
| **Pieśń** | pozycja zaznaczona w planie: tekst, akordy, pola wyboru części (na żywo dla granej), edycja; slajdy prezentacji | `PreviewViewModel`, `SongEditorViewModel`, `PresentationViewModel` |
| **Na ekranie** | to, co widzi sala: postęp, bieżąca i następna część z akordami, Wstecz / Dalej | `LiveViewModel` |

Plany (wydarzenia i szablony) zarządza się w oknie „Plany” po kliknięciu nazwy planu.

## Sterowanie

Klawisze, pilot, mysz na ekranie projekcji i „Dopasuj obraz”: [docs/projekt-aplikacji.md, rozdz. 5](../docs/projekt-aplikacji.md#5-obsługa-sterowanie).

## Struktura

| Projekt | Rola |
|---|---|
| `src/Uwielbienia.Core` | model pieśni, parser `piesn.md`, wyszukiwanie, plany, slajdy, `LiveSession` (jedyne źródło prawdy o ekranie) |
| `src/Uwielbienia.Link` | tailcat-link: protokół JSON i serwer dla telefonu / przeglądarki |
| `src/Uwielbienia.App` | interfejs Avalonia: widoki, modele widoków, motywy, czcionki, korzeń kompozycji (`AppComposition`) |
| `src/Uwielbienia.Desktop` | program dla Windows i Linuksa |
| `tests/Uwielbienia.Core.Tests` | testy (m.in. parsowanie wszystkich pieśni z `Teksty/`) |
| `tools/Uwielbienia.Screenshots` | renderuje okna do PNG bez wyświetlania — do przeglądu wyglądu |
| `web/remote` | strona pilota i ekranu w przeglądarce (`greysource.eu/uwielbienie/telefon`) |
| `web/landing` | strona startowa `greysource.eu/uwielbienie/` z linkami do pilota i panelu (React + Vite) |
| `web/panel` | panel na drugim komputerze / tablecie (`greysource.eu/uwielbienie/panel`, React + Vite) |

## Testy i zrzuty

```
dotnet test app
dotnet run --project app/tools/Uwielbienia.Screenshots -- <katalog> [light]
```

## Budowanie i wydanie

Skrypty w głównym folderze repozytorium:

```
python build.py        # testy + Windows (.exe) + Linux (.tar.gz) + Velopack + strona i panel → release/
python deploy.py       # nowa wersja: build, strona na FTP, tag vX.Y.Z, GitHub Release
python deploy.py --site-only     # tylko strona pilota
python deploy.py --github-only   # dokończenie GitHub Release z plików w release/
```

Wszystko buduje się na tym komputerze (Windows) — GitHub niczego nie buduje. `deploy.py` podbija
`version.txt`, buduje (`build.py`), wysyła stronę, wypycha commit i tag, a potem przez API GitHuba
tworzy release i wysyła do niego aplikacje oraz kanał aktualizacji Velopack (`release/velopack/`).
Gdy release się nie uda, `--github-only` go dokończy (pliki już wysłane pomija). `--no-site` /
`--no-github` pomijają stronę / release; `build.py --no-velopack` — instalator.

Wymagania: narzędzie `vpk` w wersji `VelopackVersion` z `Directory.Build.props`
(`dotnet tool update -g vpk --version X` — skrypty sprawdzają to przed zmianą wersji), Node.js dla
panelu `web/panel`. Dane: zmienne `FTP_HOST`, `FTP_USER`, `FTP_PASS` i `GITHUB_TOKEN` (token z
uprawnieniem Contents: write do repozytorium) albo plik `.env` w głównym folderze (w `.gitignore`).

## Aktualizacje (Windows)

Instaluj wydanie przez `Uwielbienia-win-Setup.exe` z GitHub Releases. Tylko ta instalacja
automatycznie sprawdza, pobiera i proponuje nowsze wersje z GitHub.

## Strona pilota

Pliki statyczne w `web/remote` (klient tailcat-link i tweetnacl skopiowane do `lib/`, bez kroku
budowania). `build.py` dokłada aktualną mapę serwerów pośredniczących (`derpmap.json`), która
musi leżeć w tej samej domenie co strona.

## Panel (drugi komputer, tablet)

Plany, plan, układ części pieśni i to, co jest na ekranie — jak w oknie operatora, w przeglądarce.
Parowanie: w aplikacji „Telefon” → „Panel”, zeskanuj kod albo otwórz adres spod kodu (urządzenie
dostaje uprawnienie „Panel”; w oknie „Telefon” można je odebrać). Treść pieśni, nowe teksty,
prezentacje, filmy i kioski zmienia się w aplikacji.

```
cd app/web/panel
npm install
npm run dev      # podgląd z przeładowaniem (derpmap.json w public/ — pobierz z https://tailcat.dev/derpmap.json)
npm run build    # dist/ — build.py robi to sam i kopiuje do release/web/uwielbienie/panel/
```

Klient tailcat-link, czcionki i ikona pochodzą z `web/remote` (wspólne pliki), tweetnacl — z npm.
Protokół: `src/Uwielbienia.Link/RemoteProtocol.cs` + `PanelProtocol.cs`, typy w `web/panel/src/protocol.ts`.
