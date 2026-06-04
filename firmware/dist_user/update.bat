@echo off
cd /d "%~dp0"
title M-Logger ファームウェア更新

echo.
echo ===============================================
echo   M-Logger ファームウェア更新ツール
echo ===============================================
echo.
echo 【事前準備】
echo   1. M-Logger の電源スイッチを OFF にしてください
echo   2. Reset ボタンを押したまま、電源スイッチを ON にしてください
echo   3. 赤 LED が点滅しているのを確認してください
echo      ^(= ブートローダモードに入っています^)
echo   4. Reset ボタンは離して構いません
echo.
echo 準備ができたら何かキーを押してください...
pause >nul

echo.
echo ===============================================
echo   書き込み中... 約 10 秒お待ちください
echo ===============================================
echo.

avrdude.exe -C avrdude.conf -P usb:04d8:0b12 -c jtag3updi -p avr64du32 -D -U flash:w:mlogger_main.X.production.hex:i
set RC=%errorlevel%

echo.
if %RC% NEQ 0 (
  echo ===============================================
  echo   *** 書き込み失敗 ***
  echo ===============================================
  echo.
  echo 以下を確認してください:
  echo   - M-Logger が USB ケーブルで PC に接続されているか
  echo   - 赤 LED が点滅していたか ^(= ブートローダモード^)
  echo   - 他の M-Logger が同時接続されていないか
  echo.
  echo 確認のうえ、電源 OFF → Reset 押下しながら電源 ON で再準備して、
  echo もう一度このファイルをダブルクリックしてください。
  echo.
  pause
  exit /b 1
)

echo ===============================================
echo   書き込み完了
echo ===============================================
echo.
echo 電源スイッチを一度 OFF にしてから、再度 ON にしてください。
echo 通常モードで起動し、新しいファームウェアが動作します。
echo.
pause
