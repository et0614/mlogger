# icon_generator

ThermalComfortCalculator の着衣 (Clo) / 活動 (Met) 選択リストに使う
**86 枚のアイコン画像** を Gemini Nano Banana
(`gemini-2.5-flash-image`) で一貫したスタイルで生成するためのツール。

被写体は **顔のない木製マネキン (gender-neutral)**、
スタイルは **グレースケール+マネキン部のみ木目色** のフラットイラスト。

## 前提

- Python 3.10+
- 環境変数 `GEMINI_API_KEY` (https://aistudio.google.com/apikey から無料発行)
- 依存ライブラリ:

```bash
pip install google-genai pillow
```

無料枠で 86 枚は十分収まる想定 (Nano Banana の free tier は分/日単位の上限あり、
詳細は AI Studio の画面で確認)。

## ディレクトリ構成

```
icon_generator/
  prompts.py         マスタープロンプト + ビルダー関数
  items_clo.csv      着衣 55 件 (filename, description)
  items_met.csv      活動 31 件 (filename, activity, prop)
  gen_anchor.py      アンカー画像 (素マネキン) 生成
  gen_batch.py       バッチ生成 (anchor + CSV → output/)
  resize.py          1024→256 リサイズ & Resources/Images へ上書き
  _anchor.png        アンカー画像 (生成後)
  output/
    Clothes/         着衣の生成結果 (1024x1024)
    Activities/      活動の生成結果 (1024x1024)
  failed_clothes.csv / failed_activities.csv  失敗一覧 (生成された場合のみ)
```

## 標準ワークフロー

### Step 1: アンカー画像を作る

```bash
python gen_anchor.py
```

`_anchor.png` が保存される。表示して **気に入る素マネキンが出るまで** 何度か実行。
これが以降 85 枚の **視覚契約 (スタイル参照)** になるので、ここで妥協しない。

判断基準:
- 顔が無い (もし顔が出たら NG)
- 木目色のマネキン
- 線が一様な太さ
- 純白背景
- 全身が映っている、3/4 角度、~10% 余白

### Step 2: バッチ生成

```bash
python gen_batch.py clo     # 着衣 55 枚
python gen_batch.py met     # 活動 31 枚
python gen_batch.py all     # 両方
```

- 1 件あたり ~1 秒 sleep を挟むので、clo+met で **~2 分** 程度
- 既存ファイルはスキップ (`--force` で強制上書き)
- 失敗したものは `failed_*.csv` に記録される。CSV を編集して同じコマンドで再開可能
  (失敗分だけ残った状態で実行 → スキップ判定で成功済みは飛ばす)

途中で気に入らない 1 枚だけ作り直したいときは、その PNG を削除して
`gen_batch.py clo` を再実行すれば該当の 1 枚だけ生成される。

### Step 3: リサイズ & 差し替え

```bash
python resize.py --dry-run    # まず差し替え先パスだけ確認
python resize.py              # 256x256 化して Resources/Images に上書き
```

リサイズ後は MLS_Mobile プロジェクトの XAML/cs 側の変更は不要
(ファイル名を既存と一致させているため差し替えだけで反映)。

## プロンプトの調整

スタイルや指示を直したいときは `prompts.py` の以下を編集:

| 関数 / 定数 | 用途 |
|---|---|
| `MASTER_PROMPT` | 全画像の視覚契約 (パレット、被写体定義、サイズ等) |
| `anchor_prompt()` | アンカー生成専用 (素マネキン姿勢) |
| `clothing_prompt(item)` | 着衣バッチで anchor と組合せ |
| `activity_prompt(activity, prop)` | 活動バッチで anchor と組合せ |

CSV の項目別文を直したいときは `items_*.csv` を直接編集。

## トラブルシュート

| 症状 | 対策 |
|---|---|
| `GEMINI_API_KEY is not set` | 環境変数を設定して新しいターミナルで実行 |
| `429 Resource exhausted` | 無料枠の rate limit。数分待つか `SLEEP_BETWEEN` を上げる |
| anchor から逸脱した画像が出る | `prompts.py` の clothing/activity プロンプトに「Match line weight and proportions exactly」を追加して該当ファイルだけ再生成 |
| 顔やテキストが入る | `[Hard constraints]` に "Absolutely no face. Absolutely no text." を再強調 |
| アンカーが気に入らない | `_anchor.png` を消して `gen_anchor.py` をもう一度。Temperature 引数を SDK で下げる手もあり |

## 注意

- アンカー画像 `_anchor.png` と `output/` は **生成物** なので、必要なら
  `.gitignore` で除外する (現状追加していないので、コミットしたくなければ各自対応)
- 生成画像のライセンスは Gemini の利用規約に従う
