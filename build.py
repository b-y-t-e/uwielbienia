#!/usr/bin/env python3
"""
Buduje wszystko do folderu release/:

  release/Uwielbienia-<wersja>-win-x64.exe       aplikacja Windows (jeden plik, bez instalacji .NET)
  release/Uwielbienia-<wersja>-linux-x64.tar.gz  aplikacja Linux (jeden plik wykonywalny)
  release/velopack/                              instalator i aktualizacje Windows (vpk pack: Setup.exe,
                                                 Portable.zip, pełny .nupkg, releases.win.json…)
  release/web/uwielbienie/                       strony do wysłania na serwer: strona startowa (app/web/landing, React)
  release/web/uwielbienie/telefon/               pilot (app/web/remote)
  release/web/uwielbienie/panel/                 panel (app/web/panel, React + Vite)

Velopack powstaje tylko na Windows, razem z win-x64; vpk musi mieć wersję VelopackVersion
z app/Directory.Build.props (dotnet tool update -g vpk --version X).

Użycie:
  python build.py                  testy + wszystkie platformy + Velopack + strona
  python build.py --rid win-x64    tylko wybrana platforma (można podać kilka razy)
  python build.py --no-tests       bez testów
  python build.py --no-web         bez strony (i panelu)
  python build.py --no-apps        tylko strona
  python build.py --no-velopack    bez instalatora i paczki aktualizacji Windows
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
APP_PROJECT = APP / "src" / "Uwielbienia.App" / "Uwielbienia.App.csproj"
ICON = APP / "src" / "Uwielbienia.App" / "Assets" / "icon.ico"
VELOPACK = RELEASE / "velopack"
WEB_SOURCE = APP / "web" / "remote"
WEB_TARGET = RELEASE / "web" / "uwielbienie"
PILOT_TARGET = WEB_TARGET / "telefon"
PANEL_SOURCE = APP / "web" / "panel"
PANEL_TARGET = WEB_TARGET / "panel"
LANDING_SOURCE = APP / "web" / "landing"
DERP_MAP_URL = "https://tailcat.dev/derpmap.json"
RIDS = ["win-x64", "linux-x64"]

# Konsola Windows domyślnie używa strony kodowej ANSI, której nie wystarcza na polskie znaki.
for _stream in (sys.stdout, sys.stderr):
    try:
        _stream.reconfigure(encoding="utf-8", errors="replace")
    except (AttributeError, ValueError):
        pass


def run(cmd: list[str], cwd: Path | None = None) -> None:
    print(f"$ {' '.join(cmd)}", flush=True)
    subprocess.run(cmd, check=True, cwd=cwd)


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
        "-p:IncludeAllContentForSelfExtract=true",  # VLC (libvlc/ z wtyczkami) w środku .exe
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


def required_vpk_version() -> str:
    """Wersja biblioteki Velopack w aplikacji — vpk musi być dokładnie taki sam."""
    result = subprocess.run(
        ["dotnet", "msbuild", str(APP_PROJECT), "-getProperty:VelopackVersion"],
        check=True, capture_output=True, text=True)
    return result.stdout.strip()


def installed_vpk_version() -> str | None:
    result = subprocess.run(["dotnet", "tool", "list", "-g"], check=True, capture_output=True, text=True)
    for line in result.stdout.splitlines():
        parts = line.split()
        if len(parts) >= 2 and parts[0].lower() == "vpk":
            return parts[1]
    return None


def check_vpk() -> None:
    """Przerywa z poleceniem do wpisania, gdy vpk nie ma albo ma inną wersję niż biblioteka Velopack."""
    required = required_vpk_version()
    installed = installed_vpk_version()
    if installed == required:
        return
    if installed is None:
        sys.exit(f"Brak narzędzia vpk (Velopack). Zainstaluj wersję zgodną z aplikacją:\n"
                 f"  dotnet tool install -g vpk --version {required}")
    sys.exit(f"vpk ma wersję {installed}, a aplikacja używa Velopack {required} (VelopackVersion w "
             f"app/Directory.Build.props). Zmień wersję vpk:\n"
             f"  dotnet tool update -g vpk --version {required}")


def pack_velopack(ver: str) -> Path:
    """Instalator Uwielbienia-win-Setup.exe i kanał aktualizacji, który UpdateService czyta z GitHub Releases."""
    out = RELEASE / "publish" / "velopack-win-x64"
    run([
        "dotnet", "publish", str(DESKTOP),
        "-c", "Release", "-r", "win-x64", "--self-contained",
        f"-p:Version={ver}",
        "-o", str(out),
    ])
    # Pusty folder wyjściowy = tylko pełna paczka, bez paczek różnicowych.
    run([
        "vpk", "pack",
        "--packId", "Uwielbienia",
        "--packVersion", ver,
        "--packDir", str(out),
        "--mainExe", "Uwielbienia.exe",
        "--icon", str(ICON),
        "--outputDir", str(VELOPACK),
    ])
    print(f"→ {VELOPACK.relative_to(ROOT)}")
    return VELOPACK


def build_web() -> Path:
    # greysource.eu/uwielbienie/: strona startowa (React), telefon/ — pilot (bez budowania), panel/ — panel (React)
    shutil.copytree(WEB_SOURCE, PILOT_TARGET, ignore=shutil.ignore_patterns("*.md", "derpmap.json"))
    # Mapa serwerów pośredniczących musi leżeć w tej samej domenie co strona (przeglądarka nie może
    # jej pobrać z tailcat.dev — brak nagłówka CORS), a nieaktualna wskazuje serwery, których już nie ma.
    with urllib.request.urlopen(DERP_MAP_URL, timeout=30) as response:
        derp_map = response.read()
    (PILOT_TARGET / "derpmap.json").write_bytes(derp_map)
    print(f"→ {PILOT_TARGET.relative_to(ROOT)}")
    if (PANEL_SOURCE / "package.json").exists():
        build_panel(derp_map)
    build_vite(LANDING_SOURCE)
    shutil.copytree(LANDING_SOURCE / "dist", WEB_TARGET, dirs_exist_ok=True)
    shutil.copy2(WEB_SOURCE / "web.config", WEB_TARGET / "web.config")  # typy plików i brak pamięci podręcznej
    print(f"→ {WEB_TARGET.relative_to(ROOT)} (strona startowa)")
    return WEB_TARGET


def build_vite(source: Path) -> None:
    """Strona React + Vite (panel, strona startowa): npm ci + npm run build → source/dist."""
    npm = shutil.which("npm")  # na Windows npm.cmd
    if npm is None:
        sys.exit(f"Brak npm (Node.js) — potrzebny do zbudowania {source.relative_to(ROOT)} (albo użyj --no-web).")
    install = "ci" if (source / "package-lock.json").exists() else "install"
    run([npm, install], cwd=source)
    run([npm, "run", "build"], cwd=source)


def build_panel(derp_map: bytes) -> Path:
    """Panel (React + Vite): npm run build → release/web/uwielbienie/panel/ z własną kopią derpmap.json."""
    build_vite(PANEL_SOURCE)
    shutil.copytree(PANEL_SOURCE / "dist", PANEL_TARGET)
    # Panel pobiera derpmap.json względem własnego adresu.
    (PANEL_TARGET / "derpmap.json").write_bytes(derp_map)
    print(f"→ {PANEL_TARGET.relative_to(ROOT)}")
    return PANEL_TARGET


def main() -> None:
    parser = argparse.ArgumentParser(description="Buduje aplikacje i stronę do folderu release/.")
    parser.add_argument("--rid", action="append", choices=RIDS, help="platforma (domyślnie wszystkie)")
    parser.add_argument("--no-tests", action="store_true", help="pomiń testy")
    parser.add_argument("--no-web", action="store_true", help="pomiń stronę pilota i panel")
    parser.add_argument("--no-apps", action="store_true", help="pomiń aplikacje (tylko strona)")
    parser.add_argument("--no-velopack", action="store_true",
                        help="pomiń instalator i paczkę aktualizacji Windows (vpk pack)")
    args = parser.parse_args()

    rids = [] if args.no_apps else args.rid or RIDS
    velopack = "win-x64" in rids and not args.no_velopack
    if velopack and sys.platform != "win32":
        print("Velopack pominięty — instalator Windows powstaje tylko na Windows.")
        velopack = False
    if velopack:
        check_vpk()

    ver = version()
    print(f"Uwielbienia {ver}")
    if RELEASE.exists():
        shutil.rmtree(RELEASE)
    RELEASE.mkdir()

    if not args.no_tests:
        test()
    for rid in rids:
        publish(rid, ver)
    if velopack:
        pack_velopack(ver)
    if not args.no_web:
        build_web()
    shutil.rmtree(RELEASE / "publish", ignore_errors=True)

    print(f"\nGotowe: {RELEASE}")


if __name__ == "__main__":
    main()
