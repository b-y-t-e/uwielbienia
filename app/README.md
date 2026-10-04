# Aplikacja „Uwielbienia” — rzutnik tekstów pieśni

Projekt i scenariusze: [`../docs/projekt-aplikacji.md`](../docs/projekt-aplikacji.md).

## Uruchomienie

```
dotnet run --project app/src/Uwielbienia.Desktop
```

Wymaga .NET 10 SDK. Pieśni są wbudowane w aplikację; uruchomiona z repozytorium czyta od razu
folder `Teksty/`, więc poprawki tekstów widać bez przebudowy.

Dane użytkownika (plany, ustawienia, sparowane urządzenia, własne pieśni i teksty w `teksty/`):
`%APPDATA%\Uwielbienia` (Linux: `~/.config/Uwielbienia`).

**Plan, pieśni i teksty:** do planu dodaje się z planu — „+” w nagłówku albo prawy klik na pozycji
(„Dodaj przed… / po…”, „Pokaż na ekranie”, „Usuń z planu”). Okno „Dodaj do planu” wyszukuje
pieśni i teksty i pozwala utworzyć nową pieśń albo tekst (część Mszy, modlitwa, ogłoszenie — bez
akordów). „Edytuj” w kolumnie „Pieśń” zmienia wybraną. Zapis jest tylko na tym komputerze; zmieniona pieśń
śpiewnika ma „Przywróć oryginał”.

## Okno operatora

Trzy kolumny (szerokość regulowana przeciąganiem, zapamiętywana):

| Kolumna | Zawartość | Kod |
|---|---|---|
| **Plan** | kolejność pieśni: klik wybiera, dwuklik pokazuje, przeciąganie zmienia kolejność | `PlanViewModel` |
| **Pieśń** | pozycja zaznaczona w planie: tekst, akordy, pola wyboru części (na żywo dla granej), edycja | `PreviewViewModel`, `SongEditorViewModel` |
| **Na ekranie** | to, co widzi sala: postęp, bieżąca i następna część z akordami, Wstecz / Dalej | `LiveViewModel` |

Plany (wydarzenia i szablony) zarządza się w oknie „Plany” po kliknięciu nazwy planu.

## Obsługa w skrócie

| Klawisz | Działanie |
|---|---|
| Spacja, →, PageDown | następny slajd / następna pieśń planu |
| Backspace, ←, PageUp | poprzedni slajd |
| B lub . | czarny ekran |
| F5 | włącz/wyłącz projekcję (drugi ekran albo ten sam) |
| pisanie cyfr/liter | „Dodaj do planu” z tym tekstem; Enter dodaje zaraz za pieśnią na ekranie |
| dwuklik w planie | pokaż pieśń na ekranie (jedyny sposób w oknie operatora) |
| Shift+1…9 | skok do slajdu bieżącej pieśni |
| Esc | zamyka nakładkę; w trybie jednego ekranu wraca do okna operatora |

## Struktura

| Projekt | Rola |
|---|---|
| `src/Uwielbienia.Core` | model pieśni, parser `piesn.md`, wyszukiwanie, plany, slajdy, `LiveSession` (jedyne źródło prawdy o ekranie) |
| `src/Uwielbienia.Link` | tailcat-link: protokół JSON i serwer dla telefonu / przeglądarki |
| `src/Uwielbienia.App` | interfejs Avalonia: widoki, modele widoków, motywy, czcionki, korzeń kompozycji (`AppComposition`) |
| `src/Uwielbienia.Desktop` | program dla Windows i Linuksa |
| `tests/Uwielbienia.Core.Tests` | testy (m.in. parsowanie wszystkich pieśni z `Teksty/`) |
| `tools/Uwielbienia.Screenshots` | renderuje okna do PNG bez wyświetlania — do przeglądu wyglądu |
| `web/remote` | strona pilota i ekranu w przeglądarce (`greysource.eu/uwielbienie`) |

## Testy i zrzuty

```
dotnet test app
dotnet run --project app/tools/Uwielbienia.Screenshots -- <katalog> [light]
```

## Budowanie i wydanie

Skrypty w głównym folderze repozytorium:

```
python build.py        # testy + Windows (.exe) + Linux (.tar.gz) + strona → release/
python deploy.py       # nowa wersja: build, strona na FTP, tag vX.Y.Z → GitHub Release
python deploy.py --site-only   # tylko strona pilota
```

`deploy.py` podbija `version.txt`, a wypchnięty tag uruchamia `.github/workflows/release.yml`,
który tym samym `build.py` buduje aplikacje i wystawia je na GitHubie. Dane FTP: zmienne
`FTP_HOST`, `FTP_USER`, `FTP_PASS` albo plik `.env` w głównym folderze (w `.gitignore`).

## Aktualizacje (Windows)

Instaluj wydanie przez `Uwielbienia-win-Setup.exe` z GitHub Releases. Tylko ta instalacja
automatycznie sprawdza, pobiera i proponuje nowsze wersje z GitHub.

## Strona pilota

Pliki statyczne w `web/remote` (klient tailcat-link i tweetnacl skopiowane do `lib/`, bez kroku
budowania). `build.py` dokłada aktualną mapę serwerów pośredniczących (`derpmap.json`), która
musi leżeć w tej samej domenie co strona.
