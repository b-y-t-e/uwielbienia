#!/usr/bin/env python3
"""
Wydanie nowej wersji — wszystko na tym komputerze (GitHub niczego nie buduje):

  1. sprawdza dane FTP, GITHUB_TOKEN i wersję vpk (przed zmianą wersji),
  2. podbija wersję w version.txt (x.y.z → x.y.z+1),
  3. buduje wszystko (build.py: testy, aplikacje Windows i Linux, Velopack, strona i panel),
  4. wysyła strony przez FTP na greysource.eu/uwielbienie (start, telefon/, panel/),
  5. commituje version.txt, wypycha commit i tag vX.Y.Z,
  6. tworzy GitHub Release „Uwielbienia vX.Y.Z” (REST API) i wysyła do niego pliki z release/
     (aplikacje i kanał aktualizacji Velopack, który czyta UpdateService).

Gdy po wypchnięciu tagu nie uda się utworzyć release'u, `python deploy.py --github-only` dokończy go
z plików w release/ (pliki już wysłane pomija).

Dane ze zmiennych środowiskowych albo z pliku .env w głównym folderze (KLUCZ=wartość w wierszu;
plik jest w .gitignore): FTP_HOST, FTP_USER, FTP_PASS (opcjonalnie FTP_DIR) oraz GITHUB_TOKEN
(token z uprawnieniem Contents: write do repozytorium).

Użycie:
  python deploy.py               pełne wydanie
  python deploy.py --site-only   tylko zbudowanie i wysłanie strony (bez nowej wersji)
  python deploy.py --no-site     wydanie bez wysyłania strony
  python deploy.py --no-github   wydanie bez GitHub Release (tylko tag)
  python deploy.py --github-only utworzenie / uzupełnienie GitHub Release dla wersji z version.txt
"""
import argparse
import ftplib
import json
import os
import re
import subprocess
import sys
import urllib.error
import urllib.parse
import urllib.request
from pathlib import Path

import build

ROOT = Path(__file__).resolve().parent
RELEASE = ROOT / "release"
SITE = RELEASE / "web" / "uwielbienie"
VELOPACK = RELEASE / "velopack"
DEFAULT_FTP_DIR = "/greysource.eu/wwwroot/uwielbienie"
GITHUB_API = "https://api.github.com"

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


def current_version() -> str:
    return VERSION_FILE.read_text().strip()


def bump_version() -> str:
    major, minor, patch = current_version().split(".")
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


# --- GitHub Release ---------------------------------------------------------------------------------

def github_repository() -> str:
    """„właściciel/nazwa” z adresu origin (https://github.com/x/y.git albo git@github.com:x/y.git)."""
    url = git_output("remote", "get-url", "origin")
    match = re.search(r"github\.com[:/]([^/]+)/(.+?)(?:\.git)?/?$", url)
    if not match:
        sys.exit(f"origin ({url}) nie wskazuje repozytorium na GitHubie.")
    return f"{match.group(1)}/{match.group(2)}"


def github_request(method: str, url: str, body: dict | None = None) -> dict | list | None:
    data = None if body is None else json.dumps(body).encode("utf-8")
    request = urllib.request.Request(url, data=data, method=method, headers={
        "Authorization": f"Bearer {os.environ['GITHUB_TOKEN']}",
        "Accept": "application/vnd.github+json",
        "X-GitHub-Api-Version": "2022-11-28",
        "Content-Type": "application/json",
    })
    with urllib.request.urlopen(request, timeout=60) as response:
        content = response.read()
    return json.loads(content) if content else None


def describe_http_error(error: urllib.error.HTTPError) -> str:
    try:
        details = error.read().decode("utf-8", errors="replace")
    except OSError:
        details = ""
    return f"HTTP {error.code} {error.reason} {details}".strip()


def require_github() -> str:
    """Sprawdza token i prawo zapisu do repozytorium, zanim cokolwiek się zmieni. Zwraca „właściciel/nazwa”."""
    if not os.environ.get("GITHUB_TOKEN"):
        sys.exit("Brak GITHUB_TOKEN — ustaw zmienną środowiskową albo dopisz do .env (token z uprawnieniem "
                 "Contents: write do repozytorium), albo użyj --no-github.")
    if sys.platform != "win32":
        sys.exit("Instalator Windows (Velopack) powstaje tylko na Windows — wydawaj z Windows albo użyj --no-github.")
    repository = github_repository()
    try:
        info = github_request("GET", f"{GITHUB_API}/repos/{repository}")
    except urllib.error.HTTPError as error:
        sys.exit(f"GitHub odrzucił token dla {repository}: {describe_http_error(error)}")
    except urllib.error.URLError as error:
        sys.exit(f"Brak połączenia z GitHubem: {error.reason}")
    if not info.get("permissions", {}).get("push"):
        sys.exit(f"GITHUB_TOKEN nie ma prawa zapisu do {repository} (potrzebne Contents: write).")
    return repository


def release_assets(version: str) -> list[Path]:
    """Te same pliki, co dawniej wystawiał workflow — nazwy muszą zostać (kanał aktualizacji Velopack)."""
    files = [
        RELEASE / f"Uwielbienia-{version}-win-x64.exe",
        RELEASE / f"Uwielbienia-{version}-linux-x64.tar.gz",
        VELOPACK / "Uwielbienia-win-Setup.exe",
        VELOPACK / "Uwielbienia-win-Portable.zip",
        VELOPACK / f"Uwielbienia-{version}-full.nupkg",
        VELOPACK / "releases.win.json",
        VELOPACK / "assets.win.json",
        VELOPACK / "RELEASES",
    ]
    missing = [f.relative_to(ROOT).as_posix() for f in files if not f.is_file()]
    if missing:
        raise FileNotFoundError(f"brak plików wersji {version}: {', '.join(missing)} — zbuduj ją: python build.py")
    return files


def find_release(repository: str, tag: str) -> dict | None:
    """Także szkic (szkice nie mają adresu /releases/tags/…)."""
    page = 1
    while True:
        releases = github_request("GET", f"{GITHUB_API}/repos/{repository}/releases?per_page=100&page={page}")
        for release in releases:
            if release["tag_name"] == tag:
                return release
        if len(releases) < 100:
            return None
        page += 1


def upload_asset(release: dict, file: Path) -> None:
    upload_url = release["upload_url"].split("{", 1)[0]
    url = f"{upload_url}?name={urllib.parse.quote(file.name)}"
    size = file.stat().st_size
    print(f"  ↑ {file.name} ({size / 1_048_576:.1f} MB)", flush=True)
    with file.open("rb") as data:
        request = urllib.request.Request(url, data=data, method="POST", headers={
            "Authorization": f"Bearer {os.environ['GITHUB_TOKEN']}",
            "Accept": "application/vnd.github+json",
            "X-GitHub-Api-Version": "2022-11-28",
            "Content-Type": "application/octet-stream",
            "Content-Length": str(size),
        })
        with urllib.request.urlopen(request, timeout=600) as response:
            response.read()


def github_release(repository: str, version: str) -> None:
    """Szkic → pliki → publikacja: aplikacje nie zobaczą wydania bez kompletu plików aktualizacji."""
    tag = f"v{version}"
    assets = release_assets(version)
    release = find_release(repository, tag)
    if release is None:
        release = github_request("POST", f"{GITHUB_API}/repos/{repository}/releases", {
            "tag_name": tag,
            "name": f"Uwielbienia {tag}",
            "generate_release_notes": True,
            "draft": True,
        })
        print(f"Utworzono GitHub Release {tag} (szkic).")
    else:
        print(f"GitHub Release {tag} już istnieje — uzupełniam pliki.")

    present = set()
    for asset in release.get("assets", []):
        if asset["state"] == "uploaded":
            present.add(asset["name"])
        else:  # przerwane wysyłanie zostawia plik, który blokuje tę samą nazwę
            github_request("DELETE", asset["url"])
    for file in assets:
        if file.name in present:
            print(f"  = {file.name} (już jest)")
        else:
            upload_asset(release, file)

    if release.get("draft"):
        release = github_request("PATCH", release["url"], {"draft": False, "make_latest": "true"})
    print(f"GitHub Release: {release['html_url']}")


def publish_github_release(repository: str, version: str) -> None:
    try:
        github_release(repository, version)
    except urllib.error.HTTPError as error:
        fail_github_release(describe_http_error(error))
    except (urllib.error.URLError, OSError, KeyError, ValueError) as error:
        fail_github_release(str(error))


def fail_github_release(reason: str) -> None:
    sys.exit(f"\nGitHub Release nie powstał: {reason}\n"
             "Tag jest już wypchnięty. Dokończ wydanie z plików w release/:\n"
             "  python deploy.py --github-only")


# --- Wydanie ----------------------------------------------------------------------------------------

def push_tag(new_version: str) -> None:
    tag = f"v{new_version}"
    branch = git_output("rev-parse", "--abbrev-ref", "HEAD")
    run(["git", "add", "version.txt"])
    run(["git", "commit", "--only", "version.txt", "-m", f"chore: wersja {new_version}"])
    run(["git", "push", "-u", "origin", branch])
    run(["git", "tag", tag])
    run(["git", "push", "origin", tag])
    print(f"\nTag {tag} wypchnięty.")


def warn_about_uncommitted_changes() -> None:
    """Commitujemy tylko version.txt, więc niezacommitowane zmiany ostrzegają, a nie przerywają wydania."""
    pending = [line[3:] for line in git_output("status", "--porcelain").splitlines() if line[3:] != "version.txt"]
    if not pending:
        return
    print("Uwaga: niezacommitowane zmiany. Aplikacje i strona budują się z folderu roboczego, więc wejdą")
    print("do wydania, ale nie do commita tagu:")
    for path in pending:
        print(f"  {path}")


def main() -> None:
    parser = argparse.ArgumentParser(description="Wydaje nową wersję: budowanie lokalne, strona na FTP, "
                                                 "tag i GitHub Release.")
    mode = parser.add_mutually_exclusive_group()
    mode.add_argument("--site-only", action="store_true", help="tylko zbuduj i wyślij stronę")
    mode.add_argument("--github-only", action="store_true",
                      help="tylko utwórz / uzupełnij GitHub Release dla version.txt z plików w release/")
    parser.add_argument("--no-site", action="store_true", help="nie wysyłaj strony")
    parser.add_argument("--no-github", action="store_true", help="nie twórz GitHub Release (tylko tag)")
    args = parser.parse_args()
    load_env_file()

    if args.github_only:
        publish_github_release(require_github(), current_version())
        return

    if not args.no_site:
        require_ftp_settings()

    if args.site_only:
        run([sys.executable, str(ROOT / "build.py"), "--no-tests", "--no-apps"])
        upload_site()
        return

    repository = None if args.no_github else require_github()
    if sys.platform == "win32":
        build.check_vpk()

    warn_about_uncommitted_changes()

    previous_version_text = VERSION_FILE.read_text()
    new_version = bump_version()
    try:
        run([sys.executable, str(ROOT / "build.py")])
        if repository:
            release_assets(new_version)  # komplet plików, zanim wersja trafi do repozytorium
        if not args.no_site:
            upload_site()
    except (subprocess.CalledProcessError, OSError, ftplib.Error) as error:
        VERSION_FILE.write_text(previous_version_text)  # także niezacommitowana wartość użytkownika
        sys.exit(f"Wydanie przerwane ({error}) — wersja nie została zmieniona.")
    push_tag(new_version)
    if repository:
        publish_github_release(repository, new_version)


if __name__ == "__main__":
    main()
