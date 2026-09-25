"""Import pieśni ze śpiewnika PDF do Teksty/NNN-slug/piesn.md (format opisany w CLAUDE.md).

Użycie: python tools/import_spiewnik.py <plik.pdf> [katalog_wyjsciowy]
"""
import json
import re
import sys
import unicodedata
from pathlib import Path

import fitz  # PyMuPDF

CATEGORIES = {"Pieśni Maryjne", "Msza Święta", "Uwielbienie", "Pieśni do Ducha Świętego"}
COLUMN_SPLIT = 300  # x (pt) oddzielające lewą i prawą kolumnę strony A4
STANZA_GAP = 18     # odstęp pionowy (pt) oznaczający nową strofę

_CHORD = r"[A-Ha-h](?:is|es|s|#)?(?:maj7|moll|m|dim|sus\d?|add\d+)?\d*\+?(?:/[A-Ha-h](?:is|es|#)?)?"
CHORD_TOKEN = re.compile(rf"^(?:[|\\/()]*{_CHORD})+[|\\/()]*$|^[|/\\]+$")
CHORD_SPLIT = re.compile(rf"\(?{_CHORD}\)?|[|/\\]+")
STOP_WORDS = {"ach", "echa", "bada", "ha", "da", "be", "chce", "bh", "abba"}
REPEAT_END = re.compile(r"\s*[-/|(]*\s*(?:[xX×]\s*(\d)|(\d)\s*[xX×])\s*\)?\s*$")
VERSE_START = re.compile(r"^(\d{1,2})\s*\.\s*(.*)$")
REF_START = re.compile(r"^(?:ref(?:ren)?)\s*[.:]*\s*:?\s*(.*)$", re.I)
SONG_START = re.compile(r"^(\d{1,3})\s*\.?\s+(.*)$")
SOURCE_NUM = re.compile(r"^\d{3,4}$")
NOTE_END = re.compile(r"[\s,/]*\b(początek\s+[A-Ha-h][a-z]*|bar)\s*,?\s*$", re.I)


def read_lines(pdf_path):
    """Zwraca listę wierszy (kolejność czytania) jako dict: text, bold, gap."""
    out = []
    for page in fitz.open(pdf_path):
        cols = {0: [], 1: []}
        for block in page.get_text("dict")["blocks"]:
            for line in block.get("lines", []):
                spans = [s for s in line["spans"] if s["text"].strip()]
                if not spans:
                    continue
                col = 0 if spans[0]["bbox"][0] < COLUMN_SPLIT else 1
                cols[col].append((line["bbox"][1], spans))
        for col in (0, 1):
            prev_y = None
            for y, spans in sorted(cols[col], key=lambda t: t[0]):
                if prev_y is not None and abs(y - prev_y) < 2 and out:
                    # ten sam wiersz rozbity na dwie linie PDF (np. akordy osobno)
                    out[-1]["text"] += "   " + join_spans(spans)
                    continue
                gap = prev_y is None or y - prev_y > STANZA_GAP
                out.append({
                    "text": join_spans(spans),
                    "bold": bool(spans[0]["flags"] & 16) or bool(spans[-1]["flags"] & 16 and len(spans) > 1
                                                              and spans[0]["text"].strip().isdigit()),
                    "gap": gap,
                })
                prev_y = y
            if out:
                out[-1]["col_end"] = True
    return out


def join_spans(spans):
    text = spans[0]["text"]
    for prev, cur in zip(spans, spans[1:]):
        gap = cur["bbox"][0] - prev["bbox"][2]
        text += ("" if gap < 1.5 else "   ") + cur["text"]
    return text.replace(" ", " ").rstrip()


def split_chords(text):
    """Oddziela akordy z końca wersu. Zwraca (tekst, akordy|None)."""
    tokens = re.split(r"(\s+)", text.strip())
    words = tokens[::2]
    seps = [""] + tokens[1::2]
    i = len(words)
    while i > 0 and words[i - 1] and CHORD_TOKEN.match(words[i - 1]):
        i -= 1
    chord_words = words[i:]
    if not chord_words:
        return text.strip(), None
    if i > 0:
        wide = len(seps[i]) >= 2
        single = len(chord_words) == 1 and chord_words[0].islower()
        if not wide and single and (len(chord_words[0]) > 1 and chord_words[0] not in {"fis", "cis", "gis", "dis", "es", "b", "h"}
                                    or chord_words[0].lower() in STOP_WORDS):
            return text.strip(), None
        if not wide and len(chord_words) == 1 and chord_words[0].lower() in STOP_WORDS:
            return text.strip(), None
    lyric = "".join(t for pair in zip(words[:i], seps[1:i + 1] + [""]) for t in pair).strip()
    chords = " ".join(tok for w in chord_words for tok in CHORD_SPLIT.findall(w))
    return lyric, chords


def parse_line(raw):
    """Zwraca dict: text, chords, repeat, open, close, note."""
    note = None
    m = NOTE_END.search(raw)
    if m:
        note, raw = m.group(1), raw[:m.start()]
    text, chords = split_chords(raw)
    if chords:
        chords = chords.strip(" /\\") or None
    repeat = None
    close = False
    m = REPEAT_END.search(text)
    if m and (m.group(1) or m.group(2)):
        repeat = int(m.group(1) or m.group(2))
        close = "/" in text[m.start():m.end()] or text[:m.start()].rstrip().endswith("/")
        text = text[:m.start()].rstrip().rstrip("/").rstrip()
        text2, chords2 = split_chords(text)  # akordy przed znacznikiem powtórzenia
        if chords2 and not chords:
            text, chords = text2, chords2
    opened = False
    if text.startswith("/") and not text.startswith("//"):
        opened = True
        text = text[1:].lstrip()
    if text.endswith("/"):
        close = True
        text = text[:-1].rstrip()
    return {"text": text, "chords": chords, "repeat": repeat, "open": opened, "close": close, "note": note}


def slugify(s):
    s = s.replace("ł", "l").replace("Ł", "L")
    s = unicodedata.normalize("NFKD", s).encode("ascii", "ignore").decode()
    s = re.sub(r"[^a-zA-Z0-9]+", "-", s).strip("-").lower()
    return s[:60].rstrip("-")


def clean_title(text):
    t = re.sub(r"\s+", " ", text).strip()
    t = re.sub(r"[\s,.;:–\-!/]+$", "", t)
    if t.isupper():
        t = t[:1] + t[1:].lower()
    return t


class Song:
    def __init__(self, number, category):
        self.number = number
        self.category = category
        self.source_num = None
        self.title = None
        self.sections = []  # dict: code, name, lines
        self.order = []
        self.verse_no = 0
        self.chorus_texts = []

    def new_section(self, kind, verse_no=None):
        if kind == "V":
            self.verse_no = verse_no if verse_no else self.verse_no + 1
            code, name = f"V{self.verse_no}", f"Zwrotka {self.verse_no}"
            while any(s["code"] == code for s in self.sections):
                self.verse_no += 1
                code, name = f"V{self.verse_no}", f"Zwrotka {self.verse_no}"
        else:
            n = len(self.chorus_texts) + 1
            code, name = ("C", "Refren") if n == 1 else (f"C{n}", f"Refren {n}")
            self.chorus_texts.append(None)
        sec = {"code": code, "name": name, "lines": []}
        self.sections.append(sec)
        self.order.append(code)
        return sec

    def finish(self):
        # usuń puste sekcje; refren identyczny z wcześniejszym traktuj jako powtórzenie
        seen = {}
        remap = {}
        kept = []
        for sec in self.sections:
            key = "\n".join(l["text"] for l in sec["lines"])
            if sec["code"].startswith("C") and key in seen and key:
                remap[sec["code"]] = seen[key]
                continue
            if not sec["lines"]:
                remap[sec["code"]] = None
                continue
            if sec["code"].startswith("C"):
                seen[key] = sec["code"]
            kept.append(sec)
        order = [remap.get(c, c) for c in self.order]
        order = [c for c in order if c]
        # renumeracja refrenów po scaleniu
        chorus_codes = [s["code"] for s in kept if s["code"].startswith("C")]
        rename = {old: ("C" if i == 0 else f"C{i + 1}") for i, old in enumerate(chorus_codes)}
        for s in kept:
            if s["code"] in rename:
                s["code"] = rename[s["code"]]
                s["name"] = "Refren" if s["code"] == "C" else f"Refren {s['code'][1:]}"
        order = [rename.get(c, c) for c in order]
        # konwencja śpiewnika: refren po każdej zwrotce, jeśli nie zapisano inaczej
        if chorus_codes:
            full = []
            last_chorus = "C"
            for idx, code in enumerate(order):
                full.append(code)
                if code.startswith("C"):
                    last_chorus = code
                nxt = order[idx + 1] if idx + 1 < len(order) else None
                if code.startswith("V") and (nxt is None or nxt.startswith("V")):
                    full.append(last_chorus)
            order = full
        self.sections, self.order = kept, order

    def key(self):
        for sec in self.sections:
            for l in sec["lines"]:
                if l["chords"]:
                    m = CHORD_SPLIT.search(l["chords"].replace("(", "").replace("|", " "))
                    first = re.match(r"[A-Ha-h](?:is|es|s)?", l["chords"].lstrip("(|/\\ "))
                    return first.group(0) if first else None
        return None

    def to_markdown(self, source_name):
        fm = [
            "---",
            f"numer: {self.number}",
            f"tytul: {json.dumps(self.title, ensure_ascii=False)}",
            f"kategoria: {json.dumps(self.category, ensure_ascii=False)}",
            f"numer_zrodlowy: {self.source_num if self.source_num else 'null'}",
            f"tonacja: {json.dumps(self.key(), ensure_ascii=False) if self.key() else 'null'}",
            f"kolejnosc: [{', '.join(self.order)}]",
            f"zrodlo: {json.dumps(source_name, ensure_ascii=False)}",
            "---",
            "",
            f"# {self.title}",
        ]
        body = []
        for sec in self.sections:
            body.append("")
            body.append(f"## [{sec['code']}] {sec['name']}")
            for l in sec["lines"]:
                if l["note"]:
                    body.append(f"> {l['note']}")
                parts = []
                if l["open"]:
                    parts.append("|:")
                if l["text"]:
                    parts.append(l["text"])
                if l["close"]:
                    parts.append(":|")
                if l["repeat"]:
                    parts.append(f"{{x{l['repeat']}}}")
                if l["chords"]:
                    parts.append(f"`{l['chords']}`")
                body.append(" ".join(parts))
        return "\n".join(fm + body) + "\n"


def parse(pdf_path):
    lines = read_lines(pdf_path)
    songs = []
    category = None
    song = None
    section = None
    for ln in lines:
        raw = ln["text"].strip()
        if raw in CATEGORIES:
            category = raw
            continue
        compact = re.sub(r"\s+", "", raw)
        if SOURCE_NUM.match(compact):
            if song and not song.source_num:
                song.source_num = int(compact)
            continue
        m = SONG_START.match(raw) if ln["bold"] else None
        if m is None and ln["bold"]:
            m2 = re.match(r"^(\d)\s{0,3}(\d)\s+(.*)$", raw)  # numer rozbity na spany, np. "5|2"
            m = m2 and re.match(r"^(\d+)\s+(.*)$", m2.group(1) + m2.group(2) + " " + m2.group(3))
        if m and int(m.group(1)) >= (songs[-1].number if songs else 0):
            if song:
                song.finish()
            song = Song(int(m.group(1)), category)
            songs.append(song)
            rest = m.group(2)
            src = re.search(r"(?:^|\s)(\d{3,4})(?=\s|$)", rest)
            if src:
                song.source_num = int(src.group(1))
                rest = (rest[:src.start()] + "   " + rest[src.end():]).strip()
            section = None
            raw = rest
            ln = dict(ln, gap=False)
        if song is None:
            continue

        rm = REF_START.match(raw)
        vm = VERSE_START.match(raw)
        if rm and re.match(r"(?i)ref", raw):
            section = song.new_section("C")
            raw = rm.group(1)
        elif vm:
            section = song.new_section("V", int(vm.group(1)))
            raw = vm.group(2)
        elif section is None or ln["gap"]:
            section = song.new_section("V")
        if not raw.strip():
            continue
        parsed = parse_line(raw)
        if song.title is None:
            song.title = clean_title(parsed["text"]) or f"Pieśń {song.number}"
        if parsed["text"] or parsed["chords"]:
            section["lines"].append(parsed)
    if song:
        song.finish()
    return songs


def main():
    pdf = Path(sys.argv[1])
    out_dir = Path(sys.argv[2]) if len(sys.argv) > 2 else Path("Teksty")
    out_dir.mkdir(exist_ok=True)
    used = set()
    for song in parse(pdf):
        folder = f"{song.number:03d}-{slugify(song.title)}"
        while folder in used:
            folder += "-2"
        used.add(folder)
        (out_dir / folder).mkdir(exist_ok=True)
        (out_dir / folder / "piesn.md").write_text(song.to_markdown(pdf.name), encoding="utf-8")
        print(folder)


if __name__ == "__main__":
    main()
