#!/usr/bin/env python3
"""
Wydanie nowej wersji:

  1. podbija wersję w version.txt (x.y.z → x.y.z+1),
  2. buduje wszystko (build.py: testy, aplikacje, strona),
  3. wysyła stronę pilota przez FTP na greysource.eu/uwielbienie,
  4. commituje version.txt, wypycha commit i tag vX.Y.Z — GitHub Actions (.github/workflows/release.yml)
     buduje aplikacje Windows i Linux i wystawia je jako GitHub Release.

Dane FTP ze zmiennych środowiskowych FTP_HOST, FTP_USER, FTP_PASS (opcjonalnie FTP_DIR)
albo z pliku .env w głównym folderze (KLUCZ=wartość w wierszu; plik jest w .gitignore).

Użycie:
  python deploy.py              pełne wydanie
  python deploy.py --site-only  tylko zbudowanie i wysłanie strony (bez nowej wersji)
  python deploy.py --no-site    wydanie bez wysyłania strony
"""
import argparse
import ftplib
import os
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent
SITE = ROOT / "release" / "web" / "uwielbienie"
DEFAULT_FTP_DIR = "/greysource.eu/wwwroot/uwielbienie"

for _stream in (sys.stdout, sys.stderr):
    try:
        _stream.reconfigure(encoding="utf-8", errors="replace")
    except (AttributeError, ValueError):
        pass


def run(cmd: list[str], check: bool = True) -> subprocess.CompletedProcess:
    print(f"$ {' '.join(cmd)}", flush=True)
    return subprocess.run(cmd, check=check)


def git_output(*args: str) -> str:
    return subprocess.run(["git", *args], check=True, capture_output=True, text=True, cwd=ROOT).stdout.rstrip()


def load_env_file() -> None:
    env_file = ROOT / ".env"
    if not env_file.exists():
        return
    for line in env_file.read_text(encoding="utf-8").splitlines():
        line = line.strip()
        if line and not line.startswith("#") and "=" in line:
            key, value = line.split("=", 1)
            os.environ.setdefault(key.strip(), value.strip())


VERSION_FILE = ROOT / "version.txt"


def bump_version() -> str:
    major, minor, patch = VERSION_FILE.read_text().strip().split(".")
    new_version = f"{major}.{minor}.{int(patch) + 1}"
    VERSION_FILE.write_text(new_version + "\n")
    print(f"Wersja → {new_version}")
    return new_version


def require_ftp_settings() -> None:
    missing = [k for k in ("FTP_HOST", "FTP_USER", "FTP_PASS") if not os.environ.get(k)]
    if missing:
        sys.exit(f"Brak danych FTP: {', '.join(missing)} — ustaw zmienne środowiskowe albo utwórz .env "
                 "(albo użyj --no-site).")


def upload_site() -> None:
    target = os.environ.get("FTP_DIR", DEFAULT_FTP_DIR).rstrip("/")

    with ftplib.FTP(os.environ["FTP_HOST"], timeout=60) as ftp:
        ftp.login(os.environ["FTP_USER"], os.environ["FTP_PASS"])
        ftp.encoding = "utf-8"
        for file in sorted(p for p in SITE.rglob("*") if p.is_file()):
            relative = file.relative_to(SITE).as_posix()
            remote_dir = f"{target}/{relative.rpartition('/')[0]}".rstrip("/")
            ensure_dir(ftp, remote_dir)
            with file.open("rb") as data:
                ftp.storbinary(f"STOR {target}/{relative}", data)
            print(f"  ↑ {relative}")
    print("Strona wysłana: https://greysource.eu/uwielbienie/")


def ensure_dir(ftp: ftplib.FTP, path: str) -> None:
    current = ""
    for part in path.strip("/").split("/"):
        current += "/" + part
        try:
            ftp.mkd(current)
        except ftplib.error_perm:
            pass  # już istnieje


def release(new_version: str) -> None:
    tag = f"v{new_version}"
    branch = git_output("rev-parse", "--abbrev-ref", "HEAD")
    run(["git", "add", "version.txt"])
    run(["git", "commit", "--only", "version.txt", "-m", f"chore: wersja {new_version}"])
    run(["git", "push", "-u", "origin", branch])
    run(["git", "tag", tag])
    run(["git", "push", "origin", tag])
    print(f"\nTag {tag} wypchnięty — GitHub Actions zbuduje Windows + Linux i utworzy release.")


def warn_about_uncommitted_changes() -> None:
    """Commitujemy tylko version.txt, więc niezacommitowane zmiany ostrzegają, a nie przerywają wydania."""
    pending = [line[3:] for line in git_output("status", "--porcelain").splitlines() if line[3:] != "version.txt"]
    if not pending:
        return
    print("Uwaga: niezacommitowane zmiany. GitHub Actions buduje aplikacje z commita tagu, więc NIE wejdą")
    print("do aplikacji w GitHub Release — ale lokalne testy i strona pilota wysyłana na FTP je zawierają:")
    for path in pending:
        print(f"  {path}")


def main() -> None:
    parser = argparse.ArgumentParser(description="Wydaje nową wersję: strona na FTP + release na GitHubie.")
    parser.add_argument("--site-only", action="store_true", help="tylko zbuduj i wyślij stronę")
    parser.add_argument("--no-site", action="store_true", help="nie wysyłaj strony")
    args = parser.parse_args()
    load_env_file()

    if not args.no_site:
        require_ftp_settings()

    if args.site_only:
        run([sys.executable, str(ROOT / "build.py"), "--no-tests", "--no-apps"])
        upload_site()
        return

    warn_about_uncommitted_changes()

    previous_version_text = VERSION_FILE.read_text()
    new_version = bump_version()
    try:
        run([sys.executable, str(ROOT / "build.py")])
        if not args.no_site:
            upload_site()
    except (subprocess.CalledProcessError, OSError, ftplib.Error) as error:
        VERSION_FILE.write_text(previous_version_text)  # także niezacommitowana wartość użytkownika
        sys.exit(f"Wydanie przerwane ({error}) — wersja nie została zmieniona.")
    release(new_version)


if __name__ == "__main__":
    main()
