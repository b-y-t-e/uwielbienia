"""Walidator formatu Teksty/*/piesn.md (reguły w CLAUDE.md).

Użycie: python tools/validate.py [katalog_teksty]
Kod wyjścia 1, gdy którykolwiek plik ma błędy.
"""
import pathlib
import re
import sys

SECTION = re.compile(r"^## \[(?P<kod>[A-Z]+\d*)\] (?P<nazwa>.+?)(?: \{x(?P<n>\d+)\})?$")
CODES = re.compile(r"^(I|V\d+|PC\d*|C\d*|B\d*|FC|T|INT\d*|E|O\d*)$")
KEYS = ["numer", "tytul", "kategoria", "numer_zrodlowy", "tonacja", "kolejnosc", "zrodlo"]
CATEGORIES = {"Pieśni Maryjne", "Msza Święta", "Uwielbienie", "Pieśni do Ducha Świętego"}
CHORDS = re.compile(r"\s*`(?P<akordy>[^`]*)`\s*$")
# notacja angielska zamiast polskiej: Am, Bb, F#, oraz Ais zamiast B (B♭)
BAD_CHORD = re.compile(r"(?<![A-Za-z])(?:[A-H]m(?!aj)|[A-Ha-h]b|[A-Ha-h]#|[Aa]is)(?![a-z])")


def check(path):
    errs = []
    text = path.read_bytes().decode("utf-8")
    if "\r" in text:
        errs.append("końce linii CRLF (wymagane LF)")
    text = text.replace("\r\n", "\n")
    parts = text.split("---\n", 2)
    if len(parts) < 3 or parts[0] != "":
        return ["brak front matter"]
    fm, body = parts[1], parts[2]

    meta = dict(re.findall(r"^(\w+): (.*)$", fm, re.M))
    for k in KEYS:
        if k not in meta:
            errs.append(f"brak klucza {k}")
    if meta.get("numer") and not path.parent.name.startswith(f"{int(meta['numer']):03d}-"):
        errs.append("numer nie zgadza się z nazwą folderu")
    if meta.get("kategoria", "").strip('"') not in CATEGORIES:
        errs.append(f"nieznana kategoria {meta.get('kategoria')}")
    m = re.match(r"^\[(.*)\]$", meta.get("kolejnosc", ""))
    order = [x.strip() for x in m.group(1).split(",")] if m else []
    if not order:
        errs.append("pusta lub błędna kolejnosc")

    lines = body.strip("\n").split("\n")
    title = meta.get("tytul", "").strip('"')
    if lines[0] != f"# {title}":
        errs.append("nagłówek # różny od tytul")
    if sum(1 for l in lines if l.startswith("# ")) != 1:
        errs.append("musi być dokładnie jeden nagłówek #")

    sections, cur, prev_blank, in_repeat = [], None, False, False
    for i, line in enumerate(lines[1:], start=2):
        if line.startswith("## "):
            s = SECTION.match(line)
            if not s:
                errs.append(f"zły nagłówek sekcji: {line}")
                continue
            if not CODES.match(s["kod"]):
                errs.append(f"nieznany kod {s['kod']}")
            if not prev_blank:
                errs.append(f"brak pustej linii przed {line}")
            sections.append(s["kod"])
            cur, in_repeat = s["kod"], False
        elif line == "":
            if prev_blank:
                errs.append("podwójna pusta linia")
            elif i < len(lines) and not lines[i].startswith("## "):
                errs.append(f"pusta linia wewnątrz sekcji (linia {i})")
        elif cur is None:
            errs.append(f"tekst poza sekcją: {line}")
        elif not line.startswith(">"):
            c = CHORDS.search(line)
            if c and BAD_CHORD.search(c["akordy"]):
                errs.append(f"niezalecana notacja akordów: {c['akordy']}")
            if line.startswith("|: "):
                if in_repeat:
                    errs.append(f"zagnieżdżone |: w sekcji {cur}")
                in_repeat = True
            if " :|" in line:
                if not in_repeat:
                    errs.append(f":| bez |: w sekcji {cur}")
                in_repeat = False
        prev_blank = line == ""
        if line == "" and in_repeat:
            errs.append(f"niezamknięte |: w sekcji {cur}")
            in_repeat = False
    if in_repeat:
        errs.append(f"niezamknięte |: w sekcji {cur}")

    if len(set(sections)) != len(sections):
        errs.append(f"zdublowana sekcja: {sections}")
    for code in order:
        if code not in sections:
            errs.append(f"kolejnosc: {code} nie istnieje jako sekcja")
    for code in sections:
        if code not in order:
            errs.append(f"sekcja {code} nieużyta w kolejnosc")
    return errs


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    root = pathlib.Path(sys.argv[1] if len(sys.argv) > 1 else "Teksty")
    files = sorted(root.glob("*/piesn.md"))
    bad = 0
    numbers = {}
    for f in files:
        numbers.setdefault(f.parent.name[:3], []).append(f.parent.name)
    for num, dirs in numbers.items():
        if len(dirs) > 1:
            print(f"uwaga: numer {num} użyty wielokrotnie: {', '.join(dirs)}")
    for f in files:
        errs = check(f)
        if errs:
            bad += 1
            print(f"{f.parent.name}: " + "; ".join(errs))
    print(f"plików: {len(files)}, z błędami: {bad}")
    sys.exit(1 if bad else 0)


if __name__ == "__main__":
    main()
