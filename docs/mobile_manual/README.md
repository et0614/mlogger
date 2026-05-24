# M-Logger スマートフォン操作マニュアル (MkDocs)

MLS_Mobile アプリ (iPhone / Android) の操作マニュアルを MkDocs Material で構築するプロジェクトです。
日本語版 (`ja/`) と英語版 (`en/`) を独立した MkDocs プロジェクトとして並置しています。

## ビルド方法

### 必要環境

- Python 3.9 以上

### 初回セットアップ

```bash
pip install -r requirements.txt
```

### ローカルプレビュー

日本語版:

```bash
python -m mkdocs serve -f ja/mkdocs.yml
```

英語版:

```bash
python -m mkdocs serve -f en/mkdocs.yml
```

ブラウザで `http://127.0.0.1:8000/` を開くと、編集内容がリアルタイムに反映されます。

### 公開用ビルド

```bash
python -m mkdocs build -f ja/mkdocs.yml
python -m mkdocs build -f en/mkdocs.yml
```

それぞれ `ja/site/` と `en/site/` に静的 HTML が生成されます。公開先は以下の通りです。

- 日本語版: `mlogger.jp/ja/mobile/v{アプリ版}/`
- 英語版: `mlogger.jp/en/mobile/v{アプリ版}/`

## ディレクトリ構成

```
docs/mobile_manual/
├── README.md
├── requirements.txt        # 依存パッケージ (両言語共通)
├── ja/
│   ├── mkdocs.yml          # 日本語版 MkDocs 設定
│   └── docs/
│       ├── index.md
│       ├── installation.md
│       ├── ...
│       └── assets/screenshots/
└── en/
    ├── mkdocs.yml          # 英語版 MkDocs 設定
    └── docs/
        ├── index.md
        ├── installation.md
        ├── ...
        └── assets/screenshots/
```

スクリーンショットは各言語ディレクトリに独立して配置しています (UI 言語ごとに別キャプチャ)。

## バージョン運用

- アプリの `ApplicationDisplayVersion` (例: 1.3.1) に対応してマニュアルを更新します
- 公開時は `mlogger.jp/{ja|en}/mobile/v1.3/` のようにバージョン番号付きのパスへ配置します
- 過去バージョンのマニュアルは消さず残します
