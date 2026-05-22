"""output/ 配下の 1024x1024 PNG を 256x256 にリサイズして書き出す。

デフォルトの出力先は MLS_Mobile/Resources/Images/{Clothes,Activities}/。
既存ファイルは上書きするので、最初に確認したい場合は --out で別フォルダ
(例: --out preview) に出してチェックしてから本番ディレクトリに反映する。

使い方:
    python resize.py                 # Resources/Images に上書き
    python resize.py --out preview   # ./preview/{Clothes,Activities}/ に出力 (上書きしない)
    python resize.py --dry-run       # 出力先パスだけ表示
"""
import argparse
import sys
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw


HERE = Path(__file__).parent
SRC = HERE / "output"
DEFAULT_DST = HERE.parent.parent / "dotnet" / "MLS_Mobile" / "Resources" / "Images"
TARGET = 256
# autocrop: 白背景とみなす閾値 (R,G,B いずれも >= この値) と外側に残す余白 px
BG_THRESHOLD = 245
PAD_PX = 16  # 256 中の片側 ~6%
# 白→透過化の許容色差 (ImageDraw.floodfill の thresh、0 = 完全一致のみ)
WHITE_TO_ALPHA_THRESH = 255 - BG_THRESHOLD


def _whiten_to_alpha(img: Image.Image) -> Image.Image:
    """画像の外周から接する「ほぼ白」領域を flood-fill で透明化する。

    被写体内部の白っぽいハイライトは外周と連結していないので保持される。
    """
    rgba = img.convert("RGBA")
    w, h = rgba.size
    fill = (255, 255, 255, 0)
    # 4 隅 + 各辺中点を起点に試行 (外周に少なくとも 1 つは白ピクセルがある想定)
    seeds = [
        (0, 0), (w - 1, 0), (0, h - 1), (w - 1, h - 1),
        (w // 2, 0), (w // 2, h - 1), (0, h // 2), (w - 1, h // 2),
    ]
    for xy in seeds:
        try:
            ImageDraw.floodfill(rgba, xy, fill, thresh=WHITE_TO_ALPHA_THRESH)
        except Exception:
            # 既にその点が透過なら例外を握り潰して次の seed へ
            pass
    return rgba


def _autocrop_to_subject(img: Image.Image) -> Image.Image:
    """白背景を切り詰めて被写体を中央に配置した正方形 RGBA を返す。

    1) RGB に直して「ほぼ白」(各成分 >= BG_THRESHOLD) でない領域の bbox を取得
    2) その bbox の周囲に PAD_PX 相当の余白を残してクロップ
    3) 短辺=長辺の正方形キャンバスに中央配置
    """
    rgb = img.convert("RGB")
    # ImageChops.difference では純白との差分 → 非白ピクセルの bbox
    bg = Image.new("RGB", rgb.size, (255, 255, 255))
    diff = ImageChops.difference(rgb, bg)
    # threshold より小さい差分 (= ほぼ白) はノイズ扱いで切る
    mask = diff.point(lambda v: 0 if v < (255 - BG_THRESHOLD) else 255)
    bbox = mask.getbbox()
    if bbox is None:
        return img.convert("RGBA")

    w, h = img.size
    # 1024 -> 256 にする際の縮小比で margin を換算
    src_pad = int(PAD_PX * max(w, h) / TARGET)
    l, t, r, b = bbox
    l = max(0, l - src_pad)
    t = max(0, t - src_pad)
    r = min(w, r + src_pad)
    b = min(h, b + src_pad)
    cropped = img.crop((l, t, r, b)).convert("RGBA")

    cw, ch = cropped.size
    side = max(cw, ch)
    canvas = Image.new("RGBA", (side, side), (255, 255, 255, 0))
    ox = (side - cw) // 2
    oy = (side - ch) // 2
    canvas.paste(cropped, (ox, oy), cropped)
    return canvas


def _resize_one(src: Path, dst: Path, do_autocrop: bool) -> None:
    img = Image.open(src)
    if do_autocrop:
        img = _autocrop_to_subject(img)
    else:
        img = img.convert("RGBA")
    # 外周白を透過化 (アプリの淡緑背景に乗せたとき白いハロが出ないように)
    img = _whiten_to_alpha(img)
    # 長辺を TARGET に合わせるよう縮小。比率を保持。
    img.thumbnail((TARGET, TARGET), Image.LANCZOS)
    # 正方形キャンバスに中央配置 (背景は透過)
    canvas = Image.new("RGBA", (TARGET, TARGET), (0, 0, 0, 0))
    ox = (TARGET - img.width) // 2
    oy = (TARGET - img.height) // 2
    canvas.paste(img, (ox, oy), img)
    dst.parent.mkdir(parents=True, exist_ok=True)
    canvas.save(dst, "PNG", optimize=True)


def main() -> int:
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument(
        "--out",
        type=Path,
        default=DEFAULT_DST,
        help="出力先のルートディレクトリ (デフォルト: Resources/Images)。"
             "相対パスを指定した場合は icon_generator/ からの相対。",
    )
    p.add_argument("--dry-run", action="store_true")
    p.add_argument(
        "--no-autocrop",
        action="store_true",
        help="背景余白の自動切り詰めを無効化 (デフォルトは有効)",
    )
    args = p.parse_args()

    dst_root = args.out if args.out.is_absolute() else (HERE / args.out)
    do_autocrop = not args.no_autocrop

    count = 0
    for sub in ("Clothes", "Activities"):
        src_dir = SRC / sub
        dst_dir = dst_root / sub
        if not src_dir.exists():
            print(f"[skip] {src_dir} not found")
            continue
        for src in sorted(src_dir.glob("*.png")):
            dst = dst_dir / src.name
            if args.dry_run:
                print(f"[dry] {src}  ->  {dst}")
            else:
                _resize_one(src, dst, do_autocrop)
                print(f"[ok ] {dst}")
            count += 1

    if args.dry_run:
        print(f"\n(dry-run) would process {count} files")
    else:
        print(f"\nresized {count} files")
    return 0


if __name__ == "__main__":
    sys.exit(main())
