# VPM 構築・検証結果

確認日: 2026-09-16

## 公開対象

- パッケージ: https://github.com/RINYA-Mitsuki/unity-power-rename
- 正式版: `1.1.0`（単体スクリプト v9）
- VCC登録: https://rinya-mitsuki.github.io/vpm-repository/add.html

## 確認済み

| 項目 | 結果 |
|---|---|
| 元コードの保持 | パッケージ内 `Editor/UnityPowerRenameWindow_v9.cs` と確認済みv9ソースのSHA-256が一致 |
| スクリプトGUID | 既存の `ab6ccd9cba634eb38505a7142da9954a` をファイル名変更後も維持 |
| C#コンパイル | Unity 2022.3.22f1参照ライブラリで、更新元作成タスクにて成功 |
| Unity実動作 | Asset / Hierarchy GameObject両モードをユーザーが確認済み |
| パッケージ検証 | manifest、SemVer、Editor限定assembly、GUID、決定的ZIP生成のテストに成功 |
| 配布ファイル名 | ユーザー指定に従い `UnityPowerRenameWindow_v9.cs` を収録 |

元コードSHA-256: `22aaa9ebab8399593e7de0387b353e84a37e0693b895ec44ea78b89a51c3572f`

VPMのバージョンは単体スクリプトの改訂番号と分け、機能追加として `1.0.0` から `1.1.0` へ更新しています。

## 注意

Asset変更はUnity Undoの対象外です。Hierarchy変更は一括Undo/Redoに対応しています。正式版は配布区分を表し、すべての環境での動作を保証するものではありません。利用前にプロジェクトをバックアップしてください。
