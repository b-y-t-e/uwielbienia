#!/usr/bin/env python3
"""
Buduje wszystko do folderu release/:

  release/Uwielbienia-<wersja>-win-x64.exe       aplikacja Windows (jeden plik, bez instalacji .NET)
  release/Uwielbienia-<wersja>-linux-x64.tar.gz  aplikacja Linux (jeden plik wykonywalny)
  release/web/uwielbienie/                       strona pilota gotowa do wysłania na serwer

Użycie:
  python build.py                  testy + wszystkie platformy + strona
  python build.py --rid win-x64    tylko wybrana platforma (można podać kilka razy)
  python build.py --no-tests       bez testów
  python build.py --no-web         bez strony
  python build.py --no-apps        tylko strona
"""
import argparse
import shutil
import subprocess
import sys
import tarfile
import urllib.request
from pathlib import Path

ROOT = Path(__file__).resolve().parent
RELEASE = ROOT / "release"
APP = ROOT / "app"
DESKTOP = APP / "src" / "Uwielbienia.Desktop" / "Uwielbienia.Desktop.csproj"
WEB_SOURCE = APP / "web" / "remote"
WEB_TARGET = RELEASE / "web" / "uwielbienie"
DERP_MAP_URL = "https://tailcat.dev/derpmap.json"
RIDS = ["win-x64", "linux-x64"]

# Konsola Windows domyślnie używa strony kodowej ANSI, której nie wystarcza na polskie znaki.
for _stream in (sys.stdout, sys.stderr):
    try:
        _stream.reconfigure(encoding="utf-8", errors="replace")
    except (AttributeError, ValueError):
        pass


def run(cmd: list[str]) -> None:
    print(f"$ {' '.join(cmd)}", flush=True)
    subprocess.run(cmd, check=True)


def version() -> str:
    return (ROOT / "version.txt").read_text().strip()


def test() -> None:
    run(["dotnet", "test", str(APP), "-c", "Release"])
    run([sys.executable, str(ROOT / "tools" / "validate.py"), str(ROOT / "Teksty")])


def publish(rid: str, ver: str) -> Path:
    out = RELEASE / "publish" / rid
    run([
        "dotnet", "publish", str(DESKTOP),
        "-c", "Release", "-r", rid, "--self-contained",
        "-p:PublishSingleFile=true",
        "-p:IncludeNativeLibrariesForSelfExtract=true",
        "-p:EnableCompressionInSingleFile=true",
        "-p:DebugType=none",
        f"-p:Version={ver}",
        "-o", str(out),
    ])

    if rid.startswith("win"):
        artifact = RELEASE / f"Uwielbienia-{ver}-{rid}.exe"
        shutil.copy2(out / "Uwielbienia.exe", artifact)
    else:
        artifact = RELEASE / f"Uwielbienia-{ver}-{rid}.tar.gz"
        with tarfile.open(artifact, "w:gz") as tar:
            for file in sorted(out.iterdir()):
                info = tar.gettarinfo(str(file), arcname=f"Uwielbienia-{ver}/{file.name}")
                if file.name == "Uwielbienia":
                    info.mode = 0o755  # plik wykonywalny także po zbudowaniu na Windows
                with file.open("rb") as data:
                    tar.addfile(info, data)
    print(f"→ {artifact.relative_to(ROOT)}")
    return artifact


def build_web() -> Path:
    shutil.copytree(WEB_SOURCE, WEB_TARGET, ignore=shutil.ignore_patterns("*.md", "derpmap.json"))
    # Mapa serwerów pośredniczących musi leżeć w tej samej domenie co strona (przeglądarka nie może
    # jej pobrać z tailcat.dev — brak nagłówka CORS), a nieaktualna wskazuje serwery, których już nie ma.
    with urllib.request.urlopen(DERP_MAP_URL, timeout=30) as response:
        (WEB_TARGET / "derpmap.json").write_bytes(response.read())
    print(f"→ {WEB_TARGET.relative_to(ROOT)}")
    return WEB_TARGET


def main() -> None:
    parser = argparse.ArgumentParser(description="Buduje aplikacje i stronę do folderu release/.")
    parser.add_argument("--rid", action="append", choices=RIDS, help="platforma (domyślnie wszystkie)")
    parser.add_argument("--no-tests", action="store_true", help="pomiń testy")
    parser.add_argument("--no-web", action="store_true", help="pomiń stronę pilota")
    parser.add_argument("--no-apps", action="store_true", help="pomiń aplikacje (tylko strona)")
    args = parser.parse_args()

    ver = version()
    print(f"Uwielbienia {ver}")
    if RELEASE.exists():
        shutil.rmtree(RELEASE)
    RELEASE.mkdir()

    if not args.no_tests:
        test()
    for rid in [] if args.no_apps else args.rid or RIDS:
        publish(rid, ver)
    if not args.no_web:
        build_web()
    shutil.rmtree(RELEASE / "publish", ignore_errors=True)

    print(f"\nGotowe: {RELEASE}")


if __name__ == "__main__":
    main()
