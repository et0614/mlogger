# firmware/dist_user/ - エンドユーザー向け配布キット

エンドユーザー (研究者等) に avrdude 経由でファームウェア更新してもらうための
zip パッケージのテンプレート。ユーザーは zip 解凍 → `update.bat` ダブルクリック
の 2 ステップで更新できる。

## 配布物の組み立て手順 (dev 側作業)

1. `mlogger_main.X` を MPLAB IDE で Build (Production) し、新 hex を生成
2. `dist_user/mlogger_main.X.production.hex` を新しい hex で上書き
3. `avrdude.exe` と `avrdude.conf` が配置済みであることを確認
   (Windows 用 avrdude 8.x 公式バイナリを使用)
4. `dist_user/` ディレクトリ全体を zip 圧縮
   - ファイル名は `mlogger_firmware_update_<version>.zip` 推奨
   - 例: `mlogger_firmware_update_v4.1.0.zip`
5. README.md (本ファイル) は配布には不要なので zip から除外して構わない

## 同梱ファイル

| ファイル | 用途 | git 管理 |
|---|---|---|
| `update.bat` | エンドユーザーがダブルクリックして書き込み実行 | ✓ |
| `README_user.txt` | エンドユーザー向け手順説明 | ✓ |
| `README.md` | 本ファイル (dev 向け、配布不要) | ✓ |
| `.gitignore` | 大容量バイナリ除外設定 | ✓ |
| `avrdude.exe` | AVR 書き込みツール (~10MB、GPL) | ✗ |
| `avrdude.conf` | avrdude チップ定義ファイル (~1MB) | ✗ |
| `mlogger_main.X.production.hex` | ファームウェア本体 | ✗ |

avrdude バイナリ / hex はリポジトリに含めず、配布パッケージ作成時に手動配置する。

## avrdude のライセンス

avrdude は GPL ライセンス。同梱配布する場合は zip に avrdude の COPYING ファイル
([GitHub](https://github.com/avrdudes/avrdude/blob/main/COPYING)) を併せて同梱
すること。

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
