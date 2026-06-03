# M-Logger 親機用 USB ブートローダ

M-Logger v4 親機 (AVR64DU32) に USB 経由でアプリケーション firmware を書き込むためのブートローダです。
初回のみ PICkit で本ブートローダを焼き込めば、以降のアプリ更新は USB 接続 + `avrdude` だけで完結します。
(USB ケーブルは PC に挿しっぱなし、M-Logger 本体の電源スイッチで bootloader/通常モードを切替)

## 出典

本ディレクトリの内容は [askn37/euboot](https://github.com/askn37/euboot) (MIT License, Copyright (c) 2024 askn (K.Sato) multix.jp)
をそのまま取り込み、M-Logger のピン配置に合わせて `Makefile` に variant `BUILTIN_LD0_SF2` を追加したものです。
ソースコード (`src/`, `euboot.ino`, `gencrc.pl`) と `LICENSE` は無改変。
オリジナルの README は `README_euboot.md` / `README_euboot_jp.md` を参照してください。

## M-Logger 用ピン配置

| 役割 | ピン | M-Logger 側の用途 |
|---|---|---|
| ブートローダ起動スイッチ (SW_BUILTIN) | `PF2` (pin 22) | 既存の Reset ボタン (プルアップ + 押下で GND) |
| 進捗表示 LED (LED_BUILTIN) | `PD0` (pin 10) | 既存の赤 LED |

通常のアプリ動作時:
- `PF2` はアプリが poll し、3 sec 長押しで自動モード解除 + SWR
- `PD0` はアプリがステータス点滅に使用

ブートローダ動作時 (= `PF2` Reset ボタンを押下したまま電源スイッチ ON):
- `PD0` の orange blink パターンで進捗表示
  - 1 回点滅: USB enumerate 待ち
  - 2 回点滅: 待機 (avrdude からの通信待ち)
  - 4 回点滅: read/write 中

両者は時間軸で排他なので競合しません。

## ビルド手順 (WSL 推奨)

Makefile が Bash + GNU Make 前提なので、**WSL 上でビルド → 生成物を Windows 側で PICkit に流す** 分担が一番楽です。
Windows ネイティブでも MSYS2 等あれば動きますが、ここでは WSL 前提で記載。

### 初回セットアップ (WSL 上、Ubuntu/Debian)

```sh
# arduino-cli の install
curl -fsSL https://raw.githubusercontent.com/arduino/arduino-cli/master/install.sh \
  | BINDIR=~/.local/bin sh
export PATH=$HOME/.local/bin:$PATH    # ~/.bashrc に追記推奨

# modernAVR SDK の install
arduino-cli core update-index \
  --additional-urls https://askn37.github.io/package_multix_zinnia_index.json
arduino-cli core install MultiX-Zinnia:modernAVR \
  --additional-urls https://askn37.github.io/package_multix_zinnia_index.json

# Perl は標準で入っているはず (gencrc.pl 用)。念のため:
which perl  # 何も出なければ: sudo apt install perl
```

### ビルド

```sh
cd /mnt/c/git_repositories/mlogger/firmware/bootloader
make hex/euboot_LD0_SF2.hex
```

成功すると `hex/euboot_LD0_SF2.hex` と `hex/euboot_LD0_SF2.fuse` が生成されます。
ファイルは Windows 側からもそのまま `C:\git_repositories\mlogger\firmware\bootloader\hex\` に見えます。

## PICkit での初回焼き込み (Windows 側で実行)

PICkit のドライバは Windows 側で動くため、焼き込みフェーズは Windows 側で実施するのが簡単
(WSL から USB device を扱うには `usbipd-win` での bridge が要るので避ける)。

**MPLAB IPE** を使う場合: `hex/euboot_LD0_SF2.hex` を選択して書き込み、fuse は `.fuse` ファイルの内容に従って手動設定。

**`avrdude` (Windows 版) 8.0+** を使う場合:

```cmd
avrdude -cpkobn_updi -pavr64du32 -Uflash:w:hex\euboot_LD0_SF2.hex:i
avrdude -cpkobn_updi -pavr64du32 -Ufuses:w:hex\euboot_LD0_SF2.fuse:i
```

## アプリケーションの USB 更新手順 (PICkit 不要)

USB ケーブルは PC に挿しっぱなしで OK。M-Logger 本体の **電源スイッチ** で ON/OFF します。

1. **M-Logger の電源スイッチを OFF**
2. **`PF2` (Reset ボタン) を押下したまま、電源スイッチを ON**
3. 赤 LED が orange パターンで blink し始めるのを確認 (= ブートローダ起動)
4. Reset ボタンは離して良い
5. ホスト PC から `avrdude` でアプリ書き込み:

   ```sh
   avrdude -Pusb:04d8:0b12 -cjtag3updi -pavr64du32 -D \
     -Uflash:w:path/to/mlogger_main.production.hex:i
   ```

6. 書き込み完了後、電源スイッチを一度 OFF → ON すると新しいアプリで起動

`-D` (auto-erase 抑止) は **必須**。これを忘れると euboot 領域も erase されてしまい復旧に PICkit が必要になります。

## アプリ側 (`mlogger_main.X`) の必須設定

ブートローダが Flash 先頭 `0x000-0x9FF` (2.5 KB) を占有するため、アプリのコード領域を `0x0A00` 以降に shift する必要があります。

### 1. Linker offset

MPLAB X プロジェクト → Project Properties → **XC8 Linker** → Option categories: **Additional options**
→ **Extra linker options** に以下を入力:

```
-Wl,--defsym=__TEXT_REGION_ORIGIN__=0xA00
```

`--section-start=.text=...` は XC8 v3 の linker script (`avrxmega2.xn`) と相性が悪く allocation エラーになります。`__TEXT_REGION_ORIGIN__` 経由で MEMORY 領域の origin を動かすのが正解。

### 2. Fuse 値の合わせ込み

`mlogger_main.X/mcc_generated_files/system/src/config_bits.c` の FUSES が以下を満たすこと
(MCC で設定 → Generate で反映):

| Fuse | 値 |
|---|---|
| BOOTSIZE | 0x05 (= boot section 2.5KB 確保) |
| SYSCFG0 | 0xD9 (= EESAVE on / Boot Row Save off / RSTPINCFG=Reset / UPDIPINCFG=UPDI) |

これにより、PICkit でアプリを焼く際も euboot 互換 fuse がセットされ、euboot 領域を意図せず無効化することがなくなります。

### 3. Reset 処理に解放待ち spinlock

`main.c` の Reset ボタン長押し処理で SWR (software reset) を発行する直前に、PF2 が解放されるまで wait する spinlock が必須:

```c
blinkRedLED(3);
while (!RST_GetValue()) ;     // ← euboot 連動防止: SWR 時点で PF2 が High でないと bootloader 突入してしまう
_PROTECTED_WRITE(RSTCTRL.SWRR, RSTCTRL_SWRST_bm);
```

## ライセンス

`LICENSE` ファイル (MIT License) を参照してください。
