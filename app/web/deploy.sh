#!/usr/bin/env bash
# Wysyła stronę pilota na serwer FTP (greysource.eu/uwielbienie).
# Dane logowania tylko ze zmiennych środowiskowych — nie zapisujemy ich w repozytorium:
#   FTP_HOST=… FTP_USER=… FTP_PASS=… ./deploy.sh
# FTP_DIR (opcjonalnie) — katalog docelowy, domyślnie /greysource.eu/wwwroot/uwielbienie
set -euo pipefail

: "${FTP_HOST:?Ustaw FTP_HOST}" "${FTP_USER:?Ustaw FTP_USER}" "${FTP_PASS:?Ustaw FTP_PASS}"
FTP_DIR="${FTP_DIR:-/greysource.eu/wwwroot/uwielbienie}"
HERE="$(cd "$(dirname "$0")/remote" && pwd)"

# Mapa serwerów pośredniczących musi leżeć w tej samej domenie co strona (wymóg przeglądarki).
curl -fsSL https://tailcat.dev/derpmap.json -o "$HERE/derpmap.json"

cd "$HERE"
find . -type f ! -name '*.md' | sed 's|^\./||' | while read -r file; do
  echo "→ $file"
  curl -fsS --ftp-create-dirs --user "$FTP_USER:$FTP_PASS" -T "$file" "ftp://$FTP_HOST$FTP_DIR/$file"
done
echo "Gotowe: https://greysource.eu/uwielbienie/"
