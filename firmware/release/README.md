# firmware/release/ - 配布するファームウェア

配布用 zip (mlogger_tools.zip) の `firmware_update/` に入れるファームウェア。
`software/make_dist_zip.py` (と GitHub Actions の tools-release) はここの hex を使う。

- 開発中の書き込みに使う `firmware/dist_user/` の hex とは分けている
  (開発途中のビルドを誤って配布しないため)。
- 配布するファームウェアを確定したときだけ、`mlogger_main.X/dist/default/production/`
  の hex をここにコピーしてコミットする。版番号は `mlogger_main.X/version.h` の FW_VERSION。
