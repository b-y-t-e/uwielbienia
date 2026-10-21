#!/usr/bin/env python3
"""
Kompiluje i uruchamia aplikację z repozytorium (pieśni z Teksty/, dane w %APPDATA%\\Uwielbienia).

Użycie:
  python run.py              kompilacja Debug i uruchomienie
  python run.py --release    kompilacja Release
  python run.py -- <arg…>    argumenty dla aplikacji
"""
import argparse
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent
PROJECT = ROOT / "app" / "src" / "Uwielbienia.Desktop"


def main() -> None:
    parser = argparse.ArgumentParser(description="Kompiluje i uruchamia aplikację Uwielbienia.")
    parser.add_argument("--release", action="store_true", help="kompilacja Release zamiast Debug")
    parser.add_argument("app_args", nargs=argparse.REMAINDER, help="argumenty dla aplikacji (po --)")
    args = parser.parse_args()

    configuration = "Release" if args.release else "Debug"
    app_args = args.app_args[1:] if args.app_args[:1] == ["--"] else args.app_args
    print(f"$ dotnet build ({configuration}) i uruchomienie", flush=True)
    try:
        code = subprocess.call(
            ["dotnet", "run", "--project", str(PROJECT), "-c", configuration, "--", *app_args], cwd=ROOT)
    except FileNotFoundError:
        sys.exit("Brak polecenia dotnet — zainstaluj .NET 10 SDK.")
    except KeyboardInterrupt:
        code = 130
    sys.exit(code)


if __name__ == "__main__":
    main()
