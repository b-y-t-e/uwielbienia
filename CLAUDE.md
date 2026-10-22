# Uwielbienia — śpiewnik pieśni uwielbieniowych

Repozytorium przechowuje teksty pieśni w ujednoliconym formacie Markdown, przeznaczonym
do odczytu maszynowego przez aplikację (np. rzutnik tekstów, śpiewnik z akordami).

## Struktura katalogów

```
Teksty/
  001-maryjo-sliczna-pani/
    piesn.md
  002-maryjo-wskazujaca-droge/
    piesn.md
  ...
tools/
  import_spiewnik.py   # jednorazowy import z PDF (PyMuPDF) — NIE uruchamiać ponownie
  validate.py          # walidator formatu: python tools/validate.py
run.py                 # kompiluje i uruchamia aplikację: python run.py [--release]
build.py               # buduje aplikacje, instalator Velopack i stronę do release/
deploy.py              # wydanie (wszystko lokalnie): strona na FTP + tag + GitHub Release przez API
app/                   # aplikacja rzutnika (Avalonia, .NET 10) + strona pilota i panel — patrz app/README.md
docs/
  projekt-aplikacji.md # scenariusze, architektura i roadmapa aplikacji
```

- Każda pieśń = osobny folder `NNN-slug`, gdzie `NNN` to numer w śpiewniku (3 cyfry),
  a `slug` to tytuł bez polskich znaków, małymi literami, słowa rozdzielone `-`.
  Po zmianie `tytul` zmieniamy też `slug` (`git mv`).
- Numery 001–136 odpowiadają numeracji PDF. PDF ma dwie pieśni o numerze 116 — obie
  zachowują `numer: 116` (walidator zgłasza to tylko jako uwagę).
- Pieśni spoza numeracji PDF (np. wydzielone z błędnie sklejonej pozycji) dostają kolejny
  wolny numer (137, 138…); w pliku nie zmieniamy numerów istniejących pieśni.
- Plik z tekstem ma zawsze nazwę `piesn.md`. W folderze mogą w przyszłości pojawić się
  inne pliki (nuty, audio), aplikacja czyta tylko `piesn.md`.

## Terminologia (budowa pieśni)

| Kod    | Nazwa PL                | Nazwa EN (CCLI/OpenLP) | Opis |
|--------|-------------------------|------------------------|------|
| `I`    | Wstęp                   | Intro                  | Część instrumentalna lub wokalna przed 1. zwrotką |
| `V1`…  | Zwrotka 1…              | Verse                  | Część narracyjna, zmienny tekst na tej samej melodii |
| `PC`   | Przedrefren             | Pre-Chorus             | Krótkie przejście między zwrotką a refrenem |
| `C`    | Refren                  | Chorus                 | Powtarzana część z niezmiennym tekstem |
| `C2`…  | Refren 2…               | Chorus 2               | Drugi, inny refren w tej samej pieśni |
| `B`    | Mostek                  | Bridge                 | Kontrastująca część (inna melodia/harmonia), zwykle raz, przed ostatnim refrenem |
| `FC`   | Refren końcowy          | Final Chorus           | Ostatni refren ze zmienionym tekstem/melodią |
| `T`    | Tag (zawołanie)         | Tag                    | Wielokrotnie powtarzana ostatnia fraza |
| `INT`  | Interludium             | Interlude              | Wstawka instrumentalna między częściami |
| `E`    | Koda / Zakończenie      | Ending / Outro         | Zamknięcie pieśni |
| `O`    | Inne                    | Other                  | Część, której nie da się sklasyfikować |

`V` zawsze ma numer (`V1`, `V2`…). `C`, `B`, `PC`, `INT`, `O` mogą mieć sufiks liczbowy,
gdy w pieśni jest kilka różnych takich części (`C` ≡ `C1`, `C2`…). `I`, `FC`, `T`, `E` — bez numeru.

## Format pliku `piesn.md`

```markdown
---
numer: 1
tytul: "Maryjo, śliczna Pani"
kategoria: "Pieśni Maryjne"
numer_zrodlowy: 1069
tonacja: "G"
kolejnosc: [V1, C, V2, C]
zrodlo: "20260925-073507-spiewnik_17_08_10.pdf"
---

# Maryjo, śliczna Pani

## [V1] Zwrotka 1
Maryjo, śliczna Pani, `G h`
Matko Boga i ludzi na ziemi, `C D G`

## [C] Refren
Tekst linii refrenu {x2} `G C G`

## [V2] Zwrotka 2
|: Pierwsza linia powtarzanego fragmentu `e`
ostatnia linia fragmentu :| {x2} `D`
```

### Nagłówek YAML (front matter) — klucze obowiązkowe (poza `rodzaj`)

| Klucz            | Typ             | Znaczenie |
|------------------|-----------------|-----------|
| `numer`          | int             | Numer pieśni w śpiewniku |
| `tytul`          | string (w `""`) | Tytuł; zwykle incipit (pierwszy wers) bez końcowej interpunkcji |
| `kategoria`      | string          | `Pieśni Maryjne`, `Msza Święta`, `Uwielbienie`, `Pieśni do Ducha Świętego` |
| `numer_zrodlowy` | int \| `null`   | Numer pomocniczy z PDF (np. `1069`) — numer w innym śpiewniku/bazie |
| `tonacja`        | string \| `null`| Pierwszy akord pieśni (przybliżenie tonacji) |
| `kolejnosc`      | lista kodów     | Kolejność wykonania (arrangement); kody muszą istnieć jako sekcje |
| `zrodlo`         | string          | Plik źródłowy (PDF, z którego pochodzi pieśń) |
| `rodzaj`         | `"tekst"`       | Opcjonalny, tylko w plikach lokalnych aplikacji: **tekst** (część Mszy, modlitwa, ogłoszenie…) — bez akordów, `numer: null`, `tytul` może być pusty (`""`), części `[O1] Część 1`, `[O2] Część 2`…; z tytułem może nie mieć żadnej części (plansza, np. „Uwielbienie”) |

### Treść

1. `# Tytuł` — dokładnie jeden, równy `tytul`.
2. Sekcje: `## [KOD] Nazwa` — kod z tabeli terminologii w nawiasach kwadratowych, potem
   nazwa polska. Opcjonalnie na końcu `{xN}` = cała sekcja powtarzana N razy.
   Każda sekcja występuje w pliku **raz**; powtórzenia wynikają wyłącznie z `kolejnosc`.
3. Wiersze tekstu — jeden wers w jednej linii, bez pustych linii wewnątrz sekcji.
   Gramatyka wiersza (elementy w tej kolejności, wszystkie poza tekstem opcjonalne):
   ```
   [|: ]tekst[ :|][ {xN}][ `akordy`]
   ```
   - `` `akordy` `` — akordy dla wersu, na końcu linii w backtickach, rozdzielone spacjami.
     Notacja polska: dur wielką literą (`C`, `Fis`), moll małą (`a`, `fis`), `H` = B, `B` = B♭
     (piszemy `B`/`b`, nie `Ais`/`ais`), `Es` = E♭, krzyżyki przez `-is` (`Fis`, `Cis`, `Dis`).
     Dodatki: `7`, `7+`, `9`, `/` (bas, np. `H/Dis`, lub separator taktu), `|`,
     `( )` = akord opcjonalny. Nie używamy notacji angielskiej (`Am`, `Bb`, `F#`).
     Wiersz zawierający wyłącznie `` `akordy` `` = linia instrumentalna (bez tekstu).
     **Dziedziczenie akordów:** sekcja, w której żaden wers nie ma akordów, przejmuje je
     z pierwszej sekcji tego samego typu, która je ma (`V2`, `V3`… od `V1`; `C2` od `C`),
     wers po wersie według pozycji — tak jak w śpiewniku, gdzie kolejne zwrotki śpiewa się
     na tę samą melodię. Nie kopiujemy akordów do takich sekcji. Pojedynczy wers bez
     akordów w sekcji, która akordy ma, niczego nie dziedziczy i jest wyświetlany bez
     akordów — w źródle PDF akordy przy wersie często obejmują też kolejne wersy (ciąg
     harmoniczny), więc w trybie z akordami taki wers pokazujemy po prostu bez akordów.
   - `{xN}` — ten wers (lub fragment `|: … :|`) śpiewa się N razy.
   - `|:` … `:|` — początek i koniec fragmentu powtarzanego obejmującego kilka wersów;
     `{xN}` stoi po `:|`.
4. `> uwaga` — linia z uwagą wykonawczą (nie jest śpiewana, nie wyświetlać na rzutniku).
5. Sekcje rozdziela jedna pusta linia. Kodowanie UTF-8, końce linii LF (`.gitattributes`).

### Reguły parsowania (dla aplikacji)

- Front matter: YAML między pierwszymi dwoma liniami `---`.
- Sekcja: `^## \[(?<kod>[A-Z]+\d*)\] (?<nazwa>.+?)(?: \{x(?<n>\d+)\})?$`
- Akordy wersu: `` \s*`(?<akordy>[^`]*)`\s*$ ``
- Powtórzenie wersu: `\s*\{x(?<n>\d+)\}\s*$` (po usunięciu akordów)
- Repetycja wielowersowa: prefiks `|: `, sufiks ` :|`.
- Tryb bez akordów: usunąć `` `akordy` `` z wersów i pominąć linie instrumentalne.
- Tryb z akordami: dla sekcji bez akordów zastosować dziedziczenie (patrz wyżej).

## Konwencje edycji

- Bazą jest tekst z PDF, zweryfikowany ze źródłami internetowymi (np. giszowiec.org,
  budowniczy.net, otworzcieserca.pl, spiewnik.wywrota.pl, strony zespołów, oryginał
  angielski dla tłumaczeń). Przy weryfikacji korzystamy z min. 2 zgodnych źródeł.
- Gdy słowa w PDF różnią się od zgodnych źródeł — poprawiamy wg źródeł. Literówki i
  interpunkcję poprawiamy zawsze.
- Brakujące części (zwrotki, refren, mostek), które PDF pominął, dopisujemy wg źródeł.
  Akordy dopisujemy tylko ze źródła, przetransponowane do tonacji pliku; bez pewnego
  źródła zostawiamy sekcję bez akordów (dziedziczenie).
- Części występujące tylko w jednym źródle (np. lokalne dopiski) nie są dopisywane.
- Nie usuwamy wersów. Usuwamy tylko: dosłowne duplikaty sekcji (powtórzenie wyraża
  `kolejnosc`) oraz artefakty importu (dopiski typu „refren”, akordy sklejone z tekstem).
  Błędnie wydzielone sekcje można scalić, zachowując wszystkie wersy.
- Struktura (`kolejnosc`, podział na sekcje) ma odzwierciedlać źródła; gdy źródła jej nie
  podają, przyjmujemy typowy układ (np. `[V1, C, V2, C]`).
- `tools/import_spiewnik.py` służył do jednorazowego importu — ponowne uruchomienie
  nadpisze wszystkie ręczne poprawki w `Teksty/`.
- Po każdej zmianie uruchomić `python tools/validate.py` (kod wyjścia 0 = OK).

## Aplikacja (`app/`)

Rzutnik tekstów pieśni: okno operatora + ekran projekcji (drugi monitor, ten sam ekran albo
przeglądarka) + pilot w telefonie. Opis scenariuszy i wyglądu: `docs/projekt-aplikacji.md`,
uruchamianie, budowanie i wydanie: `app/README.md`.

### Projekty

| Projekt | Rola |
|---|---|
| `src/Uwielbienia.Core` | logika bez UI: pieśni (`Songs/`), plany (`Plans/`), ekran (`Presentation/`), prezentacje (`Slideshows/`), filmy (`Videos/`), kioski (`Kiosks/`), aktualizacje (`Updates/`) |
| `src/Uwielbienia.Link` | tailcat-link: protokół JSON (`RemoteProtocol`, panel — `PanelProtocol`), polecenia z telefonu i panelu (`RemoteCommandHandler`, `PanelCommands`), serwer (`LinkRemoteServer`) |
| `src/Uwielbienia.App` | Avalonia: widoki (`Views/`), modele widoków (`ViewModels/`), usługi okienkowe (`Services/`), motywy (`Themes/`), czcionki, korzeń DI `AppComposition` |
| `src/Uwielbienia.Desktop` | program Windows/Linux (`Program.cs`, Velopack) |
| `tests/Uwielbienia.Core.Tests` | testy rdzenia i protokołu (m.in. parsowanie i zapis wszystkich pieśni z `Teksty/`) |
| `tools/Uwielbienia.Screenshots` | renderuje okna do PNG bez wyświetlania (scenariusz w `Program.cs`) |
| `web/remote` | strona pilota i ekranu w przeglądarce (`greysource.eu/uwielbienie/telefon`), bez kroku budowania |
| `web/landing` | strona startowa `greysource.eu/uwielbienie/` (React + Vite): linki do pilota i panelu, pobranie aplikacji; adresy z `#code=` / `#ekran` (kody QR starszych wersji aplikacji) przekierowuje do `telefon/` |
| `web/panel` | panel na drugim komputerze / tablecie (`greysource.eu/uwielbienie/panel`): React + Vite + TypeScript, `npm run build` → `dist/` (robi to `build.py`) |

.NET 10, Avalonia 12, CommunityToolkit.Mvvm, DI (`Microsoft.Extensions.DependencyInjection`),
`TreatWarningsAsErrors`. Clean Code i SOLID: logika w Core, UI tylko wyświetla i deleguje.

### Jak to działa (przepływ danych)

- **Pieśni.** `SongSourceFactory` wybiera śpiewnik wspólny (`FallbackSongSource`: folder z ustawień
  → `Teksty/` nad katalogiem programu, gdy uruchomiono z repozytorium → zasoby wbudowane przy
  budowaniu), `LayeredSongSource` nakłada na niego pliki lokalne. `SongLibrary` parsuje
  (`MarkdownSongParser` — reguły z tego pliku) i po `Reload()` wysyła `Changed`; nasłuchują go
  `SongSearch` (indeks bez polskich znaków), `ActivePlan` i kolumna „Pieśń”.
- **Pieśń i tekst** to ten sam model `Song` (`Kind`): pieśń ma numer i akordy, tekst (część Mszy,
  modlitwa, ogłoszenie) nie ma numeru ani akordów (w planie kropka zamiast numeru i kolor `TextItemBrush`), a tytuł jest
  opcjonalny — bez tytułu przedstawia go pierwszy wers (`Song.DisplayTitle`); ten sam format pliku.
  Rzutnik pokazuje tytuł z linią nad pierwszym slajdem tylko, gdy coś wnosi (`LiveItem.ShowTitle`):
  jest własny i nie jest początkiem tekstu pierwszego slajdu (porównanie bez wielkości liter, polskich
  znaków i interpunkcji, całymi słowami — `SongPlanItemType.TitleSpans`). W śpiewniku tytuł to
  zwykle incipit — wtedy zamiast tytułu słowa, które go powtarzają, mają kolor tytułu, a reszta wersu
  zwykły (`SlideLine.TitleLength` — `TitlePart` / `Rest`; `SongPlanItemType.TitleSpans`), co zaznacza
  początek pieśni. Tekst z samym tytułem (bez części — puste pola
  edytora się nie zapisują) to jeden slajd z samym tytułem w stylu tytułu (`SongPlanItemType.TitleSlideCode`).
- **Zapis lokalny.** `LocalSongEditor` (`ISongEditor`) zapisuje własne pieśni (numery od 1000),
  teksty (`tekst-slug`) i poprawki pieśni śpiewnika w `%APPDATA%\Uwielbienia\teksty\{id}\piesn.md`
  (`MarkdownSongWriter` — odwrotność parsera, odtwarza pliki śpiewnika co do znaku). Plik o `id`
  pieśni śpiewnika ją zastępuje (`SongOrigin.Modified`, „Przywróć oryginał” usuwa plik). Śpiewnik
  wspólny (`Teksty/`) aplikacja nigdy nie zmienia; `id` jest stały, bo odwołują się do niego plany.
  `SongDraft` zamienia formularz edytora na `Song`: kody części (`V1`, `C`, `C2`, tekst `O1`…),
  kolejność (edycja: dotychczasowa; nowa pieśń: refren po każdej zwrotce), tonacja.
- **Slajdy.** `SectionSlideBuilder`: jedno wystąpienie części z `kolejnosc` = slajd, dłuższe niż
  `MaxLinesPerSlide` dzielone równo; `ChordResolver` realizuje dziedziczenie akordów. Krótka zwrotka
  i refren zaraz po niej to jeden slajd („Zwrotka 1 + Refren”), gdy razem mają najwyżej 9 wierszy
  rzutnika (wers dłuższy niż ok. 40 znaków = 2 wiersze; 8 mieści się w pełnej wielkości, przy 9 tekst
  zmniejsza się o ok. 12%): mniej przełączania w pieśniach o krótkich częściach. Nigdy więcej niż dwie części, nie części podzielone,
  nie teksty; wersy refrenu mają `SlideLine.IsChorus` (kursywa) i `StartsPart` (odstęp nad nim) — na
  rzutniku, w kolumnie „Na ekranie” i w przeglądarce. Kod slajdu = kod zwrotki, a `LiveSession` dopasowuje
  slajdy po `Slide.FirstPartLabel`, więc pominięcie refrenu na żywo zostawia na ekranie tę samą zwrotkę.
  Wyłącznik: `AppSettings.JoinVerseAndChorus`.
- **Plany.** `Plan` (rekord: nazwa, osobna data, rodzaj wydarzenie/szablon, pozycje
  `SongPlanItem` z `Arrangement` / `Layout` — `null` = układ z pliku pieśni) w `JsonPlanStore`
  (`%APPDATA%\Uwielbienia\plany\{id}.json`). `ActivePlan` trzyma otwarty plan, zapisuje każdą
  zmianę, buduje `Playlist` (`LiveItem`) i wysyła `Changed` / `Opened` (po otwarciu innego planu
  widoki zaczynają od początku: zaznaczona pierwsza pozycja, a na ekranie jej pierwszy slajd —
  `PlanViewModel.StartFromBeginning`; także przy starcie z ostatnim planem).
- **API hosta (Core) — jedno dla okna operatora i panelu.** Każda czynność na planach i planie jest w Core, a widoki
  i `PanelCommands` tylko ją wywołują: `PlanLibrary` (lista wydarzeń / szablonów, `Create`, `Rename`, `Open`, `OpenInitial`,
  `CopyToToday`, `SaveAsTemplate`, `Delete` — nie otwartego; `Changed` = zmiana listy), `PlanActions` (otwarty plan: `Insert` /
  `InsertAt` / `AddAfterLive` / `AddToEnd`, `Move`, `Remove`, `DuplicateAsync`, `Show`, `SetLayout` — układ części, `Change` —
  inna zmiana pozycji; `ItemAdded` = zaznacz w planie, z `select: false` — dodanie z innego urządzenia nie zmienia zaznaczenia
  operatora), `SongPart.For` (część pieśni z wersami do kolumny „Pieśń”), ekran — `ILiveControl` / `ILiveStateSource`.
  Nowa czynność = metoda w Core + wywołanie w modelu widoku + (dla panelu) polecenie w `PanelCommands`, typ w
  `web/panel/src/protocol.ts` i przycisk w panelu. `ActivePlan.Opened` zapamiętuje `LastPlanId` (`PlansViewModel`).
  Wyjątek (jeszcze nie w Core, panel ich nie obsługuje): edycja kiosku (`KioskViewModel`), przejścia planu
  (`PlanTransitionsViewModel`, `PlanSlideTransitionsViewModel`) i prezentacji (`PresentationViewModel`) zmieniają plan przez
  `ActivePlan.Update` — przed dodaniem ich do panelu przenieść do `PlanActions`.
- **Rodzaje pozycji planu.** Plan nie zakłada, że pozycja to pieśń: `IPlanItemTypes`
  (`PlanItemTypes` — rejestr wszystkich `IPlanItemType`) daje opis na liście (`PlanItemInfo`:
  tytuł, numer, `PlanItemKind`, uwaga) i slajdy (`LiveItem`), a jego `Changed` (zmieniona treść)
  przebudowuje plan. Rodzaje: `SongPlanItemType` (pieśń i tekst; też `ISongPresenter` — pieśń
  spoza planu na ekran), `PresentationPlanItemType` (prezentacja), `VideoPlanItemType` (film) i `KioskPlanItemType` (kiosk).
- **Prezentacje** (`PresentationPlanItem`: folder + tytuł). `SlideshowLibrary` kopiuje plik
  PowerPoint (`.pptx .ppt .pps .ppsx .pptm .ppsm .odp`) albo obrazy (`.png .jpg .jpeg .bmp .webp`,
  kilka = jedna prezentacja; „Obraz” — `ImportEachAsync`: każdy obraz osobno, wszystko albo nic) do
  `prezentacje/{id8}-{slug}/` — plan działa po wyjęciu pendrive'a. Import / kopia przerwane błędem usuwają swój folder.
  Slajdy prezentacji eksportuje raz, w tle, do `slajdy/` (najpierw `slajdy.tmp`, potem znacznik
  `.complete`; PNG 1920 px, bez slajdów ukrytych; kroki animacji — kolejne kliknięcia — jako osobne slajdy: `AnimationStepsExporter`
  przerabia kopię `.pptx/.ppsx/.pptm/.ppsm` przed eksportem, `AnimationSteps`; slajdy sprzed tego albo starszej wersji przeróbki — znacznik inny niż `AnimationStepsExporter.Current`, np. „kroki” ze sklejonymi słowami, bo XML wczytany bez `PreserveWhitespace` gubił spacje `<a:t> </a:t>` — prezentacja z animacjami
  eksportuje jeszcze raz, a do tego czasu i przy błędzie pokazuje stare slajdy) `SlideExporterChain` (Core) — pierwszy dostępny
  `ISlideExporter` (App/Services), a gdy zawiedzie, następny: `PowerPointExporter` (automatyzacja COM
  na wątku STA, jak w pps_viewer; **tylko z ważną licencją Office** — `OfficeLicense` czyta `ExpiresOn`
  z `%LOCALAPPDATA%\Microsoft\Office\Licenses\5`, bo po wygaśnięciu subskrypcji PowerPoint może
  zatrzymać eksport oknem aktywacji) → `DeckCraftExporter` (`C:\Program Files\DeckCraft\deckcraft-cli.exe`:
  `info` + `render --all --scale`, ułamek sekundy) → `LibreOfficeExporter` (`soffice --headless
  --convert-to pdf` z osobnym profilem, strony PDF → PNG przez PDFium/`Docnet.Core`; Linux). Bez
  żadnego z nich — komunikat w planie, można dodać obrazy. Do końca eksportu pozycja nie ma
  slajdów, a plan pokazuje uwagę („Slajd 3 z 20…”, błąd + „Spróbuj ponownie”); `Changed` po
  zakończeniu podmienia ją na ekranie. Slajd to
  `ImageSlide` (plik obrazu); obrazy wczytuje w tle `SlideImages` (pamięć podręczna, następny
  slajd zawczasu, miniatury dla operatora).
- **Filmy** (`VideoPlanItem`: ścieżka pliku + tytuł; plik tylko wskazany, nie kopiowany — brak pliku =
  uwaga w planie i pozycja bez slajdów). Jeden slajd `VideoSlide`. `VideoDirector` (Core) steruje
  `IVideoPlayer` według `LiveState`: wejście na film = odtwarzanie, zejście = `Stop`; przerwany film po
  powrocie wznawia się od tego miejsca (osobno dla każdej pozycji planu), od początku gra nowy albo
  obejrzany do końca; czarny ekran = pauza (wznowienie, chyba że operator sam zatrzymał); pod czarnym
  ekranem nic filmu nie wznawia (`IVideoPlayer.IsHeld`) — w sali nie gra dźwięk filmu, którego nie widać. `VlcVideoPlayer` (App/Services,
  LibVLCSharp; każdy format VLC, dźwięk na domyślne urządzenie) **nie używa natywnego okna** VLC —
  dekoduje klatki (BGRA, najwyżej 1920 px) wprost do `WriteableBitmap` w `VideoFrames`, a rysuje je
  `VideoSurface` (zwykła kontrolka): rzutnik (`ProjectedVideo` w `SlideView`) i podgląd w kolumnie
  „Na ekranie” — dzięki temu film podlega „Dopasuj obraz” i przenikaniu. Przy filmie spacja i kliknięcie
  w obraz (rzutnik w trybie jednego ekranu, podgląd w „Na ekranie”) = pauza / wznowienie jak w odtwarzaczu
  (jedna reguła: `VideoPlayerExtensions.TogglePause` — zwraca `false`, gdy filmu nie ma).
  **Pilot** (`FilmRemote`, `LiveKeyboard`): wysyła krótko `PageDown` / `PageUp`, a przytrzymany — inny klawisz:
  `b` (do przodu) i na zmianę `Shift+F5` / `Escape` (do tyłu — pilot przełącza „start / koniec pokazu”;
  przy filmie oba to długie „do tyłu”, bez filmu `Escape` dalej zamyka okna i tryb jednego ekranu); strzałki
  działają jak pilot. Przy filmie: krótko = ±15 s (szybkie
  klikanie tylko przewija, ale w pierwszych / ostatnich 2 s filmu krótko do tyłu / do przodu = poprzedni / następny slajd — `FilmRemote.Edge`), długo do przodu = pauza, długo do tyłu = podpowiedź na rzutniku i w „Na ekranie”
  (4 s), a po niej długo do tyłu = poprzednia, długo do przodu = następna pozycja planu. Przy zwykłym slajdzie:
  krótko = slajd, `b` = nic (czarny ekran tylko `.` z klawiatury — przypadkowe przytrzymanie pilota nie gasi tekstu), `Shift+F5` tylko włącza projekcję (nigdy jej nie wyłącza). `Ctrl+PageDown` /
  `Ctrl+PageUp` = następna / poprzednia pozycja planu (`ILiveControl.NextItem` / `PreviousItem`). Przyciski
  „Dalej” / „Wstecz” (okno, telefon) zawsze zmieniają slajd / pozycję.
  „−15 s” / „+15 s” (`IVideoPlayer.Skip`, w granicach filmu) w „Na ekranie” i na telefonie; pauza / odtwarzanie
  to ikony (okrągły przycisk w kolorze świecy; pod podglądem filmu: postęp na całą szerokość, a pod nim sterowanie i czas).
  **Obraz względem dźwięku** (rzutnik spóźnia obraz): mały przełącznik „Obraz ‹ +1,6 s ›” (krok 0,1 s; kliknięcie w wartość
  w oknie = 0; kolor akordów, gdy nie jest zerem) w „Na ekranie”, na telefonie
  i `[` / `]` przy filmie (`FilmRemote.ShiftPicture`, `IVideoPlayer.PictureOffset`, + = obraz później, najwyżej ±5 s).
  Przesuwamy obraz, nie dźwięk — VLC umie opóźnić tylko dźwięk, więc `VlcVideoPlayer` ustawia opóźnienie dźwięku
  o przeciwnym znaku (ponownie po starcie każdego pliku). Jedno dla wszystkich filmów: `AppSettings.PictureOffsetMs`.
  Na rzutniku pasek
  postępu z suwakiem (`ProjectionWindow` → `VideoBar`, w warstwie `Warped` — z tą samą perspektywą co film)
  widać razem z uchwytami „Dopasuj obraz” (ruch myszy / tryb K) oraz przez 2,5 s po ±15 s albo pauzie z
  klawiatury, pilota czy kliknięcia (`FilmRemote.FilmChanged`; nie na czarnym ekranie). Brak odtwarzacza
  (`IVideoPlayer.Unavailable`, np. VLC na Linuksie) widać już po zaznaczeniu filmu w planie. VLC: Windows — pakiet `VideoLAN.LibVLC.Windows`
  (`app/Vlc.props`: tylko x64, bez wtyczek nagrywania / transmisji / lua; `build.py` pakuje go do .exe
  z jednego pliku), Linux — VLC z systemu (bez niego czytelny komunikat przy filmie). Próba z
  prawdziwym plikiem: `dotnet run --project app/tools/Uwielbienia.Screenshots -- <katalog> --video <plik>`.
- **Kiosk** (`KioskPlanItem`: tytuł, `Seconds` — domyślnie 8, i `KioskEntry`: folder obrazu lub prezentacji
  i / albo napis z opcjonalnym tytułem). Obrazy i prezentacje kopiuje `SlideshowLibrary` (jak „Obraz” / „Prezentacja”;
  każdy slajd prezentacji to osobny element). Dla planu, klawiatury i telefonu to **jeden slajd** `KioskSlide` (lista
  `KioskFrame` + odstęp; porównanie treści, nie list) — „Dalej” idzie do następnej pozycji planu. Elementy zmienia
  `KioskRotation` (Core, jedna dla rzutnika, „Na ekranie” i telefonu): co `Interval`, w kółko; czarny ekran = stoi;
  powrót do kiosku = od elementu, na którym skończył (osobno dla każdej pozycji); edycja na żywo zostawia ten sam
  element. Rzutnik: `ProjectedKiosk` (strona stała — równa każdej innej stronie tego kiosku, także po edycji na żywo) z wewnętrznym
  przejściem `KioskFramePage` (`KioskPlayback.Transition`; nowa strona tylko dla innego elementu); układ wg
  `KioskFrame.LayoutFor` — napis tam, gdzie obraz zostaje największy (pionowy do ok. 3:2 → napis z prawej, szerszy →
  pod spodem; sam obraz — cały ekran, sam napis — jak tekst). Kolumna „Pieśń” (`KioskViewModel`) edytuje kiosk na
  miejscu (nazwa, „Zmiana co … s”, elementy z przeciąganiem, „Obraz / Napis / Prezentacja”), każda zmiana od razu w planie.
  Telefon: `StateMessage.IsKiosk` + wiadomość `kiosk` (`KioskMessage`: `ItemId` — strona pokazuje element tylko przy stanie
  z tą samą pozycją, napis, podgląd JPEG 480 px z `IImagePreviews`); tryb „Ekran” w przeglądarce pokazuje ten podgląd i napis
  pod nim (nie układy rzutnika). Edycja dodana w starym modelu (np. kopiowanie skończone po zmianie zaznaczenia) trafia
  do aktualnego kiosku w planie; kolumna wczytuje kiosk od nowa, gdy treść zmieniła się poza nią.
  **Przejścia** między elementami (`KioskPlanItem.Transitions`: żaden = przenikanie, jeden = ten, kilka = losowo jeden;
  `null` = domyślnie Akwarela; tylko własne — przejścia planu ich nie zmieniają) i **powolny zoom** zdjęć (`SlowZoom`, efekt Kena Burnsa — `Views/SlowZoom`: przejście
  `TransformOperationsTransition`, bo Avalonia nie animuje `RenderTransform` klatkami kluczowymi; rusza po przejściu; tylko
  w ramce na wymiar zdjęcia — przesuwany element na cały ekran zostawiał jasną linię na brzegu).
- **Przejścia slajdów** (`App/Transitions`, ogólne). Są dwa rodzaje: **między pozycjami planu** — tylko efekty planu
  (`Plan.Transitions`, pierwszy przycisk przejścia w pasku górnym — napis to sam efekt, np. „Akwarela”, `PlanTransitionsViewModel`;
  pozycja nie ma własnego wejścia), i **między slajdami jednej pozycji** — drugi przycisk w pasku (`Plan.InnerTransitions`,
  `null` = jak między pozycjami; `PlanSlideTransitionsViewModel`), domyślny dla zwrotek pieśni, części tekstu i slajdów prezentacji
  (też kilku obrazów); pieśń, tekst i prezentacja mogą mieć własny (`SongPlanItem.SlideEffects`, `PresentationPlanItem.SlideEffects`,
  `null` = jak w planie; przycisk „Slajdy: …” w kolumnie „Pieśń” tylko przy kilku slajdach, `TransitionOverrideViewModel`;
  `PlanItem.InnerTransitions`). Kiosk ma tylko własne efekty między elementami; film i pojedynczy obraz — jeden slajd, bez
  przejść wewnętrznych. Wybór: „Przenikanie” albo efekty (kilka = losowo jeden; `TransitionChoiceViewModel`, jeden szablon
  w `MainWindow.axaml`). `ActivePlan` wpisuje efekty w `LiveItem.Transitions` / `InnerTransitions`; `LiveState.TransitionsFrom` (Core)
  daje je tylko między dwoma widocznymi slajdami planu — `LiveViewModel.PageTransition` zamienia je na przejście; zapis i opis
  wyboru: `Plan.WithTransitions`, `SlideTransitions.Default` / `Describe`. Ten sam slajd,
  czarny ekran i pieśń spoza planu — krótkie przenikanie (`LiveViewModel.SlideChange`). Przenikanie to `DirectCrossFade`: stara strona zostaje, aż nowa się wczyta i ułoży (`PageReadiness`), potem `CrossFadeView` miesza zrzuty obu stron (z tłem ekranu) — wprost, bez przyciemnienia. Nie przezroczystość strony: Avalonia nakłada `Opacity` na każdy element osobno (`RequiresFullOpacityHandling` nie pomaga), więc nieprzezroczyste tło i obraz nowej strony kolejno przyciemniały starą — w połowie ciemniej niż obie (czarne mignięcie); `CrossFade` z Avalonii tak samo. Animacji `FillMode.Forward` nie używamy do przejść — po końcu trzyma wartość ponad lokalną. `SlideTransition` (Core:
  Przesunięcie w losowym kierunku — wstecz w przeciwnym, Witraż, Płomień, Akwarela, Promień światła, Fala) →
  `ShaderPageTransition` (`IPageTransition` dla `TransitioningContentControl`): zrzut starej i nowej strony
  (`RenderTargetBitmap` → `SKImage`), na czas przejścia w ich miejscu `ShaderTransitionView` rysuje shader SkSL
  (`TransitionShaders`, `SKRuntimeEffect`; model gl-transitions: `from`, `to`, `progress`). Bez karty graficznej albo gdy odstępy między
  klatkami (mierzone w `Animate`, czyli z rysowaniem na karcie) przekraczają 100 ms (`TransitionShaders.SlowFrame`; 50 ms wyłączało efekty przy płynnych 20 klatkach/s; każde przejście, jego miejsce, wybrane i wylosowane efekty, wynik, klatki i przyczyna przenikania oraz wyłączenie efektów trafiają do `przejscia.log` — `TransitionLog`) pięć razy pod rząd w jednym przejściu — przejście
  kończy się od razu, a do końca działania zwykłe przenikanie (`SmoothnessGuard`, `Begin()` = osobny licznik dla każdego przejścia:
  przejścia naraz — rzutnik i „Na ekranie” — nie sumują wolnych klatek; pojedyncze zacięcia nie wyłączają efektów). Gdy przed albo
  po nie ma treści (kiosk wchodzi na ekran lub z niego schodzi), bez efektu — przejście strony już go ma. Nowa strona, której obraz jeszcze się wczytuje (`IWaitablePage` — `ProjectedImage`, `ProjectedKiosk`: pierwszy element), czeka na niego przed zrzutem (najwyżej 2 s) — inaczej efekt prowadziłby do pustego tła. Zrzut strony dostaje tło, na którym leży (`ShaderPageTransition.BackgroundOf` — strona z tekstem jest przezroczysta, a shadery rysują nieprzezroczyście: bez tła jasny ekran szedłby przez czerń). Uwaga: w SkSL `pow` z ujemną podstawą jest niezdefiniowane (kwadrat = `x * x`);
  `SKRuntimeShaderBuilder` nie zwalniamy (zwolniłby wspólny efekt). Klatki efektów: zrzuty `przejscie-*.png`.
- **Przeciąganie** (plan, części pieśni, części w edytorze — za uchwyt ⠿): `ListReorderDrag` — zwykłe przeciąganie myszą ze
  znacznikiem miejsca i przewijaniem przy krawędzi; wiersze implementują `IReorderableItem`.
- **Ekran.** `LiveSession` to jedyne źródło prawdy: polecenia przez `ILiveControl` (klawiatura —
  `LiveKeyboard`, przyciski, telefon), stan przez `ILiveStateSource` (`LiveState` — niezmienna
  migawka). Odbiorcy: kolumna „Na ekranie” (`LiveViewModel`), okno projekcji (`ProjectionWindow` +
  `SlideView`: płótno 1920×1080, Literata, tytuł z linią na pierwszym slajdzie — `ProjectedPage`),
  telefon i przeglądarka (`LinkRemoteServer`). `SetPlaylist` podmienia graną pieśń, gdy zmienił
  się jej układ części lub tekst (te same slajdy = ta sama migawka, bez przenikania).
- **Projekcja.** `ProjectionController` (F5): pełny ekran na innym monitorze, przenosi się przy
  podłączeniu/odłączeniu; bez drugiego ekranu tryb jednego ekranu z szybkim wyborem pieśni.
- **Dopasuj obraz** (jak w pps_viewer): `ProjectionCalibration` — 4 narożniki obrazu projekcji
  (korekcja trapezu rzutnika, zmniejszenie, przesunięcie) dla wszystkiego, co pokazuje projekcja.
  `Keystone` (Core) liczy macierz perspektywy prostokąt → czworokąt; `ProjectionWindow` nakłada ją
  jako `RenderTransform` na panel `Warped` (tekst zostaje wektorowy; panel ma tło ekranu — wygładzany brzeg slajdu
  nie świeci jasną linią), poza obrazem tło ekranu. Obsługa
  tylko na ekranie projekcji, bez przycisków w oknie operatora (jak w pps_viewer): ruch myszy nad
  obrazem pokazuje kursor, ramkę i narożniki (`CalibrationHandles`, bez napisów) na 3 s;
  K (na drugim ekranie, przy widocznych uchwytach) = tryb kalibracji: uchwyty i siatka
  (`CalibrationGrid`) stale, G = siatka, R = pełny obraz, Tab / Ctrl+strzałki = narożnik; poza tym
  trybem litery służą szukaniu pieśni (żeby np. „R” nie kasowało dopasowania).
  Rzutnik zwykle świeci szerzej niż ekran w sali — narożniki są wtedy poza ekranem i nie da się
  w nie trafić, dlatego przeciąganie **w dowolnym miejscu przesuwa najbliższy narożnik** (wypełniony
  punkt; przesuwa się o tyle, o ile kursor), z Shift — cały obraz. Kółko myszy =
  wielkość, Shift+kółko = szerokość, Ctrl+kółko = wysokość, Tab + Ctrl(+Shift)+strzałki = narożnik
  dokładnie, Esc = koniec kalibracji. Kliknięcie bez przeciągania w trybie jednego ekranu = dalej /
  wstecz (prawa / lewa połowa). Zapis: `AppSettings.ProjectionCorners` (8 liczb 0..1).
- **Telefon / przeglądarka.** `RemoteViewModel` paruje przez kod QR (tailcat-link, połączenie
  szyfrowane); sparowane urządzenia łączą się same przy starcie. Do podyktowania pod kodem QR jest krótki kod
  (np. `ajk59`, 5 znaków bez i/l/o/0/1, ważny 5 min): `ShortInvitationCodes` zapisuje długi kod w `kod.php`
  (`web/landing/public`, PHP na greysource.eu; odczyt jednorazowy, limit nieudanych prób z adresu), a pilot i panel
  zamieniają krótki kod (`#code=ajk59` albo wpisany) na długi; bez usługi zostaje sam kod QR.
  „Odłącz” (ikona w nagłówku pilota i panelu, z potwierdzeniem) wysyła `leave` — `LinkRemoteServer` zapomina
  urządzenie jak „Zapomnij” (chwilę po odpowiedzi), a strona usuwa swoje parowanie; powrót tylko z nowym kodem. **Telefon zmienia slajdy** (dodawanie pieśni —
  tylko z uprawnieniem, niżej) —
  jeden prosty widok (`web/remote`, tokeny kolorów i kroje jak w aplikacji): bieżący slajd z akordami
  jak w śpiewniku (tekst Literatą, refren jej kursywą `fonts/Literata-Italic.ttf`, zawinięty wers wcięty;
  24 px, mniej tylko gdy slajd się nie mieści — cały widoczny bez przewijania, bo muzyk gra i nie dotyka
  telefonu — i lekko zmniejszany, żeby wersy się nie zawijały; przełączniki „Akordy” i jasny / ciemny wygląd telefonu zapamiętane w przeglądarce), początek następnego
  slajdu, Wstecz / Dalej; ekran telefonu się nie wygasza. Rozmiary jak w aplikacji: 13 / 15 / 18 / 24
  (`--fs-*`), tekst pieśni 24 px lub mniej, akordy i „×N” 0,6 tego rozmiaru. Bez planu, gaszenia ekranu i edycji, a bez uprawnienia (niżej) także bez wyszukiwania
  — telefon można dać dziecku. Tryb „Ekran” (przeglądarka jako drugi ekran; przycisk tylko na
  komputerze z myszą) rysuje slajd jak rzutnik (`renderSlide`: płótno 16:9 w jednostkach `cqw`,
  wymiary i kolory jak `SlideView`). Stan niesie wersy z akordami (`LineMessage`: `Chords`, `TitleLength`,
  `Chorus`, `PartStart`), `ShowTitle`, `NextLines` i `IsVideo`; przy filmie telefon pokazuje czas, pasek postępu,
  pauzę, ±15 s i obraz ±100 ms (wiadomość `video` — `VideoMessage` z `PictureOffsetMs`, czas najwyżej raz na sekundę;
  polecenia `videoToggle`, `videoSkip`, `videoOffset`). Polecenia `blank` / `goto` / `showItem` /
  `showSong` zostają w protokole, strona ich nie używa. Podgląd bez aplikacji: kopia strony
  z udawanym `lib/tailcat/index.js`.
  **Dodawanie pieśni z telefonu** — uprawnienie urządzenia, domyślnie wyłączone: okno „Urządzenia” (`RemoteOverlay`) ma listę
  sparowanych urządzeń (`IRemoteServer.Devices`, `RemoteDeviceViewModel`) z polem „Dodaje pieśni” i „Zapomnij”
  (drugie kliknięcie „Zapomnieć?” rozparowuje — `ForgetAsync`, uprawnienie znika; wraca tylko z nowym kodem). Uprawnienie jest przy
  kluczu urządzenia (`ILinkPeer.Key` — nazwy nic nie uwierzytelnia), zapis `DevicePermissions` w
  `polaczenia/uprawnienia.json`; `LinkRemoteServer` podaje je w każdym poleceniu jako `RemoteCaller`, a zmianę wysyła od razu
  (wiadomość `device` — `DeviceMessage`, też w odpowiedzi na `hello`). Z uprawnieniem telefon ma „+” w nagłówku → wyszukiwarka
  na cały ekran (`search`, jak w aplikacji: numer albo słowa) → dotknięcie wyniku = `addNext`: pieśń zaraz za tą na ekranie
  (`Plan.InsertAfter`; bez planu — błąd), ekran się nie zmienia; komunikat „Następna: …” z „Cofnij” przez 5 s (`undoAdd` —
  tylko własne dodanie z telefonu i tylko póki pieśni nie pokazano). W oknie operatora dodana pieśń ma przez 8 s obwódkę
  i „Dodane z telefonu” (`RemoteCommandHandler.ItemAdded` → `PlanItemViewModel.IsAddedRemotely`); zaznaczenie operatora
  się nie zmienia.
- **Panel** (drugi komputer, tablet — `web/panel`, React): kolumny Plan · Pieśń · Na ekranie jak w oknie operatora (wąski
  ekran: jedna naraz, przełączana na dole) i okno „Plany”. Plany (lista, nowy, nazwa / data, otwórz, kopia na dziś, szablon,
  usuń), plan (dodaj z wyszukiwarki w miejsce, przeciąganie za ⠿ — zdarzenia wskaźnika, działa dotykiem, duplikuj, usuń,
  pokaż), układ części pieśni (pominięcie, kolejność, „Powtórz”, usuń, przywróć) i ekran (slajdy, Wstecz / Dalej, czarny
  ekran, film, kiosk). Edycja treści pieśni, nowe teksty, prezentacje, filmy i kioski — tylko w aplikacji (pliki są tam).
  Parowanie: okno „Urządzenia” → zakładka „Panel” (kod QR na `RemoteServerOptions.PanelPageUrl`, domyślnie `panel/` obok pilota).
  Urządzenie sparowane kodem panelu dostaje uprawnienie `DevicePermission.Panel` samo (`LinkRemoteServer.Recognize`: zaproszenie
  jednorazowe, które zniknęło, gdy pojawiło się nowe urządzenie); w oknie „Urządzenia” pole „Panel” je nadaje / odbiera. Bez
  uprawnienia polecenia panelu są odrzucane, a strona prosi o nie. Panel ma własną tożsamość w przeglądarce (`panelStore` —
  inny klucz IndexedDB niż pilot na tej samej domenie). Stan zawsze z aplikacji: `hello` → stan, plan, plany (`plans` —
  rozsyłane tylko panelom przy `PlanLibrary.Changed` / `ActivePlan.Opened`); kolumna „Pieśń” pyta `item` przy każdej zmianie planu.
  Próba bez aplikacji okienkowej: host z usługami Core + `LinkRemoteServer` (bez Avalonii), `npx vite preview` w `web/panel`
  z `public/derpmap.json` i `PanelUrl` = adres podglądu.
- **Odporność na błędy plików.** Zajęty plik planu: `PendingPlanStore` trzyma plan w pamięci (widoczny w każdym odczycie),
  następny udany zapis zapisuje całość; `ActivePlan.SaveError` / `PlanLibrary.SaveError` — panel odsyła „Nie udało się: …”.
  Ustawienia zapisują się przez plik tymczasowy; nieczytelny plik pieśni albo zła liczba (`SongFormatException`) pomija tylko
  tę pieśń. Pozycja z ekranu usunięta z planu: `LiveSession` pamięta jej sąsiadów (po `Id`) — „Dalej” / „Wstecz” idą do nich.
  `StateMessage.Session` (uruchomienie aplikacji): pilot i panel porównują `Version` tylko w tym samym uruchomieniu.
- **Aktualizacje.** `UpdateService` + `VelopackReleaseFeed`: tylko instalacja z
  `Uwielbienia-win-Setup.exe` sprawdza GitHub Releases, pobiera w tle i proponuje „Aktualizuj”.
- **Start** (`App.OnFrameworkInitializationCompleted`): DI → otwarcie ostatniego planu →
  okno operatora (maksymalizowane, F11 = pełny ekran) → projekcja → telefon → aktualizacje.

### Nowy rodzaj elementu prezentacji (obok pieśni i tekstu)

Plan, ekran, telefon i lista „Plany” obsługują nowy rodzaj bez zmian — wystarczą te kroki
(wzorzec: `PlanItemTypesTests`, rodzaj „ogłoszenie”):

1. **Pozycja planu** (Core, `Plans/Plan.cs`): `record XPlanItem(Guid Id, …) : PlanItem(Id)` z
   `WithNewId()` i atrybut `[JsonDerivedType(typeof(XPlanItem), "x")]` na `PlanItem` — inaczej
   plan się nie zapisze. Stare plany muszą się dalej wczytywać.
2. **Rodzaj** (Core, `Presentation/`): `XPlanItemType : IPlanItemType` — `Describe` (tytuł,
   numer albo `null`, `PlanItemKind`; nowy znacznik = nowa wartość enum) i `Present` (`LiveItem`
   ze slajdami). Rejestracja w `AppComposition` jako `IPlanItemType`.
3. **Slajd:** treść tekstowa — zwykły `Slide` z `Lines`. Inna treść (obraz, odliczanie…) —
   `record XSlide(…) : Slide(kod, etykieta, [])` i `DataTemplate DataType="p:XSlide"` w
   `Views/SlideView.axaml` **przed** szablonem `p:Slide`. Porównanie slajdów w `LiveSession`
   (podmiana na żywo) działa dla pól rekordu; kolekcje porównuje tylko `Lines`.
4. **Lista planu** (`MainWindow.axaml`, wiersz planu i „Dodaj do planu”): znacznik i kolor wg
   `PlanItemKind` (numer; kropka + `TextItemBrush` dla `Text`; znak slajdu + `PresentationItemBrush`
   dla `Presentation`; trójkąt odtwarzania w tym samym kolorze dla `Video`; dwa nałożone slajdy dla `Kiosk`). `PlanItemInfo.Note` = uwaga pod tytułem.
5. **Kolumna „Pieśń”:** `MainViewModel.ShowDetails` wybiera widok dla zaznaczonej pozycji;
   nowy rodzaj dostaje własny model widoku i szablon (wzorzec: `PresentationViewModel`).
6. **Dodawanie:** przycisk w oknie „Dodaj do planu” (`PlanAddViewModel`) tworzący pozycję i
   wstawiający ją przez `PlanActions.Insert(item, index)` (wzorzec: „Prezentacja”, „Obraz”; każdy przycisk ma ikonę — klasa `addKind`).
7. **Kolumna „Na ekranie” i telefon:** pokazują `Lines` — dla treści innej niż tekst dodać
   szablon w `MainWindow.axaml` (bieżący slajd / NASTĘPNA, wzorzec: obraz `ImageSlide`), a na rzutniku
   stronę (`ProjectedPage` → np. `ProjectedImage` z szablonem w `SlideView.axaml`, gdy treść ma
   zająć cały ekran). Telefon i ekran w przeglądarce dostają tylko wersy i `Label` — obraz
   wymagałby pola w `RemoteProtocol.cs` + `web/remote` (zmiana w obu miejscach).
8. Testy w Core (rodzaj, zapis planu z nową pozycją) i zrzuty (`tools/Uwielbienia.Screenshots`).

### Okno operatora

- Trzy kolumny o regulowanej i zapamiętywanej szerokości; nagłówek środkowej pokazuje, co jest
  wybrane („Pieśń” / „Tekst”, w edycji „Edycja pieśni”, „Nowy tekst”…). Tak je nazywamy wszędzie (UI,
  komentarze, dokumentacja): **Plan** (`PlanViewModel`) · **Pieśń** (pozycja zaznaczona w planie:
  czytanie i edycja — `PreviewViewModel`, `SongEditorViewModel`) · **Na ekranie** (to, co widzi
  sala — `LiveViewModel`). Nie używamy nazw „Podgląd”, „Prowadzenie”, „Zarządzanie”.
- Każda czynność ma jedno miejsce:
  - dodawanie do planu tylko z planu: „+”, prawy klik („Dodaj powyżej / poniżej”) albo pisanie
    gdziekolwiek w oknie → okno „Dodaj do planu” (`PlanAddViewModel`: zaznacz + „Wybierz”, Enter,
    dwuklik; „Nowa pieśń / Nowy tekst / Prezentacja / Obraz / Film / Kiosk”, z ikonami; „Obraz” = każdy wybrany obraz
    osobną pozycją, na cały ekran — jednoslajdowa prezentacja); plik prezentacji (lub obrazy,
    filmy) upuszczony na plan trafia za wiersz pod kursorem;
  - na ekran wyłącznie dwuklikiem albo prawym klikiem („Pokaż na ekranie”) w planie;
  - kolejność: przeciąganie w planie; usuwanie: ✕ / prawy klik (z potwierdzeniem);
  - „Duplikuj” (prawy klik w planie): niezależna kopia zaraz za oryginałem — pieśń lub tekst jako nowa
    własna (`ISongEditor.Copy`: cała treść, nowy numer / identyfikator, ten sam tytuł i układ części),
    prezentacja jako nowy folder (`ISlideshowLibrary.CopyAsync`); zmiana kopii nie zmienia oryginału;
  - edycja: „Edytuj” w kolumnie „Pieśń”;
  - układ pieśni w tym planie (lista części w kolumnie „Pieśń”): przeciąganie za nagłówek części,
    „⋯” / prawy klik („Powtórz”, „Usuń”), „Przywróć usuniętą część”, pole wyboru = pominięcie.
    Zapis: `SongPlanItem.Layout` (pełny układ z pominięciami), `Arrangement` (śpiewana część) i `Removed`
    (części usunięte w tym planie) — `WithLayout` wraca do `null`, gdy układ jest taki jak w pliku pieśni.
    Układ uzgadnia się z aktualną treścią przy każdym odczycie (`LayoutFor` / `ArrangementFor`): części,
    których już nie ma, znikają; nowe (dopisane w edytorze, nowa wersja śpiewnika), nieusunięte w planie,
    dochodzą na koniec jako śpiewane. Tekst ma kolejność z edytora — w planie tylko pominięcie i usunięcie
    (bez przeciągania i „Powtórz”). Edytor zachowuje kody części między zapisami (`SongDraft`: nowy kod tylko
    dla nowej części albo zmienionego rodzaju; nazwy części tekstu „Część n” wg kolejności), więc
    zapamiętane w planach pominięcia zostają przy tej samej treści.
- Kolumna „Pieśń” nie zmienia projekcji — wyjątek: zmiana układu części granej pieśni działa na
  żywo. Dla prezentacji pokazuje miniatury slajdów. Pusty ekran: kolumna „Na ekranie” bez przycisków.
- Nakładki: „Plany” (`PlansViewModel` → `PlanLibrary` — zakładki Wydarzenia/Szablony, lista + formularz; nazwa
  wymagana, bez daty w nazwie), „Dodaj do planu”, „Urządzenia”, potwierdzenia usuwania. Esc zamyka;
  w edytorze klawisze globalne (spacja, pisanie) nie działają.

### Dane i ustawienia (`AppPaths`, `%APPDATA%\Uwielbienia`, Linux `~/.config/Uwielbienia`)

`ustawienia.json` (`AppSettings`: motyw okna i ekranu, akordy, szerokości kolumn, ostatni plan,
`MaxLinesPerSlide`, `JoinVerseAndChorus`, opcjonalny folder pieśni, narożniki „Dopasuj obraz”, `PictureOffsetMs`), `plany/`, `teksty/`,
`prezentacje/` (kopie prezentacji, obrazów i kiosków oraz ich slajdy; nieużywane nie są dziś usuwane), `polaczenia/`
(tailcat-link; `uprawnienia.json` — urządzenia, które mogą dodawać pieśni, i panele), `aktualizacje.log`, `bledy.log` (nieoczekiwane błędy z datą — `CrashGuard`, `IErrorLog`: błąd czynności nie zamyka aplikacji), `przejscia.log` (dziennik przejść slajdów — `TransitionLog`; przy starcie ponad ok. 1 MB zaczyna się od nowa).

### Wygląd

- Paleta „nocny granat + świeca” (`Themes/Palette.axaml`, jasny i ciemny). Akcenty mają stałe
  znaczenie: świeca (`CandleBrush`) = to, co jest na ekranie; `ChordBrush` = akordy;
  `TextItemBrush` = teksty w planie; `PresentationItemBrush` = prezentacje, filmy i kioski w planie;
  `DangerBrush` = usuwanie i błędy. Typografia operatora: skala
  13/15/18/24 (`Themes/Controls.axaml`). Kroje: Atkinson Hyperlegible Next (interfejs), Literata
  (tytuły, tekst na rzutniku).
- Ikona: płomień świecy na nocnym granacie — `Assets/icon.ico` / `icon.png` (okna w XAML, `ApplicationIcon`
  w `Uwielbienia.Desktop.csproj`, `vpk pack --icon` w `build.py`); generuje ją `python app/tools/make_icon.py`.
- Rzutnik (`ProjectionAppearance`): tekst w polu 1640×1000 (marginesy 140 px z boków, 40 px u góry
  i u dołu — brzegi ekranu w sali załatwia „Dopasuj obraz”), za długi zmniejsza się w całości.
  Kolory: ciemny = czerń i ciepła kość słoniowa, jasny = biel i granat;
  tytuł pieśni w delikatnym ciepłym odcieniu z linią pod spodem; bez stałej wysokości wiersza
  (Literata ma długie ogonki liter). Slajd prezentacji: obraz na cały ekran na tle ekranu (czarnym albo jasnym).
- Przełączniki pokazują obecny stan („Czarne tło” ekranu w sali, „Ciemny wygląd”, podświetlone „Akordy”), kliknięcie zmienia go
  na drugi; polecenia (np. „Włącz projekcję”) mówią, co zrobią.
- Bez zbędnych objaśnień i podpisów: pusty stan, krótka podpowiedź w polu, ostrzeżenie przy
  usuwaniu — tak; opisy pod nagłówkami — nie. Teksty interfejsu po polsku.

### Zasady pracy

- Zmiana formatu `piesn.md` = zmiana parsera, writera i testów (`dotnet test app`).
- Po zmianach w UI sprawdzić wygląd: `dotnet run --project app/tools/Uwielbienia.Screenshots`.
- Strona pilota `app/web/remote` mówi protokołem z `Uwielbienia.Link/RemoteProtocol.cs`, a panel `app/web/panel` — także
  z `PanelProtocol.cs` (typy w `web/panel/src/protocol.ts`); zmiana protokołu = zmiana po obu stronach. Strony pilota przy
  zmianach panelu nie ruszamy (nowe pola i wiadomości ignoruje).
- Wydanie: `python deploy.py` (wersja w `version.txt`; buduje lokalnie, GitHub niczego nie buduje — tag i GitHub
  Release z plikami z `release/` przez API; `GITHUB_TOKEN` z Contents: write w `.env`; vpk w wersji `VelopackVersion`;
  nieudany release po tagu: `python deploy.py --github-only`).

### Do zrobienia (TODO)

- **LibreOffice na Linuksie:** eksport (`LibreOfficeExporter`) sprawdzony na Windows (LibreOffice 26.2);
  ręczna próba na Linuksie (ścieżki `soffice`, natywny PDFium z `Docnet.Core` w pakiecie linux-x64).
- **PWA dla pilota i panelu:** dziś `web/remote` i `web/panel` to zwykłe strony (bez manifestu i service workera),
  więc „Dodaj do ekranu głównego” otwiera je z paskiem przeglądarki. Dodać `manifest.webmanifest`
  (`display: standalone`, nazwa, `theme-color`, ikony z `Assets/icon.png`) i minimalny service worker na skorupę
  strony — chodzi o tryb standalone i ikonę, nie o pracę bez aplikacji (bez połączenia strony nic nie pokazują).
