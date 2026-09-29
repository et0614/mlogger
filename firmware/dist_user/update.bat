@echo off
cd /d "%~dp0"
title M-Logger firmware update

echo.
echo ===============================================
echo   M-Logger ファームウェア更新 / Firmware update
echo ===============================================
echo.
echo 【準備】 ^(このファイルを開く前に^)
echo   - 電池を取り外し、電源スイッチを電池給電の側にしたまま、
echo     USB ケーブルで PC につないでおきます ^(この時点では電源は入りません^)
echo.
echo 【書き込み待ちにする】
echo   1. Reset ボタンを押したまま、電源スイッチを USB 給電の側に切り替えます
echo   2. 赤 LED が点滅していることを確かめてから、Reset ボタンを離します
echo.
echo [Before running this file]
echo   - Remove the batteries, keep the power switch on the battery side,
echo     and connect the USB cable to the PC ^(the device stays off^)
echo.
echo [Enter the update mode]
echo   1. While holding the Reset button, move the power switch to the USB side
echo   2. Check that the red LED is blinking, then release the Reset button
echo.
echo 書き込み待ちになったら Enter キーを押してください / Press Enter when ready...
pause >nul

echo.
echo ===============================================
echo   書き込み中... 約 10 秒 / Writing... about 10 seconds
echo ===============================================
echo.

avrdude.exe -C avrdude.conf -P usb:04d8:0b12 -c jtag3updi -p avr64du32 -D -U flash:w:mlogger_main.X.production.hex:i
set RC=%errorlevel%

echo.
if %RC% NEQ 0 (
  echo ===============================================
  echo   *** 書き込み失敗 / Update failed ***
  echo ===============================================
  echo.
  echo 次を確認してください / Please check:
  echo   - USB ケーブルで PC につないでいるか ^(USB cable connected^)
  echo   - 赤 LED が点滅していたか ^(red LED was blinking^)
  echo   - PC に M-Logger を 1 台だけつないでいるか ^(only one M-Logger connected^)
  echo.
  echo 電源スイッチを電池給電の側に戻してから、もう一度このファイルを
  echo ダブルクリックし、書き込み待ちにする手順からやり直してください。
  echo Move the power switch back to the battery side, run this file again,
  echo and redo "Enter the update mode".
  echo.
  pause
  exit /b 1
)

echo ===============================================
echo   書き込み完了 / Update completed
echo ===============================================
echo.
echo 電源スイッチを一度電池給電の側に戻してから USB 給電の側にすると、
echo 新しいファームウェアで起動します。
echo Move the power switch to the battery side and back to the USB side
echo to start with the new firmware.
echo.
pause
