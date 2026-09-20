"""
Build mlogger_tools.zip for distribution (GitHub Release asset).

Publishes MLServer (framework-dependent, runs on Windows/macOS/Linux with the
.NET runtime installed) and bundles it with dist_user/ (top-level READMEs,
python_tools/, MLServer/ READMEs).

Usage:
    python make_dist_zip.py                    # -> software/mlogger_tools.zip
    python make_dist_zip.py -o path/to/out.zip
"""
import argparse
import shutil
import subprocess
import sys
import tempfile
import zipfile
from pathlib import Path

ROOT          = Path(__file__).resolve().parent            # software/
DIST_SRC      = ROOT / "dist_user"
MLSERVER_PROJ = ROOT / "dotnet" / "MLServer" / "MLServer.csproj"

# Development leftovers that must not end up in the zip
IGNORE = shutil.ignore_patterns("__pycache__", "mlogger_*.csv", "*.pyc")


def main():
    ap = argparse.ArgumentParser(description="Build mlogger_tools.zip")
    ap.add_argument("-o", "--output", default=str(ROOT / "mlogger_tools.zip"))
    args = ap.parse_args()
    out = Path(args.output)

    with tempfile.TemporaryDirectory() as td:
        stage = Path(td) / "mlogger_tools"

        # 1) Static content (READMEs + python_tools + MLServer READMEs)
        shutil.copytree(DIST_SRC, stage, ignore=IGNORE)

        # 2) MLServer framework-dependent publish into stage/MLServer
        print("Publishing MLServer...")
        r = subprocess.run(
            ["dotnet", "publish", str(MLSERVER_PROJ),
             "-c", "Release", "-o", str(stage / "MLServer")],
        )
        if r.returncode != 0:
            print("dotnet publish failed")
            return 1

        # 3) Zip (zip contains a single top-level mlogger_tools/ folder)
        print(f"Writing {out}...")
        out.parent.mkdir(parents=True, exist_ok=True)
        with zipfile.ZipFile(out, "w", zipfile.ZIP_DEFLATED) as zf:
            for f in sorted(stage.rglob("*")):
                if f.is_file():
                    zf.write(f, f.relative_to(stage.parent))

    print(f"Done: {out} ({out.stat().st_size / 1024 / 1024:.1f} MB)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
