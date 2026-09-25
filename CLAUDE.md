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

### Nagłówek YAML (front matter) — wszystkie klucze obowiązkowe

| Klucz            | Typ             | Znaczenie |
|------------------|-----------------|-----------|
| `numer`          | int             | Numer pieśni w śpiewniku |
| `tytul`          | string (w `""`) | Tytuł; zwykle incipit (pierwszy wers) bez końcowej interpunkcji |
| `kategoria`      | string          | `Pieśni Maryjne`, `Msza Święta`, `Uwielbienie`, `Pieśni do Ducha Świętego` |
| `numer_zrodlowy` | int \| `null`   | Numer pomocniczy z PDF (np. `1069`) — numer w innym śpiewniku/bazie |
| `tonacja`        | string \| `null`| Pierwszy akord pieśni (przybliżenie tonacji) |
| `kolejnosc`      | lista kodów     | Kolejność wykonania (arrangement); kody muszą istnieć jako sekcje |
| `zrodlo`         | string          | Plik źródłowy (PDF, z którego pochodzi pieśń) |

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

- .NET 10 + Avalonia 12, MVVM (CommunityToolkit.Mvvm), DI w `AppComposition`. Clean Code i SOLID:
  logika w `Uwielbienia.Core` (bez UI), wszystkie źródła poleceń idą przez `ILiveControl`,
  wszyscy odbiorcy obrazu słuchają `ILiveStateSource`.
- Parser pieśni w aplikacji (`MarkdownSongParser`) implementuje reguły z tego pliku — zmiana
  formatu wymaga zmiany parsera i testów (`dotnet test app`).
- Pieśni są wbudowane w aplikację przy budowaniu (`Teksty/**/piesn.md`).
- Po zmianach w UI sprawdzić wygląd: `dotnet run --project app/tools/Uwielbienia.Screenshots`.
- Strona pilota `app/web/remote` mówi protokołem z `Uwielbienia.Link/RemoteProtocol.cs`;
  zmiana protokołu = zmiana w obu miejscach.
