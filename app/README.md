# Aplikacja „Uwielbienia” — rzutnik tekstów pieśni

Projekt i scenariusze: [`../docs/projekt-aplikacji.md`](../docs/projekt-aplikacji.md).

## Uruchomienie

```
dotnet run --project app/src/Uwielbienia.Desktop
```

Wymaga .NET 10 SDK. Pieśni są wbudowane w aplikację; uruchomiona z repozytorium czyta od razu
folder `Teksty/`, więc poprawki tekstów widać bez przebudowy.

Dane użytkownika (plany, ustawienia, sparowane urządzenia): `%APPDATA%\Uwielbienia`
(Linux: `~/.config/Uwielbienia`).

## Obsługa w skrócie

| Klawisz | Działanie |
|---|---|
| Spacja, →, PageDown | następny slajd / następna pieśń planu |
| Backspace, ←, PageUp | poprzedni slajd |
| Enter | pokaż na ekranie pieśń z podglądu |
| B lub . | czarny ekran |
| F5 | włącz/wyłącz projekcję (drugi ekran albo ten sam) |
| pisanie cyfr/liter | szybkie wyszukiwanie; Enter pokazuje, Ctrl+Enter dodaje jako następną |
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

## Publikacja

```
dotnet publish app/src/Uwielbienia.Desktop -c Release -r win-x64 --self-contained
dotnet publish app/src/Uwielbienia.Desktop -c Release -r linux-x64 --self-contained
```

## Strona pilota

Pliki statyczne w `web/remote` (klient tailcat-link i tweetnacl skopiowane do `lib/`, bez kroku
budowania). Wdrożenie przez FTP — dane logowania tylko ze zmiennych środowiskowych:

```
FTP_HOST=… FTP_USER=… FTP_PASS=… app/web/deploy.sh
```

Skrypt pobiera aktualną mapę serwerów pośredniczących (`derpmap.json`), która musi leżeć w tej
samej domenie co strona.
