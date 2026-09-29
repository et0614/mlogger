"""
Build mlogger_tools.zip for distribution (GitHub Release asset).

Publishes MLServer (framework-dependent, runs on Windows/macOS/Linux with the
.NET runtime installed) and bundles it with dist_user/ (top-level READMEs,
python_tools/, MLServer/ READMEs) and the firmware update kit (firmware_update/).

The firmware update kit is assembled from:
  - firmware/dist_user/  : update.bat, README.md, README_ja.md, COPYING_avrdude.txt
  - firmware/release/    : mlogger_main.X.production.hex (the firmware to distribute)
  - --avrdude DIR        : avrdude.exe and avrdude.conf (default: firmware/dist_user,
                           where they are placed by hand; not tracked by git)

Usage:
    python make_dist_zip.py                    # -> software/mlogger_tools.zip
    python make_dist_zip.py -o path/to/out.zip
    python make_dist_zip.py --avrdude path/to/avrdude_dir
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
FW_KIT_SRC    = ROOT.parent / "firmware" / "dist_user"
FW_RELEASE    = ROOT.parent / "firmware" / "release"

# Files of the firmware update kit, by source folder (only these go into the zip)
FW_KIT_DOCS    = ["update.bat", "README.md", "README_ja.md", "COPYING_avrdude.txt"]
FW_KIT_AVRDUDE = ["avrdude.exe", "avrdude.conf"]
FW_KIT_HEX     = "mlogger_main.X.production.hex"

# Development leftovers that must not end up in the zip
IGNORE = shutil.ignore_patterns("__pycache__", "mlogger_*.csv", "*.pyc")


def main():
    ap = argparse.ArgumentParser(description="Build mlogger_tools.zip")
    ap.add_argument("-o", "--output", default=str(ROOT / "mlogger_tools.zip"))
    ap.add_argument("--avrdude", default=str(FW_KIT_SRC),
                    help="folder containing avrdude.exe and avrdude.conf")
    args = ap.parse_args()
    out = Path(args.output)
    avrdude_dir = Path(args.avrdude)

    kit = {f: FW_KIT_SRC / f for f in FW_KIT_DOCS}
    kit.update({f: avrdude_dir / f for f in FW_KIT_AVRDUDE})
    kit[FW_KIT_HEX] = FW_RELEASE / FW_KIT_HEX
    missing = [str(p) for p in kit.values() if not p.is_file()]
    if missing:
        print("firmware update kit is incomplete, missing:\n  " + "\n  ".join(missing))
        return 1

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

        # 3) Firmware update kit
        (stage / "firmware_update").mkdir()
        for name, src in kit.items():
            shutil.copy2(src, stage / "firmware_update" / name)

        # 4) Zip (zip contains a single top-level mlogger_tools/ folder)
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
