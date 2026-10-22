r"""Szybkie wyszukiwanie pieśni i wypisanie samego tekstu (bez nut i Markdownu).

Użycie:
    python tools/szukaj.py chce wywyzszac
    python tools/szukaj.py krew baranka --akordy   # akordy przy wersach
    python tools/szukaj.py chce wywyzszac --plik   # części raz, w kolejności z pliku
    python tools/szukaj.py 30                      # po numerze pieśni
    python tools/szukaj.py panie --nr 30           # wybór z listy trafień

Słowa można podać w dowolnej kolejności, bez polskich znaków i wielkości liter.
Trafienie w tytule ma pierwszeństwo przed trafieniem w treści.
Jedno trafienie = od razu pełny tekst gotowy do skopiowania.
Kilka trafień = lista do wyboru (interaktywnie lub przez `--nr NNN`).
Przy wyborze liczba z zakresu listy to zawsze pozycja, a `nr NNN` — numer
pieśni; gdy numer się dubluje (śpiewnik ma dwie pieśni 116), rozstrzyga pozycja.

W wypisanym tekście `(xN)` = tyle razy powtarzany wers, `[Nazwa części] xN` =
cała część, a powtarzany fragment wielu wersów jest wcięty i oznaczony na
brzegach: `/` pierwszy wers, `|` środkowe, `\` ostatni (`|` sam = fragment
z jednego wersu). `(xN)` po wersie `\` dotyczy całego fragmentu.
"""
import collections
import pathlib
import re
import sys
import unicodedata

SECTION = re.compile(r"^## \[(?P<kod>[A-Z]+\d*)\] (?P<nazwa>.+?)(?: \{x(?P<n>\d+)\})?$")
CHORDS = re.compile(r"\s*`(?P<akordy>[^`]*)`\s*$")
REPEAT = re.compile(r"\s*\{x(\d+)\}\s*$")
SECTION_TYPE = re.compile(r"^[A-Z]+")
BY_NUMBER = re.compile(r"^nr\s*(\d+)$", re.I)

TEKSTY = pathlib.Path(__file__).resolve().parent.parent / "Teksty"
WCIECIE = "  "

Wers = collections.namedtuple("Wers", "tekst akordy w_fragmencie poczatek koniec")


def norm(s):
    """Małe litery bez polskich znaków — do porównywania."""
    s = unicodedata.normalize("NFKD", s.replace("ł", "l").replace("Ł", "L"))
    s = "".join(c for c in s if not unicodedata.combining(c))
    return s.lower()


def _liczba(s):
    """Liczba całkowita z tekstu albo None (`isdigit` przyjmuje np. `²`)."""
    try:
        return int(s)
    except ValueError:
        return None


def _prefiks(wers):
    """Wcięcie i oznaczenie brzegów powtarzanego fragmentu."""
    if not wers.w_fragmencie:
        return ""
    if wers.poczatek and not wers.koniec:
        znak = "/"
    elif wers.koniec and not wers.poczatek:
        znak = "\\"
    else:
        znak = "|"
    return WCIECIE + znak + " "


def _wers(line, w_fragmencie):
    """(Wers, czy następny wers jest w powtarzanym fragmencie).

    Wers.tekst None = linia instrumentalna (same akordy)."""
    akordy = ""
    m = CHORDS.search(line)
    if m:
        akordy = m.group("akordy").strip()
        line = CHORDS.sub("", line)
    if not line.strip():
        return Wers(None, akordy, w_fragmencie, False, False), w_fragmencie
    m = REPEAT.search(line)
    powt = int(m.group(1)) if m else 1
    tekst = REPEAT.sub("", line)
    poczatek = tekst.lstrip().startswith("|:")
    koniec = tekst.rstrip().endswith(":|")
    if poczatek:
        tekst = tekst.replace("|:", "", 1)
    if koniec:
        tekst = tekst.rstrip()[:-2]
    tekst = tekst.strip()
    if powt > 1:
        tekst += f" (x{powt})"
    w_tym = w_fragmencie or poczatek
    return Wers(tekst, akordy, w_tym, poczatek, w_tym and koniec), w_tym and not koniec


def _linia(wers, akord):
    """Wers jako czysty tekst; None = nie ma czego wypisać."""
    if wers.tekst is None:
        tresc = akord
    else:
        tresc = f"{wers.tekst}   {akord}" if akord else wers.tekst
    if not tresc:
        return None
    return _prefiks(wers) + tresc


class Song:
    def __init__(self, path):
        text = path.read_text(encoding="utf-8").replace("\r\n", "\n")
        _, fm, self.body = text.split("---\n", 2)
        meta = dict(re.findall(r"^(\w+): (.*)$", fm, re.M))
        self.path = path
        self.numer = int(meta.get("numer", 0))
        self.tytul = meta.get("tytul", path.parent.name).strip('"')
        self.kategoria = meta.get("kategoria", "").strip('"')
        m = re.match(r"^\[(.*)\]$", meta.get("kolejnosc", ""))
        self.kolejnosc = [x.strip() for x in m.group(1).split(",")] if m else []
        self.sections = self._parse()

    def _parse(self):
        """{kod: (nazwa, powtorzenia_sekcji, [Wers])} w kolejności z pliku."""
        sections, kod, we_fragmencie = {}, None, False
        for line in self.body.split("\n"):
            m = SECTION.match(line)
            if m:
                kod, we_fragmencie = m.group("kod"), False
                sections[kod] = (m.group("nazwa"), int(m.group("n") or 1), [])
                continue
            if kod and line.strip() and not line.startswith(("#", ">")):
                wers, we_fragmencie = _wers(line.rstrip(), we_fragmencie)
                sections[kod][2].append(wers)
        return sections

    def haystack(self):
        return norm(self.tytul + "\n" + self.body)

    def _akordy(self, kod):
        """Akordy sekcji; sekcja bez akordów dziedziczy je z pierwszej sekcji
        tego samego typu, która je ma (V2 od V1, C2 od C) — wers po wersie."""
        wersy = self.sections[kod][2]
        wlasne = [w.akordy for w in wersy]
        if any(wlasne):
            return wlasne
        typ = SECTION_TYPE.match(kod).group(0)
        for inny, (_, _, inne_wersy) in self.sections.items():
            if inny == kod or SECTION_TYPE.match(inny).group(0) != typ:
                continue
            zrodlo = [w.akordy for w in inne_wersy]
            if any(zrodlo):
                return [zrodlo[i] if i < len(zrodlo) else "" for i in range(len(wersy))]
        return wlasne

    def tekst(self, akordy=False, kolejnosc=True):
        out = []
        kody = [k for k in self.kolejnosc if k in self.sections] if kolejnosc else []
        if not kody:
            kody = list(self.sections)
        for kod in kody:
            nazwa, powt, wersy = self.sections[kod]
            dziedziczone = self._akordy(kod) if akordy else []
            blok = []
            for i, wers in enumerate(wersy):
                akord = dziedziczone[i] if akordy and i < len(dziedziczone) else ""
                linia = _linia(wers, akord)
                if linia is not None:
                    blok.append(linia)
            if not blok:
                continue
            naglowek = f"[{nazwa}]" + (f" x{powt}" if powt > 1 else "")
            out.append(naglowek + "\n" + "\n".join(blok))
        return "\n\n".join(out)


def wczytaj():
    return sorted((Song(p) for p in TEKSTY.glob("*/piesn.md")), key=lambda s: s.numer)


def szukaj(songs, slowa):
    """(trafienia w tytule, wszystkie trafienia — tytuł albo treść; w wyniku
    trafienia w tytule idą na początku listy).

    Wszystkie słowa muszą pasować; sam numer = pieśń o tym numerze."""
    numer = _liczba(slowa[0]) if len(slowa) == 1 else None
    if numer is not None:
        po_numerze = [s for s in songs if s.numer == numer]
        if po_numerze:
            return po_numerze, po_numerze
    q = [norm(w) for w in slowa]
    w_tytule = [s for s in songs if all(w in norm(s.tytul) for w in q)]
    wszystkie = [s for s in songs if all(w in s.haystack() for w in q)]
    return w_tytule, wszystkie


def _wytnij_nr(argv):
    """(argv bez `--nr NNN`, numer albo None); ValueError przy braku/złej
    wartości oraz przy powtórzonym `--nr`."""
    numer = None
    reszta = []
    i = 0
    while i < len(argv):
        if argv[i] != "--nr":
            reszta.append(argv[i])
            i += 1
            continue
        if numer is not None:
            raise ValueError("--nr można podać tylko raz.")
        wartosc = _liczba(argv[i + 1]) if i + 1 < len(argv) else None
        if wartosc is None:
            raise ValueError("--nr wymaga numeru pieśni, np. --nr 30.")
        numer = wartosc
        i += 2
    return reszta, numer


def _wypisz_liste(trafienia, strumien):
    for i, s in enumerate(trafienia, 1):
        print(f"{i:2d}) {s.numer:3d}. {s.tytul}  ({s.kategoria})", file=strumien)


def _kandydaci(trafienia, wybor):
    """Pieśni wskazane wyborem: `nr NNN` = numer pieśni, liczba z zakresu listy
    = pozycja (poza zakresem — numer pieśni, by sam numer też działał)."""
    m = BY_NUMBER.match(wybor)
    if m:
        return [s for s in trafienia if s.numer == int(m.group(1))]
    liczba = _liczba(wybor)
    if liczba is None:
        return []
    if 1 <= liczba <= len(trafienia):
        return [trafienia[liczba - 1]]
    return [s for s in trafienia if s.numer == liczba]


def _dopytaj(trafienia):
    """Jedna pieśń z listy trafień albo None, gdy nie da się wybrać."""
    while len(trafienia) > 1:
        _wypisz_liste(trafienia, sys.stderr)
        print("\nPozycja z listy albo `nr NNN`: ", end="", file=sys.stderr)
        sys.stderr.flush()
        try:
            wybor = input().strip()
        except EOFError:
            print("\nDoprecyzuj zapytanie lub użyj --nr NNN.", file=sys.stderr)
            return None
        kandydaci = _kandydaci(trafienia, wybor)
        if not kandydaci:
            print("Nie ma takiej pozycji.", file=sys.stderr)
            return None
        if kandydaci == trafienia:
            print("To nadal kilka pieśni — wybierz pozycję z listy.", file=sys.stderr)
        trafienia = kandydaci
    return trafienia[0]


def main(argv):
    for strumien in (sys.stdout, sys.stderr):
        try:
            strumien.reconfigure(encoding="utf-8")  # polskie znaki w konsoli Windows
        except (AttributeError, ValueError):
            pass
    try:
        argv, nr = _wytnij_nr(argv)  # wartość --nr sprawdzamy przed resztą opcji
    except ValueError as blad:
        print(blad, file=sys.stderr)
        print(__doc__, file=sys.stderr)
        return 2
    akordy = "--akordy" in argv
    plik = "--plik" in argv
    argv = [a for a in argv if a not in ("--akordy", "--plik")]
    nieznane = [a for a in argv if a.startswith("--")]
    if nieznane:
        print(f"Nieznana opcja: {' '.join(nieznane)}", file=sys.stderr)
        print(__doc__, file=sys.stderr)
        return 2
    if not argv:
        print(__doc__)
        return 2

    w_tytule, wszystkie = szukaj(wczytaj(), argv)
    if not wszystkie:
        print("Nic nie znaleziono.", file=sys.stderr)
        return 1
    if nr is None:
        trafienia = w_tytule + [s for s in wszystkie if s not in w_tytule]
    else:
        trafienia = [s for s in wszystkie if s.numer == nr]
        if not trafienia:
            print(f"Nr {nr} nie jest w trafieniach.", file=sys.stderr)
            return 1
        if len(trafienia) > 1:
            print(f"Nr {nr} mają dwie pieśni — wybierz pozycję z listy:",
                  file=sys.stderr)

    wybrana = _dopytaj(trafienia)
    if wybrana is None:
        return 1
    print(f"{wybrana.tytul} ({wybrana.numer})\n")
    print(wybrana.tekst(akordy=akordy, kolejnosc=not plik))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
