# Projekt aplikacji „Uwielbienia” — rzutnik tekstów pieśni

Status: etap 1 i strona pilota zrobione (2026-09-25); dalsze etapy w p. 7.

## 1. Idea w jednym zdaniu

Operator ma przed sobą **plan** (listę pieśni) i **podgląd**, a publiczność widzi tylko
**ekran** — duży, czysty tekst bieżącej części pieśni. Przeglądanie i układanie pieśni
nigdy nie zmienia tego, co jest na ekranie. Ekran zmienia się tylko po świadomym
„Pokaż” albo spacji / Backspace.

## 2. Scenariusze

| # | Scenariusz | Jak działa |
|---|---|---|
| S1 | **Laptop + drugi ekran** (główny) | Okno operatora na ekranie, na którym jest aplikacja. Okno projekcji otwiera się na pełnym ekranie na **innym** monitorze (wykrywanym automatycznie; przy podłączeniu/odłączeniu przenosi się samo). |
| S2 | **Jedno urządzenie, bez drugiego ekranu** (awaryjny) | Laptop, tablet lub telefon sam jest ekranem. Tryb sceniczny: pełny ekran z tekstem, a szybki wybór pieśni nakłada się na chwilę na tekst (opis w p. 4). |
| S3 | **Dwa komputery sparowane** (tailcat-link) | Komputer A = operator, komputer B = tylko ekran (ta sama aplikacja w trybie „Ekran”). A wysyła stan: pieśń, slajd, czarny ekran. |
| S4 | **Telefon jako pilot** (strona `greysource.eu/uwielbienie`) | Parowanie kodem QR z okna operatora. Na telefonie: Następny / Poprzedni, Czarny ekran, plan z możliwością wyboru pieśni, wyszukiwarka. |
| S5 | **Telefon jako ekran** (przyszłość) | Ta sama strona w trybie „ekran” — wymaga tylko renderowania stanu, który już wysyłamy w S3/S4. |

Wspólny mianownik S3–S5: **stan projekcji** (`LiveState`) to mały, samowystarczalny
komunikat. Każdy odbiorca (okno projekcji, drugi komputer, strona www) tylko go
wyświetla. Dzięki temu nowe odbiorniki nie wymagają zmian w logice.

## 3. Pojęcia w aplikacji

- **Pieśń** — plik `Teksty/NNN-*/piesn.md`.
- **Plan** — uporządkowana lista pozycji na wydarzenie. Ma nazwę, opcjonalną datę i
  rodzaj: *wydarzenie* („Uwielbienie 25 września 2026”, „Msza 27 września 2026”) albo
  *szablon* („Próba z dziećmi”). Plany można przeglądać (historia wg daty), klonować
  („Użyj ponownie” → nowa data) i zapisywać jako szablon.
- **Pozycja planu** — pieśń + wybrane części i ich kolejność. Domyślnie wszystkie
  części wg `kolejnosc`; operator odznacza części albo zmienia kolejność.
  W przyszłości ten sam typ pozycji obejmie „stałe elementy” (ogłoszenie, modlitwa,
  obraz) — dlatego pozycja jest abstrakcją (`IPlanItem`), a pieśń jednym z typów.
- **Slajd** — to, co jest naraz na ekranie: jedna część pieśni albo jej fragment, jeśli
  część jest zbyt długa (np. >6 wersów → dzielimy równo).
- **Podgląd** vs **Na ekranie** — dwa niezależne wskaźniki. Podgląd = to, co operator
  ogląda. Na ekranie = to, co widzi sala.

## 4. Obsługa (sterowanie)

| Klawisz / gest | Działanie |
|---|---|
| `Spacja`, `→`, `PageDown`, `↓` | następny slajd; po ostatnim slajdzie → pierwszy slajd następnej pozycji planu |
| `Backspace`, `←`, `PageUp`, `↑` | poprzedni slajd |
| `Enter` | pokaż na ekranie to, co jest w podglądzie |
| `B` lub `.` | czarny ekran (włącz/wyłącz), jak w PowerPoint |
| `Shift`+`1`–`9` | skok do n-tego slajdu bieżącej pieśni (same cyfry zaczynają wyszukiwanie numeru) |
| Pisanie cyfr lub liter | **szybki wybór**: `47` albo `jezus mój` → lista wyników, `Enter` = pokaż od razu (pieśń trafia do planu zaraz za bieżącą, więc „Dalej” wraca do planu), `Ctrl+Enter` = dodaj jako następną w planie |
| `F5` | włącz/wyłącz okno projekcji |
| Pilot do prezentacji | działa od razu — wysyła `PageUp` / `PageDown` |

Szybki wybór to odpowiedź na sytuację „alarmową” i uwielbienie bez planu. Numer ze
śpiewnika wystarcza, a wyszukiwarka ignoruje polskie znaki i interpunkcję.

**Tryb jednego urządzenia (S2)** działa tak samo z klawiatury. Na ekranie dotykowym:
- stuknięcie prawej / lewej połowy ekranu = następny / poprzedni slajd;
- przeciągnięcie od dolnej krawędzi = szuflada z planem i wyszukiwarką; tekst na
  ekranie zostaje, dopóki nie wybierzesz nowej pieśni.

## 5. Wygląd

### Okno operatora

```
┌───────────────┬─────────────────────────────────┬─────────────────────┐
│ Plan          │ Podgląd                         │ Na ekranie          │
│ Uwielbienie   │ 47  Jezus mój Pan               │ ┌─────────────────┐ │
│ 25 wrz 2026 ▾ │ [V1] [C] [V1] [C]  ← kolejność  │ │ Jezus mój Pan   │ │
│               │                                 │ │ Jezus mój Król  │ │
│ 1  Chwała ... │ ┌ Zwrotka 1 ─────┐ ┌ Refren ──┐ │ └─────────────────┘ │
│ 2▶ Jezus mój  │ │ tekst…         │ │ tekst…   │ │ Następny slajd:     │
│ 3  Duchu Św.  │ └────────────────┘ └──────────┘ │ Refren              │
│ + Dodaj       │  (klik = pokaż ten slajd)       │ [ Czarny ekran  B ] │
├───────────────┴─────────────────────────────────┴─────────────────────┤
│ Szukaj: numer lub tytuł…                          Akordy ☐  ☾/☀       │
└───────────────────────────────────────────────────────────────────────┘
```

- Lewo: plan. Bieżąca pozycja „na ekranie” ma znacznik świecy (bursztynowy pasek).
- Środek: podgląd wybranej pieśni jako slajdy. Stuknięcie w slajd = pokaż go na ekranie.
  Części do pominięcia przełącza się chipami `[V1] [C] …`.
- Prawo: miniatura ekranu na żywo (dokładnie ten sam widok, pomniejszony) i następny slajd.
- Dół: wyszukiwarka zawsze pod ręką; przełącznik akordów w podglądzie (dla muzyków).
- Na wąskim ekranie (telefon, tablet pionowo) kolumny zamieniają się w zakładki.

### Kierunek wizualny

Temat: modlitwa wieczorna, świece, śpiewnik. Paleta nie jest ani czarno-zielona, ani
kremowo-terakotowa. Opiera się na nocnym granacie i ciepłym świetle świecy.

| Token | Ciemny | Jasny | Rola |
|---|---|---|---|
| `Ink` | `#161B2E` | `#F6F7FB` | tło operatora |
| `Surface` | `#1F2640` | `#FFFFFF` | panele |
| `Text` | `#E9ECF5` | `#1B2138` | tekst |
| `Muted` | `#8E97B3` | `#5E6784` | opisy, numery |
| `Candle` | `#F2B84B` | `#B7791F` | jedyny akcent: „na ekranie”, kursor planu |
| `Chord` | `#7FB6E8` | `#2F6DB5` | akordy w podglądzie |

**Ekran projekcji** ma własny, niezależny motyw: domyślnie czysta czerń `#000000`
(projektor świeci wtedy najmniej, a sala widzi tylko litery) i biały tekst `#FFFFFF`.
Wariant jasny (biały ekran, czarny tekst) jest do sal zalanych słońcem.

**Kroje** (wbudowane w aplikację, pełne polskie znaki, licencja OFL):
- **Atkinson Hyperlegible Next** — tekst na ekranie i interfejs. Krój projektowany dla
  czytelności (Braille Institute), rozróżnia `I l 1` i `O 0` i dobrze się czyta z
  daleka.
- **Literata** — tylko tytuły pieśni w operatorze. Szeryf książkowy daje wrażenie
  śpiewnika.

Tekst na ekranie jest wyśrodkowany, a rozmiar czcionki dobiera się automatycznie, żeby
najdłuższy wers zmieścił się na szerokość. Ma przy tym minimum, żeby rozmiar nie skakał
między slajdami jednej pieśni. Nazwy części („Refren”) nigdy nie pojawiają się na
ekranie. Przejście między slajdami to krótkie przenikanie (150 ms, wyłączalne) — jedyna
animacja w aplikacji.

## 6. Architektura (Clean Code + SOLID)

```
app/
  Uwielbienia.sln
  src/
    Uwielbienia.Core/          # czysta logika, bez UI i bez sieci (net10.0)
      Songs/     Song, Section, Line, ChordSet, ISongParser → MarkdownSongParser,
                 ISongLibrary → FileSongLibrary / EmbeddedSongLibrary, SongSearch
      Plans/     Plan, IPlanItem, SongPlanItem, IPlanStore → JsonPlanStore
      Presentation/
                 Slide, ISlideBuilder → SectionSlideBuilder,
                 LiveSession (stan: pozycja, slajd, czarny ekran; Next/Prev/Show/Blank),
                 LiveState (niezmienny snapshot wysyłany do odbiorców),
                 ILiveStateSink (odbiorca stanu: okno projekcji, link, www)
    Uwielbienia.App/           # wspólne UI Avalonia: ViewModels (CommunityToolkit.Mvvm),
                               # Views, motywy, fonty, IScreenService, IProjectionWindow
    Uwielbienia.Desktop/       # głowica Windows + Linux
    Uwielbienia.Android/       # głowica Android
    Uwielbienia.Link/          # tailcat-link: LinkHost (HostManyAsync), protokół (DTO JSON),
                               # LinkLiveStateSink, RemoteCommandHandler
  web/remote/                  # strona pilota (greysource.eu/uwielbienie), @tailcat/link
  tests/
    Uwielbienia.Core.Tests/    # parser na wszystkich 138 plikach, LiveSession, SlideBuilder
```

Najważniejsze decyzje:
- **Jedno źródło prawdy o tym, co jest na ekranie:** `LiveSession`. Okno projekcji,
  drugi komputer i telefon są tylko odbiorcami `LiveState` (`ILiveStateSink`) —
  Open/Closed: nowy odbiorca to nowa klasa, bez zmian w sesji.
- **Komendy z każdego źródła** (klawiatura, przyciski, pilot, telefon) trafiają do
  jednego interfejsu `ILiveControl` (`Next`, `Previous`, `Show(item, slide)`,
  `ToggleBlank`). Telefon nie ma osobnej logiki.
- **Parser zgodny z CLAUDE.md** (regexy stamtąd), z dziedziczeniem akordów. Testy
  parsują wszystkie pliki z `Teksty/`.
- **Pieśni:** wbudowane w aplikację (kopia `Teksty/` jako zasób, wymagane na Androidzie).
  Na komputerze można wskazać folder `Teksty/` z repozytorium, żeby od razu widzieć
  poprawki bez wydawania nowej wersji.
- **Plany:** pliki JSON w folderze danych użytkownika (`%APPDATA%`, `~/.local/share`,
  pamięć aplikacji na Androidzie). Do tego eksport/import pliku planu, np. żeby przesłać
  plan drugiej osobie.
- **Ekrany:** `IScreenService` na bazie `Window.Screens` — wykrywa ekran okna
  operatora, wybiera inny do projekcji i reaguje na `Screens.Changed` (podłączenie
  rzutnika w trakcie).
- **Protokół linku** (JSON): `state` (host → odbiorcy: pieśń, slajd, tekst slajdu,
  czarny ekran), `plan` (host → pilot: lista pozycji), `command` (pilot → host: next,
  prev, show, blank, search). Stan niesie gotowy tekst slajdu, więc strona www nie musi
  mieć pieśni ani parsera.

## 7. Etapy (roadmapa)

1. **Zrobione — rdzeń i S1/S2 (desktop Windows/Linux):** parser, biblioteka (138 pieśni wbudowanych),
   wyszukiwanie, plany (wydarzenia, szablony, historia, klonowanie), okno operatora z podglądem
   „Na ekranie” i akordami, okno projekcji na drugim ekranie z automatycznym wykrywaniem monitora,
   tryb jednego ekranu z szybkim wyborem, motywy jasny/ciemny.
2. **Zrobione — S4 i S3 przez przeglądarkę:** tailcat-link w aplikacji (host), strona
   `greysource.eu/uwielbienie` — pilot (dalej, wstecz, czarny ekran, plan, wyszukiwarka)
   i tryb „Ekran” (przeglądarka na drugim komputerze jako ekran projekcji).
3. **Nowe pieśni w aplikacji:** edytor pieśni (tytuł, kategoria, sekcje z kodami, akordy przy
   wersach, `kolejnosc`) zapisujący plik `piesn.md` w formacie z CLAUDE.md, z walidacją jak
   `tools/validate.py`; pieśni użytkownika w osobnym folderze, numeracja od 1000, żeby nie
   kolidowały ze śpiewnikiem.
4. **Szukanie pieśni w internecie:** wyszukiwanie tekstu i akordów (np. giszowiec.org,
   budowniczy.net, otworzcieserca.pl, spiewnik.wywrota.pl), podgląd wyniku i import do edytora
   z rozpoznaniem zwrotek/refrenu i transpozycją akordów do wybranej tonacji; operator zawsze
   zatwierdza wynik przed zapisem.
5. **Android:** ta sama aplikacja (`Uwielbienia.Android`), układ zakładek, gesty, tryb jednego urządzenia.
6. **Później:** stałe elementy w planie (ogłoszenie, modlitwa, obraz — nowy typ `PlanItem`),
   telefon jako ekran (S5), ekran dla muzyków z akordami, przeciąganie pozycji planu myszą.

## 8. Wymagania środowiska

- .NET 10 SDK (jest), Avalonia 12.1, CommunityToolkit.Mvvm 8.4, Tailcat.Link 0.5.
- Android: `dotnet workload install android` + Android SDK/JDK (obecnie niezainstalowane).
- Linux: do bezpośredniego połączenia link potrzebuje `libmsquic`; bez niego działa przez
  relay.
- Strona pilota: hosting statyczny na `greysource.eu/uwielbienie`, z kopią `derpmap.json`
  na tej samej domenie (wymóg tailcat-link w przeglądarce).
