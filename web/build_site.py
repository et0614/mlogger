"""
mlogger.jp の公開用一式を組み立てる。

  - web/ 以下の HTML と成績 (inspection / velocity_calibration の reports)
  - docs/manual をビルドしたユーザーマニュアル (ja/manual/, en/manual/)
  - git に入れていない大きなファイル (PDF, 動画, 画像)。--assets で指定したフォルダから
    同じ相対パスのまま取り込む

を web/_site/ にまとめる。できた _site/ の中身をサーバーに配置する。

使い方:
    python web/build_site.py --assets <バイナリを置いたフォルダ>
    python web/build_site.py                 # バイナリなし (HTML とマニュアルだけ)

必要なもの: pip install -r docs/manual/requirements.txt
"""
import argparse
import os
import shutil
import subprocess
import sys

WEB_DIR = os.path.dirname(os.path.abspath(__file__))
REPO_DIR = os.path.dirname(WEB_DIR)
MANUAL_DIR = os.path.join(REPO_DIR, "docs", "manual")
OUT_DIR = os.path.join(WEB_DIR, "_site")

# --assets から取り込む拡張子
ASSET_EXTS = {".pdf", ".mp4", ".jpg", ".jpeg", ".png"}
# --assets 側にあっても取り込まないフォルダ (mobile/v1.3 のマニュアル、git で管理している成績)
ASSET_SKIP_DIRS = {"mobile", "manual", "reports", "_site"}


def copy_web_sources():
    def ignore(dirpath, names):
        # 組み立て用のファイル (開発者向けの README を含む) はサイトに入れない
        skip = {"_site", "build_site.py", ".gitignore", "README.md", "__pycache__"}
        return [n for n in names if n in skip]
    shutil.copytree(WEB_DIR, OUT_DIR, ignore=ignore)


def build_manual():
    for lang in ("ja", "en"):
        dest = os.path.join(OUT_DIR, lang, "manual")
        subprocess.run([sys.executable, "-m", "mkdocs", "build", "--quiet",
                        "-f", os.path.join(MANUAL_DIR, lang, "mkdocs.yml"),
                        "-d", dest], check=True)


def copy_assets(assets_dir):
    n = 0
    for dirpath, dirnames, filenames in os.walk(assets_dir):
        dirnames[:] = [d for d in dirnames if d not in ASSET_SKIP_DIRS]
        for name in filenames:
            if os.path.splitext(name)[1].lower() not in ASSET_EXTS:
                continue
            src = os.path.join(dirpath, name)
            dst = os.path.join(OUT_DIR, os.path.relpath(src, assets_dir))
            os.makedirs(os.path.dirname(dst), exist_ok=True)
            shutil.copy2(src, dst)
            n += 1
    return n


def main():
    ap = argparse.ArgumentParser(description="mlogger.jp の公開用一式を web/_site/ に組み立てる")
    ap.add_argument("--assets", metavar="DIR",
                    help="git に入れていない PDF・動画・画像を置いたフォルダ (サイトと同じ構成)")
    args = ap.parse_args()

    if os.path.exists(OUT_DIR):
        shutil.rmtree(OUT_DIR)
    copy_web_sources()
    build_manual()
    if args.assets:
        print(f"バイナリ {copy_assets(args.assets)} 件を取り込みました")
    else:
        print("--assets の指定がないため、PDF・動画・画像は含まれていません")
    print(f"出力: {OUT_DIR}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
