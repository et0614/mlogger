# mlogger.jp サイトのソース

| パス | 内容 |
|---|---|
| `index.htm` | ブラウザの言語で `ja/` か `en/` に振り分ける |
| `ja/index.html`, `en/index.html` | トップページ |
| `mobile.html` | スマートフォンアプリの紹介とプライバシーポリシー (ストア登録用) |
| `inspection/` | 出荷時試験成績の閲覧ページ。成績は `reports/<hardware_id>.json` (`software/python/mlogger/factory_test.py` が出力) |
| `velocity_calibration/` | 風速プローブ校正成績の閲覧ページ。成績は `reports/<device_id>.json` (`software/python/anemometer/calibrate_anemometer.py` が出力) |

ユーザーマニュアルは `docs/manual` にあり、公開時に `ja/manual/`, `en/manual/` として組み込む。

## git に入れないもの

解説書の PDF、動画、写真・スクリーンショットは git に入れず、Drive 等に置く (`.gitignore` 参照)。
実測例の PDF (`ja/case*.pdf`) も git では公開しない。

## 公開

```bash
pip install -r docs/manual/requirements.txt
python web/build_site.py --assets <PDF・動画・画像を置いたフォルダ>
```

`web/_site/` に公開用一式ができるので、その中身をサーバーに配置する。
`--assets` のフォルダはサイトと同じ構成 (例: `ja/document_3.4.1.pdf`, `visualize.mp4`, `screenshots/`) で置く。
