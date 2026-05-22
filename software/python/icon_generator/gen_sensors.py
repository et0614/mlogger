"""DataReceive / DeviceSetting 用のセンサアイコン 6 種を一括生成する。

各アイコンは Font Awesome 6 Free Solid のテイストに揃えた透過 PNG
シルエット (charcoal #212121)。Nano Banana (gemini-2.5-flash-image)
にマスタープロンプト + 被写体プロンプトを投げて 1024x1024 を作り、
1024 オリジナルは ./output/Sensors/ に、Resources/Images/Sensors/
には 96x96 にリサイズして配置する。

使い方:
    python gen_sensors.py                 # 6 種をまとめて生成 (既存はスキップ)
    python gen_sensors.py --force         # 既存も上書き
    python gen_sensors.py --only co2,illuminance   # 一部だけ
"""
import argparse
import os
import sys
import time
from pathlib import Path

from google import genai
from PIL import Image

from prompts import sensor_prompt


HERE = Path(__file__).parent
OUT_FULL = HERE / "output" / "Sensors"          # 1024 オリジナル保存先
OUT_FINAL = (
    HERE.parent.parent / "dotnet" / "MLS_Mobile" / "Resources" / "Images" / "Sensors"
)  # 96 配置先 (Resources/Images/Sensors)
MODEL = "gemini-2.5-flash-image"
TARGET_DISPLAY = 96
SLEEP_BETWEEN = 1.0

# (key, filename, subject_description)
SENSORS: list[tuple[str, str, str]] = [
    (
        "drybulb",
        "sensor_drybulb.png",
        "A classic thermometer icon: a vertical thin tube with a small circular bulb "
        "at the bottom (filled), and short horizontal tick marks on one side of the "
        "tube representing the temperature scale. The bulb and tube form one "
        "connected silhouette. Absolutely no numerical readings on the scale.",
    ),
    (
        "humidity",
        "sensor_humidity.png",
        "A water droplet icon: a single teardrop-shaped silhouette pointed at the top "
        "and rounded at the bottom, filled solid charcoal. Simple and symmetrical. "
        "A small highlight notch in negative space inside the droplet is OK.",
    ),
    (
        "globe",
        "sensor_globe.png",
        "A black globe thermometer icon (Vernon globe): a perfect solid filled circle "
        "representing the matte black sensing sphere, with a small straight vertical "
        "thermometer line emerging from the top center, ending in a tiny circular bulb "
        "at the very top. The thermometer and globe form one connected silhouette.",
    ),
    (
        "velocity",
        "sensor_velocity.png",
        "A wind / air movement icon: three horizontal flowing lines of varying length "
        "stacked vertically, with the right ends slightly curving upward, suggesting "
        "moving air. Lines are medium uniform stroke. Reads clearly as 'wind / "
        "airflow', not as plain stripes.",
    ),
    (
        "illuminance",
        "sensor_illuminance.png",
        "A ceiling downlight icon viewed from the side: a small horizontal rectangle "
        "at the very top of the canvas representing the ceiling fixture, and beneath "
        "it a downward-opening light cone shown as a trapezoid outline (two diagonal "
        "lines flaring outward + a horizontal line at the bottom), representing "
        "light spreading from ceiling toward floor. ABSOLUTELY NOT a lightbulb, NOT "
        "an Edison bulb, NOT a pendant bulb, NOT a round bulb shape. No filament. "
        "No spherical glass. The icon must read as 'ceiling light illuminating a "
        "surface', not as 'a bulb'.",
    ),
    (
        "co2",
        "sensor_co2.png",
        "A human exhalation / breath icon viewed from the side: a simplified "
        "side-profile silhouette of a human head facing right (only the curved back "
        "of the head, the forehead, a pointed nose, and a small mouth opening — "
        "kept minimal and iconic, no detailed facial features). Out of the mouth, "
        "three short parallel wavy curved lines flow to the right, representing "
        "exhaled breath. The wavy breath lines are the same solid charcoal as the "
        "head silhouette. Reads clearly as 'a person breathing out'. No chemical "
        "formula text, no CO2 label.",
    ),
]


def _generate(client: genai.Client, prompt: str, dst_full: Path) -> bool:
    resp = client.models.generate_content(model=MODEL, contents=[prompt])
    for part in resp.candidates[0].content.parts:
        if part.inline_data is not None and part.inline_data.data:
            dst_full.write_bytes(part.inline_data.data)
            return True
    return False


def _resize_to_display(src_full: Path, dst_small: Path) -> None:
    img = Image.open(src_full).convert("RGBA")
    img.thumbnail((TARGET_DISPLAY, TARGET_DISPLAY), Image.LANCZOS)
    # 正方形キャンバスに中央配置 (透過維持)
    canvas = Image.new("RGBA", (TARGET_DISPLAY, TARGET_DISPLAY), (0, 0, 0, 0))
    ox = (TARGET_DISPLAY - img.width) // 2
    oy = (TARGET_DISPLAY - img.height) // 2
    canvas.paste(img, (ox, oy), img)
    dst_small.parent.mkdir(parents=True, exist_ok=True)
    canvas.save(dst_small, "PNG", optimize=True)


def main() -> int:
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("--force", action="store_true", help="既存ファイルも上書き")
    p.add_argument(
        "--only",
        type=str,
        default="",
        help="カンマ区切りで生成対象を絞る (例: --only co2,illuminance)",
    )
    args = p.parse_args()

    api_key = os.environ.get("GEMINI_API_KEY")
    if not api_key:
        print("ERROR: environment variable GEMINI_API_KEY is not set.", file=sys.stderr)
        return 1

    selected = {s.strip() for s in args.only.split(",") if s.strip()}
    targets = [t for t in SENSORS if not selected or t[0] in selected]
    if not targets:
        print(f"ERROR: --only {args.only!r} matched nothing.", file=sys.stderr)
        return 2

    OUT_FULL.mkdir(parents=True, exist_ok=True)
    OUT_FINAL.mkdir(parents=True, exist_ok=True)
    client = genai.Client(api_key=api_key)

    failed: list[tuple[str, str]] = []
    for key, fname, subject in targets:
        full_path = OUT_FULL / fname
        final_path = OUT_FINAL / fname
        if full_path.exists() and not args.force:
            print(f"[skip] {fname} (already exists; use --force to regenerate)")
            # 既存 1024 から 96 だけ更新
            _resize_to_display(full_path, final_path)
            print(f"       -> resized to {final_path}")
            continue
        prompt = sensor_prompt(subject)
        print(f"[gen ] {fname}  ({key})", flush=True)
        try:
            ok = _generate(client, prompt, full_path)
            if not ok:
                failed.append((fname, "no image in response"))
                print("       -> FAILED (no image)")
                continue
        except Exception as e:  # noqa: BLE001
            failed.append((fname, repr(e)))
            print(f"       -> FAILED ({e})")
            continue
        _resize_to_display(full_path, final_path)
        print(f"       -> {full_path}")
        print(f"       -> {final_path}  ({TARGET_DISPLAY}x{TARGET_DISPLAY})")
        time.sleep(SLEEP_BETWEEN)

    if failed:
        for fname, err in failed:
            print(f"FAILED: {fname}  ({err})", file=sys.stderr)
        return 3

    print("\nAll sensor icons generated.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
