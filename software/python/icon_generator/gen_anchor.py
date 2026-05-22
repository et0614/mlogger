"""アンカー画像 (素マネキン T-stance lite) を生成する。

実行すると `_anchor.png` を上書き保存する。気に入る 1 枚が出るまで
何度か再実行してよい。気に入った 1 枚が _anchor.png として保存されたら
gen_batch.py に進む。
"""
import os
import sys
from pathlib import Path

from google import genai

from prompts import anchor_prompt


HERE = Path(__file__).parent
OUT = HERE / "_anchor.png"
MODEL = "gemini-2.5-flash-image"


def main() -> int:
    api_key = os.environ.get("GEMINI_API_KEY")
    if not api_key:
        print("ERROR: environment variable GEMINI_API_KEY is not set.", file=sys.stderr)
        return 1

    client = genai.Client(api_key=api_key)

    print(f"Generating anchor image with model {MODEL} ...", flush=True)
    resp = client.models.generate_content(
        model=MODEL,
        contents=[anchor_prompt()],
    )

    for part in resp.candidates[0].content.parts:
        if part.inline_data is not None and part.inline_data.data:
            OUT.write_bytes(part.inline_data.data)
            print(f"Saved: {OUT}")
            return 0

    print("ERROR: no image returned in response.", file=sys.stderr)
    print("--- response text (if any) ---", file=sys.stderr)
    for part in resp.candidates[0].content.parts:
        if getattr(part, "text", None):
            print(part.text, file=sys.stderr)
    return 2


if __name__ == "__main__":
    sys.exit(main())
