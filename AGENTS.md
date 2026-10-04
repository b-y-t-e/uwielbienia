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
build.py               # buduje aplikacje i stronę do release/
deploy.py              # wydanie: strona na FTP + tag → GitHub Release (.github/workflows/release.yml)
app/                   # aplikacja rzutnika (Avalonia, .NET 10) + strona pilota — patrz app/README.md
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
| `rodzaj`         | `"tekst"`       | Opcjonalny, tylko w plikach lokalnych aplikacji: **tekst** (część Mszy, modlitwa, ogłoszenie…) — bez akordów, `numer: null`, `tytul` może być pusty (`""`), części `[O1] Część 1`, `[O2] Część 2`… |

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
| `src/Uwielbienia.Core` | logika bez UI: pieśni (`Songs/`), plany (`Plans/`), ekran (`Presentation/`), prezentacje (`Slideshows/`), aktualizacje (`Updates/`) |
| `src/Uwielbienia.Link` | tailcat-link: protokół JSON (`RemoteProtocol`), polecenia z telefonu (`RemoteCommandHandler`), serwer (`LinkRemoteServer`) |
| `src/Uwielbienia.App` | Avalonia: widoki (`Views/`), modele widoków (`ViewModels/`), usługi okienkowe (`Services/`), motywy (`Themes/`), czcionki, korzeń DI `AppComposition` |
| `src/Uwielbienia.Desktop` | program Windows/Linux (`Program.cs`, Velopack) |
| `tests/Uwielbienia.Core.Tests` | testy rdzenia i protokołu (m.in. parsowanie i zapis wszystkich pieśni z `Teksty/`) |
| `tools/Uwielbienia.Screenshots` | renderuje okna do PNG bez wyświetlania (scenariusz w `Program.cs`) |
| `web/remote` | strona pilota i ekranu w przeglądarce (`greysource.eu/uwielbienie`), bez kroku budowania |

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
  znaków i interpunkcji, całymi słowami — `SongPlanItemType.TitleLineCount`). W śpiewniku tytuł to
  zwykle incipit — wtedy zamiast tytułu wersy, które go powtarzają, mają kolor tytułu
  (`SlideLine.IsTitle`), co zaznacza początek pieśni.
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
  widoki zaczynają od początku). Dodawanie do planu: `PlanActions` (`Insert`, `InsertAt`, `ItemAdded`).
- **Rodzaje pozycji planu.** Plan nie zakłada, że pozycja to pieśń: `IPlanItemTypes`
  (`PlanItemTypes` — rejestr wszystkich `IPlanItemType`) daje opis na liście (`PlanItemInfo`:
  tytuł, numer, `PlanItemKind`, uwaga) i slajdy (`LiveItem`), a jego `Changed` (zmieniona treść)
  przebudowuje plan. Rodzaje: `SongPlanItemType` (pieśń i tekst; też `ISongPresenter` — pieśń
  spoza planu na ekran) i `PresentationPlanItemType` (prezentacja).
- **Prezentacje** (`PresentationPlanItem`: folder + tytuł). `SlideshowLibrary` kopiuje plik
  PowerPoint (`.pptx .ppt .pps .ppsx .pptm .ppsm .odp`) albo obrazy (`.png .jpg .jpeg .bmp .webp`,
  kilka = jedna prezentacja) do `prezentacje/{id8}-{slug}/` — plan działa po wyjęciu pendrive'a.
  Slajdy prezentacji eksportuje raz, w tle, `PowerPointExporter` (`ISlideExporter`, App/Services —
  jak w pps_viewer: automatyzacja COM PowerPointa na wątku STA, PNG 1920 px, bez slajdów ukrytych)
  do `slajdy/` (najpierw `slajdy.tmp`, potem znacznik `.complete`). Do tego czasu pozycja nie ma
  slajdów, a plan pokazuje uwagę („Slajd 3 z 20…”, błąd + „Spróbuj ponownie”); `Changed` po
  zakończeniu podmienia ją na ekranie. Bez PowerPointa — dziś tylko obrazy (TODO niżej). Slajd to
  `ImageSlide` (plik obrazu); obrazy wczytuje w tle `SlideImages` (pamięć podręczna, następny
  slajd zawczasu, miniatury dla operatora).
- **Przeciąganie** (plan, części pieśni): `ListReorderDrag` — zwykłe przeciąganie myszą ze
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
  jako `RenderTransform` na panel `Warped` (tekst zostaje wektorowy), poza obrazem czerń. Obsługa
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
  szyfrowane); sparowane urządzenia łączą się same przy starcie. **Telefon tylko zmienia slajdy** —
  jeden prosty widok (`web/remote`, tokeny kolorów i kroje jak w aplikacji): bieżący slajd z akordami
  jak w śpiewniku (tekst Literatą, refren jej kursywą `fonts/Literata-Italic.ttf`, zawinięty wers wcięty;
  rozmiar dopasowany do ekranu bez przewijania — muzyk gra i nie dotyka telefonu — i lekko zmniejszany,
  żeby wersy się nie zawijały), początek następnego
  slajdu, Wstecz / Dalej; ekran telefonu się nie wygasza. Bez planu, wyszukiwania, gaszenia ekranu i
  edycji — telefon można dać dziecku. Tryb „Ekran” (przeglądarka jako drugi ekran; przycisk tylko na
  komputerze z myszą) rysuje slajd jak rzutnik (`renderSlide`: płótno 16:9 w jednostkach `cqw`,
  wymiary i kolory jak `SlideView`). Stan niesie wersy z akordami (`LineMessage`: `Chords`, `Title`,
  `Chorus`, `PartStart`), `ShowTitle` i `NextLines`. Polecenia `blank` / `goto` / `showItem` /
  `showSong` / `search` zostają w protokole, strona ich nie używa. Podgląd bez aplikacji: kopia strony
  z udawanym `lib/tailcat/index.js`.
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
   dla `Presentation`). `PlanItemInfo.Note` = uwaga pod tytułem.
5. **Kolumna „Pieśń”:** `MainViewModel.ShowDetails` wybiera widok dla zaznaczonej pozycji;
   nowy rodzaj dostaje własny model widoku i szablon (wzorzec: `PresentationViewModel`).
6. **Dodawanie:** przycisk w oknie „Dodaj do planu” (`PlanAddViewModel`) tworzący pozycję i
   wstawiający ją przez `PlanActions.Insert(item, index)` (wzorzec: „+ Prezentacja”).
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
  - dodawanie do planu tylko z planu: „+”, prawy klik („Dodaj przed… / po…”) albo pisanie
    gdziekolwiek w oknie → okno „Dodaj do planu” (`PlanAddViewModel`: zaznacz + „Wybierz”, Enter,
    dwuklik; „+ Nowa pieśń / + Nowy tekst / + Prezentacja”); plik prezentacji (lub obrazy)
    upuszczony na plan trafia za wiersz pod kursorem;
  - na ekran wyłącznie dwuklikiem albo prawym klikiem („Pokaż na ekranie”) w planie;
  - kolejność: przeciąganie w planie; usuwanie: ✕ / prawy klik (z potwierdzeniem);
  - „Duplikuj” (prawy klik w planie): niezależna kopia zaraz za oryginałem — pieśń lub tekst jako nowa
    własna (`ISongEditor.Copy`: cała treść, nowy numer / identyfikator, ten sam tytuł i układ części),
    prezentacja jako nowy folder (`ISlideshowLibrary.CopyAsync`); zmiana kopii nie zmienia oryginału;
  - edycja: „Edytuj” w kolumnie „Pieśń”;
  - układ pieśni w tym planie (lista części w kolumnie „Pieśń”): przeciąganie za nagłówek części,
    „⋯” / prawy klik („Powtórz”, „Usuń”), „Przywróć usuniętą część”, pole wyboru = pominięcie.
    Zapis: `SongPlanItem.Layout` (pełny układ z pominięciami) i `Arrangement` (śpiewana część) —
    `WithLayout` wraca do `null`, gdy układ jest taki jak w pliku pieśni.
- Kolumna „Pieśń” nie zmienia projekcji — wyjątek: zmiana układu części granej pieśni działa na
  żywo. Dla prezentacji pokazuje miniatury slajdów. Pusty ekran: kolumna „Na ekranie” bez przycisków.
- Nakładki: „Plany” (`PlansViewModel` — zakładki Wydarzenia/Szablony, lista + formularz; nazwa
  wymagana, bez daty w nazwie), „Dodaj do planu”, „Telefon”, potwierdzenia usuwania. Esc zamyka;
  w edytorze klawisze globalne (spacja, pisanie) nie działają.

### Dane i ustawienia (`AppPaths`, `%APPDATA%\Uwielbienia`, Linux `~/.config/Uwielbienia`)

`ustawienia.json` (`AppSettings`: motyw okna i ekranu, akordy, szerokości kolumn, ostatni plan,
`MaxLinesPerSlide`, `JoinVerseAndChorus`, opcjonalny folder pieśni, narożniki „Dopasuj obraz”), `plany/`, `teksty/`,
`prezentacje/` (kopie prezentacji i ich slajdy; nieużywane nie są dziś usuwane), `polaczenia/`
(tailcat-link), `aktualizacje.log`.

### Wygląd

- Paleta „nocny granat + świeca” (`Themes/Palette.axaml`, jasny i ciemny). Akcenty mają stałe
  znaczenie: świeca (`CandleBrush`) = to, co jest na ekranie; `ChordBrush` = akordy;
  `TextItemBrush` = teksty w planie; `PresentationItemBrush` = prezentacje w planie;
  `DangerBrush` = usuwanie i błędy. Typografia operatora: skala
  13/15/18/24 (`Themes/Controls.axaml`). Kroje: Atkinson Hyperlegible Next (interfejs), Literata
  (tytuły, tekst na rzutniku).
- Ikona: płomień świecy na nocnym granacie — `Assets/icon.ico` / `icon.png` (okna w XAML, `ApplicationIcon`
  w `Uwielbienia.Desktop.csproj`, `vpk pack --icon` w `release.yml`); generuje ją `python app/tools/make_icon.py`.
- Rzutnik (`ProjectionAppearance`): tekst w polu 1640×1000 (marginesy 140 px z boków, 40 px u góry
  i u dołu — brzegi ekranu w sali załatwia „Dopasuj obraz”), za długi zmniejsza się w całości.
  Kolory: ciemny = czerń i ciepła kość słoniowa, jasny = biel i granat;
  tytuł pieśni w delikatnym ciepłym odcieniu z linią pod spodem; bez stałej wysokości wiersza
  (Literata ma długie ogonki liter). Slajd prezentacji: obraz na cały ekran na czarnym tle.
- Bez zbędnych objaśnień i podpisów: pusty stan, krótka podpowiedź w polu, ostrzeżenie przy
  usuwaniu — tak; opisy pod nagłówkami — nie. Teksty interfejsu po polsku.

### Zasady pracy

- Zmiana formatu `piesn.md` = zmiana parsera, writera i testów (`dotnet test app`).
- Po zmianach w UI sprawdzić wygląd: `dotnet run --project app/tools/Uwielbienia.Screenshots`.
- Strona pilota `app/web/remote` mówi protokołem z `Uwielbienia.Link/RemoteProtocol.cs`;
  zmiana protokołu = zmiana w obu miejscach.
- Wydanie: `python deploy.py` (wersja w `version.txt`, tag → `.github/workflows/release.yml`).

### Do zrobienia (TODO)

- **Prezentacje bez Microsoft Office (Linux i Windows bez PowerPointa):** eksport slajdów przez
  LibreOffice w trybie headless — druga implementacja `ISlideExporter` (np. `LibreOfficeExporter`),
  wybierana, gdy PowerPoint jest niedostępny (`Type.GetTypeFromProgID("PowerPoint.Application")`
  zwraca `null`; na Linuksie zawsze). Szukać `soffice` w `PATH` i typowych miejscach
  (`C:\Program Files\LibreOffice\program\soffice.exe`, `/usr/bin/soffice`, `/usr/lib/libreoffice/program/soffice`,
  flatpak/snap). Konwersja `soffice --headless --convert-to pdf --outdir <tmp> <plik>` (osobny
  profil `-env:UserInstallation=file:///<tmp>/profil`, żeby nie kolidować z otwartym LibreOffice),
  potem PDF → PNG 1920 px na slajd (biblioteka do renderowania PDF, np. PDFium/Docnet — ta sama
  posłuży do dodawania PDF jako prezentacji). Postęp przez `IProgress` jak w `PowerPointExporter`;
  gdy nie ma ani PowerPointa, ani LibreOffice — czytelny komunikat w planie („Zainstaluj
  LibreOffice albo dodaj slajdy jako obrazy”). Test z fałszywym `ISlideExporter` + ręczna próba na
  Linuksie.
