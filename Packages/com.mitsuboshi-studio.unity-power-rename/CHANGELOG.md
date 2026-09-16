# Changelog

## 1.1.0
- 単体スクリプト v9 を収録し、Asset / Hierarchy GameObject のモード切替を追加。
- 選択したGameObjectと子階層を、非アクティブを含めて再帰取得し、重複を除外。
- 検索・置換、接頭辞・接尾辞、連番を両モードで共通利用可能に変更。
- Hierarchyの一括変更を1回のUndo/Redoで操作できるように対応。
- 実行前確認ダイアログを削除。
- 配布内のスクリプト名を `UnityPowerRenameWindow_v9.cs` とし、既存meta GUIDを維持。

## 1.0.0
- ユーザーによる起動・動作確認を受け、正式版へ移行。
- ソースコードと既存GUIDはbeta.1から変更なし。

## 1.0.0-beta.1
- UnityPowerRenameWindow_v7.cs の内容を変更せず、VPM パッケージへ移行。
- Editor 限定の assembly、固定ファイル名、manifest と配布自動化を追加。
- legacyFolders / legacyFiles による削除は行わず、旧版退避手順を明文化。

v7 / v9 は単体スクリプトの改訂番号です。VPM 版は SemVer で別管理します。
