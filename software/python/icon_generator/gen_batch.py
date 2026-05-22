"""アンカー画像 + items_{clo,met}.csv からアイコンをバッチ生成する。

使い方:
    python gen_batch.py clo     # 着衣 55 枚を生成
    python gen_batch.py met     # 活動 31 枚を生成
    python gen_batch.py all     # 両方
    python gen_batch.py clo --force   # 既存ファイルも上書き

出力先: ./output/Clothes/, ./output/Activities/
失敗したエントリは ./failed_{clo,met}.csv に書き出される。
"""
import argparse
import csv
import os
import sys
import time
from pathlib import Path
from typing import Callable

from google import genai
from google.genai import types

from prompts import activity_prompt, clothing_prompt


HERE = Path(__file__).parent
ANCHOR = HERE / "_anchor.png"
OUT_ROOT = HERE / "output"
MODEL = "gemini-2.5-flash-image"
SLEEP_BETWEEN = 1.0  # rate-limit margin (sec)


def _generate_one(client: genai.Client, prompt: str, anchor_bytes: bytes, out_path: Path) -> bool:
    anchor_part = types.Part.from_bytes(data=anchor_bytes, mime_type="image/png")
    resp = client.models.generate_content(
        model=MODEL,
        contents=[anchor_part, prompt],
    )
    for part in resp.candidates[0].content.parts:
        if part.inline_data is not None and part.inline_data.data:
            out_path.write_bytes(part.inline_data.data)
            return True
    return False


def _run(csv_path: Path, subdir: str, builder: Callable[..., str], force: bool) -> int:
    if not csv_path.exists():
        print(f"ERROR: csv not found: {csv_path}", file=sys.stderr)
        return 2

    api_key = os.environ.get("GEMINI_API_KEY")
    if not api_key:
        print("ERROR: environment variable GEMINI_API_KEY is not set.", file=sys.stderr)
        return 1
    if not ANCHOR.exists():
        print(f"ERROR: anchor image not found: {ANCHOR}", file=sys.stderr)
        print("Run gen_anchor.py first.", file=sys.stderr)
        return 2

    client = genai.Client(api_key=api_key)
    anchor_bytes = ANCHOR.read_bytes()
    out_dir = OUT_ROOT / subdir
    out_dir.mkdir(parents=True, exist_ok=True)

    failed: list[tuple[str, str]] = []
    with csv_path.open(encoding="utf-8") as f:
        reader = csv.reader(f)
        header = next(reader)
        # header[0] は filename。残りが builder の引数。
        for row in reader:
            if not row or not row[0].strip():
                continue
            fname = row[0]
            args = row[1:]
            out_path = out_dir / fname
            if out_path.exists() and not force:
                print(f"[skip] {fname} (already exists; use --force to overwrite)")
                continue
            prompt = builder(*args)
            print(f"[gen ] {fname}", flush=True)
            try:
                ok = _generate_one(client, prompt, anchor_bytes, out_path)
                if not ok:
                    failed.append((fname, "no image in response"))
                    print(f"       -> FAILED (no image)")
            except Exception as e:  # noqa: BLE001
                failed.append((fname, repr(e)))
                print(f"       -> FAILED ({e})")
            time.sleep(SLEEP_BETWEEN)

    if failed:
        fpath = HERE / f"failed_{subdir.lower()}.csv"
        with fpath.open("w", encoding="utf-8", newline="") as f:
            w = csv.writer(f)
            w.writerow(["filename", "error"])
            w.writerows(failed)
        print(f"\n{len(failed)} entries failed -> {fpath}")
        return 3

    print(f"\nAll entries in {csv_path.name} generated successfully.")
    return 0


def main() -> int:
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("target", choices=["clo", "met", "all"])
    p.add_argument("--force", action="store_true", help="overwrite existing output files")
    args = p.parse_args()

    rc = 0
    if args.target in ("clo", "all"):
        rc |= _run(HERE / "items_clo.csv", "Clothes", clothing_prompt, args.force)
    if args.target in ("met", "all"):
        rc |= _run(HERE / "items_met.csv", "Activities", activity_prompt, args.force)
    return rc


if __name__ == "__main__":
    sys.exit(main())
