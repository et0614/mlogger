# M-Logger ユーザーマニュアル (MkDocs)

M-Logger 本体の基本操作と、ソフトウェア (スマートフォンアプリ MLS_Mobile・MLServer・Python ツール) の使い方をまとめたマニュアルを MkDocs Material で構築するプロジェクトです。
日本語版 (`ja/`) と英語版 (`en/`) を独立した MkDocs プロジェクトとして並置しています。

## ビルド方法

### 必要環境

- Python 3.9 以上

### 初回セットアップ

```bash
pip install -r requirements.txt
```

### ローカルプレビュー

```bash
python -m mkdocs serve -f ja/mkdocs.yml
python -m mkdocs serve -f en/mkdocs.yml
```

ブラウザで `http://127.0.0.1:8000/` を開くと、編集内容がリアルタイムに反映されます。

### 公開用ビルド

```bash
python -m mkdocs build -f ja/mkdocs.yml
python -m mkdocs build -f en/mkdocs.yml
```

それぞれ `ja/site/` と `en/site/` に静的 HTML が生成されます。公開先は以下の通りです。

- 日本語版: `mlogger.jp/ja/manual/`
- 英語版: `mlogger.jp/en/manual/`

## ディレクトリ構成

```
docs/manual/
├── README.md
├── requirements.txt          # 依存パッケージ (両言語共通)
├── ja/
│   ├── mkdocs.yml            # 日本語版 MkDocs 設定 (nav もここ)
│   └── docs/
│       ├── index.md          # トップ
│       ├── start/            # はじめに (システム構成・本体の基本操作・ファームウェアの版)
│       ├── mobile/           # スマートフォンアプリ
│       ├── server/           # PC (MLServer)
│       ├── python/           # Python ツール
│       ├── appendix/         # トラブルシューティング・用語
│       ├── stylesheets/      # extra.css (ファームウェアの版の印)
│       └── assets/screenshots/
└── en/                       # 英語版 (同じ構成)
```

スクリーンショットは各言語ディレクトリに独立して配置しています (UI 言語ごとに別キャプチャ)。

## バージョン運用

- ソフトウェアは過去の M-Logger にも対応させる方針なので、マニュアルは最新版のみを公開します (版ごとのパスは作らない)。

## ファームウェアによる違いの書き方

- 対応一覧の正本は `start/firmware.md` の表。各所の表記はこれに合わせる。
- 一部の版だけの機能: 文中に `<span class="fw">v4 以降</span>` (英語版は `v4+`) を付ける。
- 操作や画面が版で異なる箇所: 次のラベルのタブで併記する。ラベルを揃えるとサイト全体でタブの選択が連動する (`content.tabs.link`)。
  - 日本語版: `=== "v4 ファームウェア"` / `=== "v3 ファームウェア"` (新しい版が出たら同じ形で追加する)
  - 英語版: `=== "v4 firmware"` / `=== "v3 firmware"`
