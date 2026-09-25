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
  import_spiewnik.py   # jednorazowy import z PDF (PyMuPDF)
```

- Każda pieśń = osobny folder `NNN-slug`, gdzie `NNN` to numer w śpiewniku (3 cyfry),
  a `slug` to tytuł bez polskich znaków, małymi literami, słowa rozdzielone `-`.
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

Kody numerowane (`V`, `C`, `B`) przyjmują sufiks liczbowy od 1 (`C` ≡ `C1`, `B` ≡ `B1`).

## Format pliku `piesn.md`

```markdown
---
numer: 1
tytul: "Maryjo, śliczna Pani"
kategoria: "Pieśni Maryjne"
numer_zrodlowy: 1069
tonacja: "G"
kolejnosc: [V1, C, V2, C]
zrodlo: "spiewnik_17_08_10.pdf"
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
| `zrodlo`         | string          | Plik źródłowy |

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
     Notacja polska: dur wielką literą (`C`, `Fis`), moll małą (`a`, `fis`), `H` = B, `B` = B♭,
     dodatki: `7`, `7+`, `9`, `/` (bas lub separator taktu), `|`, `( )` = akord opcjonalny.
     Wiersz zawierający wyłącznie `` `akordy` `` = linia instrumentalna (bez tekstu).
     **Dziedziczenie akordów:** sekcja, w której żaden wers nie ma akordów, przejmuje je
     z pierwszej sekcji tego samego typu, która je ma (`V2`, `V3`… od `V1`; `C2` od `C`),
     wers po wersie według pozycji — tak jak w śpiewniku, gdzie kolejne zwrotki śpiewa się
     na tę samą melodię. Nie kopiujemy akordów do takich sekcji. Pojedynczy wers bez
     akordów w sekcji, która akordy ma, niczego nie dziedziczy.
   - `{xN}` — ten wers (lub fragment `|: … :|`) śpiewa się N razy.
   - `|:` … `:|` — początek i koniec fragmentu powtarzanego obejmującego kilka wersów;
     `{xN}` stoi po `:|`.
4. `> uwaga` — linia z uwagą wykonawczą (nie jest śpiewana, nie wyświetlać na rzutniku).
5. Sekcje rozdziela jedna pusta linia.

### Reguły parsowania (dla aplikacji)

- Front matter: YAML między pierwszymi dwoma liniami `---`.
- Sekcja: `^## \[(?<kod>[A-Z]+\d*)\] (?<nazwa>.+?)(?: \{x(?<n>\d+)\})?$`
- Akordy wersu: `` \s*`(?<akordy>[^`]*)`\s*$ ``
- Powtórzenie wersu: `\s*\{x(?<n>\d+)\}\s*$` (po usunięciu akordów)
- Repetycja wielowersowa: prefiks `|: `, sufiks ` :|`.
- Tryb bez akordów: usunąć `` `akordy` `` z wersów i pominąć linie instrumentalne.
- Tryb z akordami: dla sekcji bez akordów zastosować dziedziczenie (patrz wyżej).

## Konwencje edycji

- Tekst zachowuje pisownię ze źródła; poprawiamy tylko oczywiste literówki.
- Nie usuwać sekcji ani wersów przy edycji — zmieniać kolejność przez `kolejnosc`.
- Po imporcie z PDF struktura (podział na zwrotki/refreny, `kolejnosc`) jest heurystyczna
  i wymaga ręcznej weryfikacji — szczególnie sekcje `B`, `FC`, `T`, których PDF nie oznacza.
