# firmware/dist_user/ - ファームウェア更新キット (開発者向けメモ)

エンドユーザーが avrdude でファームウェアを更新するためのキット。
`software/make_dist_zip.py` が配布用 zip (mlogger_tools.zip) の `firmware_update/` に同梱する。

## 配布物の組み立て手順

配布セットは GitHub Actions (`.github/workflows/tools-release.yml`) で作る。

1. 配布するファームウェアを確定したら、`mlogger_main.X/dist/default/production/mlogger_main.X.production.hex`
   を `firmware/release/` にコピーしてコミットする。出荷後に更新を配る場合は、先に
   `version.h` の FW_VERSION を上げること (上げないと利用者も MLServer も新旧を区別できない)。
2. `git tag tools-vN` (N は連番) を push すると、avrdude を公式配布から取得して zip を作り、
   Releases に登録する。

手元で zip を作る場合は、`avrdude.exe` と `avrdude.conf` をこのフォルダに置いて
`python software/make_dist_zip.py` を実行する。

## ファイル

| ファイル | 用途 | git 管理 | zip に同梱 |
|---|---|---|---|
| `update.bat` | エンドユーザーがダブルクリックして書き込む (CP932 / CRLF で保存すること) | ✓ | ✓ |
| `README.md`, `README_ja.md` | エンドユーザー向け手順 | ✓ | ✓ |
| `COPYING_avrdude.txt` | avrdude のライセンス (GPL v2) | ✓ | ✓ |
| `BUILD.md` | 本ファイル | ✓ | − |
| `avrdude.exe`, `avrdude.conf` | 書き込みツール (~10 MB)。CI では公式配布から取得 | − | ✓ |
| `mlogger_main.X.production.hex` | 開発中の書き込み用 (配布には `firmware/release/` の hex を使う) | − | − |

試験用の hex などを置いても、zip には上表のファイルしか入らない。

## avrdude のライセンス

avrdude は GPL v2。バイナリを同梱配布するため、ライセンス文 (`COPYING_avrdude.txt`) を同梱し、
README に「ソースコードを 3 年間、求めに応じて提供する」旨の書面の申し出 (GPL v2 第 3 条 b) を
記載している。求めがあれば、使用している版のソース (avrdude 公式リリースの avrdude-X.Y.zip) を渡す。
avrdude の版を上げたら、README の版表記と workflow の AVRDUDE_VERSION も更新すること。

## 動作仕様

`update.bat` の中で実行されるコマンド:

```cmd
avrdude.exe -C avrdude.conf -P usb:04d8:0b12 -c jtag3updi -p avr64du32 -D ^
  -U flash:w:mlogger_main.X.production.hex:i
```

- `-P usb:04d8:0b12`: euboot bootloader の USB VID:PID で device 指定
  (COM port 名に依存しないので Windows のポート番号変動に強い)
- `-D`: chip erase を抑止 → boot loader 領域を保持
- `-c jtag3updi`: euboot が emulate するプロトコル

bootloader 領域 (`0x000000-0x0009FF`) は touch されないため、ユーザー操作で
bootloader が消えることはない。万一壊した場合は PICkit による再書き込みが必要。

## ブートローダへの入り方

Reset ボタン (PF2) を押したまま電源を入れる。電源スイッチは USB 給電と電池給電の
切り替えなので、電池が入っていると電源が切れない。利用者向けの手順は次のとおり
(USB を先につなぎ、スイッチの切り替えで電源を入れる。ケーブルの抜き差しより操作が確実):

1. 電池を外し、スイッチを電池給電側のまま USB を PC につなぐ (電源は入らない)
2. update.bat を起動する
3. Reset を押したままスイッチを USB 給電側に切り替える → 赤 LED 点滅を確認して Reset を離す
4. update.bat で Enter → 書き込み
5. スイッチを電池給電側 → USB 給電側に戻して再起動
